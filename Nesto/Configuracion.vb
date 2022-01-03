Imports System.Net.Http
Imports System.Threading.Tasks
Imports Newtonsoft.Json
Imports System.DirectoryServices.AccountManagement
Imports System.Linq
Imports System.Globalization
Imports Nesto.Infrastructure.Contracts

Public Class Configuracion
    Implements IConfiguracion

    Public ReadOnly Property servidorAPI As String Implements IConfiguracion.servidorAPI
        Get
            If Environment.MachineName = "VSTUDIO" Then
                Return "http://localhost:53364/api/"
            End If

            Return "http://api.nuevavision.es/api/"
        End Get
    End Property

    Public ReadOnly Property usuario As String Implements IConfiguracion.usuario
        Get
            Dim usuarioFormatedo As String = Environment.UserName
            If usuarioFormatedo = usuarioFormatedo.ToLower OrElse usuarioFormatedo = usuarioFormatedo.ToUpper Then
                usuarioFormatedo = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(usuarioFormatedo)
            End If
            Return Environment.UserDomainName + "\" + usuarioFormatedo
            'Return System.Environment.UserDomainName + "\manuel"
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

    Public Function LeerParametroSync(empresa As String, clave As String) As String Implements IConfiguracion.LeerParametroSync

        Using client As New HttpClient
            client.BaseAddress = New Uri(Me.servidorAPI)
            Dim response As HttpResponseMessage

            Try
                response = client.GetAsync("ParametrosUsuario?empresa=" + empresa + "&usuario=" + System.Environment.UserName + "&clave=" + clave).Result

                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = response.Content.ReadAsStringAsync().Result
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

    Public Function UsuarioEnGrupo(grupo As String) As Boolean Implements IConfiguracion.UsuarioEnGrupo
        Dim yourDomain As String = System.Environment.UserDomainName
        Using ctx As New PrincipalContext(ContextType.Domain, yourDomain)
            Using grp = GroupPrincipal.FindByIdentity(ctx, IdentityType.Name, grupo)
                Dim isInRole As Boolean = Not IsNothing(grp) AndAlso grp.GetMembers(True).Any(Function(m) m.SamAccountName.ToLower = usuario.ToLower.Replace(yourDomain.ToLower + "\", String.Empty))
                Return isInRole
            End Using
        End Using
    End Function


End Class
