Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Nesto#597 (gemela de NestoAPI#790): contabilizar el inventario desde la pantalla, con las comprobaciones de antes
''' (bloqueantes que no dejan seguir, avisos con «Continuar de todos modos»), y la ventana de diferencias.
''' </summary>
<TestClass()>
Public Class InventarioViewModelContabilizarTests

    Private ReadOnly _dia As New Date(2026, 11, 21)
    Private _configuracion As IConfiguracion
    Private _dialogos As IServicioDialogos
    Private _navegacion As IServicioNavegacion
    Private _servicio As IServicioInventario
    Private _comprobaciones As ComprobacionesInventarioDTO

    <TestInitialize()>
    Public Sub Inicializar()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _navegacion = A.Fake(Of IServicioNavegacion)()
        _servicio = A.Fake(Of IServicioInventario)()
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AlmacenInventario)) _
            .Returns(Task.FromResult("REI"))
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(True)
        _comprobaciones = New ComprobacionesInventarioDTO With {
            .Almacen = "REI", .Fecha = _dia,
            .Recuento = New List(Of RecuentoUsuarioInventarioDTO) From {
                New RecuentoUsuarioInventarioDTO With {.Usuario = "NUEVAVISION\Paloma", .Filas = 64},
                New RecuentoUsuarioInventarioDTO With {.Usuario = "NUEVAVISION\Laura", .Filas = 36}
            }
        }
        A.CallTo(Function() _servicio.Comprobaciones(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .ReturnsLazily(Function() Task.FromResult(_comprobaciones))
        A.CallTo(Function() _servicio.Contabilizar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored)) _
            .Returns(Task.FromResult(New ResultadoContabilizarInventario With {
                .Contabilizacion = New ContabilizacionInventarioDTO With {
                    .Almacen = "REI", .Fecha = _dia,
                    .Contabilizados = New List(Of UsuarioContabilizadoInventarioDTO) From {
                        New UsuarioContabilizadoInventarioDTO With {.Usuario = "NUEVAVISION\Paloma", .Filas = 64, .Traspaso = 77263},
                        New UsuarioContabilizadoInventarioDTO With {.Usuario = "NUEVAVISION\Laura", .Filas = 36, .Traspaso = 77264}
                    }
                }
            }))
        Confirma(True)
        DialogoComprobaciones(ResultadoBoton.OK)
    End Sub

    Private Sub Confirma(si As Boolean)
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(si)
    End Sub

    Private Sub DialogoComprobaciones(boton As ResultadoBoton)
        A.CallTo(Function() _dialogos.ShowDialogAsync(ComprobacionesInventarioDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoDialogo(boton)))
    End Sub

    Private Function CrearViewModel() As InventarioViewModel
        Dim vm As New InventarioViewModel(_navegacion, _configuracion, _dialogos, _servicio)
        vm.fechaSeleccionada = _dia
        Return vm
    End Function

    Private Shared Function Comprobacion(codigo As String, titulo As String) As ComprobacionInventarioDTO
        Return New ComprobacionInventarioDTO With {
            .Codigo = codigo, .Titulo = titulo,
            .Filas = New List(Of FilaComprobacionInventarioDTO) From {
                New FilaComprobacionInventarioDTO With {.NumOrden = 12, .Producto = "17404", .StockReal = 17404, .Usuario = "NUEVAVISION\Paloma"}
            }
        }
    End Function

    Private Sub ContabilizarNoHaPasado()
        A.CallTo(Function() _servicio.Contabilizar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Async Function ConBloqueantes_EnsenaLasComprobacionesYNoContabiliza() As Task
        _comprobaciones.Bloqueantes.Add(Comprobacion("CantidadDisparatada", "Cantidad disparatada"))
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _dialogos.ShowDialogAsync(ComprobacionesInventarioDialogViewModel.NOMBRE,
            A(Of ParametrosDialogo).That.Matches(Function(p) p.GetValue(Of ComprobacionesInventarioDTO)(ComprobacionesInventarioDialogViewModel.PARAMETRO_COMPROBACIONES) Is _comprobaciones))) _
            .MustHaveHappenedOnceExactly()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
        ContabilizarNoHaPasado()
    End Function

    <TestMethod()>
    Public Async Function ConAvisos_Continuar_ConfirmaYContabiliza() As Task
        _comprobaciones.Avisos.Add(Comprobacion("SinPrecioMedio", "Sin precio medio"))
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _dialogos.ShowDialogAsync(ComprobacionesInventarioDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer("Contabilizar inventario",
            "Se va a contabilizar el inventario de REI del 21/11/2026: 100 filas de 2 usuarios. ¿Continuar?", True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Contabilizar("1", "REI", _dia, False)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function ConAvisos_Cerrar_NoContabiliza() As Task
        _comprobaciones.Avisos.Add(Comprobacion("SinPrecioMedio", "Sin precio medio"))
        DialogoComprobaciones(ResultadoBoton.Cancel)
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        ContabilizarNoHaPasado()
    End Function

    <TestMethod()>
    Public Async Function SinNada_NoEnsenaComprobaciones_SoloConfirma() As Task
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _dialogos.ShowDialogAsync(A(Of String).Ignored, A(Of ParametrosDialogo).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.Contabilizar("1", "REI", _dia, False)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function NoConfirma_NoContabiliza() As Task
        Confirma(False)
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        ContabilizarNoHaPasado()
        A.CallTo(Sub() _navegacion.RequestNavigate(A(Of String).Ignored, A(Of String).Ignored, A(Of ParametrosNavegacion).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function SinFilasPendientes_AvisaYNoContabiliza() As Task
        _comprobaciones.Recuento.Clear()
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Sub() _dialogos.ShowNotification(A(Of String).Ignored, "No hay filas pendientes de contabilizar en REI el 21/11/2026.")).MustHaveHappenedOnceExactly()
        ContabilizarNoHaPasado()
    End Function

    <TestMethod()>
    Public Async Function Conflicto409_VuelveAEnsenarLasComprobacionesYNoAbreDiferencias() As Task
        Dim bloqueado As New ComprobacionesInventarioDTO With {.Almacen = "REI", .Fecha = _dia}
        bloqueado.Bloqueantes.Add(Comprobacion("OtraFecha", "Filas pendientes de otra fecha"))
        A.CallTo(Function() _servicio.Contabilizar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of Boolean).Ignored)) _
            .Returns(Task.FromResult(New ResultadoContabilizarInventario With {.Bloqueado = bloqueado}))
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Function() _dialogos.ShowDialogAsync(ComprobacionesInventarioDialogViewModel.NOMBRE,
            A(Of ParametrosDialogo).That.Matches(Function(p) p.GetValue(Of ComprobacionesInventarioDTO)(ComprobacionesInventarioDialogViewModel.PARAMETRO_COMPROBACIONES) Is bloqueado))) _
            .MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _navegacion.RequestNavigate(A(Of String).Ignored, A(Of String).Ignored, A(Of ParametrosNavegacion).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Contabilizado_EnsenaLosTraspasosYAbreLasDiferenciasDeEsosTraspasos() As Task
        Dim vm = CrearViewModel()

        Await vm.ContabilizarInventario()

        A.CallTo(Sub() _dialogos.ShowNotification("Inventario contabilizado",
            A(Of String).That.Matches(Function(t) t.Contains("NUEVAVISION\Paloma: 64 filas, traspaso 77263") AndAlso t.Contains("traspaso 77264")))) _
            .MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _navegacion.RequestNavigate("MainRegion", DiferenciasInventarioViewModel.NOMBRE_VISTA,
            A(Of ParametrosNavegacion).That.Matches(Function(p) p.GetValue(Of String)(DiferenciasInventarioViewModel.PARAMETRO_ALMACEN) = "REI" AndAlso
                                                        p.GetValue(Of List(Of Integer))(DiferenciasInventarioViewModel.PARAMETRO_TRASPASOS).SequenceEqual({77263, 77264})))) _
            .MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub TextoContabilizacion_ConErrores_LosEnsena()
        Dim texto = InventarioViewModel.TextoContabilizacion(New ContabilizacionInventarioDTO With {
            .Contabilizados = New List(Of UsuarioContabilizadoInventarioDTO) From {
                New UsuarioContabilizadoInventarioDTO With {.Usuario = "Paloma", .Filas = 1, .Traspaso = 77263}},
            .Errores = New List(Of ErrorContabilizarInventarioDTO) From {
                New ErrorContabilizarInventarioDTO With {.Usuario = "Laura", .Mensaje = "Interbloqueo"}}
        })

        StringAssert.Contains(texto, "Paloma: 1 fila, traspaso 77263")
        StringAssert.Contains(texto, "No se ha podido contabilizar:")
        StringAssert.Contains(texto, "Laura: Interbloqueo")
    End Sub

    <TestMethod()>
    Public Async Function SinPermiso_NiComprueba() As Task
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(False)
        Dim vm = CrearViewModel()

        Assert.IsFalse(vm.PuedeContabilizar)
        Assert.IsFalse(vm.PuedeVerDiferencias)
        Await vm.ContabilizarInventario()

        A.CallTo(Function() _servicio.Comprobaciones(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Sub Compras_VeDiferenciasPeroNoContabiliza()
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(False)
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)).Returns(True)
        Dim vm = CrearViewModel()

        Assert.IsFalse(vm.PuedeContabilizar)
        Assert.IsTrue(vm.PuedeVerDiferencias)
    End Sub

    <TestMethod()>
    Public Sub VerDiferencias_AbreLaVentanaConLaFecha()
        Dim vm = CrearViewModel()
        vm.almacen = "REI"

        vm.cmdVerDiferencias.Execute(Nothing)

        A.CallTo(Sub() _navegacion.RequestNavigate("MainRegion", DiferenciasInventarioViewModel.NOMBRE_VISTA,
            A(Of ParametrosNavegacion).That.Matches(Function(p) p.GetValue(Of Date?)(DiferenciasInventarioViewModel.PARAMETRO_FECHA) = _dia AndAlso
                                                        Not p.ContainsKey(DiferenciasInventarioViewModel.PARAMETRO_TRASPASOS)))) _
            .MustHaveHappenedOnceExactly()
    End Sub

    ' --- Diálogo de comprobaciones ---

    <TestMethod()>
    Public Sub Dialogo_ConBloqueantes_NoDejaContinuar()
        _comprobaciones.Bloqueantes.Add(Comprobacion("Ficticio", "Productos ficticios con stock"))
        Dim dialogo As New ComprobacionesInventarioDialogViewModel()

        dialogo.Cargar(_comprobaciones)

        Assert.IsTrue(dialogo.HayBloqueantes)
        Assert.IsFalse(dialogo.PuedeContinuar)
        Assert.IsFalse(dialogo.ContinuarCommand.CanExecute(Nothing))
        Assert.AreEqual("Productos ficticios con stock (1 fila)", dialogo.Bloqueantes.Single().Cabecera)
    End Sub

    <TestMethod()>
    Public Sub Dialogo_SoloAvisos_ContinuarDeTodosModosDevuelveOK()
        _comprobaciones.Avisos.Add(Comprobacion("CalculadoNegativo", "Stock calculado negativo"))
        Dim dialogo As New ComprobacionesInventarioDialogViewModel()
        Dim resultado As ResultadoDialogo = Nothing
        AddHandler dialogo.RequestClose, Sub(r) resultado = r

        dialogo.Cargar(_comprobaciones)
        dialogo.ContinuarCommand.Execute(Nothing)

        Assert.AreEqual("Continuar de todos modos", dialogo.TextoContinuar)
        Assert.AreEqual(2, dialogo.Recuento.Count)
        Assert.AreEqual(ResultadoBoton.OK, resultado.Result)
    End Sub

    ' --- Diferencias ---

    Private Shared Function Linea(producto As String, grupo As String, familia As String, diferencia As Integer, valor As Decimal) As LineaDiferenciaInventarioDTO
        Return New LineaDiferenciaInventarioDTO With {
            .Producto = producto, .Grupo = grupo, .Familia = familia, .Diferencia = diferencia,
            .ValorUnitario = If(diferencia = 0, 0D, Math.Abs(valor / diferencia)), .ValorDiferencia = valor}
    End Function

    Private Shared Function Diferencias() As DiferenciasInventarioDTO
        Return New DiferenciasInventarioDTO With {
            .Almacen = "REI", .Traspasos = New List(Of Integer) From {77263}, .Valoracion = "PVP",
            .Lineas = New List(Of LineaDiferenciaInventarioDTO) From {
                Linea("A", "COS", "Eva Visnu", -1, -3.5D),
                Linea("B", "COS", "Lisap", 2, 40D),
                Linea("C", "APA", "Maystar", -1, -120D),
                Linea("D", "COS", "Eva Visnu", 1, 4.99D)
            }
        }
    End Function

    <TestMethod()>
    Public Sub Diferencias_SinMinimo_TodasOrdenadasPorValorYConTotales()
        Dim vm As New DiferenciasInventarioViewModel(_servicio, _dialogos)

        vm.Mostrar(Diferencias())

        CollectionAssert.AreEqual({"C", "B", "D", "A"}, vm.Lineas.Select(Function(l) l.Producto).ToArray())
        Assert.AreEqual(4, vm.TotalFilas)
        Assert.AreEqual(1, vm.TotalUnidades)
        Assert.AreEqual(-78.51D, vm.TotalValor)
        Assert.AreEqual("Valoración: PVP", vm.TextoValoracion)
        Assert.AreEqual(2, vm.PorGrupo.Count)
        Assert.AreEqual(41.49D, vm.PorGrupo.Single(Function(g) g.Grupo = "COS").Valor)
        Assert.AreEqual(2, vm.PorFamilia.Single(Function(f) f.Familia = "Eva Visnu").Filas)
    End Sub

    <TestMethod()>
    Public Sub Diferencias_ImporteMinimo_FiltraPorValorAbsolutoYRehaceTotales()
        Dim vm As New DiferenciasInventarioViewModel(_servicio, _dialogos)
        vm.Mostrar(Diferencias())

        vm.ImporteMinimo = 5D

        CollectionAssert.AreEqual({"C", "B"}, vm.Lineas.Select(Function(l) l.Producto).ToArray())
        Assert.AreEqual(2, vm.TotalFilas)
        Assert.AreEqual(1, vm.TotalUnidades)
        Assert.AreEqual(-80D, vm.TotalValor)
        Assert.IsFalse(vm.PorFamilia.Any(Function(f) f.Familia = "Eva Visnu"))
    End Sub

    <TestMethod()>
    Public Sub Diferencias_ValoracionCoste_EnMinusculas()
        Dim vm As New DiferenciasInventarioViewModel(_servicio, _dialogos)
        Dim d = Diferencias()
        d.Valoracion = "Coste"

        vm.Mostrar(d)

        Assert.AreEqual("Valoración: coste", vm.TextoValoracion)
    End Sub

    <TestMethod()>
    Public Async Function Diferencias_ConTraspasos_LosPideSinFecha() As Task
        A.CallTo(Function() _servicio.Diferencias(A(Of String).Ignored, A(Of String).Ignored, A(Of Date?).Ignored, A(Of IEnumerable(Of Integer)).Ignored)) _
            .Returns(Task.FromResult(Diferencias()))
        Dim vm As New DiferenciasInventarioViewModel(_servicio, _dialogos) With {
            .Almacen = "REI", .Fecha = _dia, .TextoTraspasos = "77263, 77264;x"}

        Await vm.Cargar()

        A.CallTo(Function() _servicio.Diferencias("1", "REI", Nothing,
            A(Of IEnumerable(Of Integer)).That.Matches(Function(t) t.SequenceEqual({77263, 77264})))).MustHaveHappenedOnceExactly()
        Assert.AreEqual(4, vm.Lineas.Count)
    End Function

    <TestMethod()>
    Public Async Function Diferencias_SinFechaNiTraspaso_NoLlamaALaApi() As Task
        Dim vm As New DiferenciasInventarioViewModel(_servicio, _dialogos) With {.Almacen = "REI"}

        Await vm.Cargar()

        A.CallTo(Sub() _dialogos.ShowError("Indique la fecha del inventario o el traspaso.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Diferencias(A(Of String).Ignored, A(Of String).Ignored, A(Of Date?).Ignored, A(Of IEnumerable(Of Integer)).Ignored)).MustNotHaveHappened()
    End Function
End Class
