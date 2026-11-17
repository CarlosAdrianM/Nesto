Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#642 (NestoAPI#873, pedido 929555): la API no guarda el pedido porque alguien lo ha cambiado desde que se abrió (la
''' Version que manda Nesto ya no es la del pedido guardado). Código PEDIDO_CAMBIADO; el mensaje ya dice quién y cuándo
''' («El pedido 929555 lo ha modificado Aida a las 15:14 desde que usted lo abrió. Recárguelo y vuelva a hacer los cambios.»).
''' No se ha guardado nada.
''' </summary>
Public Class PedidoCambiadoException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "PEDIDO_CAMBIADO"

    Public Sub New(mensaje As String, Optional usuario As String = Nothing, Optional fecha As Date? = Nothing)
        MyBase.New(mensaje)
        Me.Usuario = usuario
        Me.Fecha = fecha
    End Sub

    ''' <summary>Quién lo tocó el último (sin dominio), si la API lo sabe.</summary>
    Public ReadOnly Property Usuario As String
    ''' <summary>Cuándo, si la API lo sabe.</summary>
    Public ReadOnly Property Fecha As Date?

    ''' <summary>Si el error de la API es PEDIDO_CAMBIADO, la excepción con su mensaje legible; si no, Nothing.</summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As PedidoCambiadoException
        Dim errorObj As JObject = TryCast(detallesError?("error"), JObject)
        If errorObj?("code")?.ToString() <> CODIGO_ERROR Then
            Return Nothing
        End If
        ' El mensaje limpio de la API (sin el «[PEDIDO_CAMBIADO]» que antepone HttpErrorHelper).
        Dim mensaje As String = errorObj("message")?.ToString()
        Dim detalles As JObject = TryCast(errorObj("details"), JObject)
        Dim usuario As String = detalles?("Usuario")?.ToString()
        Dim fecha As Date? = Nothing
        Dim fechaToken As JToken = detalles?("Fecha")
        If fechaToken IsNot Nothing AndAlso fechaToken.Type = JTokenType.Date Then
            fecha = fechaToken.Value(Of Date)()
        End If
        Return New PedidoCambiadoException(If(String.IsNullOrWhiteSpace(mensaje), mensajeLegible, mensaje), usuario, fecha)
    End Function
End Class
