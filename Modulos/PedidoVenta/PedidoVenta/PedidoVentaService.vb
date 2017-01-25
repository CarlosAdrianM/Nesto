Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports System.Runtime.ExceptionServices
Imports System.Text
Imports Nesto.Contratos
Imports Nesto.Models
Imports Nesto.Models.PedidoVenta
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Newtonsoft.Json
Imports Newtonsoft.Json.Linq

Public Class PedidoVentaService
    Implements IPedidoVentaService

    Private ReadOnly configuracion As IConfiguracion

    Public Sub New(configuracion As IConfiguracion)
        Me.configuracion = configuracion
    End Sub

    Private Async Function cargarListaPedidos(vendedor As String, verTodosLosVendedores As Boolean) As Task(Of ObservableCollection(Of ResumenPedido)) Implements IPedidoVentaService.cargarListaPedidos

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
                Throw New Exception(response.Content.ReadAsStringAsync().Result)
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
End Class
