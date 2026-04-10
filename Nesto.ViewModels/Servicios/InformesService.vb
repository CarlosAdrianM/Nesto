Imports System.Net.Http
Imports Nesto.Informes
Imports Nesto.Infrastructure.Contracts
Imports Newtonsoft.Json

Public Class InformesService
    Private ReadOnly _configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        _configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
    End Sub

    Public Async Function LeerResumenVentas(fechaDesde As Date, fechaHasta As Date, soloFacturas As Boolean) As Task(Of List(Of ResumenVentasModel))
        Using client As New HttpClient
            client.BaseAddress = New Uri(_configuracion.servidorAPI)

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim urlConsulta As String = $"Informes/ResumenVentas?fechaDesde={fechaDesde:yyyy-MM-dd}&fechaHasta={fechaHasta:yyyy-MM-dd}&soloFacturas={soloFacturas.ToString().ToLower()}"

            Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener el resumen de ventas: {response.StatusCode}")
            End If

            Dim respuesta As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of ResumenVentasModel))(respuesta)
        End Using
    End Function

    Public Async Function LeerControlPedidos() As Task(Of List(Of ControlPedidosModel))
        Using client As New HttpClient
            client.BaseAddress = New Uri(_configuracion.servidorAPI)

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim urlConsulta As String = "Informes/ControlPedidos"

            Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener el control de pedidos: {response.StatusCode}")
            End If

            Dim respuesta As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of ControlPedidosModel))(respuesta)
        End Using
    End Function

    Public Async Function LeerDetalleRapports(fechaDesde As Date, fechaHasta As Date, listaVendedores As String) As Task(Of List(Of DetalleRapportsModel))
        Using client As New HttpClient
            client.BaseAddress = New Uri(_configuracion.servidorAPI)

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim urlConsulta As String = $"Informes/DetalleRapports?fechaDesde={fechaDesde:yyyy-MM-dd}&fechaHasta={fechaHasta:yyyy-MM-dd}&listaVendedores={Uri.EscapeDataString(If(listaVendedores, String.Empty))}"

            Dim response As HttpResponseMessage = Await client.GetAsync(urlConsulta)
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener el detalle de rapports: {response.StatusCode}")
            End If

            Dim respuesta As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of List(Of DetalleRapportsModel))(respuesta)
        End Using
    End Function
End Class
