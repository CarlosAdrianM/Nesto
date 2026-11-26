Imports System.Collections
Imports System.DirectoryServices.AccountManagement
Imports System.Globalization
Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
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

    ''' <summary>
    ''' Nesto#639 (para poder cerrar PUT api/ParametrosUsuario): leer y guardar parámetros va con el token, por la fábrica común
    ''' (IClienteApiFactory: token, reintentos de los GET y avisos de bloqueo), como MainViewModel.leerParametro. No se inyecta por el
    ''' constructor porque la fábrica se construye con esta misma configuración (servidorAPI): se pide al contenedor en cada llamada.
    ''' Sin contenedor (diseñador, tests) o si no se puede resolver, un HttpClient sin token, como antes.
    ''' </summary>
    Friend Function CrearClienteApi() As HttpClient
        Try
            Dim fabrica As IClienteApiFactory = Contenedor.Actual?.Resolver(Of IClienteApiFactory)()
            If fabrica IsNot Nothing Then
                Return fabrica.Crear()
            End If
        Catch
            ' sin fábrica: como antes
        End Try
        Return New HttpClient With {.BaseAddress = New Uri(servidorAPI)}
    End Function

    ''' <summary>
    ''' Las versiones síncronas esperan en otro hilo: el token puede tener que pedirse (ServicioAutenticacion espera sin
    ''' ConfigureAwait(False)) y bloquear el hilo de la interfaz con .Result lo dejaría esperándose a sí mismo.
    ''' </summary>
    Private Shared Function EsperarFuera(Of T)(trabajo As Func(Of Task(Of T))) As T
        Return Task.Run(trabajo).GetAwaiter().GetResult()
    End Function

    Public Async Function GuardarParametro(empresa As String, clave As String, valor As String) As Task Implements IConfiguracion.GuardarParametro
        Using client As HttpClient = CrearClienteApi()
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
                ' Carlos 21/11/24: Usar HttpErrorHelper para parsear errores del API
                Dim contenido As String
                Try
                    detallesError = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    contenido = HttpErrorHelper.ParsearErrorHttp(detallesError)
                Catch ex As Exception
                    contenido = respuestaError
                End Try
                Throw New Exception(contenido)
            End If

        End Using
    End Function
    Public Sub GuardarParametroSync(empresa As String, clave As String, valor As String) Implements IConfiguracion.GuardarParametroSync
        Using client As HttpClient = CrearClienteApi()
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

            response = EsperarFuera(Function() client.PutAsync(urlConsulta, content))

            If response.IsSuccessStatusCode Then
                respuesta = response.Content.ReadAsStringAsync().Result
            Else
                Dim respuestaError = response.Content.ReadAsStringAsync().Result
                Dim detallesError As JObject
                ' Carlos 21/11/24: Usar HttpErrorHelper para parsear errores del API
                Dim contenido As String
                Try
                    detallesError = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    contenido = HttpErrorHelper.ParsearErrorHttp(detallesError)
                Catch ex As Exception
                    contenido = respuestaError
                End Try
                Throw New Exception(contenido)
            End If

        End Using
    End Sub

    Public Async Function leerParametro(empresa As String, clave As String) As Task(Of String) Implements IConfiguracion.leerParametro

        Using client As HttpClient = CrearClienteApi()
            Dim response As HttpResponseMessage

            Try
                response = Await client.GetAsync("ParametrosUsuario?empresa=" + empresa + "&usuario=" + UsuarioSinDominio + "&clave=" + clave)
                'response = Await client.GetAsync("ParametrosUsuario?empresa=" + empresa + "&usuario=Carolina&clave=" + clave)

                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    respuesta = JsonConvert.DeserializeObject(Of String)(respuesta)
                    ' Nesto#372: si el Valor del parámetro es NULL en BD, la API puede devolver
                    ' null y respuesta.Trim lanzaba NullReferenceException
                    Return If(respuesta, String.Empty).Trim
                Else
                    Throw New Exception("No se puede leer el parámetro")
                End If
            Catch ex As Exception
                Throw New Exception("No se puede leer el parámetro")
            End Try

        End Using

    End Function

    Public Function LeerParametroSync(empresa As String, clave As String) As String Implements IConfiguracion.LeerParametroSync

        Using client As HttpClient = CrearClienteApi()
            Dim response As HttpResponseMessage

            Try
                Dim url As String = "ParametrosUsuario?empresa=" + empresa + "&usuario=" + UsuarioSinDominio + "&clave=" + clave
                response = EsperarFuera(Function() client.GetAsync(url))

                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = response.Content.ReadAsStringAsync().Result
                    respuesta = JsonConvert.DeserializeObject(Of String)(respuesta)
                    ' Nesto#372: mismo guard de null que en leerParametro
                    Return If(respuesta, String.Empty).Trim
                Else
                    Throw New Exception("No se puede leer el parámetro")
                End If
            Catch ex As Exception
                Throw New Exception("No se puede leer el parámetro")
            End Try

        End Using

    End Function

    ''' <summary>
    ''' Nesto#450: respuestas ya calculadas de UsuarioEnGrupo. La pertenencia a un grupo del
    ''' dominio no cambia mientras la aplicación está abierta, y esta función se llama desde
    ''' CanExecute de varios comandos: WPF los reevalúa en CADA CommandManager.RequerySuggested,
    ''' o sea con cada tecla y cada clic. Sin caché eso son cientos de consultas LDAP por sesión.
    '''
    ''' Solo se cachean los aciertos: un fallo (dominio no disponible) NO se guarda, para que al
    ''' volver el controlador la respuesta se corrija sola sin reiniciar la aplicación.
    ''' </summary>
    Private Shared ReadOnly _gruposComprobados As New Concurrent.ConcurrentDictionary(Of String, Boolean)

    Public Function UsuarioEnGrupo(grupo As String) As Boolean Implements IConfiguracion.UsuarioEnGrupo
        Dim cacheado As Boolean
        If _gruposComprobados.TryGetValue(grupo, cacheado) Then
            Return cacheado
        End If

        Try
            Dim yourDomain As String = System.Environment.UserDomainName
            Using ctx As New PrincipalContext(ContextType.Domain, yourDomain)
                Using grp = GroupPrincipal.FindByIdentity(ctx, IdentityType.Name, grupo)
                    Dim isInRole As Boolean = Not IsNothing(grp) AndAlso grp.GetMembers(True).Any(Function(m) m.SamAccountName.ToLower = usuario.ToLower.Replace(yourDomain.ToLower + "\", String.Empty))
                    _gruposComprobados(grupo) = isInRole
                    Return isInRole
                End Using
            End Using
        Catch ex As Exception
            ' El controlador de dominio puede no estar disponible momentáneamente (LDAP caído/timeout).
            ' No debe tumbar la app (arranque del MenuBar, apertura de DetallePedidoVenta, CanExecute en cada requery...).
            ' NO se cachea: si se guardara el False, el usuario se quedaría sin permisos toda la
            ' sesión aunque el dominio volviera a los dos segundos.
            System.Diagnostics.Debug.WriteLine($"UsuarioEnGrupo('{grupo}') falló, se asume False: {ex.Message}")
            Return False
        End Try
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