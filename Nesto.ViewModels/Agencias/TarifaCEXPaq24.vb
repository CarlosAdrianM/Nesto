Public Class TarifaCEXPaq24
    Implements ITarifaAgencia

    Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
        Get
            Return 8 ' CEX
        End Get
    End Property

    Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
        Get
            Return 63 ' Paq 24
        End Get
    End Property

    Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
        Get
            Return "Paq 24"
        End Get
    End Property
    Private ReadOnly _costeEnvio As New List(Of (Decimal, ZonasEnvioAgencia, Decimal)) From {
        (1D, ZonasEnvioAgencia.Provincial, 3.07D),
        (2D, ZonasEnvioAgencia.Provincial, 3.2D),
        (3D, ZonasEnvioAgencia.Provincial, 3.38D),
        (4D, ZonasEnvioAgencia.Provincial, 3.52D),
        (5D, ZonasEnvioAgencia.Provincial, 3.62D),
        (10D, ZonasEnvioAgencia.Provincial, 4.64D),
        (15D, ZonasEnvioAgencia.Provincial, 5.67D),
        (1D, ZonasEnvioAgencia.Peninsular, 3.35D),
        (2D, ZonasEnvioAgencia.Peninsular, 3.66D),
        (3D, ZonasEnvioAgencia.Peninsular, 3.83D),
        (4D, ZonasEnvioAgencia.Peninsular, 4.01D),
        (5D, ZonasEnvioAgencia.Peninsular, 4.15D),
        (10D, ZonasEnvioAgencia.Peninsular, 5.17D),
        (15D, ZonasEnvioAgencia.Peninsular, 6.22D),
        (1D, ZonasEnvioAgencia.Portugal, 7.25D),
        (2D, ZonasEnvioAgencia.Portugal, 8.11D),
        (3D, ZonasEnvioAgencia.Portugal, 8.97D),
        (4D, ZonasEnvioAgencia.Portugal, 9.56D),
        (5D, ZonasEnvioAgencia.Portugal, 9.99D),
        (10D, ZonasEnvioAgencia.Portugal, 15.84D),
        (15D, ZonasEnvioAgencia.Portugal, 19.1D)
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
        Select Case zona
            Case ZonasEnvioAgencia.Provincial
                Return 0.19
            Case ZonasEnvioAgencia.Peninsular
                Return 0.26
            Case ZonasEnvioAgencia.Portugal
                Return 1
            Case Else
                Return Decimal.MaxValue
        End Select
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
