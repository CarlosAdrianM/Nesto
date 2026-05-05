Imports System.Net.Http
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models.PlanesVentajas
Imports Nesto.Infrastructure.Shared
Imports Newtonsoft.Json

Public Class PlanesVentajasService
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
    End Sub

    Public Async Function LeerEstados() As Task(Of List(Of EstadoPlanVentajasModel))
        Return Await GetAsync(Of List(Of EstadoPlanVentajasModel))("PlanesVentajas/Estados", "los estados de los planes de ventajas")
    End Function

    Private Async Function GetAsync(Of T)(urlRelativa As String, descripcion As String) As Task(Of T)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response = Await client.GetAsync(urlRelativa)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener {descripcion}: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of T)(body)
        End Using
    End Function
End Class
