Imports System.Net
Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#568 (NestoAPI#734): el pedido sale contra reembolso y tiene enlaces de pago pendientes. El almacén lo retiene y
''' consulta a Administración (POST api/PedidosVenta/{empresa}/{numero}/ConsultaReembolso), que contesta si sale con o sin
''' reembolso (POST …/ConsultaReembolso/Respuesta). Las reglas viven en la API; aquí solo las llamadas y los textos.
''' </summary>
Public NotInheritable Class ConsultaReembolso
    Private Sub New()
    End Sub

    ''' <summary>AvisoPropuestaEnvio.Accion del aviso EnlacePagoPendiente: ofrecer «Retener y consultar a Administración».</summary>
    Public Const ACCION_CONSULTAR_ADMINISTRACION As String = "ConsultarAdministracion"
    ''' <summary>Aviso Bloqueante de la propuesta: hay una consulta abierta y el pedido está retenido.</summary>
    Public Const AVISO_CONSULTA_ABIERTA As String = "ConsultaReembolsoAbierta"
    Public Const TIPO_BLOQUEANTE As String = "Bloqueante"

    ''' <summary>NotificacionPushDTO.Tipo de los avisos de la campana.</summary>
    Public Const TIPO_NOTIFICACION As String = "ConsultaReembolso"
    Public Const ACCION_NOTIFICACION_CONSULTA As String = "consulta"
    Public Const ACCION_NOTIFICACION_RESPUESTA As String = "respuesta"

    Public Const TITULO As String = "Enlace de pago pendiente"
    Public Const TEXTO_BOTON_CONSULTAR As String = "Retener y consultar a Administración"
    Public Const TEXTO_BOTON_CANCELAR As String = "No tramitar ahora"

    ''' <summary>Las formas de pago que no se ofrecen si el pedido no está pagado (la API lo vuelve a validar).</summary>
    Public Const FORMA_PAGO_TARJETA As String = "TAR"

    ''' <summary>«Pregunta enviada a Administración: el pedido 929300 queda retenido hasta que contesten.» (+ avisos de la API).</summary>
    Public Shared Function TextoConsultaEnviada(respuesta As RespuestaConsultaReembolsoModel, pedido As Integer) As String
        Dim texto As String = If(respuesta?.YaExistia = True,
            $"Ya se había consultado a Administración: el pedido {pedido} sigue retenido hasta que contesten.",
            $"Consulta enviada a Administración: el pedido {pedido} queda retenido hasta que contesten. Le llegará la respuesta a la campana.")
        Return AnadirAvisos(texto, respuesta?.Avisos)
    End Function

    Public Shared Function AnadirAvisos(texto As String, avisos As IEnumerable(Of String)) As String
        Dim lista = If(avisos, Enumerable.Empty(Of String)()).Where(Function(a) Not String.IsNullOrWhiteSpace(a)).Select(Function(a) a.Trim()).ToList()
        If Not lista.Any() Then
            Return texto
        End If
        Return texto & Environment.NewLine & Environment.NewLine & String.Join(Environment.NewLine, lista)
    End Function

    ''' <summary>El mensaje de error de la API: {"Message":"…"}, {"error":{"message":"…"}} o el texto tal cual.</summary>
    Public Shared Function ExtraerMensaje(body As String, porDefecto As String) As String
        If String.IsNullOrWhiteSpace(body) Then
            Return porDefecto
        End If
        Try
            Dim json As JObject = JsonConvert.DeserializeObject(Of JObject)(body)
            Dim mensaje As String = TryCast(json?("error"), JObject)?("message")?.ToString()
            If String.IsNullOrWhiteSpace(mensaje) Then
                mensaje = json?("Message")?.ToString()
            End If
            If Not String.IsNullOrWhiteSpace(mensaje) Then
                Return mensaje
            End If
        Catch
            ' No es JSON: el texto tal cual.
        End Try
        Return body.Trim().Trim(""""c)
    End Function
End Class

''' <summary>NestoAPI#734: ConsultaReembolsoDTO.</summary>
Public Class ConsultaReembolsoModel
    Public Property Id As Integer
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property ImporteEnlaces As Decimal
    Public Property NumeroEnlaces As Integer
    Public Property RetencionId As Integer?
    Public Property Comentario As String
    ''' <summary>Quien preguntó.</summary>
    Public Property Usuario As String
    Public Property Fecha As Date
    ''' <summary>Nothing = abierta.</summary>
    Public Property Respondida As Date?
    Public Property UsuarioRespuesta As String
    Public Property ConReembolso As Boolean?
    Public Property AnularEnlaces As Boolean?
    Public Property EnlacesAnulados As String
    Public Property FormaPagoAnterior As String
    Public Property PlazosPagoAnterior As String
    Public Property FormaPago As String
    Public Property PlazosPago As String
    Public Property ComentarioRespuesta As String
    Public Property Abierta As Boolean

    ''' <summary>«Administración (Paloma) contestó el 29/10/2026: sale con reembolso.»; Nothing si sigue abierta.</summary>
    <JsonIgnore>
    Public ReadOnly Property TextoRespuesta As String
        Get
            If Not Respondida.HasValue Then
                Return Nothing
            End If
            Dim decision As String = If(ConReembolso = True, "sale con reembolso",
                $"sale sin reembolso ({FormaPago?.Trim()}{If(String.IsNullOrWhiteSpace(PlazosPago), String.Empty, ", " & PlazosPago.Trim())})")
            Return $"Administración ({UsuarioRespuesta?.Trim()}) contestó el {Respondida.Value:dd/MM/yyyy}: {decision}."
        End Get
    End Property
End Class

''' <summary>NestoAPI#734: EnlacePagoPedido (los enlaces pendientes de una consulta abierta).</summary>
Public Class EnlacePagoPendienteModel
    Public Property Id As Integer
    Public Property Importe As Decimal
    Public Property Estado As String
    Public Property FechaCreacion As Date?
    Public Property FechaActualizacion As Date?
    Public Property Usuario As String
    ''' <summary>El concepto del enlace («Pago pedido 929300 de Nueva Visión»): dice a qué pedido corresponde.</summary>
    Public Property Descripcion As String

    ''' <summary>«252,94 € del 20/10/2026: Pago pedido 929300 de Nueva Visión».</summary>
    <JsonIgnore>
    Public ReadOnly Property Texto As String
        Get
            Return $"{Importe:C}" & If(FechaCreacion.HasValue, $" del {FechaCreacion.Value:dd/MM/yyyy}", String.Empty) &
                If(String.IsNullOrWhiteSpace(Descripcion), String.Empty, $": {Descripcion.Trim()}")
        End Get
    End Property
