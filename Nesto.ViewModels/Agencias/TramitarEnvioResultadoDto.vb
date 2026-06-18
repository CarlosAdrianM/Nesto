''' <summary>
''' Respuesta de NestoAPI al tramitar un envío con una agencia remota
''' (POST api/EnviosAgencias/{id}/Tramitar). El albarán y los bultos los asigna la agencia; la
''' etiqueta viene en ZPL (base64 en <see cref="EtiquetaContenido"/>) para mandar a la Zebra.
''' </summary>
Public Class TramitarEnvioResultadoDto
    Public Property Numero As Integer
    Public Property Albaran As String
    Public Property Bultos As Integer
    Public Property Reimpresion As Boolean
    Public Property EtiquetaTipo As String
    Public Property EtiquetaCodificacion As String
    Public Property EtiquetaContenido As String
End Class
