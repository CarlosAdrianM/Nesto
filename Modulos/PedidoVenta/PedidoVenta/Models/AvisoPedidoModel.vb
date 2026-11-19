Imports Newtonsoft.Json.Linq

''' <summary>
''' Nesto#420: respuesta del PUT PedidosVenta cuando el servidor tiene avisos operativos
''' (RespuestaModificacionPedidoDTO de NestoAPI). Sin avisos el PUT devuelve 204 sin cuerpo.
''' </summary>
Public Class RespuestaModificacionPedidoModel
    Public Property Avisos As List(Of AvisoPedidoModel) = New List(Of AvisoPedidoModel)
    ''' <summary>Nesto#588: «fechas futuras» del pedido tal como ha quedado (Nothing = la API no lo manda).</summary>
    Public Property tieneFechasFuturas As Boolean?
    ''' <summary>Nesto#642 (NestoAPI#873): la versión del pedido tal como ha quedado (Nothing = la API no la manda).</summary>
    Public Property Version As String
    ''' <summary>Nesto#642 (NestoAPI#873, ampliación): con «?devolverPedido=true», el pedido tal como ha quedado (Nothing si no).</summary>
    Public Property Pedido As Nesto.Models.PedidoVentaDTO
End Class

''' <summary>Nesto#642 (NestoAPI#873): GET api/PedidosVenta/{empresa}/{numero}/Version: la versión de ahora y quién lo tocó el último.</summary>
Public Class VersionPedidoModel
    Public Property Version As String
    Public Property Usuario As String
    Public Property Fecha As Date?
End Class

''' <summary>
''' Un aviso del guardado. Datos es un JObject porque cada Tipo lleva su propia estructura
''' (p. ej. ReembolsoEnvioSinAjustar: Envio, Reembolso, ComisionQuitada, Ajustable).
''' Tipos desconocidos se ignoran sin romper (forward-compatible).
''' </summary>
Public Class AvisoPedidoModel
    ''' <summary>Nesto#567 (NestoAPI#732): se han anulado inscripciones a eventos que justificaba el pedido (Datos.Inscripciones).</summary>
    Public Const TIPO_INSCRIPCIONES_EVENTO_ANULADAS As String = "INSCRIPCIONES_EVENTO_ANULADAS"
    ''' <summary>Nesto#581 (NestoAPI#760): en el abono de una recogida, productos que no están en su factura original.</summary>
    Public Const TIPO_ABONO_PRODUCTO_FUERA_DE_FACTURA As String = "ABONO_PRODUCTO_FUERA_DE_FACTURA"
    ''' <summary>Nesto#629 (NestoAPI#722): al aplazar, la fecha se ha cambiado también en la agencia (Datos.Envio, Agencia, FechaNueva).</summary>
    Public Const TIPO_FECHA_CAMBIADA_EN_AGENCIA As String = "FechaCambiadaEnAgencia"
    ''' <summary>
    ''' 19/11/26 (caso 33895): una línea es el curso de un evento vendido a mano en un pedido normal (no bloquea). El mensaje dice
    ''' que se inscriba a la clienta y se cobre la señal en Cajas.
    ''' </summary>
    Public Const TIPO_CURSO_VENDIDO_A_MANO As String = "CursoVendidoAMano"
    Public Const TITULO_CURSO_VENDIDO_A_MANO As String = "Curso de un evento"

    ''' <summary>19/11/26: enseña los avisos del curso vendido a mano (los demás tipos los trata cada pantalla).</summary>
    Public Shared Sub MostrarCursosVendidosAMano(avisos As IEnumerable(Of AvisoPedidoModel), mostrar As Action(Of String, String))
        If avisos Is Nothing OrElse mostrar Is Nothing Then
            Return
        End If
        For Each aviso In avisos.Where(Function(a) a?.Tipo = TIPO_CURSO_VENDIDO_A_MANO AndAlso Not String.IsNullOrWhiteSpace(a.Mensaje))
            mostrar(TITULO_CURSO_VENDIDO_A_MANO, aviso.Mensaje)
        Next
    End Sub

    Public Property Tipo As String
    Public Property Mensaje As String
    Public Property Datos As JObject
End Class
