Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports Nesto.Modulos.Cliente
Imports Nesto.Modulos.PedidoVenta

Public Interface IPlantillaVentaService
    Function CargarCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteCrear)
    Function CargarClientesVendedor(filtroCliente As String, vendedor As String, todosLosVendedores As Boolean) As Task(Of ICollection(Of ClienteJson))
    Function CargarListaPendientes(empresa As String, cliente As String) As Task(Of List(Of Integer))
    Function CargarProductosPlantilla(clienteSeleccionado As ClienteJson) As Task(Of ObservableCollection(Of LineaPlantillaVenta))
    Function CrearPedido(pedido As PedidoVentaDTO) As Task(Of String)
    Sub EnviarCobroTarjeta(cobroTarjetaCorreo As String, cobroTarjetaMovil As String, totalPedido As Decimal, pedido As String, cliente As String)
    Function PonerStocks(lineas As ObservableCollection(Of LineaPlantillaVenta), almacen As String, Optional almacenes As List(Of String) = Nothing) As Task(Of ObservableCollection(Of LineaPlantillaVenta))
    Function UnirPedidos(empresa As String, numeroPedidoOriginal As Integer, PedidoAmpliacion As PedidoVentaDTO) As Task(Of PedidoVentaDTO)
    Function CargarProductosBonificables(cliente As String, lineas As List(Of LineaPlantillaVenta)) As List(Of LineaPlantillaVenta)
    Function CargarProductosBonificablesIds() As Task(Of HashSet(Of String))
    ''' <summary>
    ''' Obtiene los productos bonificables para un pedido segun los Ganavisiones disponibles.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Function CargarProductosBonificablesParaPedido(empresa As String, baseImponibleBonificable As Decimal, almacen As String, servirJunto As Boolean, cliente As String) As Task(Of ProductosBonificablesResponse)
    Function CalcularFechaEntrega(fecha As DateTime, ruta As String, almacen As String) As Task(Of DateTime)
    Function CargarVendedoresEquipo(jefeEquipo As String) As Task(Of List(Of VendedorDTO))
    ''' <summary>
    ''' Valida si se puede desmarcar ServirJunto cuando hay productos bonificados.
    ''' Issue #94: Sistema Ganavisiones - FASE 9
    ''' </summary>
    Function ValidarServirJunto(almacen As String, productosBonificados As List(Of String)) As Task(Of ValidarServirJuntoResponse)
End Interface
