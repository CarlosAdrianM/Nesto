Imports FakeItEasy
Imports CommunityToolkit.Mvvm.Messaging
Imports Newtonsoft.Json
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Services
Imports System.Threading.Tasks
Imports Nesto.Models
Imports ControlesUsuario.Dialogs

''' <summary>
''' Nesto#620 (NestoAPI#806): lo que la API ya ha impreso en el servidor (DatosImpresion.ImpresoEnServidor) no lo imprime Nesto ni
''' lo pregunta, en ninguno de los tres sitios (Facturar rutas, Agencias/FacturadorPedido y el detalle del pedido); se dice dónde ha salido.
''' </summary>
<TestClass()>
Public Class ImpresoEnServidorTests

    Private Const IMPRESORA As String = "HP LaserJet 600"

    Private dialogService As IServicioDialogos
    Private facturacion As IServicioFacturacionRutas
    Private impresion As IServicioImpresionDocumentos

    <TestInitialize()>
    Public Sub Initialize()
        dialogService = A.Fake(Of IServicioDialogos)
        facturacion = A.Fake(Of IServicioFacturacionRutas)
        impresion = A.Fake(Of IServicioImpresionDocumentos)
        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).Returns(Task.FromResult(New ResultadoImpresion()))
        A.CallTo(Function() impresion.ImprimirFacturas(A(Of IEnumerable(Of FacturaCreadaDTO)).Ignored)).Returns(Task.FromResult(New ResultadoImpresion()))
    End Sub

    Private Shared Function EnLocal() As DocumentoParaImprimir
        Return New DocumentoParaImprimir With {.BytesPDF = {1}, .NumeroCopias = 1, .TipoBandeja = TipoBandejaImpresion.Upper}
    End Function

    Private Shared Function EnServidor(Optional impresora As String = IMPRESORA) As DocumentoParaImprimir
        Return New DocumentoParaImprimir With {.BytesPDF = {1}, .NumeroCopias = 2, .TipoBandeja = TipoBandejaImpresion.Upper, .ImpresoEnServidor = impresora}
    End Function

    Private Shared Function RespuestaTodoEnServidor() As FacturarRutasResponseDTO
        Dim response As New FacturarRutasResponseDTO With {.PedidosProcesados = 2}
        response.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV2617700", .DatosImpresion = EnServidor()})
        response.Albaranes.Add(New AlbaranCreadoDTO With {.NumeroAlbaran = 1, .DatosImpresion = EnServidor()})
        Return response
    End Function

    ''' <summary>1 factura impresa en el servidor + 1 albarán y 1 nota de entrega que imprime Nesto + 1 factura sin papel.</summary>
    Private Shared Function RespuestaMezcla() As FacturarRutasResponseDTO
        Dim response As New FacturarRutasResponseDTO With {.PedidosProcesados = 3}
        response.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV2617700", .DatosImpresion = EnServidor()})
        response.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV2617701"})
        response.Albaranes.Add(New AlbaranCreadoDTO With {.NumeroAlbaran = 1, .DatosImpresion = EnLocal()})
        response.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroPedido = 3, .Tipo = "Entrega", .DatosImpresion = EnLocal()})
        Return response
    End Function

    Private Shared Function RespuestaNingunoEnServidor() As FacturarRutasResponseDTO
        Dim response As New FacturarRutasResponseDTO With {.PedidosProcesados = 2}
        response.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV2617700", .DatosImpresion = EnLocal()})
        response.Albaranes.Add(New AlbaranCreadoDTO With {.NumeroAlbaran = 1, .DatosImpresion = EnLocal()})
        Return response
    End Function

#Region "La regla y el texto"
    <TestMethod()>
    Public Sub DocumentoParaImprimir_SinImpresoraONula_NoEstaImpresoEnServidor()
        Assert.IsFalse(New DocumentoParaImprimir().EstaImpresoEnServidor)
        Assert.IsFalse(New DocumentoParaImprimir With {.ImpresoEnServidor = "  "}.EstaImpresoEnServidor)
        Assert.IsTrue(EnServidor().EstaImpresoEnServidor)
        Assert.IsTrue(ImpresionEnServidor.HayQueImprimirEnLocal(EnLocal()))
        Assert.IsFalse(ImpresionEnServidor.HayQueImprimirEnLocal(EnServidor()))
        Assert.IsFalse(ImpresionEnServidor.HayQueImprimirEnLocal(Nothing), "Sin DatosImpresion no lleva papel")
    End Sub

    <TestMethod()>
    Public Sub Json_DeLaApi_TraeImpresoEnServidorDentroDeDatosImpresion()
        Dim json = "{""PedidosProcesados"":1,""Albaranes"":[],""NotasEntrega"":[],""PedidosConErrores"":[],""Facturas"":[" &
            "{""NumeroFactura"":""NV2617700"",""DatosImpresion"":{""BytesPDF"":""AQ=="",""NumeroCopias"":1,""TipoBandeja"":1,""ImpresoEnServidor"":""HP LaserJet 600""}}]}"

        Dim response = JsonConvert.DeserializeObject(Of FacturarRutasResponseDTO)(json)

        Assert.IsTrue(response.Facturas(0).EstaImpresoEnServidor)
        Assert.AreEqual(0, response.TotalDocumentosParaImprimir)
        Assert.AreEqual(1, response.TotalDocumentosImpresosEnServidor)
    End Sub

    <TestMethod()>
    Public Sub TextoImpresos_LaPredeterminadaYVarias()
        StringAssert.Contains(ImpresionEnServidor.TextoImpresos({EnServidor("(predeterminada)")}), "en la impresora predeterminada del servidor")
        Assert.AreEqual("2 documento(s) impresos en el servidor en «A», «B»",
                        ImpresionEnServidor.TextoImpresos({EnServidor("A"), EnLocal(), EnServidor("B")}))
        Assert.AreEqual(String.Empty, ImpresionEnServidor.TextoImpresos({EnLocal()}))
        Assert.AreEqual(String.Empty, ImpresionEnServidor.TextoImpresos(Nothing))
    End Sub
#End Region

#Region "FacturarRutasResponseDTO"
    <TestMethod()>
    Public Sub FacturarRutasResponse_TodoEnServidor_NadaParaImprimir()
        Dim response = RespuestaTodoEnServidor()

        Assert.AreEqual(0, response.FacturasParaImprimir)
        Assert.AreEqual(0, response.AlbaranesParaImprimir)
        Assert.AreEqual(0, response.TotalDocumentosParaImprimir)
        Assert.AreEqual(2, response.TotalDocumentosImpresosEnServidor)
        Assert.AreEqual("2 documento(s) impresos en el servidor en «HP LaserJet 600»", response.TextoImpresosEnServidor)
    End Sub

    <TestMethod()>
    Public Sub FacturarRutasResponse_Mezcla_CuentaSoloLosQueFaltan()
        Dim response = RespuestaMezcla()

        Assert.AreEqual(0, response.FacturasParaImprimir)
        Assert.AreEqual(1, response.AlbaranesParaImprimir)
        Assert.AreEqual(1, response.NotasDeEntregaParaImprimir)
        Assert.AreEqual(2, response.TotalDocumentosParaImprimir)
        Assert.AreEqual(1, response.TotalDocumentosImpresosEnServidor)
    End Sub

    <TestMethod()>
    Public Sub FacturarRutasResponse_NingunoEnServidor_ComoAntes()
        Dim response = RespuestaNingunoEnServidor()

        Assert.AreEqual(2, response.TotalDocumentosParaImprimir)
        Assert.AreEqual(0, response.TotalDocumentosImpresosEnServidor)
        Assert.AreEqual(String.Empty, response.TextoImpresosEnServidor)
    End Sub
#End Region

#Region "DocumentosImpresionPedidoDTO"
    <TestMethod()>
    Public Sub DocumentosImpresionPedido_TodoEnServidor_NoHayNadaParaImprimir()
        Dim dto As New DocumentosImpresionPedidoDTO()
        dto.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV1", .DatosImpresion = EnServidor()})

        Assert.IsFalse(dto.HayDocumentosParaImprimir)
        Assert.AreEqual(0, dto.TotalDocumentosParaImprimir)
        StringAssert.Contains(dto.TextoImpresosEnServidor, "1 documento(s) impresos en el servidor")
    End Sub

    <TestMethod()>
    Public Sub DocumentosImpresionPedido_Mezcla_CuentaLasCopiasDeLosQueFaltan()
        Dim dto As New DocumentosImpresionPedidoDTO()
        dto.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV1", .DatosImpresion = EnServidor()}) ' 2 copias, ya impresas
        dto.Albaranes.Add(New AlbaranCreadoDTO With {.NumeroAlbaran = 1, .DatosImpresion = EnLocal()}) ' 1 copia

        Assert.IsTrue(dto.HayDocumentosParaImprimir)
        Assert.AreEqual(1, dto.TotalDocumentosParaImprimir)
    End Sub

    <TestMethod()>
    Public Sub DocumentosImpresionPedido_NingunoEnServidor_ComoAntes()
        Dim dto As New DocumentosImpresionPedidoDTO()
        dto.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV1", .DatosImpresion = EnLocal()})
        dto.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroPedido = 1, .DatosImpresion = EnLocal()})

        Assert.IsTrue(dto.HayDocumentosParaImprimir)
        Assert.AreEqual(2, dto.TotalDocumentosParaImprimir)
        Assert.AreEqual(String.Empty, dto.TextoImpresosEnServidor)
    End Sub
#End Region

#Region "ServicioImpresionDocumentos"
    <TestMethod()>
    Public Async Function ServicioImpresion_LoImpresoEnServidor_NoLoMandaALaImpresora() As Task
        ' Sin bytes: si lo intentara imprimir daría error (ArgumentException) y contaría como DocumentosConError.
        Dim response As New FacturarRutasResponseDTO()
        response.Facturas.Add(New FacturaCreadaDTO With {.NumeroFactura = "NV1", .DatosImpresion = New DocumentoParaImprimir With {.ImpresoEnServidor = IMPRESORA}})
        response.Albaranes.Add(New AlbaranCreadoDTO With {.NumeroAlbaran = 1, .DatosImpresion = New DocumentoParaImprimir With {.ImpresoEnServidor = IMPRESORA}})
        response.NotasEntrega.Add(New NotaEntregaCreadaDTO With {.NumeroPedido = 1, .DatosImpresion = New DocumentoParaImprimir With {.ImpresoEnServidor = IMPRESORA}})

        Dim resultado = Await New ServicioImpresionDocumentos().ImprimirDocumentos(response)

        Assert.AreEqual(0, resultado.DocumentosImpresos)
        Assert.AreEqual(0, resultado.DocumentosConError)
    End Function
#End Region

#Region "Facturar rutas"
    Private Function CrearVm(response As FacturarRutasResponseDTO) As FacturarRutasPopupViewModel
        A.CallTo(Function() facturacion.FacturarRutas(A(Of FacturarRutasRequestDTO).Ignored)).Returns(Task.FromResult(response))
        Dim vm As New FacturarRutasPopupViewModel(A.Fake(Of IConfiguracion), dialogService, facturacion, impresion, A.Fake(Of IServicioReembolsoEnvios)) With {
            .PausaAntesDeCerrarMs = 0,
            .PausaTrasImprimirMs = 0
        }
        vm.TipoRutaSeleccionado = New TipoRutaInfoDTO With {.Id = "PROPIA"}
        Return vm
    End Function

    <TestMethod()>
    Public Sub Mensaje_Mezcla_PreguntaPorLosQueFaltanYDiceLosDelServidor()
        Dim mensaje = FacturarRutasPopupViewModel.MensajeDocumentosParaImprimir(RespuestaMezcla())

        StringAssert.Contains(mensaje, "Se han generado 2 documentos para imprimir")
        StringAssert.Contains(mensaje, "- 1 albarán(es)")
        StringAssert.Contains(mensaje, "- 1 nota(s) de entrega")
        Assert.IsFalse(mensaje.Contains("factura(s)"), "La factura ya impresa en el servidor no se pregunta")
        StringAssert.Contains(mensaje, "Además, 1 documento(s) impresos en el servidor en «HP LaserJet 600»: esos no se vuelven a imprimir.")
        StringAssert.Contains(mensaje, "¿Desea imprimirlos ahora?")
    End Sub

    <TestMethod()>
    Public Sub Mensaje_NingunoEnServidor_NoHablaDelServidor()
        Dim mensaje = FacturarRutasPopupViewModel.MensajeDocumentosParaImprimir(RespuestaNingunoEnServidor())

        StringAssert.Contains(mensaje, "Se han generado 2 documentos para imprimir")
        Assert.IsFalse(mensaje.Contains("servidor"))
    End Sub

    <TestMethod()>
    Public Async Function Facturar_TodoEnServidor_NoPreguntaNiImprimeYLoDiceElResumen() As Task
        Dim vm = CrearVm(RespuestaTodoEnServidor())

        Await vm.FacturarRutasAsync()

        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).MustNotHaveHappened()
        StringAssert.Contains(vm.MensajeEstado, "2 documento(s) impresos en el servidor en «HP LaserJet 600»")
        Assert.IsFalse(vm.MensajeEstado.Contains("para imprimir"))
    End Function

    <TestMethod()>
    Public Async Function Facturar_Mezcla_PreguntaSoloPorLosQueFaltan() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Imprimir documentos", A(Of String).Ignored)).Returns(True)
        Dim response = RespuestaMezcla()
        Dim vm = CrearVm(response)

        Await vm.FacturarRutasAsync()

        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Imprimir documentos",
            A(Of String).That.Matches(Function(m) m.Contains("Se han generado 2 documentos") AndAlso m.Contains("impresos en el servidor")))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() impresion.ImprimirDocumentos(response)).MustHaveHappenedOnceExactly()
    End Function
#End Region

#Region "FacturadorPedido (Agencias y detalle)"
    <TestMethod()>
    Public Sub HayAlgoParaImprimir_TodoEnServidorMezclaYNinguno()
        Assert.IsFalse(FacturadorPedido.HayAlgoParaImprimir(RespuestaTodoEnServidor()))
        Assert.IsTrue(FacturadorPedido.HayAlgoParaImprimir(RespuestaMezcla()))
        Assert.IsTrue(FacturadorPedido.HayAlgoParaImprimir(RespuestaNingunoEnServidor()))
        Assert.IsFalse(FacturadorPedido.HayAlgoParaImprimir(New FacturarRutasResponseDTO()))
        Assert.IsFalse(FacturadorPedido.HayAlgoParaImprimir(Nothing))
    End Sub

    <TestMethod()>
    Public Async Function Facturador_TodoEnServidor_NoImprimeYLoDiceEnElResumen() As Task
        Dim response = RespuestaTodoEnServidor()
        A.CallTo(Function() facturacion.FacturarPedido("1", 929244)).Returns(Task.FromResult(response))
        Dim facturador As New FacturadorPedido(facturacion, impresion, dialogService)

        Await facturador.FacturarAsync("1", 929244, imprimirDocumentos:=True)

        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowNotification("Facturación",
            A(Of String).That.Contains("2 documento(s) impresos en el servidor en «HP LaserJet 600»"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Facturador_Mezcla_ImprimeLosQueFaltan() As Task
        Dim response = RespuestaMezcla()
        A.CallTo(Function() facturacion.FacturarPedido("1", 929244)).Returns(Task.FromResult(response))
        Dim facturador As New FacturadorPedido(facturacion, impresion, dialogService)

        Await facturador.FacturarAsync("1", 929244, imprimirDocumentos:=True)

        A.CallTo(Function() impresion.ImprimirDocumentos(response)).MustHaveHappenedOnceExactly()
    End Function
#End Region

#Region "Detalle del pedido: Crear factura"
    Private Function CrearDetalle() As DetallePedidoViewModel
        Dim vm As New DetallePedidoViewModel(A.Fake(Of IServicioNavegacion), A.Fake(Of IConfiguracion), A.Fake(Of IPedidoVentaService),
                                             New WeakReferenceMessenger(), dialogService, A.Fake(Of IAbridorPedidos), A.Fake(Of IServicioAutenticacion))
        vm.ServicioImpresion = impresion
        Return vm
    End Function

    <TestMethod()>
    Public Async Function Detalle_FacturaImpresaEnServidor_NoPreguntaNiImprimeYDiceDonde() As Task
        Dim vm = CrearDetalle()

        Await vm.ImprimirFacturaEnPapelAsync(New CrearFacturaResponseDTO With {.NumeroFactura = "NV2617700", .DatosImpresion = EnServidor()})

        A.CallTo(Function() dialogService.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() impresion.ImprimirFacturas(A(Of IEnumerable(Of FacturaCreadaDTO)).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogService.ShowNotification("Factura en papel",
            "La factura NV2617700 va en papel y ya se ha impreso en el servidor en «HP LaserJet 600».")).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Detalle_FacturaEnPapelSinImprimirEnServidor_PreguntaEImprimeAqui() As Task
        A.CallTo(Function() dialogService.ShowConfirmationAnswer("Factura en papel", A(Of String).Ignored)).Returns(True)
        Dim vm = CrearDetalle()

        Await vm.ImprimirFacturaEnPapelAsync(New CrearFacturaResponseDTO With {.NumeroFactura = "NV2617700", .DatosImpresion = EnLocal()})

        A.CallTo(Function() impresion.ImprimirFacturas(A(Of IEnumerable(Of FacturaCreadaDTO)).That.Matches(
            Function(f) f.Count() = 1 AndAlso f.First().NumeroFactura = "NV2617700"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Detalle_FacturaSinPapel_NoHaceNada() As Task
        Dim vm = CrearDetalle()

        Await vm.ImprimirFacturaEnPapelAsync(New CrearFacturaResponseDTO With {.NumeroFactura = "NV2617700"})
        Await vm.ImprimirFacturaEnPapelAsync(Nothing)

        A.CallTo(dialogService).MustNotHaveHappened()
        A.CallTo(impresion).Where(Function(c) c.Method.Name = "ImprimirFacturas").MustNotHaveHappened()
    End Function
#End Region

End Class
