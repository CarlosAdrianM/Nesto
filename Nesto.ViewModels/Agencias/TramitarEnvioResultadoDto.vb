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

''' <summary>
''' Datos corregidos para modificar en la agencia un envío YA registrado
''' (POST api/EnviosAgencias/{id}/Modificar, NestoAPI#317). Solo se pisan los campos
''' informados (Nothing/vacío = conservar el valor actual del envío en el servidor).
''' </summary>
Public Class ModificarEnvioAgenciaDto
    Public Property Nombre As String
    Public Property Direccion As String
    Public Property CodigoPostal As String
    Public Property Poblacion As String
    Public Property Provincia As String
    Public Property Telefono As String
    Public Property Movil As String
    Public Property Observaciones As String
End Class
