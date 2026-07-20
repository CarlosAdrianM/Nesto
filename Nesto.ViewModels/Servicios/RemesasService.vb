Imports System.Net.Http
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models
Imports Nesto.Infrastructure.Shared
Imports Newtonsoft.Json

' Nesto#340 Fase 1C.14: espejo de ProductosAlquilerService para el módulo de Remesas.
Public Class RemesasService
    Implements IRemesasService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion
    Private ReadOnly _clienteApiFactory As IClienteApiFactory

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
    End Sub

    Public Async Function LeerEmpresas() As Task(Of List(Of EmpresaModel)) Implements IRemesasService.LeerEmpresas
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response = Await client.GetAsync("Empresas")
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener la lista de empresas: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of EmpresaModel))(body)
        End Using
    End Function

    ' Nesto#340 Fase 1C.14 slice 2: sustituye la lectura EF de DbContext.Remesas.
    Public Async Function LeerRemesas(empresa As String, top As Integer?) As Task(Of List(Of RemesaModel)) Implements IRemesasService.LeerRemesas
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim url As String = $"Remesas?empresa={Uri.EscapeDataString(empresa)}"
            If top.HasValue Then
                url += $"&top={top.Value}"
            End If

            Dim response = Await client.GetAsync(url)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener la lista de remesas: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of RemesaModel))(body)
        End Using
    End Function

    ' Nesto#340 Fase 1C.14 slice 3: sustituye la lectura EF de DbContext.ExtractoCliente.
    Public Async Function LeerMovimientos(empresa As String, remesa As Integer) As Task(Of List(Of MovimientoRemesaModel)) Implements IRemesasService.LeerMovimientos
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim url As String = $"Remesas/Movimientos?empresa={Uri.EscapeDataString(empresa)}&remesa={remesa}"
            Dim response = Await client.GetAsync(url)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener los movimientos de la remesa: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of MovimientoRemesaModel))(body)
        End Using
    End Function
End Class
