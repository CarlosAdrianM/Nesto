Imports System.Collections.ObjectModel
Imports System.ComponentModel.DataAnnotations
Imports System.IO
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.Models.Rectificativas
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class PedidoVentaService
    Implements IPedidoVentaService

    Private ReadOnly configuracion As IConfiguracion
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion

    Public Sub New(configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)
        Me.configuracion = configuracion
        _servicioAutenticacion = servicioAutenticacion
    End Sub

    Private Async Function cargarListaPedidos(vendedor As String, verTodosLosVendedores As Boolean, mostrarPresupuestos As Boolean) As Task(Of ObservableCollection(Of ResumenPedido)) Implements IPedidoVentaService.cargarListaPedidos

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""
            Dim vendedorConsulta As String = vendedor

            If verTodosLosVendedores Then
                vendedorConsulta = ""
            End If

            Try
                Dim urlConsulta As String = "PedidosVenta"
                urlConsulta += "?vendedor=" + vendedorConsulta

                If mostrarPresupuestos Then
                    urlConsulta += "&estado=-3"
                End If

                response = Await client.GetAsync(urlConsulta)

                respuesta = If(response.IsSuccessStatusCode, Await response.Content.ReadAsStringAsync(), "")

            Catch ex As Exception
                Throw New Exception("No se ha podido recuperar la lista de pedidos")
            Finally

            End Try

            Dim listaPedidos As ObservableCollection(Of ResumenPedido) = JsonConvert.DeserializeObject(Of ObservableCollection(Of ResumenPedido))(respuesta)

            Return listaPedidos

        End Using
    End Function
    Public Async Function cargarPedido(empresa As String, numero As Integer) As Task(Of PedidoVentaDTO) Implements IPedidoVentaService.cargarPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)

            Try
                Dim urlConsulta As String = "PedidosVenta"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numero=" + numero.ToString

                Dim response = Await client.GetAsync(urlConsulta)

                If Not response.IsSuccessStatusCode Then
                    Throw New Exception($"No se ha podido recuperar el pedido {numero}. Código de estado: {response.StatusCode}")
                End If

                Dim respuesta = Await response.Content.ReadAsStringAsync()
                Dim pedido As PedidoVentaDTO = JsonConvert.DeserializeObject(Of PedidoVentaDTO)(respuesta)

                If IsNothing(pedido) Then
                    Throw New Exception($"Error al deserializar el pedido {numero}. La respuesta de la API no contiene datos válidos.")
                End If

                Return pedido

            Catch ex As Exception
                Throw New Exception($"Error al cargar el pedido {numero}: {ex.Message}", ex)
            End Try

        End Using
    End Function
    Public Async Function cargarProducto(empresa As String, id As String, cliente As String, contacto As String, cantidad As Short) As Task(Of Producto) Implements IPedidoVentaService.cargarProducto
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "Productos"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&id=" + id
                urlConsulta += "&cliente=" + cliente
                urlConsulta += "&contacto=" + contacto
                urlConsulta += "&cantidad=" + cantidad.ToString

                response = Await client.GetAsync(urlConsulta)

                respuesta = If(response.IsSuccessStatusCode, Await response.Content.ReadAsStringAsync(), "")

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar el producto " + id)
            Finally

            End Try

            Dim producto As Producto = JsonConvert.DeserializeObject(Of Producto)(respuesta)

            Return producto

        End Using
    End Function

    Public Async Function modificarPedido(pedido As PedidoVentaDTO) As Task Implements IPedidoVentaService.modificarPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim urlConsulta As String = "PedidosVenta"
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")

            Try
                Dim response As HttpResponseMessage = Await client.PutAsync(urlConsulta, content)

                If response.IsSuccessStatusCode Then
                    Dim respuesta As String = Await response.Content.ReadAsStringAsync()
                    ' Hacer algo con respuesta si es necesario
                Else
                    Dim respuestaError = Await response.Content.ReadAsStringAsync()
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)

                    ' Carlos 28/11/25: Detectar si es un error de validación de pedido (PEDIDO_VALIDACION_FALLO)
                    ' para lanzar ValidationException que el ViewModel pueda capturar (igual que en CrearPedido)
                    Dim errorCode As String = Nothing
                    If Not IsNothing(detallesError("error")) Then
                        Dim errorObj As JObject = detallesError("error")
                        errorCode = errorObj("code")?.ToString()
                    End If

                    ' Parsear el mensaje de error usando HttpErrorHelper
                    Dim contenido As String = HttpErrorHelper.ParsearErrorHttp(detallesError)

                    ' Si es error de validación de pedido, lanzar ValidationException
                    ' para que el ViewModel pueda preguntar "¿Crear sin pasar validación?"
                    If errorCode = "PEDIDO_VALIDACION_FALLO" Then
                        Throw New System.ComponentModel.DataAnnotations.ValidationException(contenido)
                    Else
                        Throw New Exception(contenido)
                    End If
                End If

            Catch ex As ValidationException
                Throw
            Catch ex As Exception
                Throw New Exception("Error al modificar el pedido: " + ex.Message)
            End Try
        End Using
    End Function

    Private Async Function ObtenerMensajeError(response As HttpResponseMessage) As Task(Of String)
        Dim respuestaError As String = Await response.Content.ReadAsStringAsync()
        Dim contenido As String

        ' Carlos 21/11/24: Usar HttpErrorHelper para parsear errores del API
        Try
            Dim detallesError As JObject = JsonConvert.DeserializeObject(Of JObject)(respuestaError)
            contenido = HttpErrorHelper.ParsearErrorHttp(detallesError)
        Catch ex As Exception
            contenido = respuestaError
        End Try

        Return contenido
    End Function

    Public Async Function sacarPickingPedido(empresa As String, numero As Integer) As Task Implements IPedidoVentaService.sacarPickingPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                ' Agregar autenticación
                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim urlConsulta As String = "Picking"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numeroPedido=" + numero.ToString

                response = Await client.GetAsync(urlConsulta)

                respuesta = Await response.Content.ReadAsStringAsync()
                If Not response.IsSuccessStatusCode Then
                    Throw New Exception(HttpErrorHelper.ParsearErrorHttp(respuesta))
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido sacar el picking del pedido " + numero.ToString + vbCr + vbCr + ex.Message)
            Finally

            End Try

        End Using
    End Function
    Public Async Function sacarPickingPedido(cliente As String) As Task Implements IPedidoVentaService.sacarPickingPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                ' Agregar autenticación
                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim urlConsulta As String = "Picking"
                urlConsulta += "?cliente=" + cliente

                response = Await client.GetAsync(urlConsulta)

                respuesta = Await response.Content.ReadAsStringAsync()
                If Not response.IsSuccessStatusCode Then
                    Throw New Exception(HttpErrorHelper.ParsearErrorHttp(respuesta))
                End If

            Catch ex As Exception
                Throw New Exception("No se han podido sacar los picking del cliente " + cliente, ex)
            Finally

            End Try

        End Using
    End Function
    Public Async Function sacarPickingPedido() As Task Implements IPedidoVentaService.sacarPickingPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                ' Agregar autenticación
                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim urlConsulta As String = "Picking"

                response = Await client.GetAsync(urlConsulta)

                respuesta = Await response.Content.ReadAsStringAsync()
                If Not response.IsSuccessStatusCode Then
                    Throw New Exception(HttpErrorHelper.ParsearErrorHttp(respuesta))
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido sacar el picking de las rutas" + vbCr + vbCr + ex.Message)
            Finally

            End Try

        End Using
    End Function

    Public Async Function CargarEnlacesSeguimiento(empresa As String, numero As Integer) As Task(Of List(Of EnvioAgenciaDTO)) Implements IPedidoVentaService.CargarEnlacesSeguimiento
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "EnviosAgencias"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&pedido=" + numero.ToString

                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                    Dim objetoRespuesta As JObject
                    objetoRespuesta = JsonConvert.DeserializeObject(respuesta)
                    If Not IsNothing(objetoRespuesta("ExceptionMessage")) Then
                        Dim textoError As String = objetoRespuesta("ExceptionMessage")
                        Throw New Exception(textoError)
                    Else
                        Throw New Exception(respuesta)
                    End If
                End If

            Catch ex As Exception
                Throw New Exception("No se han podido recuperar los seguimientos del pedido " + numero.ToString + vbCr + vbCr + ex.Message)
            Finally

            End Try

            Dim seguimientos As List(Of EnvioAgenciaDTO) = JsonConvert.DeserializeObject(Of List(Of EnvioAgenciaDTO))(respuesta)

            Return seguimientos

        End Using
    End Function

    Public Async Sub EnviarCobroTarjeta(correo As String, movil As String, totalPedido As Decimal, pedido As String, cliente As String) Implements IPedidoVentaService.EnviarCobroTarjeta
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

    Public Async Function CargarPedidosPendientes(empresa As String, cliente As String) As Task(Of ObservableCollection(Of Integer)) Implements IPedidoVentaService.CargarPedidosPendientes

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Try
                Dim urlConsulta As String = "PlantillaVentas/PedidosPendientes?empresa=" + empresa
                urlConsulta += "&clientePendientes=" + cliente
                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    Dim cadenaJson As String = Await response.Content.ReadAsStringAsync()
                    Dim pedidosPendientes As ObservableCollection(Of Integer) = JsonConvert.DeserializeObject(Of ObservableCollection(Of Integer))(cadenaJson)
                    Return New ObservableCollection(Of Integer)(pedidosPendientes.OrderByDescending(Function(p) p))
                Else
                    Throw New Exception("Se ha producido un error al comprobar los pedidos pendientes del cliente")
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            End Try
        End Using
    End Function

    Public Async Function UnirPedidos(empresa As String, numeroPedidoOriginal As Integer, numeroPedidoAmpliacion As Integer) As Task(Of PedidoVentaDTO) Implements IPedidoVentaService.UnirPedidos
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            Dim parametro As New ParametroStringIntInt With {
                .Empresa = empresa,
                .NumeroPedidoOriginal = numeroPedidoOriginal,
                .NumeroPedidoAmpliacion = numeroPedidoAmpliacion
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
                    ' Carlos 21/11/24: Usar HttpErrorHelper para parsear errores del API
                    Dim contenido As String = HttpErrorHelper.ParsearErrorHttp(detallesError)
                    Throw New Exception(contenido)
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            Finally

            End Try

        End Using
    End Function

    Public Async Function CrearAlbaranVenta(empresa As String, numeroPedido As Integer) As Task(Of Integer) Implements IPedidoVentaService.CrearAlbaranVenta
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            ' Creo estas variables para que los campos .Empresa y .Usuario se creen con mayúscula
            ' (si dejo empresa, como se llama igual que el campo, lo crea con minúscula
            Dim empresaParametro = empresa
            Dim usuarioParametro = configuracion.usuario
            Dim parametro As New With {
                .Empresa = empresaParametro,
                .Pedido = numeroPedido,
                .Usuario = usuarioParametro
            }

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(parametro), Encoding.UTF8, "application/json")

            Try
                response = Await client.PostAsync("AlbaranesVenta/CrearAlbaran", content)

                If response.IsSuccessStatusCode Then
                    Dim respuestaString As String = Await response.Content.ReadAsStringAsync()
                    Dim pedidoRespuesta As Integer = JsonConvert.DeserializeObject(Of Integer)(respuestaString)
                    If Not IsNothing(pedidoRespuesta) Then
                        Return pedidoRespuesta
                    Else
                        Throw New Exception("Albarán no creado")
                    End If
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    ' Carlos 21/11/24: Usar HttpErrorHelper para parsear errores del API
                    Dim contenido As String = HttpErrorHelper.ParsearErrorHttp(detallesError)
                    Throw New Exception(contenido)
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            Finally

            End Try

        End Using
    End Function

    Public Async Function CrearFacturaVenta(empresa As String, numeroPedido As Integer) As Task(Of CrearFacturaResponseDTO) Implements IPedidoVentaService.CrearFacturaVenta
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage

            ' Creo estas variables para que los campos .Empresa y .Usuario se creen con mayúscula
            ' (si dejo empresa, como se llama igual que el campo, lo crea con minúscula
            Dim empresaParametro = empresa
            Dim usuarioParametro = configuracion.usuario
            Dim parametro As New With {
                .Empresa = empresaParametro,
                .Pedido = numeroPedido,
                .Usuario = usuarioParametro
            }

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(parametro), Encoding.UTF8, "application/json")

            Try
                response = Await client.PostAsync("Facturas/CrearFactura", content)

                If response.IsSuccessStatusCode Then
                    Dim respuestaString As String = Await response.Content.ReadAsStringAsync()
                    Dim resultado As CrearFacturaResponseDTO = JsonConvert.DeserializeObject(Of CrearFacturaResponseDTO)(respuestaString)
                    If Not IsNothing(resultado) Then
                        Return resultado
                    Else
                        Throw New Exception("Factura no creada")
                    End If
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    ' Carlos 21/11/24: Usar HttpErrorHelper para parsear errores del API
                    Dim contenido As String = HttpErrorHelper.ParsearErrorHttp(detallesError)
                    Throw New Exception(contenido)
                End If
            Catch ex As Exception
                Throw New Exception(ex.Message)
            Finally

            End Try

        End Using
    End Function

    Public Async Function CargarFactura(empresa As String, numeroFactura As String, Optional papelConMembrete As Boolean = False) As Task(Of Byte()) Implements IPedidoVentaService.CargarFactura
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As Byte()

            Try
                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim urlConsulta As String = "Facturas"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numeroFactura=" + numeroFactura
                urlConsulta += "&papelConMembrete=" + papelConMembrete.ToString().ToLower()


                response = Await client.GetAsync(urlConsulta)

                respuesta = If(response.IsSuccessStatusCode, Await response.Content.ReadAsByteArrayAsync(), Nothing)

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar la lista de facturas desde el servidor")
            Finally

            End Try

            Return respuesta
        End Using
    End Function

    <DllImport("shell32")>
    Private Shared Function SHGetKnownFolderPath(ByRef rfid As Guid, ByVal dwFlags As UInteger, ByVal hToken As IntPtr, ByRef np As IntPtr) As Integer : End Function
    Public Async Function DescargarFactura(empresa As String, numeroFactura As String, cliente As String, Optional papelConMembrete As Boolean = False) As Task(Of String) Implements IPedidoVentaService.DescargarFactura
        Dim np As IntPtr
        Dim unused = SHGetKnownFolderPath(New Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, np)
        Dim path As String = Marshal.PtrToStringUni(np)
        Marshal.FreeCoTaskMem(np)


        Dim factura As Byte() = Await CargarFactura(empresa, numeroFactura, papelConMembrete)
        Dim ms As New MemoryStream(factura)
        'write to file
        Dim nombreArchivo As String = path + "\Cliente_" + cliente + "_" + numeroFactura.ToString + ".pdf"
        Dim file As New FileStream(nombreArchivo, FileMode.Create, FileAccess.Write)
        ms.WriteTo(file)
        file.Close()
        ms.Close()
        Return nombreArchivo
    End Function

    Public Async Function CargarAlbaran(empresa As String, numeroAlbaran As Integer, Optional papelConMembrete As Boolean = False) As Task(Of Byte()) Implements IPedidoVentaService.CargarAlbaran
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As Byte()

            Try
                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                Dim urlConsulta As String = "AlbaranesVenta"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numeroAlbaran=" + numeroAlbaran.ToString
                urlConsulta += "&papelConMembrete=" + papelConMembrete.ToString().ToLower()


                response = Await client.GetAsync(urlConsulta)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsByteArrayAsync()
                Else
                    Dim errorContent As String = Await response.Content.ReadAsStringAsync()
                    Throw New Exception($"Error del servidor al cargar el albarán {numeroAlbaran}: {response.StatusCode} - {errorContent}")
                End If

            Catch ex As Exception
                Throw New Exception($"No se ha podido cargar el albarán {numeroAlbaran} desde el servidor: {ex.Message}")
            Finally

            End Try

            Return respuesta
        End Using
    End Function

    Public Async Function DescargarAlbaran(empresa As String, numeroAlbaran As Integer, cliente As String, Optional papelConMembrete As Boolean = False) As Task(Of String) Implements IPedidoVentaService.DescargarAlbaran
        Dim np As IntPtr
        Dim unused = SHGetKnownFolderPath(New Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, np)
        Dim path As String = Marshal.PtrToStringUni(np)
        Marshal.FreeCoTaskMem(np)


        Dim albaran As Byte() = Await CargarAlbaran(empresa, numeroAlbaran, papelConMembrete)
        Dim ms As New MemoryStream(albaran)
        'write to file
        Dim nombreArchivo As String = path + "\Cliente_" + cliente + "_Albaran_" + numeroAlbaran.ToString + ".pdf"
        Dim file As New FileStream(nombreArchivo, FileMode.Create, FileAccess.Write)
        ms.WriteTo(file)
        file.Close()
        ms.Close()
        Return nombreArchivo
    End Function

    Public Async Function CrearPedido(pedido As PedidoVentaDTO) As Task(Of Integer) Implements IPedidoVentaService.CrearPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")

            Try
                Dim response As HttpResponseMessage = Await client.PostAsync("PedidosVenta", content)

                If response.IsSuccessStatusCode Then
                    ' El número del pedido viene en el Location header
                    Dim pathNumeroPedido = response.Headers.Location.LocalPath
                    Dim numeroPedidoStr = pathNumeroPedido.Substring(pathNumeroPedido.LastIndexOf("/") + 1)
                    Return Integer.Parse(numeroPedidoStr)
                Else
                    Dim respuestaError = Await response.Content.ReadAsStringAsync()
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)

                    ' Carlos 21/11/24: Detectar si es un error de validación de pedido (PEDIDO_VALIDACION_FALLO)
                    ' para lanzar ValidationException que el ViewModel pueda capturar
                    Dim errorCode As String = Nothing
                    If Not IsNothing(detallesError("error")) Then
                        Dim errorObj As JObject = detallesError("error")
                        errorCode = errorObj("code")?.ToString()
                    End If

                    ' Parsear el mensaje de error usando HttpErrorHelper
                    Dim contenido As String = HttpErrorHelper.ParsearErrorHttp(detallesError)

                    ' Si es error de validación de pedido, lanzar ValidationException
                    ' para que el ViewModel pueda preguntar "¿Crear sin pasar validación?"
                    If errorCode = "PEDIDO_VALIDACION_FALLO" Then
                        Throw New System.ComponentModel.DataAnnotations.ValidationException(contenido)
                    Else
                        Throw New Exception(contenido)
                    End If
                End If

            Catch ex As ValidationException
                Throw
            Catch ex As Exception
                Throw New Exception("Error al crear el pedido: " + ex.Message)
            End Try
        End Using
    End Function

    Public Async Function CargarParametrosIva(empresa As String, ivaCabecera As String) As Task(Of List(Of ParametrosIvaBase)) Implements IPedidoVentaService.CargarParametrosIva
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "ParametrosIva"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&ivaCabecera=" + ivaCabecera

                response = Await client.GetAsync(urlConsulta)
                respuesta = If(response.IsSuccessStatusCode, Await response.Content.ReadAsStringAsync(), "")

            Catch ex As Exception
                Throw New Exception("No se han podido cargar los parámetros de IVA")
            End Try

            Dim parametrosIva As List(Of ParametrosIvaBase) = JsonConvert.DeserializeObject(Of List(Of ParametrosIvaBase))(respuesta)
            Return parametrosIva
        End Using
    End Function

    ''' <summary>
    ''' Obtiene los documentos de impresión para un pedido ya facturado.
    ''' Genera PDFs con las copias y bandeja apropiadas según el tipo de ruta.
    ''' </summary>
    Public Async Function ObtenerDocumentosImpresion(empresa As String, numeroPedido As Integer, Optional numeroFactura As String = Nothing, Optional numeroAlbaran As Integer? = Nothing) As Task(Of DocumentosImpresionPedidoDTO) Implements IPedidoVentaService.ObtenerDocumentosImpresion
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                    Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
                End If

                ' Construir la URL del endpoint
                Dim urlConsulta As String = $"PedidosVenta/{empresa}/{numeroPedido}/DocumentosImpresion"

                ' Agregar parámetros opcionales si están presentes
                Dim parametros As New List(Of String)()
                If Not String.IsNullOrEmpty(numeroFactura) Then
                    parametros.Add($"numeroFactura={Uri.EscapeDataString(numeroFactura)}")
                End If
                If numeroAlbaran.HasValue Then
                    parametros.Add($"numeroAlbaran={numeroAlbaran.Value}")
                End If

                If parametros.Count > 0 Then
                    urlConsulta += "?" + String.Join("&", parametros)
                End If

                System.Diagnostics.Debug.WriteLine($"Llamando a API: {urlConsulta}")

                response = Await client.GetAsync(urlConsulta)

                If Not response.IsSuccessStatusCode Then
                    Dim errorContent = Await response.Content.ReadAsStringAsync()
                    System.Diagnostics.Debug.WriteLine($"Error de API: {response.StatusCode} - {errorContent}")
                    Throw New Exception($"Error al obtener documentos de impresión: {response.StatusCode}")
                End If

                respuesta = Await response.Content.ReadAsStringAsync()
                System.Diagnostics.Debug.WriteLine($"Respuesta recibida: {respuesta.Substring(0, Math.Min(200, respuesta.Length))}...")

            Catch ex As HttpRequestException
                Throw New Exception("Error de conexión al obtener documentos de impresión: " + ex.Message)
            Catch ex As Exception
                Throw New Exception("Error al obtener documentos de impresión: " + ex.Message)
            End Try

            Dim documentos As DocumentosImpresionPedidoDTO = JsonConvert.DeserializeObject(Of DocumentosImpresionPedidoDTO)(respuesta)
            System.Diagnostics.Debug.WriteLine($"✓ Documentos deserializados correctamente. Total para imprimir: {documentos.TotalDocumentosParaImprimir}")
            Return documentos
        End Using
    End Function

    ''' <summary>
    ''' Verifica si un pedido debe imprimir documento físico según sus comentarios.
    ''' Detecta frases como "factura física", "factura en papel", "albarán físico".
    ''' </summary>
    Public Async Function DebeImprimirDocumento(comentarios As String) As Task(Of Boolean) Implements IPedidoVentaService.DebeImprimirDocumento
        If String.IsNullOrWhiteSpace(comentarios) Then
            Return False
        End If

        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = $"PedidosVenta/DebeImprimirDocumento?comentarios={Uri.EscapeDataString(comentarios)}"

                response = Await client.GetAsync(urlConsulta)

                If Not response.IsSuccessStatusCode Then
                    ' Si hay error, por defecto no imprimir
                    Return False
                End If

                respuesta = Await response.Content.ReadAsStringAsync()
                Return Boolean.Parse(respuesta)

            Catch ex As Exception
                ' Si hay error de conexión, por defecto no imprimir
                System.Diagnostics.Debug.WriteLine($"Error al verificar DebeImprimirDocumento: {ex.Message}")
                Return False
            End Try
        End Using
    End Function

    ''' <summary>
    ''' Copia las líneas de una factura a un pedido nuevo o existente.
    ''' Issue #85
    ''' </summary>
    Public Async Function CopiarFactura(request As CopiarFacturaRequestDTO) As Task(Of CopiarFacturaResponseDTO) Implements IPedidoVentaService.CopiarFactura
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)

            ' Configurar autorización
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Try
                Dim urlConsulta As String = "PedidosVenta/CopiarFactura"
                Dim jsonContent = JsonConvert.SerializeObject(request)
                Dim content = New StringContent(jsonContent, Encoding.UTF8, "application/json")

                Dim response = Await client.PostAsync(urlConsulta, content)

                If Not response.IsSuccessStatusCode Then
                    Dim respuestaError = Await response.Content.ReadAsStringAsync()
                    Throw New Exception($"Error al copiar factura: {respuestaError}")
                End If

                Dim respuesta = Await response.Content.ReadAsStringAsync()
                Return JsonConvert.DeserializeObject(Of CopiarFacturaResponseDTO)(respuesta)

            Catch ex As Exception
                Throw New Exception($"Error al copiar factura {request.NumeroFactura}: {ex.Message}", ex)
            End Try
        End Using
    End Function

    ''' <summary>
    ''' Obtiene el cliente y empresa asociados a una factura.
    ''' Busca primero en la empresa especificada, si no encuentra busca en todas.
    ''' Issue #85
    ''' </summary>
    Public Async Function ObtenerClientePorFactura(empresa As String, numeroFactura As String) As Task(Of Models.Rectificativas.ClienteFacturaDTO) Implements IPedidoVentaService.ObtenerClientePorFactura
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)

            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorizacion")
            End If

            Try
                Dim urlConsulta As String = $"PedidosVenta/ClientePorFactura?empresa={empresa}&numeroFactura={Uri.EscapeDataString(numeroFactura)}"
                Dim response = Await client.GetAsync(urlConsulta)

                If Not response.IsSuccessStatusCode Then
                    Return Nothing
                End If

                Dim respuesta = Await response.Content.ReadAsStringAsync()
                Return JsonConvert.DeserializeObject(Of Models.Rectificativas.ClienteFacturaDTO)(respuesta)

            Catch ex As Exception
                Return Nothing
            End Try
        End Using
    End Function
End Class
