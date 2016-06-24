Imports Nesto.Contratos

Public Class Configuracion
    Implements IConfiguracion

    Public ReadOnly Property servidorAPI As String Implements IConfiguracion.servidorAPI
        Get
            If Environment.MachineName = "ALGETE17" Then
                'Return "http://81.43.72.38/api/"
                'Return "http://88.26.231.83/api/"
                Return "http://192.168.154.26/api/"
                'Return "http://localhost:53364/api/"
            End If
            Return "http://192.168.154.26/api/"
        End Get
    End Property
End Class
