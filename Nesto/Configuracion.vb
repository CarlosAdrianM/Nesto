Imports Nesto.Contratos

Public Class Configuracion
    Implements IConfiguracion

    Public ReadOnly Property servidorAPI As String Implements IConfiguracion.servidorAPI
        Get
            If Environment.MachineName = "ALGETE17" Then
                'Return "http://api.nuevavision.es/api/"
                Return "http://localhost:53364/api/"
            End If
            Return "http://api.nuevavision.es/api/"
        End Get
    End Property
End Class
