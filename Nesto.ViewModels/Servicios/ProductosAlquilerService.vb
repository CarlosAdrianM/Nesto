Imports System.Net.Http
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models.Alquileres
Imports Nesto.Infrastructure.Shared
Imports Newtonsoft.Json

Public Class ProductosAlquilerService
    Implements IProductosAlquilerService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion
    Private ReadOnly _clienteApiFactory As IClienteApiFactory

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
        _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
    End Sub

    Public Async Function LeerProductosAlquiler() As Task(Of List(Of ProductoAlquilerModel)) Implements IProductosAlquilerService.LeerProductosAlquiler
        Using client As HttpClient = _clienteApiFactory.Crear()
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
        Using client As HttpClient = _clienteApiFactory.Crear()
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

    ' Nesto#340 Fase 1C.2: compras (líneas del pedido de compra) de un alquiler, por producto + número de serie.
    Public Async Function LeerComprasAlquiler(producto As String, numSerie As String) As Task(Of List(Of CompraAlquilerModel)) Implements IProductosAlquilerService.LeerComprasAlquiler
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim url As String = $"Alquileres/Compras?producto={Uri.EscapeDataString(producto)}&numSerie={Uri.EscapeDataString(numSerie)}"
            Dim response = Await client.GetAsync(url)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener las compras del alquiler: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of CompraAlquilerModel))(body)
        End Using
    End Function

    ' Nesto#340 Fase 1C.2: extracto del inmovilizado de un alquiler, por empresa + número de inmovilizado.
    Public Async Function LeerInmovilizadosAlquiler(empresa As String, numero As String) As Task(Of List(Of ExtractoInmovilizadoModel)) Implements IProductosAlquilerService.LeerInmovilizadosAlquiler
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim url As String = $"Alquileres/Inmovilizados?empresa={Uri.EscapeDataString(empresa)}&numero={Uri.EscapeDataString(numero)}"
            Dim response = Await client.GetAsync(url)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener el extracto del inmovilizado del alquiler: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of ExtractoInmovilizadoModel))(body)
        End Using
    End Function

    ' Nesto#340 Fase 1C.3: cabeceras del grid principal de un producto en alquiler.
    Public Async Function LeerCabecerasAlquiler(empresa As String, producto As String) As Task(Of List(Of AlquilerModel)) Implements IProductosAlquilerService.LeerCabecerasAlquiler
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim url As String = $"Alquileres/Cabeceras?empresa={Uri.EscapeDataString(empresa)}&producto={Uri.EscapeDataString(producto)}"
            Dim response = Await client.GetAsync(url)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener las cabeceras del alquiler: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of AlquilerModel))(body)
        End Using
    End Function

    ' Nesto#340 Fase 1C.3: guardado del grid (la API reconcilia altas/ediciones/bajas).
    Public Async Function GuardarCabecerasAlquiler(empresa As String, producto As String, cabeceras As List(Of AlquilerModel)) As Task(Of List(Of AlquilerModel)) Implements IProductosAlquilerService.GuardarCabecerasAlquiler
        Using client As HttpClient = _clienteApiFactory.Crear()
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim payload = New With {
                .Empresa = empresa,
                .Producto = producto,
                .Cabeceras = cabeceras
            }
            Dim contenido As New StringContent(JsonConvert.SerializeObject(payload), Text.Encoding.UTF8, "application/json")
            Dim response = Await client.PostAsync("Alquileres/Cabeceras/Guardar", contenido)
            If Not response.IsSuccessStatusCode Then
                Dim error_ As String = Await response.Content.ReadAsStringAsync()
                Throw New Exception($"Error al guardar las cabeceras del alquiler: {response.StatusCode} {error_}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of AlquilerModel))(body)
        End Using
    End Function
End Class
