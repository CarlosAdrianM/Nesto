Imports System.Net.Http
Imports System.Threading.Tasks
Imports Nesto.Contratos
Imports Newtonsoft.Json

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

    Public ReadOnly Property usuario As String Implements IConfiguracion.usuario
        Get
            Return System.Environment.UserDomainName + "\" + System.Environment.UserName
            'Return System.Environment.UserDomainName + "\Administrador"
        End Get
    End Property

    Public Async Function leerParametro(empresa As String, clave As String) As Task(Of String) Implements IConfiguracion.leerParametro

        Using client As New HttpClient
            client.BaseAddress = New Uri(Me.servidorAPI)
            Dim response As HttpResponseMessage

            Try
                response = Await client.GetAsync("ParametrosUsuario?empresa=" + empresa + "&usuario=" + System.Environment.UserName + "&clave=" + clave)
                'response = Await client.GetAsync("ParametrosUsuario?empresa=" + empresa + "&usuario=Carolina&clave=" + clave)

                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    respuesta = JsonConvert.DeserializeObject(Of String)(respuesta)
                    Return respuesta.Trim
                Else
                    Throw New Exception("No se puede leer el parámetro")
                End If
            Catch ex As Exception
                Throw New Exception("No se puede leer el parámetro")
            End Try

        End Using

    End Function
End Class
