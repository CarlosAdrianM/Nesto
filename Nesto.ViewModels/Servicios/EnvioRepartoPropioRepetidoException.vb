Imports Newtonsoft.Json.Linq

''' <summary>
''' NestoAPI#847 (sugerencia 1103, Alfredo, 06/11/26): la etiqueta del envío 250662 (pedido 929042) no salió, se volvió a pulsar
''' «Insertar e Imprimir» y quedaron tres envíos del reparto propio para el mismo pedido. Desde NestoAPI#847 el POST de un envío
''' del reparto propio para un pedido que ya tiene el suyo (en curso, o tramitado con fecha de hoy en adelante) contesta 409
''' { Codigo = ENVIO_REPARTO_PROPIO_REPETIDO, Message, Envio, CodigoBarras, Bultos } y no crea nada: hay que reimprimir ese.
''' </summary>
Public Class EnvioRepartoPropioRepetidoException
    Inherits Exception

    Public Const CODIGO As String = "ENVIO_REPARTO_PROPIO_REPETIDO"

    ''' <summary>El envío que ya tiene el pedido (el que hay que reimprimir).</summary>
    Public ReadOnly Property NumeroEnvio As Integer

    Public Sub New(numeroEnvio As Integer, mensaje As String)
        MyBase.New(mensaje)
        Me.NumeroEnvio = numeroEnvio
    End Sub

    ''' <summary>La excepción si la respuesta es ese 409; Nothing en cualquier otro caso (otro estado, otro código o sin JSON).</summary>
    Public Shared Function DesdeRespuesta(codigoEstado As Integer, cuerpo As String) As EnvioRepartoPropioRepetidoException
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
        Dim numero As Integer = If(json.Value(Of Integer?)("Envio"), 0)
        Dim mensaje As String = json.Value(Of String)("Message")
        If String.IsNullOrWhiteSpace(mensaje) Then
            mensaje = $"El pedido ya tiene el envío {numero} del reparto propio: no se crea otro."
        End If
        Return New EnvioRepartoPropioRepetidoException(numero, mensaje)
    End Function
End Class
