Imports System.Collections.ObjectModel
Imports System.Net.Http
Imports Nesto.Contratos
Imports Nesto.Models
Imports Nesto.Models.PedidoVenta
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Newtonsoft.Json

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


End Class
