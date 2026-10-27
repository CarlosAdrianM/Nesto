Imports System.Globalization
Imports Nesto.Models

''' <summary>
''' NestoAPI#698: retener un pedido hasta nuevo aviso (en vez de poner la fecha de entrega un año más tarde). Aquí solo lo
''' que decide la pantalla (cuándo se ofrecen los botones y qué se enseña); la regla de verdad es de la API (POST
''' PedidosVenta/{empresa}/{numero}/Retener y /Liberar), que vuelve a comprobarlo todo.
''' </summary>
Public NotInheritable Class RetencionPedido
    Private Sub New()
    End Sub

    Public Const TITULO_RETENER As String = "Retener el pedido"
    Public Const TITULO_LIBERAR As String = "Liberar el pedido"

    Private Const ESTADO_PENDIENTE As Short = -1
    Private Const ESTADO_EN_CURSO As Short = 1

    Public Shared Function EstaRetenido(pedido As PedidoVentaDTO) As Boolean
        Return pedido IsNot Nothing AndAlso (pedido.retencion IsNot Nothing OrElse ModosServicio.EsRetenido(pedido.modoServicio))
    End Function

    ''' <summary>
    ''' Se ofrece «Retener…» en un pedido ya guardado, que no sea presupuesto ni nota de entrega, que no esté ya retenido y
    ''' que tenga algo pendiente de servir (líneas pendientes o en curso).
    ''' </summary>
    Public Shared Function PuedeRetenerse(pedido As PedidoVentaDTO) As Boolean
        If pedido Is Nothing OrElse pedido.numero = 0 OrElse pedido.EsPresupuesto OrElse pedido.notaEntrega OrElse EstaRetenido(pedido) Then
            Return False
        End If
        Return pedido.Lineas IsNot Nothing AndAlso pedido.Lineas.Any(Function(l) l.estado >= ESTADO_PENDIENTE AndAlso l.estado <= ESTADO_EN_CURSO)
    End Function

    ''' <summary>«Retenido por Alfredo desde 22/10/2026 hasta 15/11/2026: motivo» (o «… hasta nuevo aviso: motivo»).</summary>
    Public Shared Function Texto(retencion As RetencionPedidoDTO) As String
        If retencion Is Nothing Then
            Return "Pedido retenido hasta nuevo aviso."
        End If
        Dim usuario As String = QuitarDominio(retencion.Usuario)
        Dim resultado As String = "Retenido"
        If Not String.IsNullOrWhiteSpace(usuario) Then
            resultado &= $" por {usuario}"
        End If
        If retencion.Fecha <> Date.MinValue Then
            resultado &= $" desde {Fecha(retencion.Fecha)}"
        End If
        resultado &= If(retencion.HastaFecha.HasValue, $" hasta {Fecha(retencion.HastaFecha.Value)}", " hasta nuevo aviso")
        If Not String.IsNullOrWhiteSpace(retencion.Motivo) Then
            resultado &= $": {retencion.Motivo.Trim()}"
        End If
        Return resultado
    End Function

    Private Shared Function Fecha(valor As Date) As String
        Return valor.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
    End Function

    ''' <summary>NUEVAVISION\Alfredo → Alfredo.</summary>
    Public Shared Function QuitarDominio(usuario As String) As String
        If String.IsNullOrWhiteSpace(usuario) Then
            Return String.Empty
        End If
        Dim limpio As String = usuario.Trim()
        Dim barra As Integer = limpio.LastIndexOf("\"c)
        Return If(barra >= 0, limpio.Substring(barra + 1), limpio)
    End Function

    Public Shared Function TextoConfirmacionLiberar(numero As Integer) As String
        Return $"¿Liberar el pedido {numero}? Volverá a su modo de servicio anterior y entrará en el picking."
    End Function
End Class

''' <summary>NestoAPI#698: cuerpo de POST PedidosVenta/{empresa}/{numero}/Retener.</summary>
Public Class RetenerPedidoRequest
    ''' <summary>Obligatorio.</summary>
    Public Property Motivo As String
    ''' <summary>Nothing = hasta nuevo aviso.</summary>
    Public Property HastaFecha As Date?
    Public Property Usuario As String
End Class

''' <summary>NestoAPI#698: cuerpo de POST PedidosVenta/{empresa}/{numero}/Liberar.</summary>
Public Class LiberarPedidoRequest
    Public Property Usuario As String
End Class
