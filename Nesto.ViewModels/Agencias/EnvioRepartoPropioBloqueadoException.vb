Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

''' <summary>
''' Carlos 20/11/26 («en el reparto propio manda el bulto»): la API contesta 409 { Message, Codigo } cuando se intenta dar por
''' cerrado, anular, borrar, cambiar de agencia o cambiar el estado de un envío del reparto propio (agencia 14) que no se puede
''' tocar así:
''' <list type="bullet">
''' <item>«EN_FURGONETA»: algún bulto lo lleva un repartidor (trae además Repartidor y Desde).</item>
''' <item>«CIERRE_NO_PERMITIDO»: un envío del reparto propio no se cierra a mano (se entrega en Ariadna o se anula con sus bultos de vuelta).</item>
''' </list>
''' El Message ya está pensado para el usuario: es el texto de la excepción y se enseña tal cual (nunca el JSON). No se ha tocado nada.
''' </summary>
Public Class EnvioRepartoPropioBloqueadoException
    Inherits Exception

    Public Const CODIGO_EN_FURGONETA As String = "EN_FURGONETA"
    Public Const CODIGO_CIERRE_NO_PERMITIDO As String = "CIERRE_NO_PERMITIDO"

    Public Sub New(mensaje As String, codigo As String, repartidor As String, desde As Date?)
        MyBase.New(mensaje)
        Me.Codigo = codigo
        Me.Repartidor = repartidor
        Me.Desde = desde
    End Sub

    Public ReadOnly Property Codigo As String
    ''' <summary>Solo con EN_FURGONETA: quién lo lleva (puede venir vacío).</summary>
    Public ReadOnly Property Repartidor As String
    ''' <summary>Solo con EN_FURGONETA: desde cuándo lo lleva (puede no venir).</summary>
    Public ReadOnly Property Desde As Date?

    ''' <summary>
    ''' La excepción si la respuesta es un 409 con uno de los dos códigos y un Message; Nothing en cualquier otro caso (otro código
    ''' HTTP, otro Codigo, sin mensaje o un cuerpo que no es JSON), para que quien llama siga con su tratamiento de siempre.
    ''' </summary>
    Public Shared Function DesdeRespuesta(codigoHttp As Integer, cuerpo As String) As EnvioRepartoPropioBloqueadoException
        If codigoHttp <> 409 OrElse String.IsNullOrWhiteSpace(cuerpo) Then
            Return Nothing
        End If
        Dim respuesta As JObject
        Try
            Dim token As JToken = JToken.Parse(cuerpo)
            respuesta = TryCast(token, JObject)
        Catch ex As JsonException
            Return Nothing
        End Try
        If respuesta Is Nothing Then
            Return Nothing
        End If
        Dim codigo As String = Texto(respuesta, "Codigo")
        If codigo <> CODIGO_EN_FURGONETA AndAlso codigo <> CODIGO_CIERRE_NO_PERMITIDO Then
            Return Nothing
        End If
        Dim mensaje As String = Texto(respuesta, "Message")
        If String.IsNullOrWhiteSpace(mensaje) Then
            Return Nothing
        End If
        Return New EnvioRepartoPropioBloqueadoException(mensaje.Trim(), codigo, Texto(respuesta, "Repartidor")?.Trim(), Fecha(respuesta, "Desde"))
    End Function

    Private Shared Function Texto(respuesta As JObject, nombre As String) As String
        Dim valor As JToken = respuesta.GetValue(nombre, StringComparison.OrdinalIgnoreCase)
        If valor Is Nothing OrElse valor.Type = JTokenType.Null Then
            Return Nothing
        End If
        Return valor.ToString()
    End Function

    Private Shared Function Fecha(respuesta As JObject, nombre As String) As Date?
        Dim valor As JToken = respuesta.GetValue(nombre, StringComparison.OrdinalIgnoreCase)
        If valor Is Nothing OrElse valor.Type = JTokenType.Null Then
            Return Nothing
        End If
        If valor.Type = JTokenType.Date Then
            Return valor.Value(Of Date)()
        End If
        Dim leida As Date
        If Date.TryParse(valor.ToString(), Globalization.CultureInfo.InvariantCulture, Globalization.DateTimeStyles.None, leida) Then
            Return leida
        End If
        Return Nothing
    End Function
End Class
