''' <summary>
''' Cuerpo de POST api/EnviosAgencias/PagarReembolsos (Nesto#415 / Nesto#340 slice A4.3): qué
''' reembolsos paga la agencia y a qué cliente. Los nombres son el contrato con PagoReembolsosDTO
''' del servidor (ver AgenciaServicePagoReembolsosTests): si dejan de casar, Web API deja la
''' propiedad por defecto sin dar ningún error.
''' </summary>
Public Class PagoReembolsosDto
    Public Property Empresa As String
    Public Property Cliente As String
    Public Property Agencia As Integer
    Public Property NumerosEnvio As List(Of Integer)
End Class

''' <summary>Respuesta del servidor al pago: el asiento y cuánto se pagó.</summary>
Public Class ResultadoPagoReembolsosDto
    Public Property Asiento As Integer
    Public Property Envios As Integer
    Public Property Importe As Decimal
    Public Property Mensaje As String
End Class
