Imports System.Threading.Tasks
Imports CommunityToolkit.Mvvm.Messaging
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Services
Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels

''' <summary>
''' Nesto#563 (NestoAPI#724, sugerencia 831): «Enviar por correo…» en el detalle del pedido (pedido y factura) y en la
''' ficha del cliente (mandato SEPA). Con el servicio falso: si el diálogo se cancela no se llama a la API; si se acepta,
''' se llama con los destinatarios escritos.
''' </summary>
<TestClass()>
Public Class EnviarPorCorreoViewModelsTests

    Private _servicioCorreo As IServicioEnvioCorreoDocumentos
    Private _dialogos As IServicioDialogos

    <TestInitialize()>
    Public Sub Initialize()
        _servicioCorreo = A.Fake(Of IServicioEnvioCorreoDocumentos)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        A.CallTo(Function() _servicioCorreo.LeerCorreosContacto("1", "15191", "0")) _
            .Returns(Task.FromResult(New List(Of String) From {"ana@cliente.es"}))
    End Sub

    Private Sub ElUsuarioPulsa(boton As ResultadoBoton, Optional destinatarios As String = Nothing)
        Dim parametros As New ParametrosDialogo From {{EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS, destinatarios}}
        A.CallTo(Function() _dialogos.ShowDialogAsync(EnviarPorCorreoDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoDialogo(boton, parametros)))
    End Sub

    Private Function DetallePedido() As DetallePedidoViewModel
        Dim vm = New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), A.Fake(Of IConfiguracion), A.Fake(Of IPedidoVentaService),
                                            New WeakReferenceMessenger(), _dialogos, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
        vm.EnvioPorCorreo = New EnvioDocumentoPorCorreo(_servicioCorreo, _dialogos)
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1  ", .numero = 929498, .cliente = "15191 ", .contacto = "0  "})
        Return vm
    End Function

    <TestMethod()>
    Public Async Function DetallePedido_EnviarPedido_DialogoCancelado_NoLlamaALaApi() As Task
        ElUsuarioPulsa(ResultadoBoton.Cancel, "ana@cliente.es")
        Dim vm = DetallePedido()

        Await vm.EnviarPedidoPorCorreoAsync()

        A.CallTo(Function() _servicioCorreo.EnviarPedido(A(Of String).Ignored, A(Of Integer).Ignored, A(Of PeticionEnvioCorreo).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function DetallePedido_EnviarPedido_Aceptado_LlamaConLosDestinatariosYAvisa() As Task
        ElUsuarioPulsa(ResultadoBoton.OK, "ana@cliente.es; otra@cliente.es")
        A.CallTo(Function() _servicioCorreo.EnviarPedido(A(Of String).Ignored, A(Of Integer).Ignored, A(Of PeticionEnvioCorreo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoEnvioCorreo With {.Destinatarios = New List(Of String) From {"ana@cliente.es", "otra@cliente.es"}}))
        Dim vm = DetallePedido()

        Await vm.EnviarPedidoPorCorreoAsync()

        A.CallTo(Function() _servicioCorreo.EnviarPedido("1", 929498, A(Of PeticionEnvioCorreo).That.Matches(
            Function(p) p.Destinatarios.Count = 2 AndAlso p.Destinatarios(0) = "ana@cliente.es" AndAlso p.Destinatarios(1) = "otra@cliente.es"))) _
            .MustHaveHappenedOnceExactly()
        A.CallTo(Sub() _dialogos.ShowNotification(A(Of String).Ignored, "Enviado a ana@cliente.es, otra@cliente.es.")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function DetallePedido_EnviarPedido_ProponeLasPersonasDeContactoDelCliente() As Task
        Dim abiertoCon As ParametrosDialogo = Nothing
        A.CallTo(Function() _dialogos.ShowDialogAsync(EnviarPorCorreoDialogViewModel.NOMBRE, A(Of ParametrosDialogo).Ignored)) _
            .ReturnsLazily(Function(nombre As String, p As ParametrosDialogo)
                               abiertoCon = p
                               Return Task.FromResult(New ResultadoDialogo(ResultadoBoton.Cancel))
                           End Function)
        Dim vm = DetallePedido()

        Await vm.EnviarPedidoPorCorreoAsync()

        Assert.AreEqual("ana@cliente.es", abiertoCon.GetValue(Of String)(EnviarPorCorreoDialogViewModel.PARAMETRO_DESTINATARIOS))
    End Function

    <TestMethod()>
    Public Sub DetallePedido_SinPedido_NoSePuedeEnviar()
        Dim vm = New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), A.Fake(Of IConfiguracion), A.Fake(Of IPedidoVentaService),
                                            New WeakReferenceMessenger(), _dialogos, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))

        Assert.IsFalse(vm.EnviarPedidoPorCorreoCommand.CanExecute(Nothing))
        Assert.IsFalse(vm.EnviarFacturaPorCorreoCommand.CanExecute(Nothing))
    End Sub

    <TestMethod()>
    Public Async Function DetallePedido_EnviarFactura_Aceptado_LlamaConLaFacturaDeLaLinea() As Task
        ElUsuarioPulsa(ResultadoBoton.OK, "facturas@cliente.es")
        A.CallTo(Function() _servicioCorreo.EnviarFactura(A(Of String).Ignored, A(Of String).Ignored, A(Of PeticionEnvioCorreo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoEnvioCorreo()))
        Dim vm = DetallePedido()
        vm.lineaActual = New LineaPedidoVentaWrapper() With {.tipoLinea = 1, .Producto = "12345", .Cantidad = 1, .estado = 4, .Factura = "NV2616199"}
        Assert.IsTrue(vm.EnviarFacturaPorCorreoCommand.CanExecute(Nothing), "Con la línea facturada se puede")

        Await vm.EnviarFacturaPorCorreoAsync()

        A.CallTo(Function() _servicioCorreo.EnviarFactura("1", "NV2616199", A(Of PeticionEnvioCorreo).That.Matches(
            Function(p) p.Destinatarios.Count = 1 AndAlso p.Destinatarios(0) = "facturas@cliente.es"))).MustHaveHappenedOnceExactly()
    End Function

    Private Function FichaCliente() As ClientesViewModel
        Dim vm = New ClientesViewModel(A.Fake(Of IConfiguracion)(), _dialogos, A.Fake(Of IClienteComercialService)(),
                                       A.Fake(Of IRapportService)(), A.Fake(Of IServicioAutenticacion)())
        vm.EnvioPorCorreo = New EnvioDocumentoPorCorreo(_servicioCorreo, _dialogos)
        vm.empresaActual = "1"
        vm.clienteActual = "15191"
        vm.contactoActual = "0"
        vm.cuentaActiva = New CCCModel With {.Número = "2"}
        Return vm
    End Function

    <TestMethod()>
    Public Async Function FichaCliente_EnviarMandato_DialogoCancelado_NoLlamaALaApi() As Task
        ElUsuarioPulsa(ResultadoBoton.Cancel, "ana@cliente.es")
        Dim vm = FichaCliente()

        Await vm.EnviarMandatoPorCorreoAsync()

        A.CallTo(Function() _servicioCorreo.EnviarMandato(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored,
                                                          A(Of String).Ignored, A(Of PeticionEnvioCorreo).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function FichaCliente_EnviarMandato_Aceptado_LlamaConLaCuentaActivaYLosDestinatarios() As Task
        ElUsuarioPulsa(ResultadoBoton.OK, "ana@cliente.es")
        A.CallTo(Function() _servicioCorreo.EnviarMandato(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored,
                                                          A(Of String).Ignored, A(Of PeticionEnvioCorreo).Ignored)) _
            .Returns(Task.FromResult(New ResultadoEnvioCorreo()))
        Dim vm = FichaCliente()

        Await vm.EnviarMandatoPorCorreoAsync()

        A.CallTo(Function() _servicioCorreo.EnviarMandato("1", "15191", "0", "2", A(Of String).Ignored, A(Of PeticionEnvioCorreo).That.Matches(
            Function(p) p.Destinatarios.Count = 1 AndAlso p.Destinatarios(0) = "ana@cliente.es"))).MustHaveHappenedOnceExactly()
    End Function

End Class
