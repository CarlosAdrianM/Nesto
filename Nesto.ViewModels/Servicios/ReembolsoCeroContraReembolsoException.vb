Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#603 / NestoAPI#785 (pedido 929609, envío 250432): el PUT que registra (imprime) una etiqueta pendiente ha devuelto
''' 409 ReembolsoCeroContraReembolso porque el pedido es contra reembolso, la etiqueta va a 0 € y la factura tiene importe
''' pendiente de cobro. La API no ha guardado nada: el envío sigue pendiente hasta que se le ponga el reembolso.
''' El Message es el texto de la API para el usuario (de usted).
''' </summary>
Public Class ReembolsoCeroContraReembolsoException
    Inherits Exception

    Public Const CODIGO As String = "ReembolsoCeroContraReembolso"

    Public ReadOnly Property NumeroEnvio As Integer

    Public Sub New(numeroEnvio As Integer, mensaje As String)
        MyBase.New(mensaje)
        Me.NumeroEnvio = numeroEnvio
    End Sub

    ''' <summary>
    ''' La excepción si la respuesta es el 409 con Codigo = ReembolsoCeroContraReembolso ({ Message, Mensaje, Codigo });
    ''' Nothing en cualquier otro caso (otro estado, otro código o un cuerpo que no es JSON).
    ''' </summary>
    Public Shared Function DesdeRespuesta(numeroEnvio As Integer, codigoEstado As Integer, cuerpo As String) As ReembolsoCeroContraReembolsoException
        If codigoEstado <> 409 OrElse String.IsNullOrWhiteSpace(cuerpo) Then
            Return Nothing
        End If
        Dim json As JObject
        Try
            json = JObject.Parse(cuerpo)
        Catch
            Return Nothing
        End Try
        If json.Value(Of String)("Codigo") <> CODIGO Then
            Return Nothing
        End If
        Dim mensaje As String = If(json.Value(Of String)("Message"), json.Value(Of String)("Mensaje"))
        If String.IsNullOrWhiteSpace(mensaje) Then
            mensaje = $"El pedido es contra reembolso y la etiqueta lleva 0 € (envío {numeroEnvio}). Ponga el reembolso en la etiqueta pendiente antes de imprimirla."
        End If
        Return New ReembolsoCeroContraReembolsoException(numeroEnvio, mensaje)
    End Function
End Class
