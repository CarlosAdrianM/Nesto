Public Class TarifaSendingMaritimo
    Implements ITarifaAgencia

    Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
        Get
            Return 10 ' Sending
        End Get
    End Property

    Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
        Get
            Return 18 ' Send Marítimo
        End Get
    End Property

    Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
        Get
            Return "Send Marítimo"
        End Get
    End Property
    Private _costeDUA As Decimal = 20
    Private ReadOnly _costeEnvio As New List(Of (Decimal, ZonasEnvioAgencia, Decimal)) From {
        (5D, ZonasEnvioAgencia.BalearesMayores, 7.9D),
        (10D, ZonasEnvioAgencia.BalearesMayores, 9.95D),
        (15D, ZonasEnvioAgencia.BalearesMayores, 14.15D),
        (20D, ZonasEnvioAgencia.BalearesMayores, 18.3D),
        (5D, ZonasEnvioAgencia.BalearesMenores, 7.95D),
        (10D, ZonasEnvioAgencia.BalearesMenores, 10.1D),
        (15D, ZonasEnvioAgencia.BalearesMenores, 14.35D),
        (20D, ZonasEnvioAgencia.BalearesMenores, 18.6D),
        (5D, ZonasEnvioAgencia.CanariasMayores, 5.55D + _costeDUA),
        (10D, ZonasEnvioAgencia.CanariasMayores, 6.4D + _costeDUA),
        (15D, ZonasEnvioAgencia.CanariasMayores, 8.05D + _costeDUA),
        (20D, ZonasEnvioAgencia.CanariasMayores, 9.75D + _costeDUA),
        (5D, ZonasEnvioAgencia.CanariasMenores, 12.35D + _costeDUA),
        (10D, ZonasEnvioAgencia.CanariasMenores, 14.7D + _costeDUA),
        (15D, ZonasEnvioAgencia.CanariasMenores, 19.35D + _costeDUA),
        (20D, ZonasEnvioAgencia.CanariasMenores, 24.05D + _costeDUA)
    }
    Public ReadOnly Property CosteEnvio As List(Of (Decimal, ZonasEnvioAgencia, Decimal)) Implements ITarifaAgencia.CosteEnvio
        Get
            Return _costeEnvio
        End Get
    End Property

    Public ReadOnly Property HorarioDefectoId As Byte Implements ITarifaAgencia.HorarioDefectoId
        Get
            Return 1 ' Normal
        End Get
    End Property

    Public Function CosteKiloAdicional(zona As ZonasEnvioAgencia) As Decimal Implements ITarifaAgencia.CosteKiloAdicional
        If zona = ZonasEnvioAgencia.BalearesMayores Then
            Return 0.95
        ElseIf zona = ZonasEnvioAgencia.BalearesMenores Then
            Return 1
        ElseIf zona = ZonasEnvioAgencia.CanariasMayores Then
            Return 0.6
        ElseIf zona = ZonasEnvioAgencia.CanariasMenores Then
            Return 0.95
        Else
            Return Decimal.MaxValue
        End If
    End Function

    Public Function CosteReembolso(reembolso As Decimal) As Decimal Implements ITarifaAgencia.CosteReembolso
        Const porcentaje As Decimal = 0.015D
        Const minimo As Decimal = 1.5D
        Dim comision As Decimal = reembolso * porcentaje

        If (comision > minimo) Then
            Return comision
        Else
            Return minimo
        End If
    End Function
End Class
