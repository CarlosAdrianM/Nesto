Public Class TarifaCEXCanariasMaritimo
    Implements ITarifaAgencia

    Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
        Get
            Return 8 ' CEX
        End Get
    End Property

    Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
        Get
            Return 69 ' Canarias Marítimo
        End Get
    End Property

    Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
        Get
            Return "Canarias Marítimo"
        End Get
    End Property
    Private _costeDUA As Decimal = 20.85D
    Private ReadOnly _costeEnvio As New List(Of (Decimal, ZonasEnvioAgencia, Decimal)) From {
        (5D, ZonasEnvioAgencia.CanariasMayores, 9.95D + _costeDUA),
        (10D, ZonasEnvioAgencia.CanariasMayores, 14.08D + _costeDUA),
        (15D, ZonasEnvioAgencia.CanariasMayores, 18.34D + _costeDUA),
        (20D, ZonasEnvioAgencia.CanariasMayores, 21.43D + _costeDUA),
        (30D, ZonasEnvioAgencia.CanariasMayores, 27.63D + _costeDUA),
        (40D, ZonasEnvioAgencia.CanariasMayores, 33.85D + _costeDUA),
        (5D, ZonasEnvioAgencia.CanariasMenores, 10.94D + _costeDUA),
        (10D, ZonasEnvioAgencia.CanariasMenores, 15.49D + _costeDUA),
        (15D, ZonasEnvioAgencia.CanariasMenores, 20.18D + _costeDUA),
        (20D, ZonasEnvioAgencia.CanariasMenores, 23.6D + _costeDUA),
        (30D, ZonasEnvioAgencia.CanariasMenores, 30.41D + _costeDUA),
        (40D, ZonasEnvioAgencia.CanariasMenores, 37.24D + _costeDUA)
    }
    Public ReadOnly Property CosteEnvio As List(Of (Decimal, ZonasEnvioAgencia, Decimal)) Implements ITarifaAgencia.CosteEnvio
        Get
            Return _costeEnvio
        End Get
    End Property

    Public ReadOnly Property HorarioDefectoId As Byte Implements ITarifaAgencia.HorarioDefectoId
        Get
            Return 0 'No disponible
        End Get
    End Property

    Public Function CosteKiloAdicional(zona As ZonasEnvioAgencia) As Decimal Implements ITarifaAgencia.CosteKiloAdicional
        If zona = ZonasEnvioAgencia.CanariasMayores Then
            Return 0.63
        ElseIf zona = ZonasEnvioAgencia.CanariasMenores Then
            Return 0.69
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
