Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports System.Text
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Nesto.Models.LineaPedidoVentaDTO
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class PedidoVentaService
    Implements IPedidoVentaService

    Private ReadOnly configuracion As IConfiguracion

    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
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

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

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

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

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

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                Else
                    respuesta = ""
                End If

            Catch ex As Exception
                Throw New Exception("No se ha podido cargar el producto " + id)
            Finally

            End Try

            Dim producto As Producto = JsonConvert.DeserializeObject(Of Producto)(respuesta)

            Return producto

        End Using
    End Function
    Public Sub modificarPedido(pedido As PedidoVentaDTO) Implements IPedidoVentaService.modificarPedido
        Using client As New HttpClient
            client.BaseAddress = New Uri(configuracion.servidorAPI)
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
                Dim content As StringContent = New StringContent(reclamacionJson, Encoding.UTF8, "application/json")
                response = Await client.PostAsync(urlConsulta, content)

                If response.IsSuccessStatusCode Then
                    respuesta = Await response.Content.ReadAsStringAsync()
                    'reclamacion = JsonConvert.DeserializeObject(Of ReclamacionDeuda)(respuesta)
                    'If reclamacion.TramitadoOK Then
                    '   EnlaceReclamarDeuda = reclamacion.Enlace
                    'End If
                Else
                    respuesta = String.Empty
                End If

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
End Class
