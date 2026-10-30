Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
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
        Private ReadOnly _clienteApiFactory As IClienteApiFactory

        Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
            Me.configuracion = configuracion
            Me.servicioAutenticacion = servicioAutenticacion
            _clienteApiFactory = New ClienteApiFactory(configuracion.servidorAPI, servicioAutenticacion)
        End Sub

        ''' <summary>
        ''' Factura pedidos por rutas (propia o agencias)
        ''' </summary>
        Public Function FacturarRutas(request As FacturarRutasRequestDTO) As Task(Of FacturarRutasResponseDTO) Implements IServicioFacturacionRutas.FacturarRutas
            Return PostarFacturacion("FacturacionRutas/Facturar", request, "Error al facturar rutas")
        End Function

        ''' <summary>
        ''' NestoAPI#592: factura un pedido por el mismo camino que la facturación de rutas.
        ''' </summary>
        Public Function FacturarPedido(empresa As String, pedido As Integer) As Task(Of FacturarRutasResponseDTO) Implements IServicioFacturacionRutas.FacturarPedido
            Return PostarFacturacion("FacturacionRutas/FacturarPedido", New With {.Empresa = empresa, .Pedido = pedido}, $"Error al facturar el pedido {pedido}")
        End Function

        ' Llamada común de Facturar y FacturarPedido: misma autorización, mismo timeout, misma respuesta.
        Private Async Function PostarFacturacion(ruta As String, request As Object, textoError As String) As Task(Of FacturarRutasResponseDTO)
            Using client As HttpClient = _clienteApiFactory.Crear()
                Try
                    ' Carlos 20/11/24: Aumentar timeout para facturación masiva de rutas (500 segundos)
                    client.Timeout = TimeSpan.FromSeconds(500)

                    ' Configurar autorización con token
                    If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                        Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                    End If

                    Dim content As HttpContent = New StringContent(
                        JsonConvert.SerializeObject(request),
                        Encoding.UTF8,
                        "application/json")

                    Dim baseUrl As String = configuracion.servidorAPI.TrimEnd("/"c)
                    Dim response As HttpResponseMessage = Await client.PostAsync($"{baseUrl}/{ruta}", content)

                    If response.StatusCode = Net.HttpStatusCode.Unauthorized Then
                        ' Token expirado o inválido, limpiar y notificar
                        servicioAutenticacion.LimpiarToken()
                        Throw New UnauthorizedAccessException("Token de autenticación inválido")
                    End If

                    response.EnsureSuccessStatusCode()

                    Dim responseBody As String = Await response.Content.ReadAsStringAsync()
                    Return JsonConvert.DeserializeObject(Of FacturarRutasResponseDTO)(responseBody)

                Catch uex As UnauthorizedAccessException
                    Throw ' Re-lanzar excepciones de autorización
                Catch ex As Exception
                    Throw New Exception($"{textoError}: {ex.Message}", ex)
                End Try
            End Using
        End Function

        ''' <summary>
        ''' Genera un preview (simulación) de facturación SIN crear nada en la BD
        ''' </summary>
        Public Async Function PreviewFacturarRutas(request As FacturarRutasRequestDTO) As Task(Of PreviewFacturacionRutasResponseDTO) Implements IServicioFacturacionRutas.PreviewFacturarRutas
            Using client As HttpClient = _clienteApiFactory.Crear()
                Try
                    ' Carlos 20/11/24: Aumentar timeout para preview de facturación masiva (500 segundos)
                    client.Timeout = TimeSpan.FromSeconds(500)

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
            Using client As HttpClient = _clienteApiFactory.Crear()
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

        ''' <summary>
        ''' NestoAPI#727: facturar el fin de mes por la API. Los 400 (interruptor apagado, fecha no válida…) y el 403 llegan
        ''' como excepción con el texto de la API tal cual, sin adornos, para enseñárselo al usuario.
        ''' </summary>
        Public Async Function FacturarFinDeMes(request As FacturarFinDeMesRequestDTO) As Task(Of ResultadoFacturacionFinDeMesDTO) Implements IServicioFacturacionRutas.FacturarFinDeMes
            Using client As HttpClient = _clienteApiFactory.Crear()
                ' Todos los clientes de fin de mes de una vez: puede tardar bastante más que una ruta.
                client.Timeout = TimeSpan.FromMinutes(20)

                If Not Await servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json")
                Dim baseUrl As String = configuracion.servidorAPI.TrimEnd("/"c)
                Dim response As HttpResponseMessage = Await client.PostAsync($"{baseUrl}/FacturacionRutas/FinDeMes", content)

                If response.StatusCode = Net.HttpStatusCode.Unauthorized Then
                    servicioAutenticacion.LimpiarToken()
                    Throw New UnauthorizedAccessException("Token de autenticación inválido")
                End If

                Dim responseBody As String = If(response.Content Is Nothing, Nothing, Await response.Content.ReadAsStringAsync())
                If Not response.IsSuccessStatusCode Then
                    Throw New InvalidOperationException(MensajeErrorFinDeMes(response.StatusCode, responseBody))
                End If

                Return JsonConvert.DeserializeObject(Of ResultadoFacturacionFinDeMesDTO)(responseBody)
            End Using
        End Function

        ''' <summary>NestoAPI#727: el texto que se enseña cuando la API no factura el fin de mes.</summary>
        Public Shared Function MensajeErrorFinDeMes(estado As Net.HttpStatusCode, cuerpo As String) As String
            If String.IsNullOrWhiteSpace(cuerpo) Then
                If estado = Net.HttpStatusCode.Forbidden Then
                    Return "No tiene permiso para facturar el fin de mes: solo pueden hacerlo Administración, Facturación, Dirección e Informática."
                End If
                Return $"La API no ha podido facturar el fin de mes ({CInt(estado)} {estado})."
            End If
            Return HttpErrorHelper.ParsearErrorHttp(cuerpo)
        End Function
    End Class
End Namespace
