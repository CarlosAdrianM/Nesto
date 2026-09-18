''' <summary>
''' Respuesta de NestoAPI al confirmar la recepción de un retorno
''' (POST api/EnviosAgencias/{id}/RecibirRetorno, Nesto#340 slice A4.2). Los nombres de las
''' propiedades son el contrato con RetornoRecibidoDTO del servidor: si dejan de casar, Newtonsoft
''' deja la fecha en 01/01/0001 sin dar ningún error (ver AgenciaServiceRetornosTests).
''' </summary>
Public Class RetornoRecibidoDto
    Public Property Numero As Integer
    Public Property Pedido As Integer?
    Public Property FechaRetornoRecibido As Date
End Class
