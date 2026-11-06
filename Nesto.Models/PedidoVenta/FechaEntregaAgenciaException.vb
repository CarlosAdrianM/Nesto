Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#629 (NestoAPI#722): al aplazar un pedido cuyo envío ya tiene la agencia, la agencia admite el cambio de fecha pero
''' lo rechaza (AGENCIA_RECHAZA_FECHA_ENTREGA, 403) o no contesta (AGENCIA_NO_RESPONDE, 502). El pedido NO se ha guardado:
''' se enseña el mensaje de la API tal cual y el pedido se queda en pantalla con sus cambios para corregir la fecha.
''' </summary>
Public Class FechaEntregaAgenciaRechazadaException
    Inherits Exception

    Public Const CODIGO_RECHAZA As String = "AGENCIA_RECHAZA_FECHA_ENTREGA"
    Public Const CODIGO_NO_RESPONDE As String = "AGENCIA_NO_RESPONDE"
    Public Const TITULO As String = "No se ha cambiado la fecha en la agencia"

    Public Sub New(mensaje As String)
        MyBase.New(mensaje)
    End Sub

    ''' <summary>Si el error de la API es uno de los dos códigos, la excepción con su mensaje limpio; si no, Nothing.</summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As FechaEntregaAgenciaRechazadaException
        Dim errorObj As JObject = TryCast(detallesError?("error"), JObject)
        Dim codigo As String = errorObj?("code")?.ToString()
        If codigo <> CODIGO_RECHAZA AndAlso codigo <> CODIGO_NO_RESPONDE Then
            Return Nothing
        End If
        Dim mensaje As String = errorObj("message")?.ToString()
        Return New FechaEntregaAgenciaRechazadaException(If(String.IsNullOrWhiteSpace(mensaje), mensajeLegible, mensaje))
    End Function
End Class

''' <summary>
''' Nesto#629 (NestoAPI#722): la agencia ya tiene el envío y no admite cambiarle la fecha (PEDIDO_YA_EN_AGENCIA, 403). El pedido
''' NO se ha guardado. Con <see cref="PuedeAvisarLogistica"/> se ofrece «Mandar correo a logística» con el envío y la fecha.
''' </summary>
Public Class PedidoYaEnAgenciaException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "PEDIDO_YA_EN_AGENCIA"

    Public Sub New(mensaje As String, envio As Integer?, fechaEntrega As Date?, puedeAvisarLogistica As Boolean)
        MyBase.New(mensaje)
        Me.Envio = envio
        Me.FechaEntrega = fechaEntrega
        Me.PuedeAvisarLogistica = puedeAvisarLogistica
    End Sub

    Public ReadOnly Property Envio As Integer?
    Public ReadOnly Property FechaEntrega As Date?
    Public ReadOnly Property PuedeAvisarLogistica As Boolean

    ''' <summary>Si el error de la API es PEDIDO_YA_EN_AGENCIA, la excepción con su mensaje y los datos de details; si no, Nothing.</summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As PedidoYaEnAgenciaException
        Dim errorObj As JObject = TryCast(detallesError?("error"), JObject)
        If errorObj?("code")?.ToString() <> CODIGO_ERROR Then
            Return Nothing
        End If
        Dim mensaje As String = errorObj("message")?.ToString()
        Dim details As JObject = TryCast(errorObj("details"), JObject)
        Dim envio As Integer? = Nothing
        Dim fecha As Date? = Nothing
        Dim puede As Boolean = False
        Try
            Dim tokenEnvio As JToken = details?("envio")
            If tokenEnvio IsNot Nothing AndAlso tokenEnvio.Type <> JTokenType.Null Then
                envio = tokenEnvio.ToObject(Of Integer)()
            End If
            Dim tokenFecha As JToken = details?("fechaEntrega")
            If tokenFecha IsNot Nothing AndAlso tokenFecha.Type <> JTokenType.Null Then
                fecha = tokenFecha.ToObject(Of Date)().Date
            End If
            Dim tokenPuede As JToken = details?("puedeAvisarLogistica")
            If tokenPuede IsNot Nothing AndAlso tokenPuede.Type = JTokenType.Boolean Then
                puede = tokenPuede.ToObject(Of Boolean)()
            End If
        Catch
            ' details inesperados: se enseña el mensaje sin botón
        End Try
        ' Sin fecha no se puede mandar el correo (la API la exige)
        Return New PedidoYaEnAgenciaException(If(String.IsNullOrWhiteSpace(mensaje), mensajeLegible, mensaje), envio, fecha, puede AndAlso fecha.HasValue)
    End Function
End Class
