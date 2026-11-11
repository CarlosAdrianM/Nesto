Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Sugerencia 1185 (Alfredo, gemela de POST api/Inventarios/DejarACero): «Dejar a cero en todas sus ubicaciones» desde la
''' pantalla de inventario: primero lo que haría (Simular) para confirmar, luego de verdad; los 409 que se resuelven se preguntan.
''' </summary>
<TestClass()>
Public Class InventarioViewModelDejarACeroTests

    Private _configuracion As IConfiguracion
    Private _dialogos As IServicioDialogos
    Private _servicio As IServicioInventario
    Private _plan As DejarACeroInventarioRespuestaDTO

    <TestInitialize()>
    Public Sub Inicializar()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IServicioInventario)()
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AlmacenInventario)) _
            .Returns(Task.FromResult("ALG"))
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(False)
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(True)
        _plan = New DejarACeroInventarioRespuestaDTO With {
            .Almacen = "ALG", .Producto = "44901", .Descripcion = "TINTE", .PorUbicaciones = True,
            .Aviso = "Para que el stock se quede a cero hay que contabilizar el inventario de ALG de hoy («Contabilizar inventario»).",
            .Lineas = New List(Of LineaDejarACeroDTO) From {
                New LineaDejarACeroDTO With {.Pasillo = "001", .Fila = "002", .Columna = "005", .StockCalculado = 8, .Accion = LineaDejarACeroDTO.CREAR},
                New LineaDejarACeroDTO With {.Pasillo = "003", .Fila = "001", .Columna = "002", .StockCalculado = 4, .ContadoAntes = 2, .NumeroOrden = 948800, .Accion = LineaDejarACeroDTO.PONER_A_CERO}
            }
        }
        A.CallTo(Function() _servicio.DejarACero(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored, True)) _
            .ReturnsLazily(Function() Task.FromResult(_plan))
        A.CallTo(Function() _servicio.DejarACero(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored, False)) _
            .ReturnsLazily(Function() Task.FromResult(New DejarACeroInventarioRespuestaDTO With {
                .Almacen = "ALG", .Producto = "44901", .FilasCreadas = 1, .FilasPuestasACero = 1, .Aviso = _plan.Aviso, .Lineas = _plan.Lineas}))
        Confirma(True)
    End Sub

    Private Sub Confirma(si As Boolean)
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).Returns(si)
    End Sub

    Private Function CrearViewModel() As InventarioViewModel
        Dim vm As New InventarioViewModel(A.Fake(Of IServicioNavegacion)(), _configuracion, _dialogos, _servicio)
        vm.numeroProducto = " 44901 "
        Return vm
    End Function

    Private Sub NoSeHaHechoDeVerdad()
        A.CallTo(Function() _servicio.DejarACero(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored, False)) _
            .MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Async Function Almacen_SimulaConfirmaConLasUbicacionesYLoHace() As Task
        Dim vm = CrearViewModel()

        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, False, True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(InventarioViewModel.TITULO_DEJAR_A_CERO,
            A(Of String).That.Matches(Function(m) m.Contains("001-002-005: 8 → 0") AndAlso m.Contains("003-001-002: teórico 4, contado 2 → 0") AndAlso m.Contains("contabilizar")),
            True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, False, False)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowNotification(InventarioViewModel.TITULO_DEJAR_A_CERO,
            A(Of String).That.Matches(Function(m) m.Contains("1 fila creada a 0") AndAlso m.Contains("1 fila puesta a 0")))).MustHaveHappenedOnceExactly()
        Assert.AreEqual(String.Empty, vm.numeroProducto)
    End Function

    <TestMethod()>
    Public Async Function DiceQueNo_NoLoHace() As Task
        Confirma(False)
        Dim vm = CrearViewModel()

        Await vm.DejarACeroEnUbicaciones()

        NoSeHaHechoDeVerdad()
    End Function

    <TestMethod()>
    Public Async Function SinProducto_AvisaYNoLlamaALaApi() As Task
        Dim vm = CrearViewModel()
        vm.numeroProducto = "  "

        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Sub() _dialogos.ShowError(A(Of String).That.Contains("Producto"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.DejarACero(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored)) _
            .MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function SinPermiso_NoHaceNada() As Task
        A.CallTo(Function() _configuracion.UsuarioEnGrupo(A(Of String).Ignored)).Returns(False)
        Dim vm = CrearViewModel()

        Assert.IsFalse(vm.PuedeDejarACero)
        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Function() _servicio.DejarACero(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored, A(Of Boolean).Ignored)) _
            .MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function FilasSinUbicacion_PreguntaYRepiteQuitandolas() As Task
        Dim conflicto As New ConflictoInventarioException("El 44901 tiene 1 fila sin ubicación hoy en ALG (nº 948787: contado 1, Alfredo).",
            New ConflictoInventarioDTO With {.Codigo = CodigosConflictoInventario.FILAS_SIN_UBICACION})
        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, False, True)).ThrowsAsync(conflicto)
        Dim vm = CrearViewModel()

        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(InventarioViewModel.TITULO_DEJAR_A_CERO,
            A(Of String).That.Matches(Function(m) m.Contains("948787") AndAlso m.Contains("¿Borrar esa fila y seguir?")), True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, True, True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, True, False)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function FilasSinUbicacion_DiceQueNo_NoSigue() As Task
        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, False, True)) _
            .ThrowsAsync(New ConflictoInventarioException("Fila sin ubicación", New ConflictoInventarioDTO With {.Codigo = CodigosConflictoInventario.FILAS_SIN_UBICACION}))
        Confirma(False)
        Dim vm = CrearViewModel()

        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, True, True)).MustNotHaveHappened()
        NoSeHaHechoDeVerdad()
    End Function

    <TestMethod()>
    Public Async Function YaContabilizado_ContarOtraVezYRepiteConOtraVez() As Task
        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, False, True)) _
            .ThrowsAsync(New ConflictoInventarioException("El 44901 ya se contó y contabilizó hoy en ALG.",
                New ConflictoInventarioDTO With {.Codigo = CodigosConflictoInventario.YA_CONTABILIZADO, .PuedeContarOtraVez = True}))
        Dim vm = CrearViewModel()

        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(InventarioViewModel.TITULO_CONTAR_OTRA_VEZ, A(Of String).Ignored, True)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", True, False, False)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function FilasDuplicadas_EnsenaElErrorYNoSigue() As Task
        A.CallTo(Function() _servicio.DejarACero("1", "ALG", "44901", False, False, True)) _
            .ThrowsAsync(New ConflictoInventarioException("Más de una fila", New ConflictoInventarioDTO With {.Codigo = CodigosConflictoInventario.FILAS_DUPLICADAS}))
        Dim vm = CrearViewModel()

        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Sub() _dialogos.ShowError("Más de una fila")).MustHaveHappenedOnceExactly()
        NoSeHaHechoDeVerdad()
    End Function

    <TestMethod()>
    Public Async Function NadaQueHacer_LoDiceSinPreguntar() As Task
        _plan = New DejarACeroInventarioRespuestaDTO With {.Almacen = "ALG", .Producto = "44901",
            .Aviso = "El 44901 no tiene stock en ALG en ninguna ubicación ni filas de inventario de hoy: no hay nada que dejar a cero.",
            .Lineas = New List(Of LineaDejarACeroDTO) From {New LineaDejarACeroDTO With {.Pasillo = "001", .Fila = "002", .Columna = "005", .Accion = LineaDejarACeroDTO.YA_ESTABA_A_CERO}}}
        Dim vm = CrearViewModel()

        Await vm.DejarACeroEnUbicaciones()

        A.CallTo(Sub() _dialogos.ShowNotification(InventarioViewModel.TITULO_DEJAR_A_CERO, A(Of String).That.Contains("no hay nada"))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored, A(Of Boolean).Ignored)).MustNotHaveHappened()
        NoSeHaHechoDeVerdad()
    End Function

    <TestMethod()>
    Public Sub TextoConfirmacion_FilaQuitadaYSinUbicacion()
        Dim plan As New DejarACeroInventarioRespuestaDTO With {
            .Almacen = "REI", .Producto = "44901",
            .Lineas = New List(Of LineaDejarACeroDTO) From {New LineaDejarACeroDTO With {.StockCalculado = 8, .Accion = LineaDejarACeroDTO.CREAR}},
            .FilasQuitadas = New List(Of LineaInventarioContadaDTO) From {New LineaInventarioContadaDTO With {.NumeroOrden = 948787, .StockReal = 1}}
        }

        Dim texto = InventarioViewModel.TextoConfirmacionDejarACero(plan)

        StringAssert.Contains(texto, "sin ubicación: 8 → 0")
        StringAssert.Contains(texto, "Se borra la fila sin ubicación nº 948787 (contado 1)")
    End Sub

    <TestMethod()>
    Public Sub Almacen_PuedeContabilizarAlgete()
        Dim vm = CrearViewModel()

        Assert.IsTrue(vm.PuedeContabilizar)
        Assert.IsTrue(vm.PuedeDejarACero)
    End Sub
End Class
