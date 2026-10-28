Imports System.Threading.Tasks
Imports CommunityToolkit.Mvvm.Messaging
Imports ControlesUsuario.Dialogs
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta

''' <summary>
''' Nesto#562 (NestoAPI#723): en el detalle del pedido, «Su pedido» se resalta si el cliente exige el P.O., y el botón
''' «Poner P.O.» pone el P.O. en la factura de la línea seleccionada (PUT Facturas/{empresa}/{numero}/SuPedido). Solo
''' Administración, Dirección e Informática.
''' </summary>
<TestClass()>
Public Class DetallePedidoSuPedidoObligatorioTests

    Private configuracion As IConfiguracion
    Private servicio As IPedidoVentaService
    Private dialogService As IServicioDialogos
    Private vm As DetallePedidoViewModel

    <TestInitialize()>
    Public Sub Initialize()
        configuracion = A.Fake(Of IConfiguracion)
        servicio = A.Fake(Of IPedidoVentaService)
        dialogService = A.Fake(Of IServicioDialogos)
        A.CallTo(Function() servicio.LeerSuPedidoObligatorio(A(Of String).Ignored, A(Of String).Ignored)).Returns(Task.FromResult(False))
        vm = New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), configuracion, servicio, New WeakReferenceMessenger(),
                                        dialogService, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1", .numero = 929500, .cliente = "15191", .contacto = "0", .suPedido = Nothing})
        vm.lineaActual = New LineaPedidoVentaWrapper(New LineaPedidoVentaDTO With {.estado = Constantes.LineasPedido.ESTADO_FACTURA, .Factura = "NV2616075"})
    End Sub

    Private Sub ElUsuarioEsDe(grupo As String)
        A.CallTo(Function() configuracion.UsuarioEnGrupo(grupo)).Returns(True)
    End Sub

    Private Sub ElUsuarioEscribe(texto As String)
        A.CallTo(Function() dialogService.GetText(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).Returns(texto)
    End Sub

    <TestMethod()>
    Public Async Function ClienteQueExigeElPO_SinPO_SeResaltaElCampo() As Task
        A.CallTo(Function() servicio.LeerSuPedidoObligatorio("1", "15191")).Returns(Task.FromResult(True))

        Await vm.CargarSuPedidoObligatorioAsync()

        Assert.IsTrue(vm.SuPedidoObligatorio)
        Assert.IsTrue(vm.FaltaSuPedido)
        StringAssert.Contains(vm.EtiquetaSuPedido, "*")
        StringAssert.Contains(vm.TooltipSuPedido, "P.O. obligatorio")
        vm.pedido.suPedido = "4500123456"
        Assert.IsFalse(vm.FaltaSuPedido, "Con el P.O. escrito ya no falta")
    End Function

    <TestMethod()>
    Public Async Function ClienteQueNoLoExige_NoSeResalta() As Task
        Await vm.CargarSuPedidoObligatorioAsync()

        Assert.IsFalse(vm.SuPedidoObligatorio)
        Assert.IsFalse(vm.FaltaSuPedido)
        Assert.AreEqual("Su Pedido / P.O.:", vm.EtiquetaSuPedido)
    End Function

    <TestMethod()>
    Public Sub PonerPO_SoloVisibleParaAdministracionDireccionEInformatica()
        Assert.IsFalse(vm.PuedePonerSuPedidoFactura)
        ElUsuarioEsDe(Constantes.GruposSeguridad.ALMACEN)
        Assert.IsFalse(vm.PuedePonerSuPedidoFactura)
        ElUsuarioEsDe(Constantes.GruposSeguridad.DIRECCION)
        Assert.IsTrue(vm.PuedePonerSuPedidoFactura)
    End Sub

    <TestMethod()>
    Public Sub PonerPO_SoloSiLaLineaEstaFacturada()
        Assert.IsTrue(vm.PonerSuPedidoFacturaCommand.CanExecute(Nothing))
        vm.lineaActual = New LineaPedidoVentaWrapper(New LineaPedidoVentaDTO With {.estado = 1})
        Assert.IsFalse(vm.PonerSuPedidoFacturaCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Async Function PonerPO_ConTexto_LlamaAlPutYLoDice() As Task
        ElUsuarioEscribe(" 4500123456 ")
        A.CallTo(Function() servicio.PonerSuPedidoFactura("1", "NV2616075", "4500123456")).
            Returns(Task.FromResult(New SuPedidoFacturaModel With {.Empresa = "1", .Numero = "NV2616075", .SuPedido = "4500123456", .SuPedidoAnterior = "VIEJO"}))

        Await vm.PonerSuPedidoFacturaAsync()

        A.CallTo(Function() servicio.PonerSuPedidoFactura("1", "NV2616075", "4500123456")).MustHaveHappenedOnceExactly()
        Assert.AreEqual("4500123456", vm.pedido.suPedido, "El pedido en pantalla se queda con el P.O. nuevo")
        A.CallTo(Sub() dialogService.ShowNotification(A(Of String).Ignored,
            A(Of String).That.Matches(Function(m) m.Contains("NV2616075") AndAlso m.Contains("4500123456") AndAlso m.Contains("antes: VIEJO")))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function PonerPO_SinTexto_NoLlamaALaApi() As Task
        ElUsuarioEscribe(Nothing) ' canceló
        Await vm.PonerSuPedidoFacturaAsync()
        ElUsuarioEscribe("   ")
        Await vm.PonerSuPedidoFacturaAsync()

        A.CallTo(Function() servicio.PonerSuPedidoFactura(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function PonerPO_DemasiadoLargo_LoDiceYNoLlama() As Task
        ElUsuarioEscribe(New String("X"c, 51))

        Await vm.PonerSuPedidoFacturaAsync()

        A.CallTo(Function() servicio.PonerSuPedidoFactura(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("50"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function PonerPO_SiLaApiLoRechaza_EnsenaElMotivo() As Task
        ElUsuarioEscribe("4500123456")
        A.CallTo(Function() servicio.PonerSuPedidoFactura(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).
            ThrowsAsync(New Exception("Solo Administración, Dirección o Informática pueden cambiar el P.O. de una factura emitida."))

        Await vm.PonerSuPedidoFacturaAsync()

        A.CallTo(Sub() dialogService.ShowError(A(Of String).That.Contains("Solo Administración"))).MustHaveHappenedOnceExactly()
        Assert.IsNull(vm.pedido.suPedido, "Si falla, el pedido en pantalla no cambia")
    End Function

End Class
