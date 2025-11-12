Imports System.DirectoryServices.AccountManagement
Imports System.Globalization
Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class Configuracion
    Implements IConfiguracion

    Public ReadOnly Property servidorAPI As String Implements IConfiguracion.servidorAPI
        Get
            If Environment.MachineName.StartsWith("VSTUDIO") Then
                Return "http://localhost:53364/api/"
            End If

            Return "http://api.nuevavision.es/api/"
        End Get
    End Property

    Public ReadOnly Property usuario As String Implements IConfiguracion.usuario
        Get
            Return Environment.UserDomainName + "\" + UsuarioSinDominio
        End Get
    End Property

    Public ReadOnly Property UsuarioSinDominio As String Implements IConfiguracion.UsuarioSinDominio
        Get
            Dim usuarioFormatedo As String = Environment.UserName
            If usuarioFormatedo = usuarioFormatedo.ToLower OrElse usuarioFormatedo = usuarioFormatedo.ToUpper Then
                usuarioFormatedo = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(usuarioFormatedo)
            End If
            'If Environment.MachineName.StartsWith("VSTUDIO") Then
            '    Return "Sancho"
            'End If
            Return usuarioFormatedo
        End Get
    End Property

    Public Async Function GuardarParametro(empresa As String, clave As String, valor As String) As Task Implements IConfiguracion.GuardarParametro
        Using client As New HttpClient
            client.BaseAddress = New Uri(servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = String.Empty
            Dim urlConsulta As String = "ParametrosUsuario"
            Dim parametro As New ParametroUsuario With {
                .Empresa = empresa,
                .Usuario = UsuarioSinDominio,
                .Clave = clave,
                .Valor = valor,
                .Usuario2 = usuario,
                .Fecha_Modificación = Date.Now
            }

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(parametro), Encoding.UTF8, "application/json")

            response = Await client.PutAsync(urlConsulta, content)

            If response.IsSuccessStatusCode Then
                respuesta = response.Content.ReadAsStringAsync().Result
            Else
                Dim respuestaError = response.Content.ReadAsStringAsync().Result
                Dim detallesError As JObject
                Dim contenido As String
                Try
                    detallesError = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    contenido = detallesError("ExceptionMessage")
                Catch ex As Exception
                    detallesError = New JObject()
                    contenido = respuestaError
                End Try

                While Not IsNothing(detallesError("InnerException"))
                    detallesError = detallesError("InnerException")
                    Dim contenido2 As String = detallesError("ExceptionMessage")
                    contenido = contenido + vbCr + contenido2
                End While
                Throw New Exception(contenido)
            End If

        End Using
    End Function
    Public Sub GuardarParametroSync(empresa As String, clave As String, valor As String) Implements IConfiguracion.GuardarParametroSync
        Using client As New HttpClient
            client.BaseAddress = New Uri(servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = String.Empty
            Dim urlConsulta As String = "ParametrosUsuario"
            Dim parametro As New ParametroUsuario With {
                .Empresa = empresa,
                .Usuario = UsuarioSinDominio,
                .Clave = clave,
                .Valor = valor,
                .Usuario2 = usuario,
                .Fecha_Modificación = Date.Now
            }

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(parametro), Encoding.UTF8, "application/json")

            response = client.PutAsync(urlConsulta, content).Result

            If response.IsSuccessStatusCode Then
                respuesta = response.Content.ReadAsStringAsync().Result
            Else
                Dim respuestaError = response.Content.ReadAsStringAsync().Result
                Dim detallesError As JObject
                Dim contenido As String
                Try
                    detallesError = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    contenido = detallesError("ExceptionMessage")
                Catch ex As Exception
                    detallesError = New JObject()
                    contenido = respuestaError
                End Try

                While Not IsNothing(detallesError("InnerException"))
                    detallesError = detallesError("InnerException")
                    Dim contenido2 As String = detallesError("ExceptionMessage")
                    contenido = contenido + vbCr + contenido2
                End While
                Throw New Exception(contenido)
            End If

        End Using
    End Sub

    Public Async Function leerParametro(empresa As String, clave As String) As Task(Of String) Implements IConfiguracion.leerParametro

        Using client As New HttpClient
            client.BaseAddress = New Uri(servidorAPI)
            Dim response As HttpResponseMessage

            Try
                response = Await client.GetAsync("ParametrosUsuario?empresa=" + empresa + "&usuario=" + UsuarioSinDominio + "&clave=" + clave)
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
            client.BaseAddress = New Uri(servidorAPI)
            Dim response As HttpResponseMessage

            Try
                response = client.GetAsync("ParametrosUsuario?empresa=" + empresa + "&usuario=" + UsuarioSinDominio + "&clave=" + clave).Result

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

Public Class ParametroUsuario
    Public Property Empresa As String
    Public Property Usuario As String
    Public Property Clave As String
    Public Property Valor As String
    Public Property Usuario2 As String
    Public Property Fecha_Modificación As Date
End Class