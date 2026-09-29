Imports System.Threading.Tasks
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Models.Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels
Imports Prism.Services.Dialogs

' Sugerencia 417 de Novedades (Paloma, 29/09/26): enlace de pago y datos de transferencia de un pedido desde la
' pestaña Pedidos de la ficha del cliente, con lo mismo que usa el detalle del pedido.
<TestClass()>
Public Class ClientesViewModelCobroPedidoTests

    Private _dialogService As IDialogService
    Private _servicioPedidos As IPedidoVentaService
    Private _copiado As String
    Private _vm As ClientesViewModel

    Private Class PortapapelesFalso
        Implements IPortapapelesTexto
        Public Copiado As String
        Public Sub CopiarTexto(texto As String) Implements IPortapapelesTexto.CopiarTexto
            Copiado = texto
        End Sub
    End Class

    <TestInitialize()>
    Public Sub Initialize()
        _dialogService = A.Fake(Of IDialogService)()
        _servicioPedidos = A.Fake(Of IPedidoVentaService)()
        _vm = New ClientesViewModel(A.Fake(Of IConfiguracion)(), _dialogService, A.Fake(Of IClienteComercialService)(),
                                    A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)()) With {
            .ServicioPedidos = _servicioPedidos,
            .PortapapelesCobroPedido = New PortapapelesFalso()
        }
        _vm.CorreoReclamarDeuda = "pilmon@example.com"
    End Sub

    Private Sub ElUsuarioConfirma(ok As Boolean)
        A.CallTo(Sub() _dialogService.ShowDialog(A(Of String).Ignored, A(Of IDialogParameters).Ignored, A(Of Action(Of IDialogResult)).Ignored)) _
         .Invokes(Sub(nombre As String, parametros As IDialogParameters, callback As Action(Of IDialogResult))
                      If callback Is Nothing Then Return
                      Dim resultado = A.Fake(Of IDialogResult)
                      A.CallTo(Function() resultado.Result).Returns(If(ok, ButtonResult.OK, ButtonResult.Cancel))
                      callback(resultado)
                  End Sub)
    End Sub

    Private Async Function SeleccionarPedido(numero As Integer, estadoLinea As Short, Optional formaPago As String = "RCB", Optional plazos As String = "CONTADO") As Task(Of ResumenPedido)
        Dim pedido As New PedidoVentaDTO With {.empresa = "1", .numero = numero, .cliente = "30676", .formaPago = formaPago, .plazosPago = plazos}
        pedido.Lineas.Add(New LineaPedidoVentaDTO With {.Cantidad = 1, .PrecioUnitario = 100D, .PorcentajeIva = 0.21D, .estado = estadoLinea})
        A.CallTo(Function() _servicioPedidos.cargarPedido("1", numero)).Returns(Task.FromResult(pedido))
        Dim resumen As New ResumenPedido With {.empresa = "1", .numero = numero, .cliente = "30676"}
        _vm.PedidoSeleccionadoCobro = resumen
        Await _vm.CargarPedidoParaCobroAsync(resumen)
        Return resumen
    End Function

    <TestMethod()>
    Public Async Function PedidoPendiente_EnviaElEnlaceConElTotalDelPedido() As Task
        Await SeleccionarPedido(927295, estadoLinea:=-1)
        A.CallTo(Function() _servicioPedidos.EnviarCobroTarjeta("pilmon@example.com", Nothing, 121D, "927295", "1", "30676")) _
            .Returns(Task.FromResult("https://pago.example/abc"))
        ElUsuarioConfirma(True)

        Assert.AreEqual(121D, _vm.ImporteCobroPedido)
        Assert.IsTrue(_vm.CanEnviarEnlacePagoPedido())
        Await _vm.EnviarEnlacePagoPedidoAsync()

        A.CallTo(Function() _servicioPedidos.EnviarCobroTarjeta("pilmon@example.com", Nothing, 121D, "927295", "1", "30676")).MustHaveHappenedOnceExactly()
        Assert.AreEqual("https://pago.example/abc", DirectCast(_vm.PortapapelesCobroPedido, PortapapelesFalso).Copiado)
    End Function

    <TestMethod()>
    Public Async Function SiElUsuarioCancela_NoSeEnviaNada() As Task
        Await SeleccionarPedido(927295, estadoLinea:=-1)
        ElUsuarioConfirma(False)

        Await _vm.EnviarEnlacePagoPedidoAsync()

        A.CallTo(Function() _servicioPedidos.EnviarCobroTarjeta(A(Of String).Ignored, A(Of String).Ignored, A(Of Decimal).Ignored, A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function PedidoYaFacturado_NoDejaMandarEnlace() As Task
        Await SeleccionarPedido(899737, estadoLinea:=4)

        Assert.IsFalse(_vm.CanEnviarEnlacePagoPedido(), "Lo facturado se cobra desde Deudas")
        StringAssert.Contains(_vm.TextoCobroPedido, "ya está facturado")
    End Function

    <TestMethod()>
    Public Async Function SinCorreoNiMovil_NoDejaMandarEnlace() As Task
        Await SeleccionarPedido(927295, estadoLinea:=-1)
        _vm.CorreoReclamarDeuda = Nothing
        _vm.MovilReclamarDeuda = " "

        Assert.IsFalse(_vm.CanEnviarEnlacePagoPedido())
    End Function

    <TestMethod()>
    Public Async Function SiCambiaLaSeleccionMientrasCarga_NoSeQuedaConElPedidoAnterior() As Task
        Dim resumen1 = Await SeleccionarPedido(927295, estadoLinea:=-1)
        _vm.PedidoSeleccionadoCobro = New ResumenPedido With {.empresa = "1", .numero = 1, .cliente = "30676"}

        Await _vm.CargarPedidoParaCobroAsync(resumen1)

        Assert.IsNull(_vm.PedidoParaCobro, "Nunca se cobra el pedido equivocado")
    End Function

    <TestMethod()>
    Public Async Function PrepagoPorTransferencia_CopiaLosDatos() As Task
        Await SeleccionarPedido(927295, estadoLinea:=-1, formaPago:="TRN", plazos:="PRE")
        A.CallTo(Function() _servicioPedidos.LeerDatosTransferencia("1", 927295)).Returns(Task.FromResult(New DatosTransferenciaPedidoModel With {.Texto = "IBAN ... Concepto: Cliente 30676 - Pedido 927295"}))

        Assert.IsTrue(_vm.PedidoCobroEsPrepagoPorTransferencia)
        Await _vm.CopiarDatosTransferenciaPedidoAsync()

        StringAssert.Contains(DirectCast(_vm.PortapapelesCobroPedido, PortapapelesFalso).Copiado, "Pedido 927295")
    End Function
End Class
