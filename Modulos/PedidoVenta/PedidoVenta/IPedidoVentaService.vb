Imports System.Collections.ObjectModel
Imports Nesto.Models.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

Public Interface IPedidoVentaService
    Function cargarListaPedidos(vendedor As String, verTodosLosVendedores As Boolean) As Task(Of ObservableCollection(Of ResumenPedido))
    Function cargarPedido(empresa As String, numero As Integer) As Task(Of PedidoVentaDTO)
End Interface
