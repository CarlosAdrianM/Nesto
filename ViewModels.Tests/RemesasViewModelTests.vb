Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Events
Imports Nesto.Infrastructure.Models
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs

<TestClass()>
Public Class RemesasViewModelTests

    Private _dialogService As IDialogService
    Private _configuracion As IConfiguracion
    Private _servicio As IRemesasService

    <TestInitialize()>
    Public Sub Initialize()
        _dialogService = A.Fake(Of IDialogService)()
        _configuracion = A.Fake(Of IConfiguracion)()
        _servicio = A.Fake(Of IRemesasService)()

        A.CallTo(Function() _servicio.LeerFechaCargoPropuesta()).Returns(Task.FromResult(Date.Today))
        A.CallTo(Function() _servicio.LeerEmpresas()) _
            .Returns(Task.FromResult(New List(Of EmpresaModel)))
        ' NestoAPI#353: bytes de PDF de mentira para el informe de la remesa.
        A.CallTo(Function() _servicio.DescargarInformeRemesaPdf(A(Of String).Ignored, A(Of Integer).Ignored)) _
            .Returns(Task.FromResult(New Byte() {&H25, &H50, &H44, &H46}))
        _ficherosAbiertos = New List(Of String)
    End Sub

    ' NestoAPI#353: los tests no abren un visor de PDF real; recogen la ruta abierta.
    Private _ficherosAbiertos As List(Of String)

    Private Function CrearViewModel() As RemesasViewModel
        Dim vm = New RemesasViewModel(_configuracion, _dialogService, _servicio)
        vm.AbrirFicheroAccion = Sub(ruta) _ficherosAbiertos.Add(ruta)
        Return vm
    End Function

    <TestMethod()>
    Public Async Function CargarEmpresas_PoblaLaColeccionDesdeElServicio() As Task
        ' Nesto#340 Fase 1C.14 slice 1: las empresas se leen del API, no de EF.
        Dim empresas = New List(Of EmpresaModel) From {
            New EmpresaModel With {.Numero = "1  ", .Nombre = "Nueva Visión, S.A."},
            New EmpresaModel With {.Numero = "3  ", .Nombre = "Global"}
        }
        A.CallTo(Function() _servicio.LeerEmpresas()).Returns(Task.FromResult(empresas))

        Dim vm = CrearViewModel()
        Await vm.CargarEmpresasAsync()

        Assert.AreEqual(2, vm.listaEmpresas.Count)
        Assert.AreEqual("1  ", vm.listaEmpresas.First().Numero)
        Assert.AreEqual("Global", vm.listaEmpresas.Last().Nombre)
        A.CallTo(Function() _servicio.LeerEmpresas()).MustHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CargarEmpresas_SiElServicioFalla_NoLanzaYDejaMensajeError() As Task
        A.CallTo(Function() _servicio.LeerEmpresas()).Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        Await vm.CargarEmpresasAsync()

        Assert.AreEqual(0, vm.listaEmpresas.Count)
        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function

    <TestMethod()>
    Public Async Function CargarRemesas_PoblaElGridYSeleccionaLaPrimera() As Task
        ' Nesto#340 Fase 1C.14 slice 2: las remesas se leen del API, no de EF.
        Dim remesas = New List(Of RemesaModel) From {
            New RemesaModel With {.Numero = 10897, .Importe = 10546.66D, .Banco = "5"},
            New RemesaModel With {.Numero = 10896, .Importe = 9420.99D, .Banco = "5"}
        }
        A.CallTo(Function() _servicio.LeerRemesas(A(Of String).Ignored, 100)).Returns(Task.FromResult(remesas))

        Dim vm = CrearViewModel()
        Await vm.CargarRemesasAsync(100)

        Assert.AreEqual(2, vm.listaRemesas.Count)
        Assert.AreEqual(10897, vm.remesaActual.Numero)
    End Function

    <TestMethod()>
    Public Async Function CargarRemesas_SinTop_PideTodasAlServicio() As Task
        ' El botón "Ver Todas" carga sin límite: el servicio debe recibir top = Nothing.
        A.CallTo(Function() _servicio.LeerRemesas(A(Of String).Ignored, Nothing)) _
            .Returns(Task.FromResult(New List(Of RemesaModel)))

        Dim vm = CrearViewModel()
        Await vm.CargarRemesasAsync(Nothing)

        A.CallTo(Function() _servicio.LeerRemesas(A(Of String).Ignored, Nothing)).MustHaveHappenedOnceExactly()
        Assert.IsNull(vm.remesaActual)
    End Function

    <TestMethod()>
    Public Async Function CargarMovimientos_PoblaElGridDeEfectos() As Task
        ' Nesto#340 Fase 1C.14 slice 3: los efectos de la remesa se leen del API, no de EF.
        Dim movimientos = New List(Of MovimientoRemesaModel) From {
            New MovimientoRemesaModel With {.Id = 1, .Número = "15191", .Contacto = "0", .Importe = 250.5D},
            New MovimientoRemesaModel With {.Id = 2, .Número = "26985", .Contacto = "0", .Importe = 100D}
        }
        A.CallTo(Function() _servicio.LeerMovimientos(A(Of String).Ignored, 10897)).Returns(Task.FromResult(movimientos))

        Dim vm = CrearViewModel()
        Await vm.CargarMovimientosAsync(10897)

        Assert.AreEqual(2, vm.listaMovimientos.Count)
        Assert.AreEqual("15191", vm.listaMovimientos.First().Número)
        Assert.AreEqual(250.5D, vm.listaMovimientos.First().Importe)
    End Function

    <TestMethod()>
    Public Async Function CargarMovimientos_SiElServicioFalla_DejaElGridVacioYAvisa() As Task
        A.CallTo(Function() _servicio.LeerMovimientos(A(Of String).Ignored, A(Of Integer).Ignored)) _
            .Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        Await vm.CargarMovimientosAsync(10897)

        Assert.AreEqual(0, vm.listaMovimientos.Count, "El grid no puede quedarse con los datos de la remesa anterior")
        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function

    <TestMethod()>
    Public Async Function CargarRemesas_SiElServicioFalla_NoLanzaYDejaMensajeError() As Task
        A.CallTo(Function() _servicio.LeerRemesas(A(Of String).Ignored, A(Of Integer?).Ignored)) _
            .Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        Await vm.CargarRemesasAsync(100)

        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function

    <TestMethod()>
    Public Async Function CargarImpagados_PoblaElGridYSeleccionaElPrimero() As Task
        ' Nesto#340 Fase 1C.14 slice 4: los impagados se leen del API, no del GROUP BY de EF.
        Dim impagados = New List(Of impagado) From {
            New impagado With {.asiento = 1195101, .fecha = New Date(2026, 7, 20), .cuenta = 3},
            New impagado With {.asiento = 1194800, .fecha = New Date(2026, 7, 15), .cuenta = 1}
        }
        A.CallTo(Function() _servicio.LeerImpagados(A(Of String).Ignored, 100)).Returns(Task.FromResult(impagados))

        Dim vm = CrearViewModel()
        Await vm.CargarImpagadosAsync(100)

        Assert.AreEqual(2, vm.listaImpagados.Count)
        Assert.AreEqual(1195101, vm.impagadoActual.asiento)
    End Function

    <TestMethod()>
    Public Async Function CargarImpagados_SiElServicioFalla_DejaElGridVacioYAvisa() As Task
        A.CallTo(Function() _servicio.LeerImpagados(A(Of String).Ignored, A(Of Integer?).Ignored)) _
            .Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        Await vm.CargarImpagadosAsync(100)

        Assert.AreEqual(0, vm.listaImpagados.Count)
        Assert.IsNull(vm.impagadoActual)
        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function

    <TestMethod()>
    Public Async Function CargarMovimientosImpagado_PoblaElGridDeDetalle() As Task
        ' Nesto#340 Fase 1C.14 slice 5: el detalle del asiento se lee del API, no de EF.
        Dim movimientos = New List(Of MovimientoRemesaModel) From {
            New MovimientoRemesaModel With {.Id = 7, .Número = "15191", .Contacto = "0",
                .Importe = 250.5D, .Fecha = New Date(2026, 7, 20)}
        }
        A.CallTo(Function() _servicio.LeerMovimientosImpagado(A(Of String).Ignored, 1195101)).Returns(Task.FromResult(movimientos))

        Dim vm = CrearViewModel()
        Await vm.CargarMovimientosImpagadoAsync(1195101)

        Assert.AreEqual(1, vm.listaImpagadosDetalle.Count)
        Assert.AreEqual("15191", vm.listaImpagadosDetalle.First().Número)
        Assert.AreEqual(New Date(2026, 7, 20), vm.listaImpagadosDetalle.First().Fecha)
    End Function

    <TestMethod()>
    Public Async Function CargarMovimientosImpagado_SiElServicioFalla_DejaElGridVacioYAvisa() As Task
        A.CallTo(Function() _servicio.LeerMovimientosImpagado(A(Of String).Ignored, A(Of Integer).Ignored)) _
            .Throws(New Exception("API caída"))

        Dim vm = CrearViewModel()
        Await vm.CargarMovimientosImpagadoAsync(1195101)

        Assert.AreEqual(0, vm.listaImpagadosDetalle.Count, "El grid no puede quedarse con el detalle del asiento anterior")
        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function

    <TestMethod()>
    Public Sub ImpagadoActualNothing_VaciaElDetalle()
        Dim vm = CrearViewModel()
        vm.impagadoActual = Nothing

        Assert.IsNull(vm.listaImpagadosDetalle)
    End Sub

    ' NestoAPI#332: pestaña Crear Remesa (candidatos preseleccionados por el servidor + crear)

    Private Sub ConfirmarSiempre(respuestaOk As Boolean)
        A.CallTo(Sub() _dialogService.ShowDialog(
                    A(Of String).Ignored,
                    A(Of IDialogParameters).Ignored,
                    A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      If callback Is Nothing Then
                          Return
                      End If
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(If(respuestaOk, ButtonResult.OK, ButtonResult.Cancel))
                      callback(resultado)
                  End Sub)
    End Sub

    Private Function Candidato(id As Integer, Optional preseleccionado As Boolean = True,
                               Optional importe As Decimal = 100D, Optional conNegativos As Boolean = False,
                               Optional cliente As String = "15191") As EfectoCandidatoModel
        Return New EfectoCandidatoModel With {
            .Id = id, .Cliente = cliente, .ImportePendiente = importe,
            .Preseleccionado = preseleccionado, .ClienteConNegativos = conNegativos,
            .Motivo = If(preseleccionado, Nothing, "Retenido: envíos sin entregar (#172)")}
    End Function

    <TestMethod()>
    Public Async Function CargarCandidatos_MarcaLosPreseleccionadosYNoLosRetenidos() As Task
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(1), Candidato(2, preseleccionado:=False)}))
        Dim vm = CrearViewModel()

        Await vm.CargarCandidatosAsync()

        Assert.AreEqual(2, vm.ListaCandidatos.Count)
        Assert.IsTrue(vm.ListaCandidatos.Single(Function(c) c.Id = 1).Seleccionado)
        Assert.IsFalse(vm.ListaCandidatos.Single(Function(c) c.Id = 2).Seleccionado, "Los retenidos no vienen marcados")
        Assert.AreEqual(100D, vm.ImporteSeleccionado)
        Assert.AreEqual(1, vm.NumeroEfectosSeleccionados)
    End Function

    <TestMethod()>
    Public Async Function ResumenSeleccionado_JuntaNumeroEImporteComoTextoCopiable() As Task
        ' #1: el XAML bindea este texto a un TextBox de solo lectura para copiar el total.
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(1, importe:=40.66D), Candidato(2, importe:=29.27D)}))
        Dim vm = CrearViewModel()

        Await vm.CargarCandidatosAsync()

        Assert.AreEqual($"2 efectos, {69.93D:c}", vm.ResumenSeleccionado)
    End Function

    <TestMethod()>
    Public Async Function AplicarEfectosLiquidados_ActualizaSoloElEfectoAfectadoYNoPierdeLasMarcas() As Task
        ' Nesto#419: al liquidar en el Extracto, se actualiza EN SITIO solo el efecto afectado,
        ' SIN recargar (así no se pierden las marcas que el usuario tuviera hechas).
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(1, importe:=500D, conNegativos:=True, cliente:="15191"),
                Candidato(2, importe:=100D, conNegativos:=True, cliente:="15191"),
                Candidato(3, preseleccionado:=False, importe:=80D, cliente:="40227")}))
        Dim vm = CrearViewModel()
        Await vm.CargarCandidatosAsync()
        ' El usuario marca a mano un efecto de otro cliente: trabajo que NO se debe perder.
        vm.ListaCandidatos.Single(Function(c) c.Id = 3).Seleccionado = True

        vm.AplicarEfectosLiquidados(New EfectosLiquidadosPayload With {
            .Empresa = "1", .Cliente = "15191", .ClienteSigueConNegativos = False,
            .NuevosImportesPendientes = New Dictionary(Of Integer, Decimal) From {{1, 300D}, {2, 0D}}})

        ' Efecto 1: importe actualizado en sitio; su cliente ya no tiene negativos (fuera el naranja)
        Dim efecto1 = vm.ListaCandidatos.Single(Function(c) c.Id = 1)
        Assert.AreEqual(300D, efecto1.ImportePendiente)
        Assert.IsFalse(efecto1.ClienteConNegativos)
        ' Efecto 2: saldado a 0 -> deja de ser candidato
        Assert.IsFalse(vm.ListaCandidatos.Any(Function(c) c.Id = 2), "El efecto saldado a 0 se quita de la lista")
        ' Efecto 3 (otro cliente): la marca del usuario se conserva
        Assert.IsTrue(vm.ListaCandidatos.Single(Function(c) c.Id = 3).Seleccionado, "No se pierde el trabajo del usuario")
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_Confirmada_LlamaAlServicioConLosMarcadosYRefresca() As Task
        ConfirmarSiempre(respuestaOk:=True)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(111, importe:=250.5D), Candidato(222, importe:=90.5D)}))
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored, A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New CrearRemesaResponseModel With {.NumeroRemesa = 10900, .Importe = 341D, .NumeroEfectos = 2}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, "5",
            A(Of List(Of Integer)).That.Matches(Function(ids) ids.Count = 2 AndAlso ids.Contains(111) AndAlso ids.Contains(222)), A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .MustHaveHappenedOnceExactly()
        StringAssert.Contains(vm.mensajeError, "10900")
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)).MustHaveHappenedTwiceExactly()
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_ClienteConNegativosMarcado_AvisaYNoLlama() As Task
        ' La puerta de neteo: liquidar en Extracto de Cliente (Nesto#419) o desmarcar
        ConfirmarSiempre(respuestaOk:=True)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(111, conNegativos:=True, cliente:="15191")}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        StringAssert.Contains(vm.mensajeError, "15191")
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored, A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_SiElServidorRechaza_ElMotivoLlegaAlUsuario() As Task
        ConfirmarSiempre(respuestaOk:=True)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(111)}))
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored, A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .Throws(New Exception("El efecto 111 ya no es candidato a remesa"))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        StringAssert.Contains(vm.mensajeError, "ya no es candidato")
    End Function

    ' NestoAPI#353: informe de la remesa (imprimir al crear y desde el listado)

    ' Responde OK o Cancel según el título del diálogo (para contestar distinto a la
    ' confirmación de crear y a la pregunta de imprimir).
    Private Sub ResponderPorTitulo(respuestas As Dictionary(Of String, Boolean))
        A.CallTo(Sub() _dialogService.ShowDialog(
                    A(Of String).Ignored,
                    A(Of IDialogParameters).Ignored,
                    A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      If callback Is Nothing Then
                          Return
                      End If
                      Dim titulo = parametros.GetValue(Of String)("title")
                      Dim ok As Boolean = respuestas.ContainsKey(titulo) AndAlso respuestas(titulo)
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(If(ok, ButtonResult.OK, ButtonResult.Cancel))
                      callback(resultado)
                  End Sub)
    End Sub

    <TestMethod()>
    Public Async Function ImprimirRemesa_DescargaElInformeYLoAbre() As Task
        Dim vm = CrearViewModel()

        Await vm.ImprimirRemesaAsync(10901)

        A.CallTo(Function() _servicio.DescargarInformeRemesaPdf(A(Of String).Ignored, 10901)) _
            .MustHaveHappenedOnceExactly()
        Assert.AreEqual(1, _ficherosAbiertos.Count)
        StringAssert.EndsWith(_ficherosAbiertos.Single(), "Remesa_10901.pdf")
    End Function

    <TestMethod()>
    Public Async Function ImprimirRemesa_SiElServicioFalla_NoLanzaYDejaMensajeError() As Task
        A.CallTo(Function() _servicio.DescargarInformeRemesaPdf(A(Of String).Ignored, A(Of Integer).Ignored)) _
            .Throws(New Exception("API caída"))
        Dim vm = CrearViewModel()

        Await vm.ImprimirRemesaAsync(10901)

        Assert.AreEqual(0, _ficherosAbiertos.Count)
        StringAssert.Contains(vm.mensajeError, "10901")
        StringAssert.Contains(vm.mensajeError, "API caída")
    End Function

    <TestMethod()>
    Public Sub ImprimirRemesaCommand_SoloActivoConRemesaSeleccionada()
        Dim vm = CrearViewModel()

        Assert.IsFalse(vm.ImprimirRemesaCommand.CanExecute(), "Sin remesa seleccionada no se puede imprimir")

        vm.remesaActual = New RemesaModel With {.Numero = 10901, .Importe = 86.29D, .Banco = "5"}
        Assert.IsTrue(vm.ImprimirRemesaCommand.CanExecute())
    End Sub

    <TestMethod()>
    Public Async Function CrearRemesa_Confirmada_OfreceImprimirYAbreElInforme() As Task
        ConfirmarSiempre(respuestaOk:=True)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(111)}))
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored, A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New CrearRemesaResponseModel With {.NumeroRemesa = 10900, .Importe = 100D, .NumeroEfectos = 1}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        A.CallTo(Function() _servicio.DescargarInformeRemesaPdf(A(Of String).Ignored, 10900)) _
            .MustHaveHappenedOnceExactly()
        Assert.AreEqual(1, _ficherosAbiertos.Count)
        StringAssert.EndsWith(_ficherosAbiertos.Single(), "Remesa_10900.pdf")
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_UsuarioNoQuiereImprimir_NoDescargaElInforme() As Task
        ResponderPorTitulo(New Dictionary(Of String, Boolean) From {
            {"Crear remesa", True}, {"Imprimir remesa", False}})
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(111)}))
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored, A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New CrearRemesaResponseModel With {.NumeroRemesa = 10900, .Importe = 100D, .NumeroEfectos = 1}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored, A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.DescargarInformeRemesaPdf(A(Of String).Ignored, A(Of Integer).Ignored)) _
            .MustNotHaveHappened()
        Assert.AreEqual(0, _ficherosAbiertos.Count)
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_UsuarioCancela_NoLlama() As Task
        ConfirmarSiempre(respuestaOk:=False)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(111)}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored, A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Defaults_RespetarVencimientosMarcadoYFechaSeleccionLaborable() As Task
        ' Criterio Carlos 22/07: la casilla viene MARCADA por defecto, y la fecha "hasta" es
        ' la propuesta del servidor (siguiente laborable), nunca hoy.
        Dim propuesta As Date = Date.Today.AddDays(3)
        A.CallTo(Function() _servicio.LeerFechaCargoPropuesta()).Returns(Task.FromResult(propuesta))

        Dim vm = CrearViewModel()
        Await vm.InicializarFechaSeleccionAsync()

        Assert.IsTrue(vm.RespetarVencimientos, "Respetar vencimientos debe venir marcado por defecto")
        Assert.IsFalse(vm.ForzarFechaUnica)
        Assert.AreEqual(propuesta, vm.FechaSeleccionHasta)
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_ConVencimientosRespetados_LosParametrosViajanAlServicio() As Task
        ' NestoAPI#345: el modo de vencimientos y la fecha de cargo tienen que llegar al servidor
        ConfirmarSiempre(respuestaOk:=True)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(111)}))
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored, A(Of Boolean).Ignored, A(Of Date).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New CrearRemesaResponseModel With {.NumeroRemesa = 10901, .NumeroEfectos = 1}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        vm.RespetarVencimientos = True
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, "5",
            A(Of List(Of Integer)).Ignored, True, A(Of Date).Ignored, A(Of Date?).Ignored)).MustHaveHappenedOnceExactly()
        Assert.IsFalse(vm.ForzarFechaUnica, "ForzarFechaUnica es el espejo de RespetarVencimientos")
    End Function

    ' Marcar/Desmarcar todos: en una remesa de 100 efectos no se puede ir uno a uno. Marcar
    ' todos respeta las retenciones del servidor (solo marca preseleccionados); los grises se
    ' marcan a mano como decisión consciente. Desmarcar todos limpia TODO, también los grises.
    <TestMethod()>
    Public Async Function MarcarTodos_MarcaLosPreseleccionadosYRespetaLosRetenidos() As Task
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(1), Candidato(2), Candidato(3, preseleccionado:=False)}))
        Dim vm = CrearViewModel()
        Await vm.CargarCandidatosAsync()
        vm.DesmarcarTodosCommand.Execute()
        Assert.AreEqual(0, vm.NumeroEfectosSeleccionados, "Precondición: partimos de todo desmarcado")

        vm.MarcarTodosCommand.Execute()

        Assert.AreEqual(2, vm.NumeroEfectosSeleccionados)
        Assert.IsFalse(vm.ListaCandidatos.Single(Function(c) c.Id = 3).Seleccionado,
                       "El retenido por el servidor NO se marca en bloque")
    End Function

    <TestMethod()>
    Public Async Function DesmarcarTodos_DesmarcaTambienLosRetenidosMarcadosAMano() As Task
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored, A(Of Date?).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(1), Candidato(2, preseleccionado:=False)}))
        Dim vm = CrearViewModel()
        Await vm.CargarCandidatosAsync()
        vm.ListaCandidatos.Single(Function(c) c.Id = 2).Seleccionado = True

        vm.DesmarcarTodosCommand.Execute()

        Assert.AreEqual(0, vm.NumeroEfectosSeleccionados)
    End Function

    <TestMethod()>
    Public Sub MarcarYDesmarcarTodos_SinCandidatosCargados_NoLanzan()
        Dim vm = CrearViewModel()

        vm.MarcarTodosCommand.Execute()
        vm.DesmarcarTodosCommand.Execute()
    End Sub

    <TestMethod()>
    Public Async Function GenerarContenidoFicheroRemesa_PideElXmlAlServidorYLoParsea() As Task
        ' Nesto#340 Fase 1C.14 slice 6: el XML SEPA lo genera el servidor (único call site
        ' del SP); el VM solo lo parsea para guardarlo y copiarlo al portapapeles.
        A.CallTo(Function() _servicio.CrearFicheroRemesa(10897, "CORE", A(Of Date).Ignored)) _
            .Returns(Task.FromResult("<Document><CstmrDrctDbtInitn /></Document>"))

        Dim vm = CrearViewModel()
        vm.remesaActual = New RemesaModel With {.Numero = 10897}

        Dim doc = Await vm.GenerarContenidoFicheroRemesa("CORE")

        Assert.AreEqual("Document", doc.Root.Name.LocalName)
        A.CallTo(Function() _servicio.CrearFicheroRemesa(10897, "CORE", A(Of Date).Ignored)) _
            .MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function GenerarContenidoFicheroRemesa_SiElServidorNoDevuelveXml_Lanza() As Task
        ' Un contenido corrupto no puede acabar guardado como fichero de remesa: el parseo
        ' lanza y el comando muestra el error sin tocar el disco.
        A.CallTo(Function() _servicio.CrearFicheroRemesa(A(Of Integer).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult("esto no es xml"))

        Dim vm = CrearViewModel()
        vm.remesaActual = New RemesaModel With {.Numero = 10897}

        Await Assert.ThrowsExceptionAsync(Of Xml.XmlException)(
            Async Function()
                Dim unused = Await vm.GenerarContenidoFicheroRemesa("CORE")
            End Function)
    End Function
End Class
