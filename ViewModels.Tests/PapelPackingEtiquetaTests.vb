Imports FakeItEasy
Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Newtonsoft.Json
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Services
Imports Nesto.ViewModels
Imports System.Threading.Tasks

''' <summary>
''' NestoAPI#828 (S5): la etiqueta de Agencias factura con el mismo núcleo que F8 en Ariadna (POST FacturarEtiqueta), imprime el
''' papel aquí para meterlo en la caja y lo apunta en la API; la casilla se marca sola según FacturarAlImprimirEtiqueta; Facturar rutas
''' enseña lo del packing.
''' </summary>
<TestClass()>
Public Class PapelPackingEtiquetaTests
    Private Const PEDIDO As Integer = 929914
    Private Const ENVIO As Integer = 251001

    Private facturacion As IServicioFacturacionRutas
    Private impresion As IServicioImpresionDocumentos
    Private dialogos As IServicioDialogos
    Private facturador As FacturadorPedido
    Private confirmadas As List(Of ImpresionDocumentoEnPapelDTO)

    <TestInitialize()>
    Public Sub Inicializar()
        facturacion = A.Fake(Of IServicioFacturacionRutas)()
        impresion = A.Fake(Of IServicioImpresionDocumentos)()
        dialogos = A.Fake(Of IServicioDialogos)()
        confirmadas = New List(Of ImpresionDocumentoEnPapelDTO)
        A.CallTo(Function() facturacion.ConfirmarImpresionPapel(A(Of ImpresionDocumentoEnPapelDTO).Ignored)).
            Invokes(Sub(c As ImpresionDocumentoEnPapelDTO) confirmadas.Add(c)).Returns(Task.CompletedTask)
        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).Returns(Task.FromResult(New ResultadoImpresion()))
        facturador = New FacturadorPedido(facturacion, impresion, dialogos)
    End Sub

    Private Shared Function ConFacturaFisica(numero As String) As FacturarEtiquetaResponseDTO
        Dim respuesta As New FacturarEtiquetaResponseDTO With {.PedidosProcesados = 1}
        respuesta.Facturas.Add(New FacturaCreadaDTO With {
            .Empresa = "1", .NumeroPedido = PEDIDO, .NumeroFactura = numero,
            .DatosImpresion = New DocumentoParaImprimir With {.BytesPDF = {1}, .NumeroCopias = 1, .TipoBandeja = TipoBandejaImpresion.Lower}
        })
        respuesta.DocumentosEnPapel.Add(New DocumentoEnPapelDTO With {
            .Empresa = "1", .Pedido = PEDIDO, .Envio = ENVIO, .Tipo = DocumentoEnPapelDTO.TIPO_FACTURA, .Documento = numero,
            .Estado = DocumentoEnPapelDTO.ESTADO_PENDIENTE, .Mensaje = "Lo imprime Nesto.", .Descripcion = $"la factura {numero}"
        })
        Return respuesta
    End Function

    Private Sub Responder(respuesta As FacturarEtiquetaResponseDTO)
        A.CallTo(Function() facturacion.FacturarEtiqueta("1", PEDIDO, ENVIO, False)).Returns(Task.FromResult(respuesta))
    End Sub

    <TestMethod()>
    Public Async Function Etiqueta_FacturaConElNucleoImprimeYApuntaQueHaSalido() As Task
        Dim respuesta = ConFacturaFisica("NV2617986")
        Responder(respuesta)

        Await facturador.FacturarEtiquetaAsync("1", PEDIDO, ENVIO, True)

        A.CallTo(Function() facturacion.FacturarEtiqueta("1", PEDIDO, ENVIO, False)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() facturacion.FacturarPedido(A(Of String).Ignored, A(Of Integer).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() impresion.ImprimirDocumentos(respuesta)).MustHaveHappenedOnceExactly()
        Dim confirmada = confirmadas.Single()
        Assert.IsTrue(confirmada.Impreso)
        Assert.AreEqual("NV2617986", confirmada.Documento)
        Assert.AreEqual(ENVIO, confirmada.Envio)
        A.CallTo(Sub() dialogos.ShowNotification("Facturación", A(Of String).That.Contains("Meta en la caja la factura NV2617986."))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Etiqueta_LaImpresoraFalla_SeApuntaPendienteYSeDice() As Task
        Responder(ConFacturaFisica("NV2617986"))
        Dim resultado As New ResultadoImpresion With {.DocumentosConError = 1}
        resultado.Errores.Add(New ErrorImpresion With {.TipoDocumento = "Factura", .NumeroDocumento = "NV2617986", .MensajeError = "Impresora sin papel"})
        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).Returns(Task.FromResult(resultado))

        Await facturador.FacturarEtiquetaAsync("1", PEDIDO, ENVIO, True)

        Dim confirmada = confirmadas.Single()
        Assert.IsFalse(confirmada.Impreso)
        Assert.AreEqual("Impresora sin papel", confirmada.Mensaje)
        A.CallTo(Sub() dialogos.ShowNotification("Facturación", A(Of String).That.Contains("queda pendiente. Repita la etiqueta"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Etiqueta_SinImprimir_NoSeApuntaNadaYQuedaPendiente() As Task
        Responder(ConFacturaFisica("NV2617986"))

        Await facturador.FacturarEtiquetaAsync("1", PEDIDO, ENVIO, False)

        A.CallTo(Function() impresion.ImprimirDocumentos(A(Of FacturarRutasResponseDTO).Ignored)).MustNotHaveHappened()
        Assert.AreEqual(0, confirmadas.Count)
        A.CallTo(Sub() dialogos.ShowNotification("Facturación", A(Of String).That.Contains("no se ha mandado a imprimir"))).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function Etiqueta_RutaPropia_EnseñaElAvisoDeLaApi() As Task
        Dim respuesta As New FacturarEtiquetaResponseDTO()
        respuesta.AvisosPapel.Add("El pedido 929914 es de ruta propia (AT): no se factura al imprimir la etiqueta.")
        Responder(respuesta)

        Await facturador.FacturarEtiquetaAsync("1", PEDIDO, ENVIO, True)

        A.CallTo(Sub() dialogos.ShowNotification("Facturación", A(Of String).That.Contains("ruta propia (AT)"))).MustHaveHappenedOnceExactly()
        Assert.AreEqual(0, confirmadas.Count)
    End Function

    <TestMethod()>
    Public Async Function Etiqueta_NotaSinPacking_PreguntaYRepiteSaltandoElPacking() As Task
        Dim sinPacking As New FacturarEtiquetaResponseDTO()
        sinPacking.PedidosConErrores.Add(New PedidoConErrorDTO With {.Codigo = CodigosErrorFacturacion.NOTA_SIN_PACKING, .MensajeError = "no ha pasado por el packing"})
        Responder(sinPacking)
        A.CallTo(Function() facturacion.FacturarEtiqueta("1", PEDIDO, ENVIO, True)).Returns(Task.FromResult(New FacturarEtiquetaResponseDTO()))
        A.CallTo(Function() dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).Returns(True)

        Await facturador.FacturarEtiquetaAsync("1", PEDIDO, ENVIO, True)

        A.CallTo(Function() facturacion.FacturarEtiqueta("1", PEDIDO, ENVIO, True)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub FacturarAlImprimirEtiqueta_MismaReglaQueAriadna()
        Assert.IsTrue(AgenciasViewModel.EsFacturarAlImprimirEtiqueta("1"))
        Assert.IsTrue(AgenciasViewModel.EsFacturarAlImprimirEtiqueta(" true "))
        Assert.IsTrue(AgenciasViewModel.EsFacturarAlImprimirEtiqueta("Sí"))
        Assert.IsTrue(AgenciasViewModel.EsFacturarAlImprimirEtiqueta("si"))
        Assert.IsFalse(AgenciasViewModel.EsFacturarAlImprimirEtiqueta("0"))
        Assert.IsFalse(AgenciasViewModel.EsFacturarAlImprimirEtiqueta(Nothing))
        Assert.IsFalse(AgenciasViewModel.EsFacturarAlImprimirEtiqueta(""))
    End Sub

    <TestMethod()>
    Public Sub FacturarEtiquetaResponseDTO_SeLeeDeLaApi()
        Dim json = "{""PedidosProcesados"":1,""Facturas"":[{""NumeroFactura"":""NV1"",""NumeroPedido"":929914,""Envio"":null}],""Albaranes"":[],""NotasEntrega"":[]," &
            """PedidosConErrores"":[],""DocumentosEnPapel"":[{""Empresa"":""1"",""Pedido"":929914,""Envio"":251001,""Tipo"":""Factura"",""Documento"":""NV1""," &
            """Estado"":""Pendiente"",""Descripcion"":""la factura NV1""}],""AvisosPapel"":[""aviso""],""ResumenPapelPacking"":null}"

        Dim respuesta = JsonConvert.DeserializeObject(Of FacturarEtiquetaResponseDTO)(json)

        Assert.IsTrue(respuesta.DocumentosEnPapel.Single().EstaPendiente)
        Assert.AreEqual(251001, respuesta.DocumentosEnPapel.Single().Envio)
        Assert.AreEqual("aviso", respuesta.AvisosPapel.Single())
    End Sub

    <TestMethod()>
    Public Async Function FacturarRutas_EnseñaLoDelPapelDelPacking() As Task
        Dim response As New FacturarRutasResponseDTO With {
            .PedidosProcesados = 0,
            .ResumenPapelPacking = "3 facturas en papel ya impresas en el packing."
        }
        A.CallTo(Function() facturacion.FacturarRutas(A(Of FacturarRutasRequestDTO).Ignored)).Returns(Task.FromResult(response))
        Dim vm As New FacturarRutasPopupViewModel(A.Fake(Of IConfiguracion), dialogos, facturacion, impresion, A.Fake(Of IServicioReembolsoEnvios)) With {
            .PausaAntesDeCerrarMs = 0,
            .PausaTrasImprimirMs = 0
        }
        vm.TipoRutaSeleccionado = New TipoRutaInfoDTO With {.Id = "AGENCIA"}

        Await vm.FacturarRutasAsync()

        StringAssert.Contains(vm.MensajeEstado, "3 facturas en papel ya impresas en el packing.")
    End Function
End Class