End Class

''' <summary>NestoAPI#734: ConsultaReembolsoListadoDTO (GET api/PedidosVenta/ConsultasReembolso).</summary>
Public Class ConsultaReembolsoListadoModel
    Inherits ConsultaReembolsoModel

    Public Property Cliente As String
    Public Property Contacto As String
    Public Property NombreCliente As String
    Public Property Vendedor As String
    Public Property EnlacesPendientes As New List(Of EnlacePagoPendienteModel)
End Class

''' <summary>NestoAPI#734: RespuestaConsultaReembolsoDTO (respuesta de los dos POST).</summary>
Public Class RespuestaConsultaReembolsoModel
    Public Property Consulta As ConsultaReembolsoModel
    Public Property YaExistia As Boolean
    Public Property EnlacesAnulados As New List(Of Integer)
    Public Property Liberado As Boolean
    Public Property Avisos As New List(Of String)
End Class

''' <summary>NestoAPI#734: cuerpo de POST …/ConsultaReembolso/Respuesta.</summary>
Public Class ResponderConsultaReembolsoModel
    Public Property ConReembolso As Boolean?
    <JsonProperty(NullValueHandling:=NullValueHandling.Ignore)>
    Public Property AnularEnlaces As Boolean?
    <JsonProperty(NullValueHandling:=NullValueHandling.Ignore)>
    Public Property FormaPago As String
    <JsonProperty(NullValueHandling:=NullValueHandling.Ignore)>
    Public Property PlazosPago As String
    <JsonProperty(NullValueHandling:=NullValueHandling.Ignore)>
    Public Property Comentario As String
End Class

Public Interface IConsultaReembolsoService
    ''' <summary>POST api/PedidosVenta/{empresa}/{numero}/ConsultaReembolso: retiene el pedido y avisa a Administración (idempotente).</summary>
    Function Consultar(empresa As String, pedido As Integer, comentario As String) As Task(Of RespuestaConsultaReembolsoModel)
    ''' <summary>GET api/PedidosVenta/ConsultasReembolso?abiertas=…&amp;empresa=…</summary>
    Function LeerConsultas(empresa As String, soloAbiertas As Boolean) As Task(Of List(Of ConsultaReembolsoListadoModel))
    ''' <summary>GET api/PedidosVenta/{empresa}/{numero}/ConsultaReembolso: la última (abierta o contestada); Nothing si no hay.</summary>
    Function LeerConsulta(empresa As String, pedido As Integer) As Task(Of ConsultaReembolsoModel)
    ''' <summary>POST …/ConsultaReembolso/Respuesta. Lanza con el mensaje de la API tal cual si no la acepta.</summary>
    Function Responder(empresa As String, pedido As Integer, respuesta As ResponderConsultaReembolsoModel) As Task(Of RespuestaConsultaReembolsoModel)
End Interface

Public Class ConsultaReembolsoService
    Implements IConsultaReembolsoService

    Private ReadOnly _clienteApiFactory As IClienteApiFactory

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
    End Sub

    Friend Sub New(clienteApiFactory As IClienteApiFactory)
        _clienteApiFactory = clienteApiFactory
    End Sub

    Public Shared Function RutaConsulta(empresa As String, pedido As Integer) As String
        Dim empresaRuta As String = Uri.EscapeDataString(If(String.IsNullOrWhiteSpace(empresa), Constantes.Empresas.EMPRESA_DEFECTO, empresa.Trim()))
        Return $"PedidosVenta/{empresaRuta}/{pedido}/ConsultaReembolso"
    End Function

    Public Async Function Consultar(empresa As String, pedido As Integer, comentario As String) As Task(Of RespuestaConsultaReembolsoModel) Implements IConsultaReembolsoService.Consultar
        Dim cuerpo = New With {.Comentario = If(String.IsNullOrWhiteSpace(comentario), Nothing, comentario.Trim())}
        Dim body As String = Await Enviar(HttpMethod.Post, RutaConsulta(empresa, pedido), cuerpo, "No se ha podido consultar a Administración")
        Return JsonConvert.DeserializeObject(Of RespuestaConsultaReembolsoModel)(body)
    End Function

    Public Async Function LeerConsultas(empresa As String, soloAbiertas As Boolean) As Task(Of List(Of ConsultaReembolsoListadoModel)) Implements IConsultaReembolsoService.LeerConsultas
        Dim empresaRuta As String = Uri.EscapeDataString(If(String.IsNullOrWhiteSpace(empresa), Constantes.Empresas.EMPRESA_DEFECTO, empresa.Trim()))
        Dim body As String = Await Enviar(HttpMethod.Get, $"PedidosVenta/ConsultasReembolso?abiertas={soloAbiertas.ToString().ToLowerInvariant()}&empresa={empresaRuta}", Nothing,
                                          "No se han podido leer las consultas de reembolso")
        If String.IsNullOrWhiteSpace(body) Then
            Return New List(Of ConsultaReembolsoListadoModel)
        End If
        Return If(JsonConvert.DeserializeObject(Of List(Of ConsultaReembolsoListadoModel))(body), New List(Of ConsultaReembolsoListadoModel))
    End Function

    Public Async Function LeerConsulta(empresa As String, pedido As Integer) As Task(Of ConsultaReembolsoModel) Implements IConsultaReembolsoService.LeerConsulta
        Dim body As String = Await Enviar(HttpMethod.Get, RutaConsulta(empresa, pedido), Nothing, "No se ha podido leer la consulta de reembolso")
        Return If(String.IsNullOrWhiteSpace(body), Nothing, JsonConvert.DeserializeObject(Of ConsultaReembolsoModel)(body))
    End Function

    Public Async Function Responder(empresa As String, pedido As Integer, respuesta As ResponderConsultaReembolsoModel) As Task(Of RespuestaConsultaReembolsoModel) Implements IConsultaReembolsoService.Responder
        Dim body As String = Await Enviar(HttpMethod.Post, RutaConsulta(empresa, pedido) & "/Respuesta", respuesta, Nothing)
        Return JsonConvert.DeserializeObject(Of RespuestaConsultaReembolsoModel)(body)
    End Function

    ''' <param name="prefijoError">Delante del mensaje de la API («No se ha podido…: »); Nothing = el mensaje de la API tal cual.</param>
    Private Async Function Enviar(metodo As HttpMethod, ruta As String, cuerpo As Object, prefijoError As String) As Task(Of String)
        Using client As HttpClient = _clienteApiFactory.Crear()
            Using peticion As New HttpRequestMessage(metodo, ruta)
                If cuerpo IsNot Nothing Then
                    peticion.Content = New StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json")
                End If
                Dim response As HttpResponseMessage = Await client.SendAsync(peticion)
                Dim body As String = Await response.Content.ReadAsStringAsync()
                If response.StatusCode = HttpStatusCode.NoContent Then
                    Return String.Empty
                End If
                If Not response.IsSuccessStatusCode Then
                    Dim mensaje As String = ConsultaReembolso.ExtraerMensaje(body, $"Error {CInt(response.StatusCode)} de la API.")
                    Throw New Exception(If(String.IsNullOrWhiteSpace(prefijoError), mensaje, $"{prefijoError}: {mensaje}"))
                End If
                Return body
            End Using
        End Using
    End Function
End Class
