Public Class TarifaCEXBaleares
    Implements ITarifaAgencia

    Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
        Get
            Return 8 ' CEX
        End Get
    End Property

    Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
        Get
            Return 66 ' BalearesExpress
        End Get
    End Property

    Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
        Get
            Return "Baleares Express"
        End Get
    End Property
    Private ReadOnly _costeEnvio As New List(Of (Decimal, ZonasEnvioAgencia, Decimal)) From {
        (1D, ZonasEnvioAgencia.BalearesMayores, 5.84D),
        (2D, ZonasEnvioAgencia.BalearesMayores, 6.6D),
        (3D, ZonasEnvioAgencia.BalearesMayores, 7.74D),
        (4D, ZonasEnvioAgencia.BalearesMayores, 8.28D),
        (5D, ZonasEnvioAgencia.BalearesMayores, 8.86D),
        (10D, ZonasEnvioAgencia.BalearesMayores, 11.65D),
        (15D, ZonasEnvioAgencia.BalearesMayores, 14.41D),
        (1D, ZonasEnvioAgencia.BalearesMenores, 6.14D),
        (2D, ZonasEnvioAgencia.BalearesMenores, 7.05D),
        (3D, ZonasEnvioAgencia.BalearesMenores, 8.31D),
        (4D, ZonasEnvioAgencia.BalearesMenores, 9.01D),
        (5D, ZonasEnvioAgencia.BalearesMenores, 9.7D),
        (10D, ZonasEnvioAgencia.BalearesMenores, 13.17D),
        (15D, ZonasEnvioAgencia.BalearesMenores, 17.2D)
    }
    Public ReadOnly Property CosteEnvio As List(Of (Decimal, ZonasEnvioAgencia, Decimal)) Implements ITarifaAgencia.CosteEnvio
        Get
            Return _costeEnvio
        End Get
    End Property

    Public ReadOnly Property HorarioDefectoId As Byte Implements ITarifaAgencia.HorarioDefectoId
        Get
            Throw New NotImplementedException()
        End Get
    End Property

    Public Function CosteKiloAdicional(zona As ZonasEnvioAgencia) As Decimal Implements ITarifaAgencia.CosteKiloAdicional
        If zona = ZonasEnvioAgencia.BalearesMayores Then
            Return 0.53
        ElseIf zona = ZonasEnvioAgencia.BalearesMenores Then
            Return 0.66
        Else
            Return Decimal.MaxValue
        End If
    End Function

    Public Function CosteReembolso(reembolso As Decimal) As Decimal Implements ITarifaAgencia.CosteReembolso
        Const minimo As Decimal = 1
        Const maximo As Decimal = 60
        Dim porcentajeComision As Decimal = 0.02

        Dim importeComision = reembolso * porcentajeComision
        If importeComision < minimo Then
            Return minimo
        ElseIf importeComision > maximo Then
            Return maximo
        Else
            Return importeComision
        End If
    End Function
End Class
