Public Class TarifaGLSBusinessParcel
    Implements ITarifaAgencia

    Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
        Get
            Return 1 ' ASM/GSL
        End Get
    End Property

    Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
        Get
            Return 96 ' BusinessParcel
        End Get
    End Property

    Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
        Get
            Return "BusinessParcel"
        End Get
    End Property
    Private ReadOnly _costeEnvio As New List(Of (Decimal, ZonasEnvioAgencia, Decimal)) From {
        (1D, ZonasEnvioAgencia.Provincial, 3.01D),
        (3D, ZonasEnvioAgencia.Provincial, 3.19D),
        (5D, ZonasEnvioAgencia.Provincial, 3.25D),
        (10D, ZonasEnvioAgencia.Provincial, 3.46D),
        (15D, ZonasEnvioAgencia.Provincial, 3.95D),
        (1D, ZonasEnvioAgencia.Peninsular, 3.56D),
        (3D, ZonasEnvioAgencia.Peninsular, 3.75D),
        (5D, ZonasEnvioAgencia.Peninsular, 4.07D),
        (10D, ZonasEnvioAgencia.Peninsular, 4.58D),
        (15D, ZonasEnvioAgencia.Peninsular, 5.9D)
    }
    Public ReadOnly Property CosteEnvio As List(Of (Decimal, ZonasEnvioAgencia, Decimal)) Implements ITarifaAgencia.CosteEnvio
        Get
            Return _costeEnvio
        End Get
    End Property

    Public ReadOnly Property HorarioDefectoId As Byte Implements ITarifaAgencia.HorarioDefectoId
        Get
            Return 18
        End Get
    End Property

    Public Function CosteKiloAdicional(zona As ZonasEnvioAgencia) As Decimal Implements ITarifaAgencia.CosteKiloAdicional
        If zona = ZonasEnvioAgencia.Peninsular Then
            Return 0.39
        ElseIf zona = ZonasEnvioAgencia.Provincial Then
            Return 0.3
        Else
            Return Decimal.MaxValue
        End If
    End Function

    Public Function CosteReembolso(reembolso As Decimal) As Decimal Implements ITarifaAgencia.CosteReembolso
        Return 1.8
    End Function
End Class
