Imports System.Collections.Generic
Imports System.Linq

''' <summary>
''' Response con el preview (simulación) de facturación de rutas.
''' NO crea nada en la BD, solo muestra QUÉ se facturaría.
''' </summary>
Public Class PreviewFacturacionRutasResponseDTO
    Public Sub New()
        PedidosMuestra = New List(Of PedidoPreviewDTO)()
    End Sub

    ''' <summary>
    ''' Número total de pedidos que se procesarían
    ''' </summary>
    Public Property NumeroPedidos As Integer

    ''' <summary>
    ''' Número estimado de albaranes que se crearían
    ''' </summary>
    Public Property NumeroAlbaranes As Integer

    ''' <summary>
    ''' Número estimado de facturas que se crearían
    ''' </summary>
    Public Property NumeroFacturas As Integer

    ''' <summary>
    ''' Número estimado de notas de entrega que se crearían
    ''' </summary>
    Public Property NumeroNotasEntrega As Integer

    ''' <summary>
    ''' Base imponible total de los albaranes
    ''' </summary>
    Public Property BaseImponibleAlbaranes As Decimal

    ''' <summary>
    ''' Base imponible total de las facturas
    ''' </summary>
    Public Property BaseImponibleFacturas As Decimal

    ''' <summary>
    ''' Base imponible total de las notas de entrega
    ''' </summary>
    Public Property BaseImponibleNotasEntrega As Decimal

    ''' <summary>
    ''' Muestra de los primeros pedidos (máximo 20) para verificación
    ''' </summary>
    Public Property PedidosMuestra As List(Of PedidoPreviewDTO)
End Class

''' <summary>
''' Información resumida de un pedido para el preview
''' </summary>
Public Class PedidoPreviewDTO
    ''' <summary>
    ''' Número de pedido
    ''' </summary>
    Public Property NumeroPedido As Integer

    ''' <summary>
    ''' Cliente
    ''' </summary>
    Public Property Cliente As String

    ''' <summary>
    ''' Contacto del cliente
    ''' </summary>
    Public Property Contacto As String

    ''' <summary>
    ''' Nombre del cliente
    ''' </summary>
    Public Property NombreCliente As String

    ''' <summary>
    ''' Periodo de facturación (NRM, FDM)
    ''' </summary>
    Public Property PeriodoFacturacion As String

    ''' <summary>
    ''' Base imponible del pedido
    ''' </summary>
    Public Property BaseImponible As Decimal

    ''' <summary>
    ''' Indica si se creará un albarán
    ''' </summary>
    Public Property CreaAlbaran As Boolean

    ''' <summary>
    ''' Indica si se creará una factura
    ''' </summary>
    Public Property CreaFactura As Boolean

    ''' <summary>
    ''' Indica si se creará una nota de entrega
    ''' </summary>
    Public Property CreaNotaEntrega As Boolean

    ''' <summary>
    ''' Comentarios del pedido
    ''' </summary>
    Public Property Comentarios As String
End Class
