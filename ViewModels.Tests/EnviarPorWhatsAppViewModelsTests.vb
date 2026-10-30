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

''' <summary>
''' Nesto#570 (NestoAPI#737, sugerencias 866/867): «Enviar por WhatsApp…» en el detalle del pedido (pedido y factura). Con el
''' servicio de enlaces falso y el navegador falso: se pide el enlace del documento y se abre WhatsApp Web con el móvil del
''' cliente y el texto de usted que devuelve la API, URL-encoded.
''' </summary>
<TestClass()>
Public Class EnviarPorWhatsAppViewModelsTests

    Private Const TEXTO As String = "Buenos días, Peluquería Ana:" & vbLf & vbLf & "Le enviamos su pedido 929498, de 1.234,56 €. Puede descargarlo en PDF desde este enlace:" & vbLf & "https://api.nuevavision.es/d/abc"

    Private _servicio As IServicioEnlacesDescarga
    Private _dialogos As IServicioDialogos
    Private _abiertas As List(Of String)

    <TestInitialize()>
    Public Sub Initialize()
        _servicio = A.Fake(Of IServicioEnlacesDescarga)()
        _dialogos = A.Fake(Of IServicioDialogos)()
        _abiertas = New List(Of String)
        A.CallTo(Function() _servicio.CrearEnlace(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(New EnlaceDescargaDocumento With {
                .Url = "https://api.nuevavision.es/d/abc",
                .Texto = TEXTO,
                .Moviles = New List(Of String) From {"34600112233"}
            }))
    End Sub

    Private Function DetallePedido() As DetallePedidoViewModel
        Dim vm = New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), A.Fake(Of IConfiguracion), A.Fake(Of IPedidoVentaService),
                                            New WeakReferenceMessenger(), _dialogos, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
        vm.EnvioPorWhatsApp = New EnvioDocumentoPorWhatsApp(_servicio, _dialogos, Sub(url) _abiertas.Add(url))
        vm.pedido = New PedidoVentaWrapper(New PedidoVentaDTO With {.empresa = "1  ", .numero = 929498, .cliente = "15191 ", .contacto = "0  "})
        Return vm
    End Function

    <TestMethod()>
    Public Async Function DetallePedido_EnviarPedidoPorWhatsApp_AbreWhatsAppWebConElMovilYElTexto() As Task
        Dim vm = DetallePedido()

        Await vm.EnviarPedidoPorWhatsAppAsync()

        A.CallTo(Function() _servicio.CrearEnlace("Pedido", "1", "929498")).MustHaveHappenedOnceExactly()
        Assert.AreEqual(1, _abiertas.Count)
        Assert.AreEqual("https://web.whatsapp.com/send?phone=34600112233&text=" & Uri.EscapeDataString(TEXTO), _abiertas(0))
        StringAssert.Contains(_abiertas(0), "Le%20enviamos%20su%20pedido%20929498")
    End Function

    <TestMethod()>
    Public Async Function DetallePedido_EnviarFacturaPorWhatsApp_PideElEnlaceDeLaFacturaDeLaLinea() As Task
        Dim vm = DetallePedido()
        vm.lineaActual = New LineaPedidoVentaWrapper() With {.tipoLinea = 1, .Producto = "12345", .Cantidad = 1, .estado = 4, .Factura = "NV2616199"}
        Assert.IsTrue(vm.EnviarFacturaPorWhatsAppCommand.CanExecute(Nothing), "Con la línea facturada se puede")

        Await vm.EnviarFacturaPorWhatsAppAsync()

        A.CallTo(Function() _servicio.CrearEnlace("Factura", "1", "NV2616199")).MustHaveHappenedOnceExactly()
        StringAssert.StartsWith(_abiertas(0), "https://web.whatsapp.com/send?phone=34600112233&text=")
    End Function

    <TestMethod()>
    Public Async Function DetallePedido_ClienteSinMovil_AvisaYSiNoQuiere_NoAbreNada() As Task
        A.CallTo(Function() _servicio.CrearEnlace(A(Of String).Ignored, A(Of String).Ignored, A(Of String).Ignored)) _
            .Returns(Task.FromResult(New EnlaceDescargaDocumento With {.Url = "https://api.nuevavision.es/d/abc", .Texto = TEXTO}))
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(False)
        Dim vm = DetallePedido()

        Await vm.EnviarPedidoPorWhatsAppAsync()

        Assert.AreEqual(0, _abiertas.Count)
        A.CallTo(Function() _dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).That.Contains("no tiene ningún móvil"))) _
            .MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub DetallePedido_SinPedido_NoSePuedeEnviarPorWhatsApp()
        Dim vm = New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), A.Fake(Of IConfiguracion), A.Fake(Of IPedidoVentaService),
                                            New WeakReferenceMessenger(), _dialogos, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))

        Assert.IsFalse(vm.EnviarPedidoPorWhatsAppCommand.CanExecute(Nothing))
        Assert.IsFalse(vm.EnviarFacturaPorWhatsAppCommand.CanExecute(Nothing))
    End Sub

End Class
