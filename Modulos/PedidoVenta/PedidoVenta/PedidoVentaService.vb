Imports System.Collections.ObjectModel
Imports System.ComponentModel.DataAnnotations
Imports System.IO
Imports System.Net.Http
Imports System.Runtime.InteropServices
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
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
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "PedidosVenta"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numero=" + numero.ToString

                response = Await client.GetAsync(urlConsulta)

                respuesta = If(response.IsSuccessStatusCode, Await response.Content.ReadAsStringAsync(), "")

            Catch ex As Exception
                Throw New Exception("No se ha podido recuperar el pedido " + numero.ToString)
            Finally

            End Try

            Dim pedido As PedidoVentaDTO = JsonConvert.DeserializeObject(Of PedidoVentaDTO)(respuesta)

            Return pedido

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
    Public Async Sub modificarPedido(pedido As PedidoVentaDTO) Implements IPedidoVentaService.modificarPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            If Not Await _servicioAutenticacion.ConfigurarAutorizacion(client) Then
                Throw New UnauthorizedAccessException("No se pudo configurar la autorización")
            End If

            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Dim urlConsulta As String = "PedidosVenta"
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")

            response = client.PutAsync(urlConsulta, content).Result

            If response.IsSuccessStatusCode Then
                respuesta = response.Content.ReadAsStringAsync().Result
            Else
                Dim respuestaError = response.Content.ReadAsStringAsync().Result
                Dim detallesError As JObject
                Dim contenido As String
                Try
                    detallesError = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    contenido = detallesError("ExceptionMessage")
                Catch ex As Exception
                    detallesError = New JObject()
                    contenido = respuestaError
                End Try


                While Not IsNothing(detallesError("InnerException"))
                    detallesError = detallesError("InnerException")
                    Dim contenido2 As String = detallesError("ExceptionMessage")
                    contenido = contenido + vbCr + contenido2
                End While
                Throw New Exception(contenido)
            End If

        End Using

    End Sub
    Public Sub sacarPickingPedido(empresa As String, numero As Integer) Implements IPedidoVentaService.sacarPickingPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "Picking"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numeroPedido=" + numero.ToString

                response = client.GetAsync(urlConsulta).Result

                respuesta = response.Content.ReadAsStringAsync().Result
                If Not response.IsSuccessStatusCode Then
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
                Throw New Exception("No se ha podido sacar el picking del pedido " + numero.ToString + vbCr + vbCr + ex.Message)
            Finally

            End Try

        End Using
    End Sub
    Public Sub sacarPickingPedido(cliente As String) Implements IPedidoVentaService.sacarPickingPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "Picking"
                urlConsulta += "?cliente=" + cliente

                response = client.GetAsync(urlConsulta).Result

                respuesta = response.Content.ReadAsStringAsync().Result
                If Not response.IsSuccessStatusCode Then
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
                Throw New Exception("No se han podido sacar los picking del cliente " + cliente, ex)
            Finally

            End Try

        End Using
    End Sub
    Public Sub sacarPickingPedido() Implements IPedidoVentaService.sacarPickingPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As String = ""

            Try
                Dim urlConsulta As String = "Picking"

                response = client.GetAsync(urlConsulta).Result

                respuesta = response.Content.ReadAsStringAsync().Result
                If Not response.IsSuccessStatusCode Then
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
                Throw New Exception("No se ha podido sacar el picking de las rutas" + vbCr + vbCr + ex.Message)
            Finally

            End Try

        End Using
    End Sub

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
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
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
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
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

    Public Async Function CrearFacturaVenta(empresa As String, numeroPedido As Integer) As Task(Of String) Implements IPedidoVentaService.CrearFacturaVenta
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
                    Dim pedidoRespuesta As String = JsonConvert.DeserializeObject(Of String)(respuestaString)
                    If Not IsNothing(pedidoRespuesta) Then
                        Return pedidoRespuesta
                    Else
                        Throw New Exception("Factura no creada")
                    End If
                Else
                    Dim respuestaError = response.Content.ReadAsStringAsync().Result
                    Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
                    Dim contenido As String = detallesError("ExceptionMessage")
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")
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

    Public Async Function CargarFactura(empresa As String, numeroFactura As String) As Task(Of Byte()) Implements IPedidoVentaService.CargarFactura
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
            Dim response As HttpResponseMessage
            Dim respuesta As Byte()

            Try
                Dim urlConsulta As String = "Facturas"
                urlConsulta += "?empresa=" + empresa
                urlConsulta += "&numeroFactura=" + numeroFactura


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
    Public Async Function DescargarFactura(empresa As String, numeroFactura As String, cliente As String) As Task(Of String) Implements IPedidoVentaService.DescargarFactura
        Dim np As IntPtr
        Dim unused = SHGetKnownFolderPath(New Guid("374DE290-123F-4565-9164-39C4925E467B"), 0, IntPtr.Zero, np)
        Dim path As String = Marshal.PtrToStringUni(np)
        Marshal.FreeCoTaskMem(np)


        Dim factura As Byte() = Await CargarFactura(empresa, numeroFactura)
        Dim ms As New MemoryStream(factura)
        'write to file
        Dim nombreArchivo As String = path + "\Cliente_" + cliente + "_" + numeroFactura.ToString + ".pdf"
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
                    Dim contenido As String = detallesError("ExceptionMessage")?.ToString()

                    If String.IsNullOrEmpty(contenido) Then
                        contenido = detallesError("exceptionMessage")?.ToString()
                    End If

                    ' Recorrer inner exceptions
                    While Not IsNothing(detallesError("InnerException"))
                        detallesError = detallesError("InnerException")
                        Dim contenido2 As String = detallesError("ExceptionMessage")?.ToString()
                        If String.IsNullOrEmpty(contenido2) Then
                            contenido2 = detallesError("exceptionMessage")?.ToString()
                        End If
                        If Not String.IsNullOrEmpty(contenido2) Then
                            contenido = contenido + vbCr + contenido2
                        End If
                    End While

                    ' Verificar si es ValidationException
                    Dim tipoEx As String = detallesError("ExceptionType")?.ToString()
                    If String.IsNullOrEmpty(tipoEx) Then
                        tipoEx = detallesError("exceptionType")?.ToString()
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
End Class
