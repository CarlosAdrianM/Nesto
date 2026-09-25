Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports Nesto.Modulos.Cliente
Imports Nesto.Modulos.PedidoVenta

Public Interface IPlantillaVentaService
    Function CargarCliente(empresa As String, cliente As String, contacto As String) As Task(Of ClienteCrear)
    ''' <summary>Nesto#397: el pedido ya en forma de plantilla (GET api/PedidosVenta/ParaPlantilla).</summary>
    Function CargarPedidoParaPlantilla(empresa As String, numero As Integer) As Task(Of PedidoParaPlantillaModel)
    ''' <summary>Nesto#397 (Parte 1): un PedidoVentaDTO suelto (dump de ELMAH) a forma de plantilla (POST).</summary>
    Function ConvertirPedidoAPlantilla(jsonPedido As String) As Task(Of PedidoParaPlantillaModel)
    Function CargarClientesVendedor(filtroCliente As String, vendedor As String, todosLosVendedores As Boolean) As Task(Of ICollection(Of ClienteJson))
    Function CargarListaPendientes(empresa As String, cliente As String) As Task(Of List(Of Integer))
    Function CargarProductosPlantilla(clienteSeleccionado As ClienteJson) As Task(Of ObservableCollection(Of LineaPlantillaVenta))
    Function CrearPedido(pedido As PedidoVentaDTO) As Task(Of String)
    Function EnviarCobroTarjeta(cobroTarjetaCorreo As String, cobroTarjetaMovil As String, totalPedido As Decimal, pedido As String, empresa As String, cliente As String) As Task(Of String)
    Function PonerStocks(lineas As ObservableCollection(Of LineaPlantillaVenta), almacen As String, Optional almacenes As List(Of String) = Nothing) As Task(Of ObservableCollection(Of LineaPlantillaVenta))
    Function UnirPedidos(empresa As String, numeroPedidoOriginal As Integer, PedidoAmpliacion As PedidoVentaDTO) As Task(Of PedidoVentaDTO)
    Function CargarProductosBonificables(cliente As String, lineas As List(Of LineaPlantillaVenta)) As List(Of LineaPlantillaVenta)
    Function CargarProductosBonificablesIds() As Task(Of HashSet(Of String))
    ''' <summary>
    ''' Nesto#483 / NestoAPI#506: el modo de servicio que el servidor le pondria al pedido que se esta
    ''' montando (POST api/PedidosVenta/ModoServicioSugerido). Devuelve Nothing si la API falla o es
    ''' anterior al endpoint: entonces la plantilla se queda con su defecto de siempre.
    ''' </summary>
    Function ModoServicioSugerido(pedido As PedidoVentaDTO) As Task(Of ModoServicioSugeridoDTO)

    ''' <summary>
    ''' Nesto#493 / NestoAPI#542: el modo de facturación que rige en el pedido que se está montando y los
    ''' que se pueden elegir (POST api/PedidosVenta/ModoFacturacionSugerido; solo depende del cliente, los
    ''' plazos y el periodo, no del stock). Nothing si la API falla: el servidor valida al guardar.
    ''' </summary>
    Function ModoFacturacionSugerido(pedido As PedidoVentaDTO) As Task(Of ModoFacturacionSugeridoDTO)

    ''' <summary>
    ''' Nesto#465 / NestoAPI#457: las ofertas que el pedido podria aplicar y no esta aplicando
    ''' (POST api/PedidosVenta/OfertasSugeridas). Lista vacia si la API falla: esto es una ayuda,
    ''' nunca un bloqueo para guardar el pedido.
    ''' </summary>
    Function OfertasSugeridas(pedido As PedidoVentaDTO) As Task(Of List(Of SugerenciaOfertaDTO))

    ''' <summary>
    ''' NestoAPI#466: los grupos de producto que generan Ganavisiones, segun el servidor
    ''' (GET Ganavisiones/GruposBonificables). Lista vacia si no se puede consultar o la API es
    ''' anterior al endpoint: entonces la plantilla sigue con su lista de reserva.
    ''' </summary>
    Function CargarGruposBonificables() As Task(Of List(Of String))
    ''' <summary>
    ''' Obtiene los productos bonificables para un pedido segun los Ganavisiones disponibles.
    ''' Issue #94: Sistema Ganavisiones - FASE 7
    ''' </summary>
    Function CargarProductosBonificablesParaPedido(empresa As String, baseImponibleBonificable As Decimal, almacen As String, servirJunto As Boolean, cliente As String, Optional incluirBloqueados As Boolean = False) As Task(Of ProductosBonificablesResponse)
    Function CalcularFechaEntrega(fecha As DateTime, ruta As String, almacen As String) As Task(Of DateTime)
    Function CargarVendedoresEquipo(jefeEquipo As String) As Task(Of List(Of VendedorDTO))
    ' ValidarServirJunto se eliminó de aquí (NestoAPI#161): ahora lo expone
    ' Nesto.Infrastructure.Services.ServirJunto.IServirJuntoService, compartido por
    ' PlantillaVenta y DetallePedido.
End Interface
