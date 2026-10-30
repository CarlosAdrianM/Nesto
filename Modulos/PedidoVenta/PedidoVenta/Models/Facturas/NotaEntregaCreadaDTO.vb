''' <summary>
''' Representa una nota de entrega creada durante el proceso de facturación de rutas.
''' Las notas de entrega documentan entregas de productos que pueden estar ya facturados o pendientes de facturación.
''' Hereda de DocumentoImprimibleDTO para soportar impresión de PDFs.
''' </summary>
Public Class NotaEntregaCreadaDTO
    Inherits DocumentoImprimibleDTO

    ''' <summary>
    ''' Número de líneas procesadas en la nota de entrega
    ''' </summary>
    Public Property NumeroLineas As Integer

    ''' <summary>
    ''' Indica si alguna línea era YaFacturado=true (requirió dar de baja stock)
    ''' </summary>
    Public Property TeniaLineasYaFacturadas As Boolean

    ''' <summary>
    ''' Base imponible total de la nota de entrega
    ''' </summary>
    Public Property BaseImponible As Decimal

    ''' <summary>
    ''' Nesto#580 / NestoAPI#758: «Entrega» o «Recogida» (vacío = Entrega, las notas de Nesto viejo).
    ''' </summary>
    Public Property Tipo As String

    ''' <summary>
    ''' Nesto#580 / NestoAPI#758: el código de barras del documento (NE{Pedido} / NR{Pedido}).
    ''' </summary>
    Public Property CodigoDocumento As String

    ''' <summary>
    ''' Nesto#580 / NestoAPI#758: bultos de la nota (en las recogidas, los que se recogen).
    ''' </summary>
    Public Property Bultos As Integer

    ''' <summary>
    ''' Nesto#580 / NestoAPI#758: el envío de la agencia 14 (la parada del reparto propio), si lo hay.
    ''' </summary>
    Public Property EnvioId As Integer?

    ''' <summary>
    ''' Nesto#580 / NestoAPI#758: etiquetas de recogida (NR{Pedido}-{N}) que la API ha mandado a la Zebra.
    ''' </summary>
    Public Property EtiquetasRecogidaImpresas As Integer

    ''' <summary>Nesto#580: si es nota de recogida.</summary>
    Public ReadOnly Property EsRecogida As Boolean
        Get
            Return TiposNotaEntrega.Normalizar(Tipo) = TiposNotaEntrega.RECOGIDA
        End Get
    End Property
End Class
