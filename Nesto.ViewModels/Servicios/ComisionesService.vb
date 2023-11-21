Imports System.Net.Http
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Newtonsoft.Json

Public Class ComisionesService
    Private ReadOnly configuracion As IConfiguracion
    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub

    Public Async Function LeerVendedores() As Task(Of List(Of VendedorDTO))
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = String.Empty
            Dim vendedor As String = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.Vendedor)

            Try
                Dim urlConsulta As String = $"Vendedores?empresa={Constantes.Empresas.EMPRESA_DEFECTO}"
                If Not configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) Then
                    urlConsulta += $"&vendedor={vendedor}"
                End If

                response = Await client.GetAsync(urlConsulta)
                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = String.Empty
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido recuperar la lista de vendedores")
            Finally

            End Try

            Dim listaVendedores As List(Of VendedorDTO) = JsonConvert.DeserializeObject(Of List(Of VendedorDTO))(respuesta)

            Return listaVendedores

        End Using
    End Function
End Class
