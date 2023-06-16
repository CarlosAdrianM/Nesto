Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports Nesto.Modulos.Cliente
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PlantillaVenta.PlantillaVentaModel

Public Interface IPlantillaVentaService
    Function CargarCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteCrear)
    Function CargarClientesVendedor(filtroCliente As String, vendedor As String, todosLosVendedores As Boolean) As Task(Of ICollection(Of ClienteJson))
    Function CargarListaPendientes(empresa As String, cliente As String) As Task(Of List(Of Integer))
    Function CargarProductosPlantilla(clienteSeleccionado As ClienteJson) As Task(Of ObservableCollection(Of LineaPlantillaJson))
    Function CrearPedido(pedido As PedidoVentaDTO) As Task(Of String)
    Sub EnviarCobroTarjeta(cobroTarjetaCorreo As String, cobroTarjetaMovil As String, totalPedido As Decimal, pedido As String, cliente As String)
    Function PonerStocks(lineas As ObservableCollection(Of LineaPlantillaJson), almacen As String) As Task(Of ObservableCollection(Of LineaPlantillaJson))
    Function UnirPedidos(empresa As String, numeroPedidoOriginal As Integer, PedidoAmpliacion As PedidoVentaDTO) As Task(Of PedidoVentaDTO)
    Function CargarProductosBonificables(cliente As String, lineas As List(Of LineaPlantillaJson)) As List(Of LineaPlantillaJson)
End Interface
