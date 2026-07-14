Imports System.IO
Imports System.Threading
Imports Azure.Core
Imports Azure.Identity

''' <summary>
''' Nesto#400: InteractiveBrowserCredential con la caché de tokens PERSISTIDA en disco (MSAL la
''' cifra con DPAPI por usuario de Windows) y el AuthenticationRecord rehidratado entre sesiones:
''' el navegador solo se abre la primera vez (o si el refresh token muere por revocación, cambio
''' de contraseña/MFA o ~90 días sin uso); después el login con MS Graph es silencioso.
'''
''' Best practice de Microsoft (sample TokenCache de Azure.Identity): la persistencia de la caché
''' SOLA no basta (azure-sdk-for-net#47457) — sin rehidratar el AuthenticationRecord se vuelve a
''' pedir el navegador en cada arranque. Hay que hacer las DOS cosas. El AuthenticationRecord no
''' contiene secretos (solo metadatos de la cuenta), por eso puede ir en un json plano.
'''
''' Se registra en el contenedor como InteractiveBrowserCredential (Application.xaml.vb), así que
''' los consumidores (Rapports, Remesas, PedidoCompra) no cambian.
''' </summary>
Public Class CredencialGraphPersistente
    Inherits InteractiveBrowserCredential

    Private Shared ReadOnly RutaRegistro As String =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Nesto", "msgraph-auth-record.json")

    Private _registroPersistido As Boolean

    Public Sub New(clientId As String)
        MyBase.New(CrearOpciones(clientId))
        _registroPersistido = File.Exists(RutaRegistro)
    End Sub

    Private Shared Function CrearOpciones(clientId As String) As InteractiveBrowserCredentialOptions
        Dim opciones As New InteractiveBrowserCredentialOptions() With {
            .ClientId = clientId,
            .TokenCachePersistenceOptions = New TokenCachePersistenceOptions() With {.Name = "Nesto"}
        }
        Try
            If File.Exists(RutaRegistro) Then
                Using stream As FileStream = File.OpenRead(RutaRegistro)
                    opciones.AuthenticationRecord = AuthenticationRecord.Deserialize(stream)
                End Using
            End If
        Catch
            ' Registro ilegible/corrupto: se ignora y el primer uso volverá a abrir el navegador
            ' (que lo regenera). Nunca debe impedir arrancar Nesto.
        End Try
        Return opciones
    End Function

    Public Overrides Function GetToken(requestContext As TokenRequestContext, Optional cancellationToken As CancellationToken = Nothing) As AccessToken
        If Not _registroPersistido Then
            ' Primera vez (sin registro persistido): autenticar explícitamente para CAPTURAR el
            ' AuthenticationRecord (el GetToken implícito no lo expone) y guardarlo para las
            ' próximas sesiones. Abre el navegador igual que hacía antes este primer uso.
            GuardarRegistro(Authenticate(requestContext, cancellationToken))
        End If
        Return MyBase.GetToken(requestContext, cancellationToken)
    End Function

    ' VB no admite Async en funciones que devuelven ValueTask: se delega en un helper Task.
    Public Overrides Function GetTokenAsync(requestContext As TokenRequestContext, Optional cancellationToken As CancellationToken = Nothing) As ValueTask(Of AccessToken)
        Return New ValueTask(Of AccessToken)(ObtenerTokenAsync(requestContext, cancellationToken))
    End Function

    Private Async Function ObtenerTokenAsync(requestContext As TokenRequestContext, cancellationToken As CancellationToken) As Task(Of AccessToken)
        If Not _registroPersistido Then
            GuardarRegistro(Await AuthenticateAsync(requestContext, cancellationToken))
        End If
        Return Await MyBase.GetTokenAsync(requestContext, cancellationToken)
    End Function

    Private Sub GuardarRegistro(registro As AuthenticationRecord)
        Try
            Dim carpeta As String = Path.GetDirectoryName(RutaRegistro)
            If Not Directory.Exists(carpeta) Then
                Dim unused = Directory.CreateDirectory(carpeta)
            End If
            Using stream As FileStream = File.Create(RutaRegistro)
                registro.Serialize(stream)
            End Using
            _registroPersistido = True
        Catch
            ' Si no se puede persistir, seguimos funcionando como hasta ahora (un navegador por
            ' sesión); se reintentará en el siguiente uso.
        End Try
    End Sub
End Class
