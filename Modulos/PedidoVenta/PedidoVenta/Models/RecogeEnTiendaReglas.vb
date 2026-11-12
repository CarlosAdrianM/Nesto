Imports Nesto.Infrastructure.Shared
Imports Nesto.Models

''' <summary>
''' Nesto#503 (gemela de NestoAPI#571, cortes 1 y 2): la casilla «Recoge en tienda» de la plantilla y del detalle del pedido.
''' La lógica vive en la API: con RecogeEnTienda = True pone la ruta de recogida del almacén (ALG → AM, ALC → ALC, REI → REI)
''' y quita portes y comisión contra reembolso; con False en Algete vuelve la ruta de la ficha; al leer, es True si la ruta es
''' de recogida (AM, ALC, REI). Aquí solo la pantalla, como NestoApp#205: en Alcobendas y Reina se recoge siempre (marcada y
''' fija), en Algete se elige y en cualquier otro almacén no se ofrece. Solo se manda el valor si el usuario toca la casilla.
''' </summary>
Public NotInheritable Class RecogeEnTiendaReglas
    Private Sub New()
    End Sub

    Public Shared ReadOnly ALMACENES_QUE_SIEMPRE_RECOGEN As String() = {Constantes.Almacenes.ALMACEN_ALCOBENDAS, Constantes.Almacenes.ALMACEN_REINA}
    Public Shared ReadOnly RUTAS_DE_RECOGIDA As String() = {"AM", "ALC", "REI"}

    Public Const TEXTO_PORTES As String = "Recoge en tienda: sin portes ni comisión de reembolso"
    Public Const AYUDA As String = "El cliente pasa a recoger el pedido por la tienda: ni portes ni comisión de reembolso. " &
        "En Algete pone la ruta AM; al desmarcarla vuelve la ruta de la ficha del cliente."
    Public Const AYUDA_FIJA As String = "Los pedidos de este almacén se recogen siempre en la tienda."
    Public Const AYUDA_BLOQUEADA As String = "No se puede cambiar «Recoge en tienda»: el pedido ya tiene picking, albarán o factura."

    Private Shared Function Limpio(valor As String) As String
        Return If(valor, String.Empty).Trim().ToUpperInvariant()
    End Function

    ''' <summary>Alcobendas y Reina: el pedido se recoge siempre en la tienda (la API rechaza desmarcarlo).</summary>
    Public Shared Function AlmacenObligaARecoger(almacen As String) As Boolean
        Return ALMACENES_QUE_SIEMPRE_RECOGEN.Contains(Limpio(almacen))
    End Function

    ''' <summary>Solo Algete, Alcobendas y Reina son tiendas de recogida.</summary>
    Public Shared Function AlmacenPermiteRecoger(almacen As String) As Boolean
        Return Limpio(almacen) = Constantes.Almacenes.ALMACEN_ALGETE OrElse AlmacenObligaARecoger(almacen)
    End Function

    Public Shared Function EsRutaDeRecogida(ruta As String) As Boolean
        Return RUTAS_DE_RECOGIDA.Contains(Limpio(ruta))
    End Function

    ''' <summary>
    ''' Lo que se ve en la casilla: siempre marcada donde se recoge siempre; en Algete, lo que haya elegido el usuario o, si no
    ''' la ha tocado, lo que dice la ruta; desmarcada en el resto.
    ''' </summary>
    Public Shared Function Efectivo(almacen As String, tocado As Boolean, marcado As Boolean, ruta As String) As Boolean
        If AlmacenObligaARecoger(almacen) Then
            Return True
        End If
        If Not AlmacenPermiteRecoger(almacen) Then
            Return False
        End If
        Return If(tocado, marcado, EsRutaDeRecogida(ruta))
    End Function

    ''' <summary>Lo que viaja a la API: Nothing (no tocar) salvo que el usuario la haya tocado en Algete.</summary>
    Public Shared Function ParaGuardar(almacen As String, tocado As Boolean, marcado As Boolean) As Boolean?
        If Not tocado OrElse Limpio(almacen) <> Constantes.Almacenes.ALMACEN_ALGETE Then
            Return Nothing
        End If
        Return marcado
    End Function

    ''' <summary>
    ''' El almacén del pedido, como lo mira la API: el de las líneas vivas (presupuesto, pendiente o en curso, sin nota de
    ''' entrega) de producto o, si no hay de producto, de cualquier tipo. Nothing si no hay ninguna o son de varios almacenes.
    ''' </summary>
    Public Shared Function AlmacenDelPedido(lineas As IEnumerable(Of LineaPedidoVentaDTO)) As String
        If lineas Is Nothing Then
            Return Nothing
        End If
        Dim vivas = lineas.Where(Function(l) l IsNot Nothing AndAlso l.estado <= Constantes.LineasPedido.ESTADO_LINEA_EN_CURSO AndAlso
                                     l.estado <> -2 AndAlso Not String.IsNullOrWhiteSpace(l.almacen)).ToList()
        Dim deProducto = vivas.Where(Function(l) l.tipoLinea = 1).ToList()
        Dim almacenes = If(deProducto.Any(), deProducto, vivas).Select(Function(l) Limpio(l.almacen)).Distinct().ToList()
        Return If(almacenes.Count = 1, almacenes(0), Nothing)
    End Function

    ''' <summary>
    ''' Si se puede tocar en un pedido ya creado: ninguna línea de producto con picking, albarán o factura (lo que también
    ''' frena cambiar el almacén). La etiqueta de agencia y el picking en curso los revisa la API al guardar (400 con el motivo).
    ''' </summary>
    Public Shared Function SePuedeCambiar(lineas As IEnumerable(Of LineaPedidoVentaDTO)) As Boolean
        If lineas Is Nothing Then
            Return False
        End If
        Return lineas.Where(Function(l) l IsNot Nothing AndAlso l.tipoLinea = 1).
            All(Function(l) l.picking = 0 AndAlso Not l.Albaran.HasValue AndAlso String.IsNullOrWhiteSpace(l.Factura) AndAlso
                            l.estado < Constantes.LineasPedido.ESTADO_ALBARAN)
    End Function
End Class
