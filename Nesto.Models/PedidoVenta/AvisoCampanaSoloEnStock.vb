Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports Newtonsoft.Json

''' <summary>Un producto de la respuesta de POST api/PedidosVenta/AvisoCampanaSoloEnStock (NestoAPI#889, Black Friday 2026).</summary>
Public Class AvisoCampanaSoloEnStockProducto
    Public Property Producto As String
    Public Property Nombre As String
    Public Property Pedidas As Integer
    Public Property Disponibles As Integer
    Public Property ConDescuento As Integer
    Public Property SinDescuento As Integer
    Public Property DescuentoCampana As Decimal
    Public Property DescuentoSinCampana As Decimal
    Public Property PrecioSinCampana As Decimal
    Public Property Texto As String
End Class

''' <summary>Respuesta de POST api/PedidosVenta/AvisoCampanaSoloEnStock (siempre 200).</summary>
Public Class AvisoCampanaSoloEnStockRespuesta
    Public Property HayAviso As Boolean
    Public Property Campana As String
    ''' <summary>De usted, qué falta de cada producto; sin la pregunta final.</summary>
    Public Property Mensaje As String
    Public Property Productos As List(Of AvisoCampanaSoloEnStockProducto)
End Class

''' <summary>
''' NestoAPI#889 (Black Friday 2026, decisión de Carlos 24/11/26): por defecto lo que no hay en stock va SIN el descuento de una
''' campaña «solo en stock»; el vendedor tiene que aceptar expresamente que vaya con descuento. Antes de guardar se pregunta si
''' la API lo pide: Sí (por defecto) → <see cref="QUITAR_DESCUENTO"/> (la API parte las líneas al guardar); No →
''' <see cref="MANTENER_DESCUENTO"/>. Nesto no parte líneas: solo manda la elección en <see cref="PedidoVentaDTO.CampanaSoloEnStock"/>.
''' Si la consulta falla (API vieja, 404, red), no se pregunta y se guarda como siempre. Compartido por el detalle y la plantilla.
''' </summary>
Public NotInheritable Class AvisoCampanaSoloEnStock
    Public Const TITULO As String = "Oferta solo para lo que hay en stock"
    Public Const RUTA As String = "PedidosVenta/AvisoCampanaSoloEnStock"
    Public Const QUITAR_DESCUENTO As String = "QuitarDescuento"
    Public Const MANTENER_DESCUENTO As String = "MantenerDescuento"
    Public Const PREGUNTA As String = "¿Quitar el descuento de la campaña a lo que no hay en stock? Si responde No, irá todo con descuento aunque no haya stock."

    Private Sub New()
    End Sub

    ''' <summary>El texto del diálogo: el mensaje de la API, una línea en blanco y la pregunta.</summary>
    Public Shared Function TextoPregunta(mensaje As String) As String
        Return If(mensaje, String.Empty).Trim() & vbCrLf & vbCrLf & PREGUNTA
    End Function

    ''' <summary>Llama a la API con un cliente ya autorizado; el cuerpo es el pedido tal como se va a guardar. Nunca lanza: si algo falla, Nothing.</summary>
    Public Shared Async Function Consultar(client As HttpClient, pedido As PedidoVentaDTO) As Task(Of AvisoCampanaSoloEnStockRespuesta)
        Try
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync(RUTA, content)
            If Not response.IsSuccessStatusCode Then
                Return Nothing
            End If
            Return JsonConvert.DeserializeObject(Of AvisoCampanaSoloEnStockRespuesta)(Await response.Content.ReadAsStringAsync())
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Si el pedido ya trae la elección (un reintento del mismo guardado), se respeta y no se consulta. Si no, consulta y, con
    ''' aviso, pregunta y la pone. Devuelve True si ha preguntado. Nunca lanza por un fallo de la consulta.
    ''' </summary>
    Public Shared Async Function PreguntarYAplicar(pedido As PedidoVentaDTO,
                                                   consultar As Func(Of Task(Of AvisoCampanaSoloEnStockRespuesta)),
                                                   preguntar As Func(Of String, String, Task(Of Boolean))) As Task(Of Boolean)
        If pedido Is Nothing OrElse consultar Is Nothing OrElse preguntar Is Nothing Then
            Return False
        End If
        If Not String.IsNullOrWhiteSpace(pedido.CampanaSoloEnStock) Then
            Return False
        End If
        Dim aviso As AvisoCampanaSoloEnStockRespuesta = Nothing
        Try
            aviso = Await consultar()
        Catch
            aviso = Nothing
        End Try
        If aviso Is Nothing OrElse Not aviso.HayAviso OrElse String.IsNullOrWhiteSpace(aviso.Mensaje) Then
            Return False
        End If
        Dim quitar As Boolean = Await preguntar(TITULO, TextoPregunta(aviso.Mensaje))
        pedido.CampanaSoloEnStock = If(quitar, QUITAR_DESCUENTO, MANTENER_DESCUENTO)
        Return True
    End Function
