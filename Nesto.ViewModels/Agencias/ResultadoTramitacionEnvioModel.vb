''' <summary>
''' Nesto#340 (Agencias, slice A4.1): respuesta de NestoAPI al cerrar un envío
''' (POST api/EnviosAgencias/{id}/ConfirmarTramitacion). <see cref="Asiento"/> es 0 cuando el envío
''' no llevaba reembolso que contabilizar.
''' </summary>
Public Class ResultadoTramitacionEnvioModel
    Public Property Numero As Integer
    Public Property Asiento As Integer
    Public Property Mensaje As String
End Class
