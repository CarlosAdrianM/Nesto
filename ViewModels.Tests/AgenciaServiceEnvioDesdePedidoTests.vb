Imports Microsoft.VisualStudio.TestTools.UnitTesting
Imports Nesto.ViewModels
Imports Newtonsoft.Json

''' <summary>
''' NestoAPI#595 (slice 2): el contrato con POST api/EnviosAgencias/DesdePedido (espejo de EnvioDesdePedidoDTO.cs en
''' NestoAPI). Si un nombre deja de casar, Web API/Newtonsoft dejan la propiedad por defecto sin dar error: un peso 0 en
''' el servidor es un 400 y una confirmación con otro nombre, un 409 eterno.
''' </summary>
<TestClass()>
Public Class AgenciaServiceEnvioDesdePedidoTests

    Private Shared Function Nombres(tipo As Type) As String()
        Return tipo.GetProperties().Select(Function(p) p.Name).OrderBy(Function(n) n, StringComparer.Ordinal).ToArray()
    End Function

    <TestMethod()>
    Public Sub Ruta_EsLaDelEndpoint()
        Assert.AreEqual("EnviosAgencias/DesdePedido", AgenciaService.RUTA_ENVIO_DESDE_PEDIDO)
    End Sub

    <TestMethod()>
    Public Sub Peticion_MismosNombresQueElServidor()
        CollectionAssert.AreEqual({"Bultos", "Cambios", "Confirmaciones", "DescontarPrepagos", "Dimensiones", "Empresa", "EtiquetaRecogida", "Pedido", "Peso"},
                                  Nombres(GetType(CrearEnvioDesdePedidoDto)))
        CollectionAssert.AreEqual({"Agencia", "Atencion", "CodPostal", "Direccion", "Email", "Horario", "Movil", "Nombre", "Observaciones",
                                   "Pais", "Poblacion", "Provincia", "Reembolso", "Retorno", "Servicio", "Telefono"},
                                  Nombres(GetType(CambiosEnvioDesdePedidoDto)))
    End Sub

    <TestMethod()>
    Public Sub Respuestas_MismosNombresQueElServidor()
        CollectionAssert.AreEqual({"Avisos", "CorreoEnviado", "Envio", "EnvioOrigen", "EtiquetaEnNesto", "Flujo", "HayQueImprimir", "Origen"},
                                  Nombres(GetType(EnvioDesdePedidoDto)))
        CollectionAssert.AreEqual({"Codigo", "Texto", "Tipo"}, Nombres(GetType(AvisoEnvioDesdePedidoDto)))
        CollectionAssert.AreEqual({"Codigo", "Message"}, Nombres(GetType(ErrorEnvioDesdePedidoDto)))
    End Sub

    <TestMethod()>
    Public Sub Propuesta_AvisoEnlacePagoPendiente_SeLeeDeAvisosDetalle()
        ' NestoAPI#679: GET api/EnviosAgencias/Propuesta (PreserveReferencesHandling: $id)
        Dim cuerpo As String = "{""$id"":""1"",""Pedido"":928521,""Reembolso"":258.56,""AvisosDetalle"":[" &
            "{""$id"":""2"",""Codigo"":""Prepagos"",""Texto"":""El pedido tiene..."",""Tipo"":""Elegir""}," &
            "{""$id"":""3"",""Codigo"":""EnlacePagoPendiente"",""Texto"":""Este pedido tiene un enlace de pago pendiente de 252,94 €"",""Tipo"":""Confirmar""}]}"

        Assert.AreEqual("Este pedido tiene un enlace de pago pendiente de 252,94 €", AgenciaService.TextoAvisoEnlacePagoPendiente(cuerpo))
        Assert.IsNull(AgenciaService.TextoAvisoEnlacePagoPendiente("{""AvisosDetalle"":[]}"))
        Assert.IsNull(AgenciaService.TextoAvisoEnlacePagoPendiente(""))
        Assert.AreEqual("EnlacePagoPendiente", ContratoEnvioDesdePedido.AVISO_ENLACE_PAGO_PENDIENTE, "CodigosAvisoPropuestaEnvio.ENLACE_PAGO_PENDIENTE")
    End Sub

    <TestMethod()>
    Public Sub Peticion_SinDecidirPrepagos_ViajaComoNull()
        Dim json As String = JsonConvert.SerializeObject(New CrearEnvioDesdePedidoDto With {.Empresa = "1", .Pedido = 927700, .Bultos = 2, .Peso = 3.5D})

        StringAssert.Contains(json, """DescontarPrepagos"":null")
        StringAssert.Contains(json, """Confirmaciones"":[]")
        StringAssert.Contains(json, """Peso"":3.5")
    End Sub

    <TestMethod()>
    Public Sub Respuesta200_ConLasReferenciasDelServidor_SeLee()
        ' El servidor serializa con PreserveReferencesHandling.Objects ($id).
        Dim cuerpo As String = "{""$id"":""1"",""Envio"":{""$id"":""2"",""Numero"":250000,""Agencia"":1,""Pedido"":927700,""CodigoBarras"":""61197140250000"",""Bultos"":2}," &
            """Origen"":""Nuevo"",""EnvioOrigen"":null,""HayQueImprimir"":true,""Flujo"":""TramitarAlCerrar"",""EtiquetaEnNesto"":true,""CorreoEnviado"":true,""Avisos"":[]}"

        Dim resultado = AgenciaService.InterpretarRespuestaEnvioDesdePedido(200, cuerpo)

        Assert.AreEqual(EstadoEnvioDesdePedido.Creado, resultado.Estado)
        Assert.AreEqual(250000, resultado.Creado.Envio.Numero)
        Assert.AreEqual("61197140250000", resultado.Creado.Envio.CodigoBarras)
        Assert.IsTrue(resultado.Creado.HayQueImprimir)
        Assert.AreEqual("TramitarAlCerrar", resultado.Creado.Flujo)
    End Sub

    <TestMethod()>
    Public Sub Respuesta409_TraeLosPendientes()
        Dim cuerpo As String = "{""Pendientes"":[{""Codigo"":""SinPicking"",""Texto"":""Este pedido no tiene ninguna línea con picking."",""Tipo"":""Confirmar""}],""Propuesta"":{""Agencia"":1}}"

        Dim resultado = AgenciaService.InterpretarRespuestaEnvioDesdePedido(409, cuerpo)

        Assert.AreEqual(EstadoEnvioDesdePedido.ConPendientes, resultado.Estado)
        Assert.AreEqual("SinPicking", resultado.Pendientes.Single().Codigo)
        Assert.AreEqual("Confirmar", resultado.Pendientes.Single().Tipo)
    End Sub

    <TestMethod()>
    Public Sub Respuesta400_ConElCodigoYElMotivo()
        Dim resultado = AgenciaService.InterpretarRespuestaEnvioDesdePedido(400, "{""Message"":""Haga la etiqueta de recogida desde Nesto."",""Codigo"":""EtiquetaRecogida""}")

        Assert.AreEqual(EstadoEnvioDesdePedido.Rechazado, resultado.Estado)
        Assert.AreEqual(ContratoEnvioDesdePedido.ERROR_ETIQUETA_RECOGIDA, resultado.CodigoError)
        Assert.AreEqual("Haga la etiqueta de recogida desde Nesto.", resultado.MensajeError)
    End Sub

    <TestMethod()>
    Public Sub Respuesta400_SinElContrato_EnsenaElCuerpo()
        Dim resultado = AgenciaService.InterpretarRespuestaEnvioDesdePedido(400, "petición incorrecta")

        Assert.AreEqual(EstadoEnvioDesdePedido.Rechazado, resultado.Estado)
        Assert.IsNull(resultado.CodigoError)
        Assert.AreEqual("petición incorrecta", resultado.MensajeError)
    End Sub

    <TestMethod()>
    Public Sub Respuesta404_PedidoNoEncontrado()
        Dim resultado = AgenciaService.InterpretarRespuestaEnvioDesdePedido(404, "")

        Assert.AreEqual(EstadoEnvioDesdePedido.Rechazado, resultado.Estado)
        Assert.AreEqual(ContratoEnvioDesdePedido.ERROR_NO_ENCONTRADO, resultado.CodigoError)
    End Sub

    <TestMethod()>
    Public Sub Respuesta500_Lanza()
        Dim ex = Assert.ThrowsException(Of Exception)(Function() AgenciaService.InterpretarRespuestaEnvioDesdePedido(500, "se ha producido un error"))

        StringAssert.Contains(ex.Message, "(500)")
        StringAssert.Contains(ex.Message, "se ha producido un error")
    End Sub

End Class
