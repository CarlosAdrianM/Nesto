Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports CommunityToolkit.Mvvm.Messaging
Imports System.Collections.ObjectModel
Imports System.Threading.Tasks
Imports ControlesUsuario.Dialogs
Imports Newtonsoft.Json

''' <summary>
''' Nesto#503 (NestoAPI#571, cortes 1 y 2): la casilla «Recoge en tienda» del detalle del pedido. Refleja lo que trae la API, solo
''' viaja en el PUT si el usuario la toca, es fija en Alcobendas y Reina, no se toca con picking y, tras guardar el cambio, se vuelve
''' a leer el pedido (la API ha cambiado la ruta y los portes). El 400 de la API se enseña tal cual y la casilla vuelve a lo guardado.
''' </summary>
<TestClass()>
Public Class DetallePedidoRecogeEnTiendaTests

    Private servicio As IPedidoVentaService
    Private dialogService As IServicioDialogos
    Private vm As DetallePedidoViewModel

    <TestInitialize()>
    Public Sub Initialize()
        servicio = A.Fake(Of IPedidoVentaService)
        dialogService = A.Fake(Of IServicioDialogos)
        vm = New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), A.Fake(Of IConfiguracion), servicio, New WeakReferenceMessenger(),
                                        dialogService, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
    End Sub

    Private Shared Function Dto(almacen As String, ruta As String, recoge As Boolean?, Optional numero As Integer = 930100,
                                Optional picking As Integer = 0) As PedidoVentaDTO
        Return New PedidoVentaDTO With {
            .empresa = "1", .numero = numero, .cliente = "15191", .contacto = "0", .ruta = ruta, .RecogeEnTienda = recoge,
            .Lineas = New ObservableCollection(Of LineaPedidoVentaDTO) From {
                New LineaPedidoVentaDTO With {.id = 5001, .tipoLinea = 1, .Producto = "17404", .almacen = almacen, .estado = -1, .picking = picking}
            }}
    End Function

    Private Sub Cargar(pedido As PedidoVentaDTO)
        vm.pedido = New PedidoVentaWrapper(pedido)
    End Sub

    <TestMethod()>
    Public Sub Algete_ReflejaLoQueTraeLaApiYSePuedeCambiar()
        Cargar(Dto("ALG", "AT", False))

        Assert.IsTrue(vm.RecogeEnTiendaVisible)
        Assert.IsTrue(vm.PuedeCambiarRecogeEnTienda)
        Assert.IsFalse(vm.RecogeEnTienda)

        Cargar(Dto("ALG", "AM", True, numero:=930101))
        Assert.IsTrue(vm.RecogeEnTienda)
    End Sub

    <TestMethod()>
    Public Sub ApiSinElDato_SeMiraLaRuta()
        Cargar(Dto("ALG", "AM", Nothing))

        Assert.IsTrue(vm.RecogeEnTienda)
    End Sub

    <TestMethod()>
    Public Sub SinTocarla_NoViajaEnElPut()
        Cargar(Dto("ALG", "AT", False))

        Dim json As String = JsonConvert.SerializeObject(vm.pedido.Model)

        Assert.IsFalse(json.Contains("RecogeEnTienda"), json)
    End Sub

    <TestMethod()>
    Public Sub AlMarcarla_ViajaEnElPut()
        Cargar(Dto("ALG", "AT", False))
        Dim guardado As PedidoVentaDTO = vm.pedido.Model.CrearSnapshot()

        vm.RecogeEnTienda = True

        Assert.IsTrue(vm.RecogeEnTienda)
        Assert.IsTrue(vm.pedido.Model.RecogeEnTiendaTocado)
        StringAssert.Contains(JsonConvert.SerializeObject(vm.pedido.Model), """RecogeEnTienda"":true")
        Assert.IsFalse(vm.pedido.Model.Equals(guardado), "Marcarla y no guardar es un cambio sin guardar")
    End Sub

    <TestMethod()>
    Public Sub AlcobendasYReina_MarcadaYFija()
        Cargar(Dto("ALC", "ALC", True))

        Assert.IsTrue(vm.RecogeEnTiendaVisible)
        Assert.IsTrue(vm.RecogeEnTienda)
        Assert.IsFalse(vm.PuedeCambiarRecogeEnTienda)
        Assert.AreEqual(RecogeEnTiendaReglas.AYUDA_FIJA, vm.AyudaRecogeEnTienda)

        vm.RecogeEnTienda = False

        Assert.IsTrue(vm.RecogeEnTienda)
        Assert.IsFalse(vm.pedido.Model.RecogeEnTiendaTocado)
    End Sub

    <TestMethod()>
    Public Sub ConPicking_NoSeTocaYLaAyudaDiceElMotivo()
        Cargar(Dto("ALG", "AT", False, picking:=55))

        Assert.IsFalse(vm.PuedeCambiarRecogeEnTienda)
        Assert.AreEqual(RecogeEnTiendaReglas.AYUDA_BLOQUEADA, vm.AyudaRecogeEnTienda)
        vm.RecogeEnTienda = True
        Assert.IsFalse(vm.RecogeEnTienda)
        Assert.IsFalse(vm.pedido.Model.RecogeEnTiendaTocado)
    End Sub

    <TestMethod()>
    Public Sub OtroAlmacenOPedidoNuevo_NoSeOfrece()
        Cargar(Dto("AMZ", "FW", False))
        Assert.IsFalse(vm.RecogeEnTiendaVisible)

        Cargar(Dto("ALG", "AT", False, numero:=0))
        Assert.IsFalse(vm.RecogeEnTiendaVisible, "Al crear se usa la plantilla")
    End Sub

    <TestMethod()>
    Public Sub ConRutaDeRecogida_ElResumenNoHablaDePortes()
        Cargar(Dto("ALG", "AM", True))
        GetType(DetallePedidoViewModel).GetField("_resultadoPortes", Reflection.BindingFlags.NonPublic Or Reflection.BindingFlags.Instance).
            SetValue(vm, New ResultadoPortesDTO With {.ImporteMinimoPedidoSinPortes = 60})

        Assert.AreEqual(RecogeEnTiendaReglas.TEXTO_PORTES, vm.TextoPortes)
    End Sub

    <TestMethod()>
    Public Async Function TrasGuardarElCambio_SeVuelveALeerElPedido() As Task
        Cargar(Dto("ALG", "AT", False))
        Dim enviado As String = Nothing
        A.CallTo(Function() servicio.modificarPedido(A(Of PedidoVentaDTO).Ignored)).
            ReturnsLazily(Function(p As PedidoVentaDTO)
                              enviado = JsonConvert.SerializeObject(p)
                              Return Task.FromResult(New List(Of AvisoPedidoModel))
                          End Function)
        A.CallTo(Function() servicio.cargarPedido("1", 930100)).Returns(Task.FromResult(Dto("ALG", "AM", True)))
        vm.RecogeEnTienda = True

        Await vm.ModificarPedidoAsync()

        StringAssert.Contains(enviado, """RecogeEnTienda"":true")
        A.CallTo(Function() servicio.cargarPedido("1", 930100)).MustHaveHappenedOnceExactly()
        Assert.AreEqual("AM", vm.pedido.ruta)
        Assert.IsTrue(vm.RecogeEnTienda)
        Assert.IsFalse(vm.pedido.Model.RecogeEnTiendaTocado)
    End Function

    <TestMethod()>
    Public Async Function SinTocarla_AlGuardarNoSeVuelveALeer() As Task
        Cargar(Dto("ALG", "AT", False))
        A.CallTo(Function() servicio.modificarPedido(A(Of PedidoVentaDTO).Ignored)).Returns(Task.FromResult(New List(Of AvisoPedidoModel)))

        Await vm.ModificarPedidoAsync()

        A.CallTo(Function() servicio.cargarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function SiLaApiLoRechaza_SeEnsenaSuMotivoYLaCasillaVuelve() As Task
        Const MOTIVO As String = "No se puede cambiar «Recoge en tienda»: el pedido tiene una etiqueta de agencia."
        Cargar(Dto("ALG", "AT", False))
        A.CallTo(Function() servicio.modificarPedido(A(Of PedidoVentaDTO).Ignored)).ThrowsAsync(New Exception(MOTIVO))
        vm.RecogeEnTienda = True

        Await vm.ModificarPedidoAsync()

        A.CallTo(Sub() dialogService.ShowError(MOTIVO)).MustHaveHappened()
        Assert.IsFalse(vm.RecogeEnTienda)
        Assert.IsFalse(vm.pedido.Model.RecogeEnTiendaTocado)
    End Function

    <TestMethod()>
    Public Sub Reglas_AlmacenDelPedido_ComoLaApi()
        Dim lineas As New List(Of LineaPedidoVentaDTO) From {
            New LineaPedidoVentaDTO With {.tipoLinea = 1, .almacen = "ALG ", .estado = -1},
            New LineaPedidoVentaDTO With {.tipoLinea = 2, .almacen = "REI", .estado = -1},
            New LineaPedidoVentaDTO With {.tipoLinea = 1, .almacen = "ALC", .estado = 4}
        }
        Assert.AreEqual("ALG", RecogeEnTiendaReglas.AlmacenDelPedido(lineas), "Las de producto vivas mandan")

        lineas.Add(New LineaPedidoVentaDTO With {.tipoLinea = 1, .almacen = "REI", .estado = 1})
        Assert.IsNull(RecogeEnTiendaReglas.AlmacenDelPedido(lineas), "Varios almacenes")
    End Sub

    <TestMethod()>
    Public Sub Reglas_ParaGuardar_SoloSiSeTocaEnAlgete()
        Assert.IsNull(RecogeEnTiendaReglas.ParaGuardar("ALG", False, True))
        Assert.AreEqual(True, RecogeEnTiendaReglas.ParaGuardar("ALG", True, True))
        Assert.AreEqual(False, RecogeEnTiendaReglas.ParaGuardar("ALG", True, False))
        Assert.IsNull(RecogeEnTiendaReglas.ParaGuardar("ALC", True, False), "En Alcobendas desmarcar daría 400")
        Assert.IsNull(RecogeEnTiendaReglas.ParaGuardar("AMZ", True, True))
    End Sub

    <TestMethod()>
    Public Sub Reglas_Efectivo()
        Assert.IsTrue(RecogeEnTiendaReglas.Efectivo("REI", False, False, "AT"))
        Assert.IsFalse(RecogeEnTiendaReglas.Efectivo("ALG", False, False, "AT"))
        Assert.IsTrue(RecogeEnTiendaReglas.Efectivo("ALG", False, False, "AM"), "Sin tocar, manda la ruta")
        Assert.IsTrue(RecogeEnTiendaReglas.Efectivo("ALG", True, True, "AT"))
        Assert.IsFalse(RecogeEnTiendaReglas.Efectivo("AMZ", True, True, "AM"))
    End Sub
End Class
