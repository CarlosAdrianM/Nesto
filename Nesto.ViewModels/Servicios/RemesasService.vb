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

    ' Nesto#340 Fase 1C.14 slice 4: sustituye el GROUP BY EF de impagados (TipoApunte 4).
    ' Deserializa sobre la clase impagado existente (asiento/fecha/cuenta): Newtonsoft es
    ' case-insensitive, así que el DTO {Asiento, Fecha, Cuenta} mapea sin atributos.
    Public Async Function LeerImpagados(empresa As String, top As Integer?) As Task(Of List(Of impagado)) Implements IRemesasService.LeerImpagados
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim url As String = $"Remesas/Impagados?empresa={Uri.EscapeDataString(empresa)}"
            If top.HasValue Then
                url += $"&top={top.Value}"
            End If

            Dim response = Await client.GetAsync(url)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener la lista de impagados: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of impagado))(body)
        End Using
    End Function

    ' Nesto#340 Fase 1C.14 slice 5: sustituye la lectura EF del detalle de un asiento de impagados.
    Public Async Function LeerMovimientosImpagado(empresa As String, asiento As Integer) As Task(Of List(Of MovimientoRemesaModel)) Implements IRemesasService.LeerMovimientosImpagado
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim url As String = $"Remesas/Impagados/Movimientos?empresa={Uri.EscapeDataString(empresa)}&asiento={asiento}"
            Dim response = Await client.GetAsync(url)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener los movimientos del impagado: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of MovimientoRemesaModel))(body)
        End Using
    End Function

    ' NestoAPI#332: candidatos a remesa (modo simulación del servidor).
    Public Async Function LeerEfectosCandidatos(empresa As String) As Task(Of List(Of EfectoCandidatoModel)) Implements IRemesasService.LeerEfectosCandidatos
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response = Await client.GetAsync($"Remesas/EfectosCandidatos?empresa={Uri.EscapeDataString(empresa)}")
            Dim body As String = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener los efectos candidatos: {ExtraerMensajeError(body)}")
            End If
            Return JsonConvert.DeserializeObject(Of List(Of EfectoCandidatoModel))(body)
        End Using
    End Function

    ' NestoAPI#332: crea la remesa. El servidor revalida (candidatos frescos, gating #172,
    ' puerta de neteo) y contabiliza; los BadRequest traen el motivo legible.
    Public Async Function CrearRemesa(empresa As String, banco As String, efectos As List(Of Integer)) As Task(Of CrearRemesaResponseModel) Implements IRemesasService.CrearRemesa
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim contenido As HttpContent = New StringContent(
                JsonConvert.SerializeObject(New With {.Empresa = empresa, .Banco = banco, .Efectos = efectos}),
                Text.Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("Remesas", contenido)
            Dim body As String = Await response.Content.ReadAsStringAsync()
            If Not response.IsSuccessStatusCode Then
                Throw New Exception(ExtraerMensajeError(body))
            End If
            Return JsonConvert.DeserializeObject(Of CrearRemesaResponseModel)(body)
        End Using
    End Function

    ' Los errores de Web API llegan como {"Message":"..."}: extraer el texto legible.
    Private Shared Function ExtraerMensajeError(body As String) As String
        Try
            Dim json = JsonConvert.DeserializeObject(Of Newtonsoft.Json.Linq.JObject)(body)
            Dim mensaje = json?("Message")?.ToString()
            If Not String.IsNullOrWhiteSpace(mensaje) Then
                Return mensaje
            End If
        Catch
        End Try
        Return body
    End Function
End Class
