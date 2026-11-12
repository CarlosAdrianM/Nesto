Imports System.Globalization

''' <summary>
''' NestoAPI#858 (punto 4): un pedido «todo junto» retenido en el packing porque le falta un producto de pago. Es la fila de
''' GET api/PedidosVenta/RetenidosPorFalta y la respuesta de POST …/{empresa}/{pedido}/RetencionPorFalta/Decidir.
''' </summary>
Public Class RetencionPorFaltaModel
    Public Property Id As Integer
    Public Property Empresa As String
    Public Property Pedido As Integer
    Public Property Picking As Integer
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property NombreCliente As String
    Public Property Productos As String
    Public Property Fecha As Date
    Public Property Plazo As Date
    ''' <summary>Pendiente, ServirLoQueHay o Esperar.</summary>
    Public Property Estado As String
    Public Property UsuarioDecision As String
    Public Property FechaDecision As Date?
    Public Property PorPlazo As Boolean
    ''' <summary>Quien pregunta puede decidir (quien hizo el pedido, su vendedor o Almacén, Administración, Dirección e Informática).</summary>
    Public Property PuedeDecidir As Boolean
    Public Property Mensaje As String
End Class

''' <summary>
''' NestoAPI#858 (punto 4): lo que enseña Nesto del pedido retenido por una falta. La regla de verdad (quién decide, el plazo,
''' qué pasa con el picking) es de la API; aquí solo los textos y cuál de la lista es el pedido en pantalla.
''' </summary>
Public NotInheritable Class ReglasRetencionPorFalta
    Private Sub New()
    End Sub

    Public Const ESTADO_PENDIENTE As String = "Pendiente"
    Public Const DECISION_SERVIR_LO_QUE_HAY As String = "ServirLoQueHay"
    Public Const DECISION_ESPERAR As String = "Esperar"

    Public Const TITULO_SERVIR_LO_QUE_HAY As String = "Servir lo que hay"
    Public Const TITULO_ESPERAR As String = "Esperar a tenerlo completo"
    Public Const TEXTO_NO_PUEDE_DECIDIR As String = "Solo puede decidirlo quien hizo el pedido, su vendedor o Almacén, Administración, Dirección e Informática."

    Private Shared ReadOnly Castellano As CultureInfo = CultureInfo.GetCultureInfo("es-ES")

    ''' <summary>
    ''' La retención pendiente de ese pedido, o Nothing. La lista de la API ya trae solo las pendientes; aun así se descartan las
    ''' que vengan decididas.
    ''' </summary>
    Public Shared Function DelPedido(lista As IEnumerable(Of RetencionPorFaltaModel), empresa As String, numero As Integer) As RetencionPorFaltaModel
        If lista Is Nothing OrElse numero <= 0 Then
            Return Nothing
        End If
        Dim empresaPedido As String = If(empresa?.Trim(), String.Empty)
        Return lista.FirstOrDefault(Function(r) r IsNot Nothing AndAlso r.Pedido = numero AndAlso
                                        (String.IsNullOrWhiteSpace(r.Empresa) OrElse String.IsNullOrEmpty(empresaPedido) OrElse r.Empresa.Trim() = empresaPedido) AndAlso
                                        EstaPendiente(r))
    End Function

    Public Shared Function EstaPendiente(retencion As RetencionPorFaltaModel) As Boolean
        Return retencion IsNot Nothing AndAlso
            (String.IsNullOrWhiteSpace(retencion.Estado) OrElse String.Equals(retencion.Estado.Trim(), ESTADO_PENDIENTE, StringComparison.OrdinalIgnoreCase))
    End Function

    ''' <summary>
    ''' Solo puede estar retenido un pedido con algo ya en un picking y sin albarán: los demás no se le preguntan a la API
    ''' (así no se hace una llamada más al abrir cualquier pedido).
    ''' </summary>
    Public Shared Function PuedeEstarRetenido(pedido As Nesto.Models.PedidoVentaDTO) As Boolean
        Return pedido IsNot Nothing AndAlso pedido.numero > 0 AndAlso pedido.Lineas IsNot Nothing AndAlso
            pedido.Lineas.Any(Function(l) l IsNot Nothing AndAlso l.picking > 0 AndAlso l.estado < 2)
    End Function

    ''' <summary>«Pedido retenido en el almacén: falta 12345. Decida antes de las 12:30 del 13/11 o se esperará a tenerlo completo.»</summary>
    Public Shared Function Texto(retencion As RetencionPorFaltaModel) As String
        If retencion Is Nothing Then
            Return String.Empty
        End If
        Dim productos As String = If(String.IsNullOrWhiteSpace(retencion.Productos), "un producto", retencion.Productos.Trim())
        Return $"Pedido retenido en el almacén: falta {productos}. Decida antes de las {retencion.Plazo.ToString("HH:mm", Castellano)} " &
            $"del {retencion.Plazo.ToString("dd/MM", Castellano)} o se esperará a tenerlo completo."
    End Function

    Public Shared Function TextoConfirmacionServir(retencion As RetencionPorFaltaModel) As String
        Dim productos As String = If(String.IsNullOrWhiteSpace(retencion?.Productos), "lo que falta", retencion.Productos.Trim())
        Return $"¿Servir el pedido {retencion?.Pedido} sin {productos}?" & vbCrLf & vbCrLf &
            "El almacén empaquetará y enviará lo que ya tiene preparado. Lo que falta se queda pendiente en el pedido y se " &
            "servirá aparte cuando haya stock."
    End Function

    Public Shared Function TextoConfirmacionEsperar(retencion As RetencionPorFaltaModel) As String
        Dim productos As String = If(String.IsNullOrWhiteSpace(retencion?.Productos), "lo que falta", retencion.Productos.Trim())
        Return $"¿Esperar a tener el pedido {retencion?.Pedido} completo?" & vbCrLf & vbCrLf &
            "El pedido sale del picking: lo que ya estaba preparado vuelve a colocarse en el almacén y no se envía nada. " &
            $"Saldrá todo junto cuando llegue {productos}."
    End Function
End Class
