Imports System.Collections.Generic
Imports System.Linq
Imports System.Net
Imports System.Net.Http
Imports System.Threading
Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.Inventario.InventarioModel

''' <summary>
''' Algete (12/11/26, dos días seguidos): en un almacén con ubicaciones Nesto creaba filas de inventario SIN hueco, que no se
''' pueden contabilizar («Productos que se ubican contados sin ubicación») y, si se quedan, paran el picking. Ahora se cuenta EN
''' un hueco (pasillo, fila y columna, que se quedan puestos de un producto al siguiente); sin hueco no se manda nada. Las
''' tiendas no cambian.
''' </summary>
<TestClass()>
Public Class InventarioViewModelHuecoTests

    Private ReadOnly _dia As New Date(2026, 11, 12)
    Private _configuracion As IConfiguracion
    Private _dialogos As IServicioDialogos
    Private _servicio As IServicioInventario
    Private _creadas As List(Of InventarioDTO)

    <TestInitialize()>
    Public Sub Inicializar()
        _configuracion = A.Fake(Of IConfiguracion)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _servicio = A.Fake(Of IServicioInventario)()
        _creadas = New List(Of InventarioDTO)
        Almacen(Constantes.Almacenes.ALMACEN_ALGETE)
        A.CallTo(Function() _servicio.LeerPendientes(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored)) _
            .Returns(Task.FromResult(New List(Of InventarioPendienteDTO) From {New InventarioPendienteDTO With {.Filas = 64}}))
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(Of InventarioDTO)(Nothing))
        A.CallTo(Function() _servicio.BuscarLineaEnHueco(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored,
                                                        A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(Of InventarioDTO)(Nothing))
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)) _
            .ReturnsLazily(Function(l As InventarioDTO)
                               _creadas.Add(l)
                               Return Task.FromResult(New InventarioDTO With {.Producto = l.Producto, .Descripcion = "TINTE", .PorCodigoBarras = False,
                                   .Pasillo = l.Pasillo, .Fila = l.Fila, .Columna = l.Columna})
                           End Function)
    End Sub

    Private Sub Almacen(numero As String)
        A.CallTo(Function() _configuracion.leerParametro(A(Of String).Ignored, Parametros.Claves.AlmacenInventario)) _
            .Returns(Task.FromResult(numero))
    End Sub

    Private Function CrearViewModel() As InventarioViewModel
        Dim vm As New InventarioViewModel(A.Fake(Of IServicioNavegacion)(), _configuracion, _dialogos, _servicio)
        vm.fechaSeleccionada = _dia
        Return vm
    End Function

    Private Sub NoSeHaLlamadoALaApi()
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.BuscarLineaEnHueco(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored,
                                                        A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.ActualizarLinea(A(Of InventarioDTO).Ignored, A(Of Boolean?).Ignored)).MustNotHaveHappened()
    End Sub

    <TestMethod()>
    Public Sub AlmacenConUbicaciones_AlgeteSi_TiendasYVacioNo()
        Assert.IsTrue(CrearViewModel().AlmacenConUbicaciones)
        Almacen("REI")
        Assert.IsFalse(CrearViewModel().AlmacenConUbicaciones)
        Almacen("ALC")
        Assert.IsFalse(CrearViewModel().AlmacenConUbicaciones)
        Almacen(Nothing)
        Assert.IsFalse(CrearViewModel().AlmacenConUbicaciones)
    End Sub

    <TestMethod()>
    Public Sub NormalizarParteHueco_CompletaCifrasATres()
        Assert.AreEqual("001", InventarioViewModel.NormalizarParteHueco("1"))
        Assert.AreEqual("012", InventarioViewModel.NormalizarParteHueco(" 12 "))
        Assert.AreEqual("123", InventarioViewModel.NormalizarParteHueco("123"))
        Assert.AreEqual("A1", InventarioViewModel.NormalizarParteHueco("A1"))
        Assert.IsNull(InventarioViewModel.NormalizarParteHueco(""))
        Assert.IsNull(InventarioViewModel.NormalizarParteHueco(Nothing))
        Assert.IsNull(InventarioViewModel.NormalizarParteHueco("1234"))
    End Sub

    <TestMethod()>
    Public Async Function Algete_SinHueco_NoLlamaALaApiYAvisa() As Task
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("44901")

        Assert.IsNull(movimiento)
        A.CallTo(Sub() _dialogos.ShowError("En ALG el inventario va por huecos: indique el pasillo, la fila y la columna donde está el producto.")) _
            .MustHaveHappenedOnceExactly()
        NoSeHaLlamadoALaApi()
        Assert.AreEqual(0, vm.movimientosDia.Count)
    End Function

    <TestMethod()>
    Public Async Function Algete_HuecoIncompleto_NoLlamaALaApiYAvisa() As Task
        Dim vm = CrearViewModel()
        vm.Pasillo = "3"
        vm.Fila = "1"

        Assert.IsNull(Await vm.InsertarProducto("44901"))

        A.CallTo(Sub() _dialogos.ShowError(InventarioViewModel.MensajeFaltaHueco("ALG"))).MustHaveHappenedOnceExactly()
        NoSeHaLlamadoALaApi()
    End Function

    <TestMethod()>
    Public Async Function Algete_ConHueco_MandaPasilloFilaYColumnaATresCifras() As Task
        Dim vm = CrearViewModel()
        vm.Pasillo = "3"
        vm.Fila = "1"
        vm.Columna = "12"

        Dim movimiento = Await vm.InsertarProducto("44901")

        Assert.IsNotNull(movimiento)
        A.CallTo(Function() _servicio.BuscarLineaEnHueco("1", "ALG", _dia, "44901", "003", "001", "012")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        Dim enviada = _creadas.Single()
        Assert.AreEqual("003", enviada.Pasillo)
        Assert.AreEqual("001", enviada.Fila)
        Assert.AreEqual("012", enviada.Columna)
        Assert.AreEqual("ALG", enviada.Almacen)
        Assert.AreEqual("003-001-012", movimiento.Hueco)
        ' Las casillas enseñan el hueco ya normalizado.
        Assert.AreEqual("003", vm.Pasillo)
        Assert.AreEqual("012", vm.Columna)
    End Function

    <TestMethod()>
    Public Async Function Algete_ConHueco_LoMandaEnElJsonDelPost() As Task
        ' Los nombres del modelo Inventario de la API: Pasillo, Fila, Columna.
        Dim manejador As New ManejadorFalso With {.Cuerpo = "{""Número"":""44901"",""Pasillo"":""003"",""Fila"":""001"",""Columna"":""012""}"}
        Dim servicio = CrearServicio(manejador)

        Await servicio.CrearLinea(New InventarioDTO With {.Empresa = "1", .Almacen = "ALG", .Fecha = _dia, .Producto = "44901", .StockReal = 1,
            .Pasillo = "003", .Fila = "001", .Columna = "012"})

        Dim json = manejador.Cuerpos.Single()
        StringAssert.Contains(json, """Pasillo"":""003""")
        StringAssert.Contains(json, """Fila"":""001""")
        StringAssert.Contains(json, """Columna"":""012""")
        Assert.IsFalse(json.Contains("""Hueco"""), "Hueco es solo para la rejilla")
    End Function

    <TestMethod()>
    Public Async Function Servicio_BuscarLineaEnHueco_PreguntaPorEseHueco() As Task
        Dim manejador As New ManejadorFalso With {.Estado = HttpStatusCode.NotFound}
        Dim servicio = CrearServicio(manejador)

        Assert.IsNull(Await servicio.BuscarLineaEnHueco("1", "ALG", _dia, "44901", "003", "001", "012"))

        StringAssert.EndsWith(manejador.Peticiones.Single(), "&producto=44901&pasillo=003&fila=001&columna=012")
    End Function

    <TestMethod()>
    Public Async Function Algete_ElHuecoSeConservaParaElSiguienteProducto() As Task
        Dim vm = CrearViewModel()
        vm.Pasillo = "3"
        vm.Fila = "1"
        vm.Columna = "2"

        Await vm.InsertarProducto("44901")
        Await vm.InsertarProducto("17404")

        Assert.AreEqual("003", vm.Pasillo)
        Assert.AreEqual("001", vm.Fila)
        Assert.AreEqual("002", vm.Columna)
        Assert.AreEqual(2, _creadas.Count)
        Assert.IsTrue(_creadas.All(Function(l) l.Pasillo = "003" AndAlso l.Fila = "001" AndAlso l.Columna = "002"))
        A.CallTo(Function() _servicio.BuscarLineaEnHueco("1", "ALG", _dia, "17404", "003", "001", "002")).MustHaveHappenedOnceExactly()
        Assert.AreEqual(String.Empty, vm.numeroProducto)
    End Function

    <TestMethod()>
    Public Async Function Algete_YaHayFilaEnEseHueco_SumaAEsa() As Task
        Dim existente As New InventarioDTO With {.NumOrden = 7, .Producto = "44901", .StockReal = 2, .Pasillo = "003", .Fila = "001", .Columna = "002"}
        A.CallTo(Function() _servicio.BuscarLineaEnHueco("1", "ALG", _dia, "44901", "003", "001", "002")).Returns(Task.FromResult(existente))
        Dim vm = CrearViewModel()
        vm.Pasillo = "003"
        vm.Fila = "001"
        vm.Columna = "002"

        Dim movimiento = Await vm.InsertarProducto("44901")

        A.CallTo(Function() _servicio.ActualizarLinea(existente, False)).MustHaveHappenedOnceExactly()
        Assert.AreEqual(CShort(3), existente.StockReal)
        Assert.AreEqual("003-001-002", movimiento.Hueco)
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Algete_PostYaTieneFila_VuelveALeerEnElMismoHueco() As Task
        Dim existente As New InventarioDTO With {.NumOrden = 7, .Producto = "44901", .StockReal = 2, .Pasillo = "003", .Fila = "001", .Columna = "002"}
        Dim lecturas = 0
        A.CallTo(Function() _servicio.BuscarLineaEnHueco("1", "ALG", _dia, "44901", "003", "001", "002")) _
            .ReturnsLazily(Function()
                               lecturas += 1
                               Return Task.FromResult(If(lecturas = 1, Nothing, existente))
                           End Function)
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)) _
            .ThrowsAsync(New ConflictoInventarioException("Ya tiene fila", New ConflictoInventarioDTO With {.Codigo = CodigosConflictoInventario.YA_TIENE_FILA}))
        Dim vm = CrearViewModel()
        vm.Pasillo = "3"
        vm.Fila = "1"
        vm.Columna = "2"

        Assert.IsNotNull(Await vm.InsertarProducto("44901"))

        Assert.AreEqual(2, lecturas)
        A.CallTo(Function() _servicio.BuscarLinea(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() _servicio.ActualizarLinea(existente, False)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Algete_ApiDice400_EnsenaElMensajeTalCual() As Task
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)) _
            .ThrowsAsync(New Exception("En ALG indique el hueco donde ha contado el producto."))
        Dim vm = CrearViewModel()
        vm.Pasillo = "3"
        vm.Fila = "1"
        vm.Columna = "2"

        Assert.IsNull(Await vm.InsertarProducto("44901"))

        A.CallTo(Sub() _dialogos.ShowError("En ALG indique el hueco donde ha contado el producto.")).MustHaveHappenedOnceExactly()
        Assert.AreEqual(0, vm.movimientosDia.Count)
    End Function

    <TestMethod()>
    Public Async Function Servicio_Post400_LanzaConElMessageDeLaApi() As Task
        Dim manejador As New ManejadorFalso With {.Estado = HttpStatusCode.BadRequest,
            .Cuerpo = "{""Message"":""En ALG el inventario va por huecos: indique el hueco.""}"}
        Dim servicio = CrearServicio(manejador)

        Dim ex = Await Assert.ThrowsExceptionAsync(Of Exception)(
            Function() servicio.CrearLinea(New InventarioDTO With {.Empresa = "1", .Almacen = "ALG", .Fecha = _dia, .Producto = "44901"}))

        Assert.AreEqual("En ALG el inventario va por huecos: indique el hueco.", ex.Message)
    End Function

    <TestMethod()>
    Public Async Function Algete_ApiDice409VariasUbicaciones_EnsenaElMensajeTalCual() As Task
        A.CallTo(Function() _servicio.BuscarLineaEnHueco(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored,
                                                        A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .ThrowsAsync(New ConflictoInventarioException("El 44901 tiene 2 filas: indique la ubicación.",
                New ConflictoInventarioDTO With {.Codigo = CodigosConflictoInventario.VARIAS_UBICACIONES}))
        Dim vm = CrearViewModel()
        vm.Pasillo = "3"
        vm.Fila = "1"
        vm.Columna = "2"

        Assert.IsNull(Await vm.InsertarProducto("44901"))

        A.CallTo(Sub() _dialogos.ShowError("El 44901 tiene 2 filas: indique la ubicación.")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.CrearLinea(A(Of InventarioDTO).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Tienda_NoPideHuecoYMandaLoDeSiempre() As Task
        Almacen("REI")
        Dim vm = CrearViewModel()

        Dim movimiento = Await vm.InsertarProducto("44901")

        Assert.IsNotNull(movimiento)
        A.CallTo(Function() _servicio.BuscarLinea("1", "REI", _dia, "44901")).MustHaveHappenedOnceExactly()
        A.CallTo(Function() _servicio.BuscarLineaEnHueco(A(Of String).Ignored, A(Of String).Ignored, A(Of Date).Ignored, A(Of String).Ignored,
                                                        A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        Dim enviada = _creadas.Single()
        Assert.IsNull(enviada.Pasillo)
        Assert.IsNull(enviada.Fila)
        Assert.IsNull(enviada.Columna)
        A.CallTo(Sub() _dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Tienda_ConHuecoEscrito_LoIgnora() As Task
        ' Si se cambia de ALG a una tienda con las casillas puestas (ya no se ven), no se manda el hueco.
        Almacen("ALC")
        Dim vm = CrearViewModel()
        vm.Pasillo = "3"
        vm.Fila = "1"
        vm.Columna = "2"

        Await vm.InsertarProducto("44901")

        Assert.IsNull(_creadas.Single().Pasillo)
    End Function

    <TestMethod()>
    Public Sub CambiarAlmacen_AvisaDeAlmacenConUbicaciones()
        Almacen("REI")
        Dim vm = CrearViewModel()
        Dim avisos As New List(Of String)
        AddHandler vm.PropertyChanged, Sub(s, e) avisos.Add(e.PropertyName)

        vm.almacen = "ALG"

        CollectionAssert.Contains(avisos, NameOf(InventarioViewModel.AlmacenConUbicaciones))
        Assert.IsTrue(vm.AlmacenConUbicaciones)
    End Sub

    <TestMethod()>
    Public Sub TextoHueco_ConYSinUbicacion()
        Assert.AreEqual("003-001-012", InventarioModel.TextoHueco("003", "001 ", "012"))
        Assert.AreEqual("sin ubicación", InventarioModel.TextoHueco(Nothing, " ", Nothing))
        Assert.AreEqual("sin ubicación", (New InventarioDTO With {.Producto = "44901"}).Hueco)
    End Sub

    ' --- HTTP falso ---

    Private Class ManejadorFalso
        Inherits HttpMessageHandler

        Public ReadOnly Peticiones As New List(Of String)
        Public ReadOnly Cuerpos As New List(Of String)
        Public Property Estado As HttpStatusCode = HttpStatusCode.OK
        Public Property Cuerpo As String = String.Empty

        Protected Overrides Async Function SendAsync(request As HttpRequestMessage, cancellationToken As CancellationToken) As Task(Of HttpResponseMessage)
            Peticiones.Add(request.Method.Method + " " + request.RequestUri.PathAndQuery)
            Cuerpos.Add(If(request.Content Is Nothing, Nothing, Await request.Content.ReadAsStringAsync()))
            Return New HttpResponseMessage(Estado) With {.Content = New StringContent(If(Cuerpo, String.Empty))}
        End Function
    End Class

    Private Shared Function CrearServicio(manejador As ManejadorFalso) As ServicioInventario
        Dim factoria = A.Fake(Of IClienteApiFactory)()
        A.CallTo(Function() factoria.Crear()).ReturnsLazily(Function() New HttpClient(manejador, False) With {
            .BaseAddress = New Uri("http://localhost/api/")
        })
        Return New ServicioInventario(factoria)
    End Function
End Class
