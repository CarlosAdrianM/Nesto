Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Newtonsoft.Json

Namespace Services
    ''' <summary>
    ''' Servicio para facturación masiva de pedidos por rutas
    ''' </summary>
    Public Class ServicioFacturacionRutas
        Implements IServicioFacturacionRutas

        Private ReadOnly configuracion As IConfiguracion
        Private ReadOnly servicioAutenticacion As IServicioAutenticacion

        Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
            Me.configuracion = configuracion
            Me.servicioAutenticacion = servicioAutenticacion
        End Sub

        ''' <summary>
        ''' Factura pedidos por rutas (propia o agencias)
        ''' </summary>
        Public Async Function FacturarRutas(request As FacturarRutasRequestDTO) As Task(Of FacturarRutasResponseDTO) Implements IServicioFacturacionRutas.FacturarRutas
            Using client As New HttpClient
                Try
                    ' Configurar autorización con token
                    If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                        Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                    End If

                    ' Serializar request a JSON
                    Dim content As HttpContent = New StringContent(
                        JsonConvert.SerializeObject(request),
                        Encoding.UTF8,
                        "application/json")

                    ' Construir URL completa asegurando formato correcto
                    Dim baseUrl As String = configuracion.servidorAPI.TrimEnd("/"c)
                    Dim fullUrl As String = $"{baseUrl}/FacturacionRutas/Facturar"

                    ' Llamar al endpoint
                    Dim response As HttpResponseMessage = Await client.PostAsync(fullUrl, content)

                    ' Verificar respuesta exitosa
                    If response.StatusCode = Net.HttpStatusCode.Unauthorized Then
                        ' Token expirado o inválido, limpiar y notificar
                        servicioAutenticacion.LimpiarToken()
                        Throw New UnauthorizedAccessException("Token de autenticación inválido")
                    End If

                    response.EnsureSuccessStatusCode()

                    ' Deserializar respuesta
                    Dim responseBody As String = Await response.Content.ReadAsStringAsync()
                    Dim resultado As FacturarRutasResponseDTO = JsonConvert.DeserializeObject(Of FacturarRutasResponseDTO)(responseBody)

                    Return resultado

                Catch uex As UnauthorizedAccessException
                    Throw ' Re-lanzar excepciones de autorización
                Catch ex As Exception
                    Throw New Exception($"Error al facturar rutas: {ex.Message}", ex)
                End Try
            End Using
        End Function

        ''' <summary>
        ''' Genera un preview (simulación) de facturación SIN crear nada en la BD
        ''' </summary>
        Public Async Function PreviewFacturarRutas(request As FacturarRutasRequestDTO) As Task(Of PreviewFacturacionRutasResponseDTO) Implements IServicioFacturacionRutas.PreviewFacturarRutas
            Using client As New HttpClient
                Try
                    ' Configurar autorización con token
                    If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                        Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                    End If

                    ' Serializar request a JSON
                    Dim content As HttpContent = New StringContent(
                        JsonConvert.SerializeObject(request),
                        Encoding.UTF8,
                        "application/json")

                    ' Construir URL completa asegurando formato correcto
                    Dim baseUrl As String = configuracion.servidorAPI.TrimEnd("/"c)
                    Dim fullUrl As String = $"{baseUrl}/FacturacionRutas/Preview"

                    ' Llamar al endpoint de preview
                    Dim response As HttpResponseMessage = Await client.PostAsync(fullUrl, content)

                    ' Verificar respuesta exitosa
                    If response.StatusCode = Net.HttpStatusCode.Unauthorized Then
                        ' Token expirado o inválido, limpiar y notificar
                        servicioAutenticacion.LimpiarToken()
                        Throw New UnauthorizedAccessException("Token de autenticación inválido")
                    End If

                    response.EnsureSuccessStatusCode()

                    ' Deserializar respuesta
                    Dim responseBody As String = Await response.Content.ReadAsStringAsync()
                    Dim resultado As PreviewFacturacionRutasResponseDTO = JsonConvert.DeserializeObject(Of PreviewFacturacionRutasResponseDTO)(responseBody)

                    Return resultado

                Catch uex As UnauthorizedAccessException
                    Throw ' Re-lanzar excepciones de autorización
                Catch ex As Exception
                    Throw New Exception($"Error al generar preview de facturación: {ex.Message}", ex)
                End Try
            End Using
        End Function

        ''' <summary>
        ''' Obtiene la lista de tipos de ruta disponibles desde la API
        ''' </summary>
        Public Async Function ObtenerTiposRuta() As Task(Of List(Of TipoRutaInfoDTO)) Implements IServicioFacturacionRutas.ObtenerTiposRuta
            Using client As New HttpClient
                Try
                    ' Configurar autorización con token
                    If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                        Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                    End If

                    ' Construir URL completa
                    Dim baseUrl As String = configuracion.servidorAPI.TrimEnd("/"c)
                    Dim fullUrl As String = $"{baseUrl}/FacturacionRutas/TiposRuta"

                    ' Llamar al endpoint
                    Dim response As HttpResponseMessage = Await client.GetAsync(fullUrl)

                    ' Verificar respuesta exitosa
                    If response.StatusCode = Net.HttpStatusCode.Unauthorized Then
                        ' Token expirado o inválido, limpiar y notificar
                        servicioAutenticacion.LimpiarToken()
                        Throw New UnauthorizedAccessException("Token de autenticación inválido")
                    End If

                    response.EnsureSuccessStatusCode()

                    ' Deserializar respuesta
                    Dim responseBody As String = Await response.Content.ReadAsStringAsync()
                    Dim tipos As List(Of TipoRutaInfoDTO) = JsonConvert.DeserializeObject(Of List(Of TipoRutaInfoDTO))(responseBody)

                    Return tipos

                Catch uex As UnauthorizedAccessException
                    Throw ' Re-lanzar excepciones de autorización
                Catch ex As Exception
                    Throw New Exception($"Error al obtener tipos de ruta: {ex.Message}", ex)
                End Try
            End Using
        End Function
    End Class
End Namespace
