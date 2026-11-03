Imports System.Collections.Generic
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Nesto#596 (gemela de NestoAPI#789): en la tienda no se cuenta nada sin haber rellenado antes el inventario del día con el
''' stock actual (POST api/Inventarios/Rellenar). Se pregunta al abrir y, si no se rellenó, al insertar la primera referencia.
''' </summary>
<TestClass()>
Public Class InventarioViewModelRellenarTests

    Private ReadOnly _dia As New Date(2026, 11, 21)
    Private _configuracion As IConfiguracion
    Private _dialogos As IServicioDialogos
    Private _servicio As IServicioInventario
    Private _pendientes As List(Of InventarioPendienteDTO)

    <TestInitialize()>
    Public Sub Inicializar()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IServicioInventario)()
        _pendientes = New List(Of InventarioPendienteDTO)
        Almacen("REI")
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .ReturnsLazily(Function() Task.FromResult(_pendientes))
        A.CallTo(Function() _servicio.Rellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New RellenarInventarioRespuestaDTO With {.Almacen = "REI", .Fecha = _dia, .Filas = 120, .Unidades = 345}))
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(Of InventarioDTO)(Nothing))
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)) _
            .ReturnsLazily(Function(linea As InventarioDTO) Task.FromResult(New InventarioDTO With {
                .Producto = "17404", .Descripcion = "REPUESTO", .Familia = "Eva Visnu", .PorCodigoBarras = True}))
    End Sub

    Private Sub Almacen(numero As String)
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AlmacenInventario)) _
            .Returns(Task.FromResult(numero))
    End Sub

    Private Sub Responde(si As Boolean)
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(si)
    End Sub

    Private Function CrearViewModel() As InventarioViewModel
        Dim vm As New InventarioViewModel(A.Fake(Of IServicioNavegacion)(), _configuracion, _dialogos, _servicio)
        vm.fechaSeleccionada = _dia
        Return vm
    End Function

    <TestMethod()>
    Public Async Function AlAbrir_SinPendientes_PreguntaYRellena() As Task
        Responde(True)
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored,
            "Antes de contar hay que rellenar el inventario con el stock actual de REI. ¿Rellenar ahora?")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.Rellenar("1", "REI", _dia)).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowNotification(A(Of String).Ignored, "Inventario rellenado: 120 productos, 345 unidades.")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function AlAbrir_ConPendientes_NoPregunta() As Task
        _pendientes.Add(New InventarioPendienteDTO With {.Usuario = "NUEVAVISION\Paloma", .Fecha = _dia, .Filas = 64})
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.Rellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function AlAbrir_Algete_NiConsultaNiPregunta() As Task
        Almacen(Constantes.Almacenes.ALMACEN_ALGETE)
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()

        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function AlAbrirDosVeces_SoloPreguntaUna() As Task
        Responde(False)
        Dim vm = CrearViewModel()

        Await vm.ComprobarRellenoAlAbrir()
        Await vm.ComprobarRellenoAlAbrir()

        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Insertar_SinRellenar_DiceNo_NoGraba() As Task
        Responde(False)
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("17404")

        Assert.IsNull(movimiento)
        A.CallTo(Function() _servicio.Rellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustNotHaveHappened()
        Assert.AreEqual(0, vm.movimientosDia.Count)
    End Function

    <TestMethod()>
    Public Async Function Insertar_SinRellenar_DiceSi_RellenaYGraba() As Task
        Responde(True)
        Dim enviado As String = Nothing
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)) _
            .ReturnsLazily(Function(l As InventarioDTO)
                               enviado = l.Producto
                               Return Task.FromResult(New InventarioDTO With {.Producto = "17404", .PorCodigoBarras = True})
                           End Function)
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("2900000174047")

        A.CallTo(Function() _servicio.Rellenar("1", "REI", _dia)).MustHaveHappenedOnceExactly()
        Assert.AreEqual("2900000174047", enviado, "Se manda lo leído tal cual: la API decide si es código o referencia.")
        Assert.IsNotNull(movimiento)
        Assert.AreEqual("17404", movimiento.Producto)
        Assert.AreEqual(InventarioModel.LECTURA_CODIGO, movimiento.Lectura)
    End Function

    <TestMethod()>
    Public Async Function Insertar_YaRellenado_NoVuelveAConsultarNiPregunta() As Task
        _pendientes.Add(New InventarioPendienteDTO With {.Usuario = "NUEVAVISION\Paloma", .Fecha = _dia, .Filas = 64})
        Dim vm = CrearViewModel()

        Await vm.InsertarProducto("17404")
        Await vm.InsertarProducto("40945")

        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustHaveHappenedTwiceExactly()
    End Function

    <TestMethod()>
    Public Async Function Insertar_OtroLoRellenoMientrasTanto_CuentaIgual() As Task
        Responde(True)
        A.CallTo(Function() _servicio.Rellenar(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .ThrowsAsync(New InventarioYaRellenadoException("El inventario de REI del 21/11/2026 ya está rellenado"))
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("17404")

        Assert.IsNotNull(movimiento)
        A.CallTo(Sub() _dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function SumarAFilaExistente_LeidoPorCodigo_MandaPorCodigoBarrasTrue() As Task
        _pendientes.Add(New InventarioPendienteDTO With {.Filas = 1})
        Dim linea As New InventarioDTO With {.NumOrden = 77, .Producto = "17404     ", .StockReal = 2}
        A.CallTo(Function() _servicio.BuscarLinea("1", "REI", _dia, "2900000174047")).Returns(Task.FromResult(linea))
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("2900000174047")

        A.CallTo(Function() _servicio.ActualizarLinea(linea, True)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(CShort(3), linea.StockReal)
        Assert.AreEqual(InventarioModel.LECTURA_CODIGO, movimiento.Lectura)
    End Function

    <TestMethod()>
    Public Async Function SumarAFilaExistente_TecleadaLaReferencia_MandaPorCodigoBarrasFalse() As Task
        _pendientes.Add(New InventarioPendienteDTO With {.Filas = 1})
        Dim linea As New InventarioDTO With {.NumOrden = 77, .Producto = "17404     ", .StockReal = 2}
        A.CallTo(Function() _servicio.BuscarLinea("1", "REI", _dia, "17404")).Returns(Task.FromResult(linea))
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto(" 17404 ")

        A.CallTo(Function() _servicio.ActualizarLinea(linea, False)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(InventarioModel.LECTURA_TECLEADO, movimiento.Lectura)
    End Function

    <TestMethod()>
    Public Sub EsAlmacenDeTienda_ReinaYAlcobendasSi_AlgeteYVacioNo()
        Assert.IsTrue(InventarioViewModel.EsAlmacenDeTienda("REI"))
        Assert.IsTrue(InventarioViewModel.EsAlmacenDeTienda("ALC"))
        Assert.IsFalse(InventarioViewModel.EsAlmacenDeTienda("ALG"))
        Assert.IsFalse(InventarioViewModel.EsAlmacenDeTienda(" alg "))
        Assert.IsFalse(InventarioViewModel.EsAlmacenDeTienda(Nothing))
    End Sub
End Class
