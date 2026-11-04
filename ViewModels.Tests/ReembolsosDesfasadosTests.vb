Imports FakeItEasy
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Services
Imports Newtonsoft.Json
Imports System.Threading.Tasks

''' <summary>
''' Nesto#615 (NestoAPI#810): al facturar (popup de Facturar rutas, Agencias y detalle del pedido), si la API devuelve envíos
''' del reparto propio con el reembolso desfasado de lo facturado, se pregunta y, si se acepta, se llama a
''' PUT api/EnviosAgencias/{id}/Reembolso. Caso 928723: RP250518 480,02 € → 510,15 €.
''' </summary>
<TestClass()>
Public Class ReembolsosDesfasadosTests
    Private dialogos As IServicioDialogos
    Private reembolsos As IServicioReembolsoEnvios
    Private facturacion As IServicioFacturacionRutas
    Private impresion As IServicioImpresionDocumentos

    <TestInitialize()>
    Public Sub Inicializar()
        dialogos = A.Fake(Of IServicioDialogos)()
        reembolsos = A.Fake(Of IServicioReembolsoEnvios)()
        facturacion = A.Fake(Of IServicioFacturacionRutas)()
        impresion = A.Fake(Of IServicioImpresionDocumentos)()
        A.CallTo(Function() reembolsos.CambiarReembolso(A(Of Integer).Ignored, A(Of CambiarReembolsoEnvioDTO).Ignored)).
            Returns(Task.FromResult(New ReembolsoEnvioCambiadoDTO With {.Numero = 250518, .Pedido = 928723, .ReembolsoAnterior = 480.02D, .Reembolso = 510.15D, .Cambiado = True}))
    End Sub

    Private Shared Function RespuestaConDesfasado() As FacturarRutasResponseDTO
        Dim respuesta As New FacturarRutasResponseDTO()
        respuesta.ReembolsosDesfasados.Add(New ReembolsoDesfasadoDTO With {.Envio = 250518, .Pedido = 928723, .ReembolsoActual = 480.02D, .ReembolsoFacturado = 510.15D})
        Return respuesta
    End Function

    Private Function CrearResolutor() As ResolutorReembolsosDesfasados
        Return New ResolutorReembolsosDesfasados(reembolsos, dialogos)
    End Function

    Private Sub Aceptar(acepta As Boolean)
        A.CallTo(Function() dialogos.ShowConfirmationAnswer(ResolutorReembolsosDesfasados.TITULO, A(Of String).Ignored)).Returns(acepta)
    End Sub

#Region "Resolutor"
    <TestMethod()>
    Public Async Function Resolutor_ConDesfasadoYAcepta_PreguntaYLlamaAlPutConLoFacturado() As Task
        Aceptar(True)

        Await CrearResolutor().ResolverAsync(RespuestaConDesfasado())

        A.CallTo(Function() dialogos.ShowConfirmationAnswer(ResolutorReembolsosDesfasados.TITULO,
            A(Of String).That.Matches(Function(t) t.Contains("RP250518") AndAlso t.Contains("928723") AndAlso t.Contains("¿Actualizar el reembolso del envío?")))).
            MustHaveHappenedOnceExactly()
        A.CallTo(Function() reembolsos.CambiarReembolso(250518, A(Of CambiarReembolsoEnvioDTO).That.Matches(Function(d) d.Reembolso = 510.15D))).
            MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogos.ShowNotification(ResolutorReembolsosDesfasados.TITULO, A(Of String).That.Contains("RP250518"))).MustHaveHappenedOnceExactly()
        A.CallTo(Sub() dialogos.ShowError(A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Resolutor_SinDesfasados_NoPreguntaNiLlama() As Task
        Await CrearResolutor().ResolverAsync(New FacturarRutasResponseDTO())
        Await CrearResolutor().ResolverAsync(New FacturarRutasResponseDTO With {.ReembolsosDesfasados = Nothing})
        Await CrearResolutor().ResolverAsync(Nothing)

        A.CallTo(Function() dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() reembolsos.CambiarReembolso(A(Of Integer).Ignored, A(Of CambiarReembolsoEnvioDTO).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Resolutor_SiRechaza_NoLlamaAlPut() As Task
        Aceptar(False)

        Await CrearResolutor().ResolverAsync(RespuestaConDesfasado())

        A.CallTo(Function() dialogos.ShowConfirmationAnswer(ResolutorReembolsosDesfasados.TITULO, A(Of String).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() reembolsos.CambiarReembolso(A(Of Integer).Ignored, A(Of CambiarReembolsoEnvioDTO).Ignored)).MustNotHaveHappened()
        A.CallTo(Sub() dialogos.ShowNotification(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function Resolutor_ErrorDelPut_SeEnseñaYSigueConElSiguiente() As Task
        Aceptar(True)
        A.CallTo(Function() reembolsos.CambiarReembolso(250518, A(Of CambiarReembolsoEnvioDTO).Ignored)).
            Throws(New Exception("El envío 250518 ya está entregado"))
        Dim respuesta = RespuestaConDesfasado()
        respuesta.ReembolsosDesfasados.Add(New ReembolsoDesfasadoDTO With {.Envio = 250519, .Pedido = 928724, .ReembolsoActual = 10D, .ReembolsoFacturado = 20D})

        Await CrearResolutor().ResolverAsync(respuesta)

        A.CallTo(Sub() dialogos.ShowError(A(Of String).That.Matches(Function(t) t.Contains("RP250518") AndAlso t.Contains("ya está entregado")))).MustHaveHappenedOnceExactly()
        A.CallTo(Function() reembolsos.CambiarReembolso(250519, A(Of CambiarReembolsoEnvioDTO).Ignored)).MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Sub Resolutor_TextoResultado_SinCambioLoDice()
        Dim texto = ResolutorReembolsosDesfasados.TextoResultado(RespuestaConDesfasado().ReembolsosDesfasados(0),
            New ReembolsoEnvioCambiadoDTO With {.Numero = 250518, .Reembolso = 510.15D, .ReembolsoAnterior = 510.15D, .Cambiado = False})

        StringAssert.Contains(texto, "no se ha cambiado nada")
    End Sub

    <TestMethod()>
    Public Sub FacturarRutasResponseDTO_LeeLosReembolsosDesfasadosDeLaApi()
        Dim json = "{""PedidosProcesados"":1,""ReembolsosDesfasados"":[{""Envio"":250518,""Pedido"":928723,""ReembolsoActual"":480.02,""ReembolsoFacturado"":510.15}]}"

        Dim respuesta = JsonConvert.DeserializeObject(Of FacturarRutasResponseDTO)(json)

        Assert.AreEqual(1, respuesta.ReembolsosDesfasados.Count)
        Assert.AreEqual(250518, respuesta.ReembolsosDesfasados(0).Envio)
        Assert.AreEqual(510.15D, respuesta.ReembolsosDesfasados(0).ReembolsoFacturado)
    End Sub
#End Region

#Region "FacturadorPedido (Agencias y detalle del pedido)"
    <TestMethod()>
    Public Async Function FacturadorPedido_ConDesfasadoYAcepta_LlamaAlPut() As Task
        Aceptar(True)
        A.CallTo(Function() facturacion.FacturarPedido("1", 928723)).Returns(Task.FromResult(RespuestaConDesfasado()))
        Dim facturador As New FacturadorPedido(facturacion, impresion, dialogos, CrearResolutor())

        Await facturador.FacturarAsync("1", 928723, False)

        A.CallTo(Function() reembolsos.CambiarReembolso(250518, A(Of CambiarReembolsoEnvioDTO).That.Matches(Function(d) d.Reembolso = 510.15D))).
            MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function FacturadorPedido_SinDesfasados_NoPregunta() As Task
        A.CallTo(Function() facturacion.FacturarPedido("1", 928723)).Returns(Task.FromResult(New FacturarRutasResponseDTO()))
        Dim facturador As New FacturadorPedido(facturacion, impresion, dialogos, CrearResolutor())

        Await facturador.FacturarAsync("1", 928723, False)

        A.CallTo(Function() dialogos.ShowConfirmationAnswer(A(Of String).Ignored, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() reembolsos.CambiarReembolso(A(Of Integer).Ignored, A(Of CambiarReembolsoEnvioDTO).Ignored)).MustNotHaveHappened()
    End Function
#End Region

#Region "Popup de Facturar rutas"
    Private Function CrearPopup(response As FacturarRutasResponseDTO) As FacturarRutasPopupViewModel
        A.CallTo(Function() facturacion.FacturarRutas(A(Of FacturarRutasRequestDTO).Ignored)).Returns(Task.FromResult(response))
        Dim vm As New FacturarRutasPopupViewModel(A.Fake(Of IConfiguracion), dialogos, facturacion, impresion, reembolsos) With {
            .PausaAntesDeCerrarMs = 0,
            .PausaTrasImprimirMs = 0
        }
        vm.TipoRutaSeleccionado = New TipoRutaInfoDTO With {.Id = "PROPIA"}
        Return vm
    End Function

    <TestMethod()>
    Public Async Function FacturarRutas_ConDesfasadoYAcepta_LlamaAlPut() As Task
        Aceptar(True)
        Dim vm = CrearPopup(RespuestaConDesfasado())

        Await vm.FacturarRutasAsync()

        A.CallTo(Function() reembolsos.CambiarReembolso(250518, A(Of CambiarReembolsoEnvioDTO).That.Matches(Function(d) d.Reembolso = 510.15D))).
            MustHaveHappenedOnceExactly()
    End Function

    <TestMethod()>
    Public Async Function FacturarRutas_SiRechaza_NoLlamaAlPut() As Task
        Aceptar(False)
        Dim vm = CrearPopup(RespuestaConDesfasado())

        Await vm.FacturarRutasAsync()

        A.CallTo(Function() dialogos.ShowConfirmationAnswer(ResolutorReembolsosDesfasados.TITULO, A(Of String).Ignored)).MustHaveHappenedOnceExactly()
        A.CallTo(Function() reembolsos.CambiarReembolso(A(Of Integer).Ignored, A(Of CambiarReembolsoEnvioDTO).Ignored)).MustNotHaveHappened()
    End Function

    <TestMethod()>
    Public Async Function FacturarRutas_SinDesfasados_NoPreguntaPorElReembolso() As Task
        Dim vm = CrearPopup(New FacturarRutasResponseDTO With {.PedidosProcesados = 1})

        Await vm.FacturarRutasAsync()

        A.CallTo(Function() dialogos.ShowConfirmationAnswer(ResolutorReembolsosDesfasados.TITULO, A(Of String).Ignored)).MustNotHaveHappened()
        A.CallTo(Function() reembolsos.CambiarReembolso(A(Of Integer).Ignored, A(Of CambiarReembolsoEnvioDTO).Ignored)).MustNotHaveHappened()
    End Function
#End Region
End Class
