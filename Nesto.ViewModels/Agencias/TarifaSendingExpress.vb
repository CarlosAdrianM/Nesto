Public Class TarifaSendingExpress
    Implements ITarifaAgencia

    Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
        Get
            Return 10 ' Sending
        End Get
    End Property

    Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
        Get
            Return 1 ' Send Exprés
        End Get
    End Property

    Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
        Get
            Return "Send Exprés"
        End Get
    End Property
    Private _costeDUA As Decimal = 20D
    Private ReadOnly _costeEnvio As New List(Of (Decimal, ZonasEnvioAgencia, Decimal)) From {
        (1D, ZonasEnvioAgencia.Provincial, 2.9D),
        (3D, ZonasEnvioAgencia.Provincial, 3D),
        (5D, ZonasEnvioAgencia.Provincial, 3.15D),
        (10D, ZonasEnvioAgencia.Provincial, 3.65D),
        (15D, ZonasEnvioAgencia.Provincial, 4.3D),
        (20D, ZonasEnvioAgencia.Provincial, 4.55D),
        (1D, ZonasEnvioAgencia.Peninsular, 3.05D),
        (3D, ZonasEnvioAgencia.Peninsular, 3.2D),
        (5D, ZonasEnvioAgencia.Peninsular, 4.15D),
        (10D, ZonasEnvioAgencia.Peninsular, 5.55D),
        (15D, ZonasEnvioAgencia.Peninsular, 7.45D),
        (20D, ZonasEnvioAgencia.Peninsular, 9D),
        (1D, ZonasEnvioAgencia.BalearesMayores, 7D),
        (1D, ZonasEnvioAgencia.BalearesMenores, 7.8D),
        (1D, ZonasEnvioAgencia.CanariasMayores, 8.05D + _costeDUA),
        (1D, ZonasEnvioAgencia.CanariasMenores, 14.8D + _costeDUA),
        (1D, ZonasEnvioAgencia.Portugal, 5.55D),
        (3D, ZonasEnvioAgencia.Portugal, 5.9D),
        (5D, ZonasEnvioAgencia.Portugal, 6.5D),
        (10D, ZonasEnvioAgencia.Portugal, 8.05D),
        (15D, ZonasEnvioAgencia.Portugal, 10.45D),
        (20D, ZonasEnvioAgencia.Portugal, 12.05D)
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
        Select Case zona
            Case ZonasEnvioAgencia.Provincial
                Return 0.2
            Case ZonasEnvioAgencia.Peninsular
                Return 0.35
            Case ZonasEnvioAgencia.BalearesMayores
                Return 3.65
            Case ZonasEnvioAgencia.BalearesMenores
                Return 4.45
            Case ZonasEnvioAgencia.CanariasMayores
                Return 4.35
            Case ZonasEnvioAgencia.CanariasMenores
                Return 4.75
            Case ZonasEnvioAgencia.Portugal
                Return 0.4
            Case Else
                Return Decimal.MaxValue
        End Select
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
