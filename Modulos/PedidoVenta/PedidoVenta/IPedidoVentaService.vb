Imports System.Collections.ObjectModel
Imports Nesto.Models.PedidoVenta
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

Public Interface IPedidoVentaService
    Function cargarListaPedidos(vendedor As String, verTodosLosVendedores As Boolean, mostrarPresupuestos As Boolean) As Task(Of ObservableCollection(Of ResumenPedido))
    Function cargarPedido(empresa As String, numero As Integer) As Task(Of PedidoVentaDTO)
    Function cargarProducto(empresa As String, id As String, cliente As String, contacto As String, cantidad As Short) As Task(Of Producto)
    Sub modificarPedido(pedido As PedidoVentaDTO)
    Sub sacarPickingPedido(empresa As String, numero As Integer)
    Sub sacarPickingPedido(cliente As String)
    Sub sacarPickingPedido()
    Function CargarEnlacesSeguimiento(empresa As String, numero As Integer) As Task(Of List(Of EnvioAgenciaDTO))
    Sub EnviarCobroTarjeta(cobroTarjetaCorreo As String, cobroTarjetaMovil As String, totalPedido As Decimal, pedido As String, cliente As String)
End Interface
