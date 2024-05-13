Public Interface ITarifaAgencia
    ReadOnly Property AgenciaId As Integer
    ReadOnly Property ServicioId As Byte
    ReadOnly Property NombreServicio As String
    ReadOnly Property HorarioDefectoId As Byte
    ReadOnly Property CosteEnvio As List(Of (Decimal, ZonasEnvioAgencia, Decimal))
    Function CosteKiloAdicional(zona As ZonasEnvioAgencia) As Decimal
    Function CosteReembolso(reembolso As Decimal) As Decimal
End Interface
