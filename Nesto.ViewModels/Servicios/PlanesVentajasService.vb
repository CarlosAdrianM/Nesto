Imports System.Collections.Generic
Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models.PlanesVentajas
Imports Nesto.Infrastructure.Shared
Imports Newtonsoft.Json

Public Class PlanesVentajasService
    Implements IPlanesVentajasService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
    End Sub

    Public Async Function LeerEstados() As Task(Of List(Of EstadoPlanVentajasModel)) Implements IPlanesVentajasService.LeerEstados
        Return Await GetAsync(Of List(Of EstadoPlanVentajasModel))("PlanesVentajas/Estados", "los estados de los planes de ventajas")
    End Function

    Public Async Function LeerEmpresas() As Task(Of List(Of EmpresaResumenModel)) Implements IPlanesVentajasService.LeerEmpresas
        Return Await GetAsync(Of List(Of EmpresaResumenModel))("PlanesVentajas/Empresas", "las empresas")
    End Function

    Public Async Function ListarPlanes(vendedor As String, filtroCliente As String, incluirCancelados As Boolean) As Task(Of List(Of PlanVentajasModel)) Implements IPlanesVentajasService.ListarPlanes
        Dim sb As New StringBuilder("PlanesVentajas?")
        sb.Append($"incluirCancelados={incluirCancelados.ToString().ToLower()}")
        If Not String.IsNullOrWhiteSpace(vendedor) Then
            sb.Append($"&vendedor={Uri.EscapeDataString(vendedor)}")
        End If
        If Not String.IsNullOrWhiteSpace(filtroCliente) Then
            sb.Append($"&filtroCliente={Uri.EscapeDataString(filtroCliente)}")
        End If
        Return Await GetAsync(Of List(Of PlanVentajasModel))(sb.ToString(), "los planes de ventajas")
    End Function

    Public Async Function ObtenerPlan(numero As Integer) As Task(Of PlanVentajasModel) Implements IPlanesVentajasService.ObtenerPlan
        Return Await GetAsync(Of PlanVentajasModel)($"PlanesVentajas/{numero}", $"el plan {numero}", devolverNullSiNotFound:=True)
    End Function

    Public Async Function ObtenerClientes(numero As Integer, empresa As String) As Task(Of List(Of ClientePlanVentajasModel)) Implements IPlanesVentajasService.ObtenerClientes
        Dim url = $"PlanesVentajas/{numero}/Clientes?empresa={Uri.EscapeDataString(If(empresa, String.Empty))}"
        Return Await GetAsync(Of List(Of ClientePlanVentajasModel))(url, $"los clientes del plan {numero}")
    End Function

    Public Async Function ObtenerLineasVenta(numero As Integer, empresa As String) As Task(Of List(Of LineaVentaPlanModel)) Implements IPlanesVentajasService.ObtenerLineasVenta
        Dim url = $"PlanesVentajas/{numero}/LineasVenta?empresa={Uri.EscapeDataString(If(empresa, String.Empty))}"
        Return Await GetAsync(Of List(Of LineaVentaPlanModel))(url, $"las líneas de venta del plan {numero}")
    End Function

    Public Async Function CrearPlan(plan As PlanVentajasModel) As Task(Of PlanVentajasModel) Implements IPlanesVentajasService.CrearPlan
        Return Await SendAsync(Of PlanVentajasModel)(HttpMethod.Post, "PlanesVentajas", plan, "crear el plan")
    End Function

    Public Async Function ActualizarPlan(plan As PlanVentajasModel) As Task(Of PlanVentajasModel) Implements IPlanesVentajasService.ActualizarPlan
        Return Await SendAsync(Of PlanVentajasModel)(HttpMethod.Put, $"PlanesVentajas/{plan.Numero}", plan, $"actualizar el plan {plan.Numero}")
    End Function

    Private Async Function GetAsync(Of T)(urlRelativa As String, descripcion As String, Optional devolverNullSiNotFound As Boolean = False) As Task(Of T)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response = Await client.GetAsync(urlRelativa)
            If response.StatusCode = Net.HttpStatusCode.NotFound AndAlso devolverNullSiNotFound Then
                Return Nothing
            End If
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener {descripcion}: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of T)(body)
        End Using
    End Function

    Private Async Function SendAsync(Of T)(metodo As HttpMethod, urlRelativa As String, cuerpo As Object, descripcion As String) As Task(Of T)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim json As String = JsonConvert.SerializeObject(cuerpo)
            Dim request As New HttpRequestMessage(metodo, urlRelativa) With {
                .Content = New StringContent(json, Encoding.UTF8, "application/json")
            }
            Dim response = Await client.SendAsync(request)
            If Not response.IsSuccessStatusCode Then
                Dim detalle As String = Await response.Content.ReadAsStringAsync()
                Throw New Exception($"Error al {descripcion}: {response.StatusCode} - {detalle}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of T)(body)
        End Using
    End Function
End Class
