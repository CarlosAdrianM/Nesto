Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
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

        A.CallTo(Function() _servicio.LeerEmpresas()) _
            .Returns(Task.FromResult(New List(Of EmpresaModel)))
    End Sub

    Private Function CrearViewModel() As RemesasViewModel
        Return New RemesasViewModel(_configuracion, _dialogService, _servicio)
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
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored)) _
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
    Public Async Function CrearRemesa_Confirmada_LlamaAlServicioConLosMarcadosYRefresca() As Task
        ConfirmarSiempre(respuestaOk:=True)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(111, importe:=250.5D), Candidato(222, importe:=90.5D)}))
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored)) _
            .Returns(Task.FromResult(New CrearRemesaResponseModel With {.NumeroRemesa = 10900, .Importe = 341D, .NumeroEfectos = 2}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, "5",
            A(Of List(Of Integer)).That.Matches(Function(ids) ids.Count = 2 AndAlso ids.Contains(111) AndAlso ids.Contains(222)))) _
            .MustHaveHappenedOnceExactly()
        StringAssert.Contains(vm.mensajeError, "10900")
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored)).MustHaveHappenedTwiceExactly()
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_ClienteConNegativosMarcado_AvisaYNoLlama() As Task
        ' La puerta de neteo: liquidar en Extracto de Cliente (Nesto#419) o desmarcar
        ConfirmarSiempre(respuestaOk:=True)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {
                Candidato(111, conNegativos:=True, cliente:="15191")}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        StringAssert.Contains(vm.mensajeError, "15191")
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored)) _
            .MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_SiElServidorRechaza_ElMotivoLlegaAlUsuario() As Task
        ConfirmarSiempre(respuestaOk:=True)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(111)}))
        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored)) _
            .Throws(New Exception("El efecto 111 ya no es candidato a remesa"))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        StringAssert.Contains(vm.mensajeError, "ya no es candidato")
    End Function

    <TestMethod()>
    Public Async Function CrearRemesa_UsuarioCancela_NoLlama() As Task
        ConfirmarSiempre(respuestaOk:=False)
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored)) _
            .Returns(Task.FromResult(New List(Of EfectoCandidatoModel) From {Candidato(111)}))
        Dim vm = CrearViewModel()
        vm.BancoRemesa = "5"
        Await vm.CargarCandidatosAsync()

        Await vm.CrearRemesaAsync()

        A.CallTo(Function() _servicio.CrearRemesa(A(Of String).Ignored, A(Of String).Ignored, A(Of List(Of Integer)).Ignored)) _
            .MustNotHaveHappened()
    End Function

    ' Marcar/Desmarcar todos: en una remesa de 100 efectos no se puede ir uno a uno. Marcar
    ' todos respeta las retenciones del servidor (solo marca preseleccionados); los grises se
    ' marcan a mano como decisión consciente. Desmarcar todos limpia TODO, también los grises.
    <TestMethod()>
    Public Async Function MarcarTodos_MarcaLosPreseleccionadosYRespetaLosRetenidos() As Task
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored)) _
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
        A.CallTo(Function() _servicio.LeerEfectosCandidatos(A(Of String).Ignored)) _
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
End Class
