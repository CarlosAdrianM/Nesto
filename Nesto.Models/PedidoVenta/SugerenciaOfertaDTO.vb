''' <summary>
''' Nesto#465 / NestoAPI#457: una oferta que el pedido podría aplicar y no está aplicando, tal y como la
''' calcula POST api/PedidosVenta/OfertasSugeridas. Es accionable: trae producto y cantidades, no solo el
''' texto, para poder aplicarla de un clic. El Texto lo escribe el servidor (lo comparten Nesto, NestoApp
''' y la tienda): aquí no se redacta ni se replica ninguna regla de ofertas.
''' </summary>
Public Class SugerenciaOfertaDTO
    ''' <summary>Una de las constantes de TiposSugerenciaOferta.</summary>
    Public Property Tipo As String
    Public Property Producto As String
    ''' <summary>Unidades cobradas que hay ahora en el pedido.</summary>
    Public Property CantidadActual As Integer
    ''' <summary>Unidades cobradas que tiene que haber para que aplique (igual a la actual si ya aplica).</summary>
    Public Property CantidadSugerida As Integer
    ''' <summary>Unidades de regalo que se llevaría con la cantidad sugerida.</summary>
    Public Property CantidadRegalo As Integer
    ''' <summary>Lo que falta para llegar al importe del regalo (0 en las de cantidad y si ya se llega).</summary>
    Public Property ImporteQueFalta As Decimal
    ''' <summary>Importe de pedido a partir del cual hay regalo (0 en las de cantidad).</summary>
    Public Property ImportePedido As Decimal
    Public Property Texto As String
    ''' <summary>Nº de orden de la oferta permitida que la sustenta.</summary>
    Public Property Oferta As Integer?
    ''' <summary>Descuento en tanto por uno al que da derecho el tramo (0 en las demás).</summary>
    Public Property Descuento As Decimal
    ''' <summary>Id de la oferta escalonada que la sustenta.</summary>
    Public Property OfertaEscalonada As Integer?

    ''' <summary>
    ''' Las que se pueden aplicar de un clic desde la plantilla: las de cantidad, porque sabemos a qué
    ''' producto y a qué cantidades tocar. Las de importe y las escalonadas dicen lo que falta, pero no
    ''' qué añadir: esas se enseñan y decide el comercial.
    ''' </summary>
    Public ReadOnly Property EsAplicable As Boolean
        Get
            Return (Tipo = TiposSugerenciaOferta.OFERTA_NO_APLICADA OrElse Tipo = TiposSugerenciaOferta.AMPLIAR_CANTIDAD) AndAlso
                   Not String.IsNullOrWhiteSpace(Producto) AndAlso CantidadRegalo > 0
        End Get
    End Property
End Class

''' <summary>Nesto#465: los valores de SugerenciaOfertaDTO.Tipo que manda NestoAPI#457.</summary>
Public NotInheritable Class TiposSugerenciaOferta
    Private Sub New()
    End Sub

    Public Const OFERTA_NO_APLICADA As String = "OfertaNoAplicada"
    Public Const AMPLIAR_CANTIDAD As String = "AmpliarCantidad"
    Public Const REGALO_NO_APLICADO As String = "RegaloNoAplicado"
    Public Const AMPLIAR_IMPORTE As String = "AmpliarImporte"
    Public Const DESCUENTO_NO_APLICADO As String = "DescuentoNoAplicado"
    Public Const AMPLIAR_CANTIDAD_ESCALONADA As String = "AmpliarCantidadEscalonada"
End Class
