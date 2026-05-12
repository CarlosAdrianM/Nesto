Imports System.Collections.Generic
Imports Nesto.Infrastructure.Models.PlanesVentajas

Public Interface IPlanesVentajasService
    Function LeerEstados() As Task(Of List(Of EstadoPlanVentajasModel))
    Function LeerEmpresas() As Task(Of List(Of EmpresaResumenModel))
    Function ListarPlanes(vendedor As String, filtroCliente As String, incluirCancelados As Boolean) As Task(Of List(Of PlanVentajasModel))
    Function ObtenerPlan(numero As Integer) As Task(Of PlanVentajasModel)
    Function ObtenerClientes(numero As Integer, empresa As String) As Task(Of List(Of ClientePlanVentajasModel))
    Function ObtenerLineasVenta(numero As Integer, empresa As String) As Task(Of List(Of LineaVentaPlanModel))
    Function CrearPlan(plan As PlanVentajasModel) As Task(Of PlanVentajasModel)
    Function ActualizarPlan(plan As PlanVentajasModel) As Task(Of PlanVentajasModel)
End Interface
