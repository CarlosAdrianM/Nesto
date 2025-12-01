Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel

Public Interface IPedidoVentaService
    Function cargarListaPedidos(vendedor As String, verTodosLosVendedores As Boolean, mostrarPresupuestos As Boolean) As Task(Of ObservableCollection(Of ResumenPedido))
    Function cargarPedido(empresa As String, numero As Integer) As Task(Of PedidoVentaDTO)
    Function cargarProducto(empresa As String, id As String, cliente As String, contacto As String, cantidad As Short) As Task(Of Producto)
    Function modificarPedido(pedido As PedidoVentaDTO) As Task
    Function sacarPickingPedido(empresa As String, numero As Integer) As Task
    Function sacarPickingPedido(cliente As String) As Task
    Function sacarPickingPedido() As Task
    Function CargarEnlacesSeguimiento(empresa As String, numero As Integer) As Task(Of List(Of EnvioAgenciaDTO))
    Sub EnviarCobroTarjeta(cobroTarjetaCorreo As String, cobroTarjetaMovil As String, totalPedido As Decimal, pedido As String, cliente As String)
    Function CargarPedidosPendientes(empresa As String, cliente As String) As Task(Of ObservableCollection(Of Integer))
    Function UnirPedidos(empresa As String, numeroPedidoOriginal As Integer, numeroPedidoAmpliacion As Integer) As Task(Of PedidoVentaDTO)
    Function CrearAlbaranVenta(empresa As String, numeroPedido As Integer) As Task(Of Integer)
    Function CrearFacturaVenta(empresa As String, numeroPedido As Integer) As Task(Of CrearFacturaResponseDTO)
    Function CargarFactura(empresa As String, numeroFactura As String, Optional papelConMembrete As Boolean = False) As Task(Of Byte())
    Function DescargarFactura(empresa As String, numeroFactura As String, cliente As String, Optional papelConMembrete As Boolean = False) As Task(Of String)
    Function CargarAlbaran(empresa As String, numeroAlbaran As Integer, Optional papelConMembrete As Boolean = False) As Task(Of Byte())
    Function DescargarAlbaran(empresa As String, numeroAlbaran As Integer, cliente As String, Optional papelConMembrete As Boolean = False) As Task(Of String)
    Function CrearPedido(pedido As PedidoVentaDTO) As Task(Of Integer)
    Function CargarParametrosIva(empresa As String, ivaCabecera As String) As Task(Of List(Of ParametrosIvaBase))

    ''' <summary>
    ''' Obtiene los documentos de impresión para un pedido ya facturado.
    ''' Genera PDFs con las copias y bandeja apropiadas según el tipo de ruta.
    ''' </summary>
    ''' <param name="empresa">Empresa del pedido</param>
    ''' <param name="numeroPedido">Número del pedido</param>
    ''' <param name="numeroFactura">Número de factura (opcional, Nothing o "FDM" si es fin de mes)</param>
    ''' <param name="numeroAlbaran">Número de albarán (opcional)</param>
    ''' <returns>Documentos listos para imprimir</returns>
    Function ObtenerDocumentosImpresion(empresa As String, numeroPedido As Integer, Optional numeroFactura As String = Nothing, Optional numeroAlbaran As Integer? = Nothing) As Task(Of DocumentosImpresionPedidoDTO)

    ''' <summary>
    ''' Verifica si un pedido debe imprimir documento físico según sus comentarios.
    ''' Detecta frases como "factura física", "factura en papel", "albarán físico".
    ''' </summary>
    ''' <param name="comentarios">Comentarios del pedido</param>
    ''' <returns>True si debe imprimir documento físico</returns>
    Function DebeImprimirDocumento(comentarios As String) As Task(Of Boolean)
End Interface
