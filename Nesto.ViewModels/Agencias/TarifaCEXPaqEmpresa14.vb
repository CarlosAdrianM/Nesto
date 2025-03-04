﻿Public Class TarifaCEXPaqEmpresa14
    Implements ITarifaAgencia

    Public ReadOnly Property AgenciaId As Integer Implements ITarifaAgencia.AgenciaId
        Get
            Return 8 'CEX
        End Get
    End Property

    Public ReadOnly Property ServicioId As Byte Implements ITarifaAgencia.ServicioId
        Get
            Return 92 ' Paq Empresa 14
        End Get
    End Property

    Public ReadOnly Property NombreServicio As String Implements ITarifaAgencia.NombreServicio
        Get
            Return "Paq Empresa 14"
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
        (15D, ZonasEnvioAgencia.Peninsular, 6.22D)
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
        If zona = ZonasEnvioAgencia.Provincial Then
            Return 0.19
        ElseIf zona = ZonasEnvioAgencia.Peninsular Then
            Return 0.26
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
