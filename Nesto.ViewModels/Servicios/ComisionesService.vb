Imports System.Globalization
Imports System.Net.Http
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Models.Comisiones
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Newtonsoft.Json

Public Class ComisionesService
    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
    End Sub

    Public Async Function LeerVendedores() As Task(Of List(Of VendedorDTO))
        Dim vendedor As String = Await configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.Vendedor)
        Dim urlConsulta As String = $"Vendedores?empresa={Constantes.Empresas.EMPRESA_DEFECTO}"
        If Not configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION) Then
            urlConsulta += $"&vendedor={vendedor}"
        End If
        Return Await GetAsync(Of List(Of VendedorDTO))(urlConsulta, "la lista de vendedores")
    End Function

    Public Async Function LeerComisionesAntiguas(fechaDesde As Date, fechaHasta As Date, vendedor As String) As Task(Of ComisionesAntiguasModel)
        Dim url As String = $"Comisiones/Antiguas?fechaDesde={FormatoFecha(fechaDesde)}&fechaHasta={FormatoFecha(fechaHasta)}&vendedor={Uri.EscapeDataString(vendedor)}"
        Return Await GetAsync(Of ComisionesAntiguasModel)(url, "las comisiones antiguas", devolverNullSiNotFound:=True)
    End Function

    Public Async Function LeerPedidosVendedor(vendedor As String) As Task(Of List(Of PedidoVendedorComisionModel))
        Dim url As String = $"Comisiones/PedidosVendedor?vendedor={Uri.EscapeDataString(vendedor)}"
        Return Await GetAsync(Of List(Of PedidoVendedorComisionModel))(url, "los pedidos del vendedor")
    End Function

    Public Async Function LeerVentasVendedor(fechaDesde As Date, fechaHasta As Date, vendedor As String) As Task(Of List(Of VentaVendedorComisionModel))
        Dim url As String = $"Comisiones/VentasVendedor?fechaDesde={FormatoFecha(fechaDesde)}&fechaHasta={FormatoFecha(fechaHasta)}&vendedor={Uri.EscapeDataString(vendedor)}"
        Return Await GetAsync(Of List(Of VentaVendedorComisionModel))(url, "las ventas del vendedor")
    End Function

    Private Shared Function FormatoFecha(fecha As Date) As String
        Return fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
    End Function

    Private Async Function GetAsync(Of T)(urlRelativa As String, descripcion As String, Optional devolverNullSiNotFound As Boolean = False) As Task(Of T)
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response = Await client.GetAsync(urlRelativa)
            If devolverNullSiNotFound AndAlso response.StatusCode = Net.HttpStatusCode.NotFound Then
                Return Nothing
            End If
            If Not response.IsSuccessStatusCode Then
                Throw New Exception($"Error al obtener {descripcion}: {response.StatusCode}")
            End If

            Dim body As String = Await response.Content.ReadAsStringAsync()
            Return JsonConvert.DeserializeObject(Of T)(body)
        End Using
    End Function
End Class
