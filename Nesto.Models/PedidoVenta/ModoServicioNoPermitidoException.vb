Imports Newtonsoft.Json.Linq

''' <summary>
''' NestoAPI#518: la API rechaza el pedido porque el modo de servicio elegido ya no tiene sentido (código
''' MODO_SERVICIO_NO_PERMITIDO). El mensaje es accionable y trae el modo que sí vale, para preseleccionarlo.
''' Compartida por la plantilla y el detalle de pedido (Nesto#484).
''' </summary>
Public Class ModoServicioNoPermitidoException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "MODO_SERVICIO_NO_PERMITIDO"

    Public Sub New(mensaje As String, modoSugerido As Byte?)
        MyBase.New(mensaje)
        Me.ModoSugerido = modoSugerido
    End Sub

    Public ReadOnly Property ModoSugerido As Byte?

    ''' <summary>
    ''' Si el error de la API es un rechazo de modo de servicio, la excepción con su mensaje (ya legible) y el
    ''' modo sugerido; si es otro error, Nothing.
    ''' </summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As ModoServicioNoPermitidoException
        Dim codigo As String = TryCast(detallesError?("error"), JObject)?("code")?.ToString()
        If codigo <> CODIGO_ERROR Then
            Return Nothing
        End If
        Return New ModoServicioNoPermitidoException(mensajeLegible, LeerModoSugerido(detallesError))
    End Function

    ''' <summary>NestoAPI#518: error.details.modoSugerido del rechazo MODO_SERVICIO_NO_PERMITIDO.</summary>
    Public Shared Function LeerModoSugerido(detallesError As JObject) As Byte?
        Dim valor = detallesError?("error")?("details")?("modoSugerido")
        Dim modo As Byte
        If valor IsNot Nothing AndAlso Byte.TryParse(valor.ToString(), modo) AndAlso ModosServicio.EsValido(modo) Then
            Return modo
        End If
        Return Nothing
    End Function
End Class
