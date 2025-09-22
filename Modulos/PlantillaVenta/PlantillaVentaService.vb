Imports System.Collections.ObjectModel
Imports System.ComponentModel.DataAnnotations
Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.[Shared]
Imports Nesto.Models
Imports Nesto.Modulos.Cliente
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class PlantillaVentaService
    Implements IPlantillaVentaService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
    End Sub

    Public Async Sub EnviarCobroTarjeta(correo As String, movil As String, totalPedido As Decimal, pedido As String, cliente As String) Implements IPlantillaVentaService.EnviarCobroTarjeta
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = String.Empty

            Dim reclamacion As New ReclamacionDeuda With {
                .Cliente = cliente,
                .Asunto = String.Format("Pago pedido {0} de Nueva Visión", pedido),
                .Correo = correo,
                .Importe = totalPedido,
                .Movil = movil,
                .TextoSMS = "Este es un mensaje de @COMERCIO@. Puede pagar el pedido " + pedido + " de @IMPORTE@ @MONEDA@ aquí: @URL@"
            }

            Try
                Dim urlConsulta As String = "ReclamacionDeuda"
                Dim reclamacionJson As String = JsonConvert.SerializeObject(reclamacion)
                Dim content As New StringContent(reclamacionJson, Encoding.UTF8, "application/json")
                response = Await client.PostAsync(urlConsulta, content)

                respuesta = If(response.IsSuccessStatusCode, Await response.Content.ReadAsStringAsync(), String.Empty)

            Catch ex As Exception
                Throw New Exception("No se ha podido procesar la reclamación de deuda")
            Finally

            End Try
        End Using

    End Sub

    Public Async Function CargarClientesVendedor(filtroCliente As String, vendedor As String, todosLosVendedores As Boolean) As Task(Of ICollection(Of ClienteJson)) Implements IPlantillaVentaService.CargarClientesVendedor
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage


            Try
                response = If(todosLosVendedores,
                    Await client.GetAsync("Clientes?empresa=1&filtro=" + filtroCliente),
                    Await client.GetAsync("Clientes?empresa=1&vendedor=" + vendedor + "&filtro=" + filtroCliente))


                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Return JsonConvert.DeserializeObject(Of ObservableCollection(Of ClienteJson))(cadenaJson)
                Else
                    Throw New Exception("Se ha producido un error al cargar los clientes")
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            Finally

            End Try
        End Using
    End Function

    Public Async Function CargarCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteCrear) Implements IPlantillaVentaService.CargarCliente
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage


            Try

                response = Await client.GetAsync(String.Format("Clientes/GetClienteCrear?empresa={0}&cliente={1}&contacto={2}", empresa, cliente, contacto))

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Return JsonConvert.DeserializeObject(Of ClienteCrear)(cadenaJson)
                Else
                    Throw New Exception("Se ha producido un error al cargar los clientes")
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            Finally

            End Try
        End Using
    End Function

    Public Async Function CargarProductosPlantilla(clienteSeleccionado As ClienteJson) As Task(Of ObservableCollection(Of LineaPlantillaVenta)) Implements IPlantillaVentaService.CargarProductosPlantilla
        If IsNothing(clienteSeleccionado) Then
            Return Nothing
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            client.Timeout = client.Timeout.Add(New TimeSpan(0, 5, 0)) 'cinco minutos más
            Dim response As HttpResponseMessage

            response = Await client.GetAsync("PlantillaVentas?empresa=" + clienteSeleccionado.empresa + "&cliente=" + clienteSeleccionado.cliente)

            If response.IsSuccessStatusCode Then
                Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                Return JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaVenta))(cadenaJson)
            Else
                Dim respuestaError = response.Content.ReadAsStringAsync().Result
                Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                Dim contenido As String = detallesError("ExceptionMessage")
                If String.IsNullOrEmpty(contenido) Then
                    contenido = detallesError("exceptionMessage")
                End If
                While Not IsNothing(detallesError("InnerException"))
                    detallesError = detallesError("InnerException")
                    Dim contenido2 As String = detallesError("ExceptionMessage")
                    If String.IsNullOrEmpty(contenido2) Then
                        contenido2 = detallesError("exceptionMessage")
                    End If
                    contenido = contenido + vbCr + contenido2
                End While
                Throw New Exception("Se ha producido un error al cargar la plantilla" + vbCr + contenido)
            End If
        End Using
    End Function

    Public Async Function PonerStocks(lineas As ObservableCollection(Of LineaPlantillaVenta), almacen As String) As Task(Of ObservableCollection(Of LineaPlantillaVenta)) Implements IPlantillaVentaService.PonerStocks
        Dim param As New PonerStockParam With {
            .Lineas = lineas.ToList(),
            .Almacen = almacen
        }

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = String.Empty

            Try
                Dim urlConsulta As String = "PlantillaVentas/PonerStock"
                Dim paramJson As String = JsonConvert.SerializeObject(param)
                Dim content As New StringContent(paramJson, Encoding.UTF8, "application/json")
                response = Await client.PostAsync(urlConsulta, content)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                    Return JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaVenta))(respuesta)
                    'If reclamacion.TramitadoOK Then
                    '   EnlaceReclamarDeuda = reclamacion.Enlace
                    'End If
                Else
                    Return lineas
                End If
            Catch ex As Exception
                Throw New Exception("No se han podido poner los stocks de los productos")
            End Try
        End Using
    End Function

    Public Async Function CargarListaPendientes(empresa As String, cliente As String) As Task(Of List(Of Integer)) Implements IPlantillaVentaService.CargarListaPendientes
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Try
                Dim urlConsulta As String = "PlantillaVentas/PedidosPendientes?empresa=" + empresa
                urlConsulta += "&clientePendientes=" + cliente
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Dim pedidosPendientes As List(Of Integer) = JsonConvert.DeserializeObject(Of List(Of Integer))(cadenaJson)
                    pedidosPendientes.Add(0)
                    Return pedidosPendientes
                Else
                    Throw New Exception("Se ha producido un error al comprobar los pedidos pendientes del cliente")
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            End Try
        End Using
    End Function

    Public Async Function CrearPedido(pedido As PedidoVentaDTO) As Task(Of String) Implements IPlantillaVentaService.CrearPedido
        ' TO-DO: llamar al método CrearPedido del servicio de pedidos y eliminar este código duplicado
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If
            Dim response As HttpResponseMessage
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")

            Try
                response = Await client.PostAsync("PedidosVenta", content)

                If response.IsSuccessStatusCode Then
                    'Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    'listaProductosOriginal = JsonConvert.DeserializeObject(Of ObservableCollection(Of LineaPlantillaJson))(cadenaJson)
                    Dim pathNumeroPedido = response.Headers.Location.LocalPath
                    Return pathNumeroPedido.Substring(pathNumeroPedido.LastIndexOf("/") + 1)
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    If String.IsNullOrEmpty(contenido) Then
                        contenido = detallesError("exceptionMessage")
                    End If
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
                        If String.IsNullOrEmpty(contenido2) Then
                            contenido2 = detallesError("exceptionMessage")
                        End If
                        contenido = contenido + vbCr + contenido2
                    End While

                    ' Aquí comprobamos si es ValidationException
                    Dim tipoEx As String = CStr(detallesError("ExceptionType"))
                    If String.IsNullOrEmpty(tipoEx) Then
                        tipoEx = CStr(detallesError("exceptionType"))
                    End If

                    If Not String.IsNullOrEmpty(tipoEx) AndAlso tipoEx.Contains("ValidationException") Then
                        Throw New System.ComponentModel.DataAnnotations.ValidationException(contenido)
                    Else
                        Throw New Exception(contenido)
                    End If
                End If
            Catch ex As ValidationException
                Throw
            Catch ex As Exception
                Throw New Exception(ex.Message)
            End Try
        End Using
    End Function

    Public Async Function UnirPedidos(empresa As String, numeroPedidoOriginal As Integer, PedidoAmpliacion As PedidoVentaDTO) As Task(Of PedidoVentaDTO) Implements IPlantillaVentaService.UnirPedidos
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim parametro As New ParametroStringIntPedido With {
                .Empresa = empresa,
                .NumeroPedidoOriginal = numeroPedidoOriginal,
                .PedidoAmpliacion = PedidoAmpliacion
            }

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(parametro), Encoding.UTF8, "application/json")

            Try
                response = Await client.PostAsync("PedidosVenta/UnirPedidos", content)

                If response.IsSuccessStatusCode Then
                    Dim respuestaString As String = Await response.Content.ReadAsStringAsync()
                    Dim pedidoRespuesta As PedidoVentaDTO = JsonConvert.DeserializeObject(Of PedidoVentaDTO)(respuestaString)
                    If Not IsNothing(pedidoRespuesta) Then
                        Return pedidoRespuesta
                    Else
                        Throw New Exception("Pedido unido no generado")
                    End If
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("exceptionMessage")
                    If String.IsNullOrEmpty(contenido) Then
                        contenido = detallesError("ExceptionMessage")
                    End If
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("exceptionMessage")
                        If String.IsNullOrEmpty(contenido2) Then
                            contenido2 = detallesError("ExceptionMessage")
                        End If
                        contenido = contenido + vbCr + contenido2
                    End While

                    Throw New Exception(contenido)
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            Finally

            End Try

        End Using
    End Function

    Public Async Function CalcularFechaEntrega(fecha As Date, ruta As String, almacen As String) As Task(Of Date) Implements IPlantillaVentaService.CalcularFechaEntrega
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Try
                Dim urlConsulta As String = $"PedidosVenta/FechaAjustada?fecha={fecha:yyyy-MM-ddTHH:mm:ss.fffZ}&ruta={ruta}&almacen={almacen}"
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Dim fechaAjustada As Date = JsonConvert.DeserializeObject(Of Date)(cadenaJson)
                    Return fechaAjustada
                Else
                    Throw New Exception("Se ha producido un error al calcular la fecha de entrega")
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            End Try
        End Using
    End Function

    Public Async Function CargarVendedoresEquipo(jefeEquipo As String) As Task(Of List(Of VendedorDTO)) Implements IPlantillaVentaService.CargarVendedoresEquipo
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Try
                Dim urlConsulta As String = $"Vendedores?empresa={Constantes.Empresas.EMPRESA_DEFECTO}&vendedor={jefeEquipo}"
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Dim vendedoresEquipo = JsonConvert.DeserializeObject(Of List(Of VendedorDTO))(cadenaJson)
                    Return vendedoresEquipo
                Else
                    Throw New Exception("Se ha producido un error al cargar los vendedores del equipo")
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            End Try
        End Using
    End Function

    Private Function CargarProductosBonificables(cliente As String, lineas As List(Of LineaPlantillaVenta)) As List(Of LineaPlantillaVenta) Implements IPlantillaVentaService.CargarProductosBonificables
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Try
                Dim parametro = (cliente, lineas)
                Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(parametro), Encoding.UTF8, "application/json")
                Dim urlConsulta As String = "PlantillaVentas/ProductosBonificables"
                response = client.PostAsync(urlConsulta, content).Result

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = response.Content.ReadAsStringAsync().Result
                    Dim productos As List(Of LineaPlantillaVenta) = JsonConvert.DeserializeObject(Of List(Of LineaPlantillaVenta))(cadenaJson)
                    Return productos
                Else
                    Throw New Exception("Se ha producido un error al cargar los productos bonificables")
                End If
            Catch ex As Exception
                Throw New Exception("Se ha producido un error al cargar los productos bonificables", ex)
            End Try
        End Using
    End Function
End Class
