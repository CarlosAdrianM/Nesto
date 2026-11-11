Imports System.Net.Http
Imports System.Text
Imports System.Threading.Tasks
Imports Newtonsoft.Json

''' <summary>Respuesta de POST api/PedidosVenta/AvisoCondicionesCampana (feria online 12-16/11/26).</summary>
Public Class AvisoCondicionesCampanaRespuesta
    Public Property Avisar As Boolean
    Public Property Mensaje As String
    Public Property PlazosPagoPropuestos As String
    Public Property Campana As String
    Public Property Productos As List(Of String)
End Class

''' <summary>
''' Feria online 12-16/11/26 (Carlos 11/11/26): al CREAR un pedido (o presupuesto) con productos de una campaña que exige pago
''' al contado, si no va ya al contado, se pregunta «¿Cambiar a contado?». Sí → plazos de pago = los que propone la API (la
''' forma de pago no se toca); No → sigue igual. Decide la API; si la llamada falla, no se avisa y se crea el pedido igual.
''' Compartido por el detalle del pedido y la plantilla de venta.
''' </summary>
Public NotInheritable Class AvisoCondicionesCampana
    Public Const TITULO As String = "Condiciones de la feria online"
    Public Const RUTA As String = "PedidosVenta/AvisoCondicionesCampana"

    Private Sub New()
    End Sub

    ''' <summary>El cuerpo de la petición: empresa, productos de las líneas de producto, forma y plazos de pago, y si lleva prepago.</summary>
    Public Shared Function Peticion(pedido As PedidoVentaDTO) As Object
        Dim productos As List(Of String) = If(pedido?.Lineas, Enumerable.Empty(Of LineaPedidoVentaDTO)()) _
            .Where(Function(l) l IsNot Nothing AndAlso l.tipoLinea.GetValueOrDefault(1) = 1 AndAlso Not String.IsNullOrWhiteSpace(l.Producto)) _
            .Select(Function(l) l.Producto.Trim()) _
            .Distinct(StringComparer.OrdinalIgnoreCase) _
            .ToList()
        Return New With {
            .Empresa = pedido?.empresa?.Trim(),
            .Productos = productos,
            .FormaPago = pedido?.formaPago?.Trim(),
            .PlazosPago = pedido?.plazosPago?.Trim(),
            .TienePrepago = pedido?.Prepagos IsNot Nothing AndAlso pedido.Prepagos.Any()
        }
    End Function

    ''' <summary>Llama a la API con un cliente ya autorizado. Nunca lanza: si algo falla (API vieja, 404, red), Nothing.</summary>
    Public Shared Async Function Consultar(client As HttpClient, pedido As PedidoVentaDTO) As Task(Of AvisoCondicionesCampanaRespuesta)
        Try
            Dim content As HttpContent = New StringContent(JsonConvert.SerializeObject(Peticion(pedido)), Encoding.UTF8, "application/json")
            Dim response As HttpResponseMessage = Await client.PostAsync(RUTA, content)
            If Not response.IsSuccessStatusCode Then
                Return Nothing
            End If
            Return JsonConvert.DeserializeObject(Of AvisoCondicionesCampanaRespuesta)(Await response.Content.ReadAsStringAsync())
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>
    ''' Pregunta si la API lo pide y, si el usuario dice que sí, cambia los plazos del pedido. Devuelve True si los ha cambiado.
    ''' Nunca lanza por un fallo de la consulta (entonces no se avisa).
    ''' </summary>
    Public Shared Async Function PreguntarYAplicar(pedido As PedidoVentaDTO,
                                                   consultar As Func(Of Task(Of AvisoCondicionesCampanaRespuesta)),
                                                   preguntar As Func(Of String, String, Task(Of Boolean))) As Task(Of Boolean)
        If pedido Is Nothing OrElse consultar Is Nothing OrElse preguntar Is Nothing Then
            Return False
        End If
        Dim aviso As AvisoCondicionesCampanaRespuesta = Nothing
        Try
            aviso = Await consultar()
        Catch
            aviso = Nothing
        End Try
        If aviso Is Nothing OrElse Not aviso.Avisar OrElse String.IsNullOrWhiteSpace(aviso.PlazosPagoPropuestos) Then
            Return False
        End If
        If Not Await preguntar(TITULO, aviso.Mensaje) Then
            Return False
        End If
        pedido.plazosPago = aviso.PlazosPagoPropuestos.Trim()
        Return True
    End Function
End Class
