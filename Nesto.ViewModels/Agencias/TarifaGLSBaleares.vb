Public Class TarifaGLSBaleares
    Implements ITarifaAgencia

    Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
        Get
            Return 1 ' GLS/ASM
        End Get
    End Property

    Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
        Get
            Return 6 ' Carga 
        End Get
    End Property

    Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
        Get
            Return "Insular marítimo"
        End Get
    End Property
    Private ReadOnly _costeEnvio As New List(Of (Decimal, ZonasEnvioAgencia, Decimal)) From {
        (5D, ZonasEnvioAgencia.BalearesMayores, 12.16D),
        (5D, ZonasEnvioAgencia.BalearesMenores, 14.75D),
        (5D, ZonasEnvioAgencia.CanariasMayores, 10.63D + 20.85D), 'Los 20.85 € es un coste aproximado del DUA. Cambiar con valor real
        (5D, ZonasEnvioAgencia.CanariasMenores, 14.36D + 20.85D)
    }
    Public ReadOnly Property CosteEnvio As List(Of (Decimal, ZonasEnvioAgencia, Decimal)) Implements ITarifaAgencia.CosteEnvio
        Get
            Return _costeEnvio
        End Get
    End Property

    Public ReadOnly Property HorarioDefectoId As Byte Implements ITarifaAgencia.HorarioDefectoId
        Get
            Return 10 ' 
        End Get
    End Property

    Public Function CosteKiloAdicional(zona As ZonasEnvioAgencia) As Decimal Implements ITarifaAgencia.CosteKiloAdicional
        If zona = ZonasEnvioAgencia.BalearesMayores Then
            Return 0.91
        ElseIf zona = ZonasEnvioAgencia.BalearesMenores Then
            Return 1.11
        ElseIf zona = ZonasEnvioAgencia.CanariasMayores Then
            Return 0.97
        ElseIf zona = ZonasEnvioAgencia.CanariasMenores Then
            Return 1.46
        Else
            Return Decimal.MaxValue
        End If
    End Function

    Public Function CosteReembolso(reembolso As Decimal) As Decimal Implements ITarifaAgencia.CosteReembolso
        Return 1.8
    End Function
End Class