End Class

''' <summary>
''' Black Friday 2026 (Carlos 24/11/26): al MODIFICAR un pedido, las preguntas de la campaña (contado y «solo en stock») solo
''' saltan si ESE guardado cambia algo que importa respecto al pedido tal como se cargó (o se guardó por última vez); así una
''' excepción aceptada (responder No) no se vuelve a preguntar en cada guardado. Al CREAR se consulta siempre. Sin pedido
''' original con el que comparar, se pregunta (lo decide la API).
''' </summary>
Public NotInheritable Class ReglaPreguntasCampanaAlModificar
    Private Sub New()
    End Sub

    Private Shared Function LineasProducto(pedido As PedidoVentaDTO) As IEnumerable(Of LineaPedidoVentaDTO)
        Return If(pedido?.Lineas, Enumerable.Empty(Of LineaPedidoVentaDTO)()) _
            .Where(Function(l) l IsNot Nothing AndAlso l.tipoLinea.GetValueOrDefault(1) = 1 AndAlso Not String.IsNullOrWhiteSpace(l.Producto))
    End Function

    Private Shared Function MismoTexto(a As String, b As String) As Boolean
        Return String.Equals(If(a, String.Empty).Trim(), If(b, String.Empty).Trim(), StringComparison.OrdinalIgnoreCase)
    End Function

    ''' <summary>Alguna línea de producto que no estaba en el original (id 0, o un id que el original no tiene).</summary>
    Public Shared Function AnadeLineasProducto(original As PedidoVentaDTO, actual As PedidoVentaDTO) As Boolean
        Dim ids As New HashSet(Of Integer)(LineasProducto(original).Where(Function(l) l.id > 0).Select(Function(l) l.id))
        Return LineasProducto(actual).Any(Function(l) l.id <= 0 OrElse Not ids.Contains(l.id))
    End Function

    ''' <summary>Contado: si añade alguna línea de producto o cambia la forma de pago o los plazos.</summary>
    Public Shared Function HayQuePreguntarContado(original As PedidoVentaDTO, actual As PedidoVentaDTO) As Boolean
        If actual Is Nothing Then
            Return False
        End If
        If original Is Nothing Then
            Return True
        End If
        Return AnadeLineasProducto(original, actual) OrElse
            Not MismoTexto(original.formaPago, actual.formaPago) OrElse
            Not MismoTexto(original.plazosPago, actual.plazosPago)
    End Function

    ''' <summary>Stock: si añade alguna línea de producto o sube la cantidad de alguna que ya estaba.</summary>
    Public Shared Function HayQuePreguntarStock(original As PedidoVentaDTO, actual As PedidoVentaDTO) As Boolean
        If actual Is Nothing Then
            Return False
        End If
        If original Is Nothing Then
            Return True
        End If
        If AnadeLineasProducto(original, actual) Then
            Return True
        End If
        Dim cantidades As Dictionary(Of Integer, Integer) = LineasProducto(original).Where(Function(l) l.id > 0) _
            .GroupBy(Function(l) l.id).ToDictionary(Function(g) g.Key, Function(g) g.First().Cantidad)
        Return LineasProducto(actual).Any(Function(l) cantidades.ContainsKey(l.id) AndAlso l.Cantidad > cantidades(l.id))
    End Function
End Class
