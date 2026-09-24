Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#489 / NestoAPI#533: la API no deja cambiar el modo de entrega porque el pedido ya tiene picking (o ha
''' salido parte hoy). Código MODO_CON_PICKING; el mensaje ya explica por qué y que se le puede pedir a almacén.
''' Compartida por el detalle de pedido y la plantilla (al modificar un pedido).
''' </summary>
Public Class ModoConPickingException
    Inherits Exception

    Public Const CODIGO_ERROR As String = "MODO_CON_PICKING"

    Public Sub New(mensaje As String)
        MyBase.New(mensaje)
    End Sub

    ''' <summary>Si el error de la API es MODO_CON_PICKING, la excepción con su mensaje legible; si no, Nothing.</summary>
    Public Shared Function DesdeRespuesta(detallesError As JObject, mensajeLegible As String) As ModoConPickingException
        Dim errorObj As JObject = TryCast(detallesError?("error"), JObject)
        If errorObj?("code")?.ToString() <> CODIGO_ERROR Then
            Return Nothing
        End If
        ' El mensaje limpio de la API (sin el «[MODO_CON_PICKING]» que antepone HttpErrorHelper).
        Dim mensaje As String = errorObj("message")?.ToString()
        Return New ModoConPickingException(If(String.IsNullOrWhiteSpace(mensaje), mensajeLegible, mensaje))
    End Function
End Class
