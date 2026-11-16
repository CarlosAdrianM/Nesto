Imports Newtonsoft.Json.Linq

''' <summary>
''' NestoAPI#870: la API no deja imprimir la etiqueta, pasar a en curso ni tramitar un REENVÍO (NestoAPI#699) hasta que la caja
''' de su envío original haya vuelto físicamente a Algete. Contesta 409 con
''' <c>{ "Message", "Codigo": "ESPERANDO_CAJA", "Mensaje", "EnvioOrigen", "EnvioNuevo" }</c> y no ha tocado nada. Si la caja sí
''' está aquí, se repite la MISMA llamada con <c>yaEstaAqui=true</c> en la query: la API marca que ha vuelto («AlImprimir») y
''' sigue como siempre. El Message es el texto de la API para el usuario (de usted).
''' </summary>
Public Class EsperandoCajaException
    Inherits Exception

    Public Const CODIGO As String = "ESPERANDO_CAJA"

    ''' <summary>El envío devuelto/incidentado cuya caja se espera.</summary>
    Public ReadOnly Property EnvioOrigen As Integer?
    ''' <summary>El reenvío que no puede salir todavía.</summary>
    Public ReadOnly Property EnvioNuevo As Integer?

    Public Sub New(mensaje As String, envioOrigen As Integer?, envioNuevo As Integer?)
        MyBase.New(mensaje)
        Me.EnvioOrigen = envioOrigen
        Me.EnvioNuevo = envioNuevo
    End Sub

    ''' <summary>
    ''' La excepción si la respuesta es el 409 con Codigo = ESPERANDO_CAJA; Nothing en cualquier otro caso (otro estado, otro
    ''' código o un cuerpo que no es JSON).
    ''' </summary>
    Public Shared Function DesdeRespuesta(codigoEstado As Integer, cuerpo As String) As EsperandoCajaException
        If codigoEstado <> 409 OrElse String.IsNullOrWhiteSpace(cuerpo) Then
            Return Nothing
        End If
        Dim json As JObject
        Try
            json = JObject.Parse(cuerpo)
        Catch
            Return Nothing
        End Try
        If Not String.Equals(json.Value(Of String)("Codigo"), CODIGO, StringComparison.OrdinalIgnoreCase) Then
            Return Nothing
        End If
        Dim origen As Integer? = json.Value(Of Integer?)("EnvioOrigen")
        Dim nuevo As Integer? = json.Value(Of Integer?)("EnvioNuevo")
        Dim mensaje As String = json.Value(Of String)("Message")
        If String.IsNullOrWhiteSpace(mensaje) Then
            mensaje = json.Value(Of String)("Mensaje")
        End If
        If String.IsNullOrWhiteSpace(mensaje) Then
            mensaje = $"La caja del envío {origen} todavía no ha vuelto al almacén: el envío {nuevo} es su reenvío y no puede salir hasta que esté aquí."
        End If
        Return New EsperandoCajaException(mensaje, origen, nuevo)
    End Function

    ''' <summary>La pregunta para forzar (sí/no): el texto de la API y si la caja ya está aquí. <paramref name="accion"/>: «imprimirá la etiqueta», «tramitará el envío»…</summary>
    Public Shared Function PreguntaYaEstaAqui(ex As EsperandoCajaException, accion As String) As String
        Dim caja As String = If(ex.EnvioOrigen.HasValue, $"la caja del envío {ex.EnvioOrigen}", "la caja")
        Return $"{ex.Message}{vbCrLf}{vbCrLf}Si {caja} ya está aquí, en el almacén, pulse Sí: quedará marcado que ha vuelto y se {accion}." &
            $"{vbCrLf}{vbCrLf}¿Ya está aquí la caja?"
    End Function
End Class
