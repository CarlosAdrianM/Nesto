Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Newtonsoft.Json

''' <summary>
''' Servicio para facturación masiva de pedidos por rutas
''' </summary>
Public Class ServicioFacturacionRutas
    Implements IServicioFacturacionRutas

    Private ReadOnly configuracion As IConfiguracion

    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub

    ''' <summary>
    ''' Factura pedidos por rutas (propia o agencias)
    ''' </summary>
    Public Async Function FacturarRutas(request As FacturarRutasRequestDTO) As Task(Of FacturarRutasResponseDTO) Implements IServicioFacturacionRutas.FacturarRutas
        Using client As New HttpClient
            Try
                client.BaseAddress = New Uri(configuracion.servidorAPI)

                ' Serializar request a JSON
                Dim content As HttpContent = New StringContent(
                    JsonConvert.SerializeObject(request),
                    Encoding.UTF8,
                    "application/json")

                ' Llamar al endpoint
                Dim response As HttpResponseMessage = Await client.PostAsync("api/FacturacionRutas/Facturar", content)

                ' Verificar respuesta exitosa
                Dim unused = response.EnsureSuccessStatusCode()

                ' Deserializar respuesta
                Dim responseBody As String = Await response.Content.ReadAsStringAsync()
                Dim resultado As FacturarRutasResponseDTO = JsonConvert.DeserializeObject(Of FacturarRutasResponseDTO)(responseBody)

                Return resultado

            Catch ex As Exception
                Throw New Exception($"Error al facturar rutas: {ex.Message}", ex)
            End Try
        End Using
    End Function
End Class
