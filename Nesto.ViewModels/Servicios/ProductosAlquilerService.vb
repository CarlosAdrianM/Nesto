Imports System.Net.Http
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models.Alquileres
Imports Newtonsoft.Json

Public Class ProductosAlquilerService
    Implements IProductosAlquilerService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
    End Sub

    Public Async Function LeerProductosAlquiler() As Task(Of List(Of ProductoAlquilerModel)) Implements IProductosAlquilerService.LeerProductosAlquiler
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response = Await client.GetAsync("Alquileres/Productos")
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener la lista de productos en alquiler: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of ProductoAlquilerModel))(body)
        End Using
    End Function

    ' Nesto#340 Fase 1C.2: movimientos (líneas del pedido de venta) de un alquiler.
    Public Async Function LeerMovimientosAlquiler(empresa As String, pedido As Integer) As Task(Of List(Of MovimientoAlquilerModel)) Implements IProductosAlquilerService.LeerMovimientosAlquiler
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response = Await client.GetAsync($"Alquileres/Movimientos?empresa={empresa}&pedido={pedido}")
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener los movimientos del alquiler: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of MovimientoAlquilerModel))(body)
        End Using
    End Function
End Class
