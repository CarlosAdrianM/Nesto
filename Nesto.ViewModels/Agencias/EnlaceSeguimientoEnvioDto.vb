''' <summary>
''' Nesto#573: respuesta de GET api/EnviosAgencias/{id}/EnlaceSeguimiento (NestoAPI#748). Url = Nothing si el envío no
''' tiene enlace de seguimiento. Mismos nombres que el servidor (PascalCase).
''' </summary>
Public Class EnlaceSeguimientoEnvioDto
    Public Property Numero As Integer
    Public Property Url As String
End Class
