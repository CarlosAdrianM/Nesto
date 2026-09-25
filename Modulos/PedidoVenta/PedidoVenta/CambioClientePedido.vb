Imports Nesto.Models

''' <summary>
''' Nesto#496 / NestoAPI#519: pasar a otro cliente un pedido que todavía no ha salido. Aquí solo lo que decide
''' la pantalla (cuándo enseñar el botón y qué se pregunta y se cuenta); la regla de verdad, y el recálculo,
''' son de la API.
''' </summary>
Public NotInheritable Class CambioClientePedido
    Private Sub New()
    End Sub

    Public Const TITULO As String = "Cambiar cliente"

    Private Const ESTADO_EN_CURSO As Short = 1

    ''' <summary>
    ''' Si tiene sentido ofrecer el cambio: pedido ya guardado, que no es nota de entrega, sin líneas con picking,
    ''' albarán o factura y sin prepagos ni efectos a mano. La API lo vuelve a comprobar (y mira también los
    ''' envíos de agencia y los cobros con tarjeta, que la pantalla no tiene).
    ''' </summary>
    Public Shared Function PuedeCambiarse(pedido As PedidoVentaDTO) As Boolean
        If pedido Is Nothing OrElse pedido.numero = 0 OrElse String.IsNullOrWhiteSpace(pedido.cliente) OrElse pedido.notaEntrega Then
            Return False
        End If
        If pedido.Lineas IsNot Nothing AndAlso pedido.Lineas.Any(Function(l) l.picking <> 0 OrElse l.estado > ESTADO_EN_CURSO OrElse
                                                                  l.yaFacturado OrElse l.Albaran.HasValue OrElse
                                                                  Not String.IsNullOrWhiteSpace(l.Factura)) Then
            Return False
        End If
        If pedido.Prepagos IsNot Nothing AndAlso pedido.Prepagos.Any(Function(p) String.IsNullOrWhiteSpace(p.Factura)) Then
            Return False
        End If
        If pedido.CrearEfectosManualmente AndAlso pedido.Efectos IsNot Nothing AndAlso pedido.Efectos.Any() Then
            Return False
        End If
        Return True
    End Function

    Public Shared Function TextoConfirmacion(numero As Integer, clienteActual As String, contactoActual As String,
                                             clienteNuevo As String, contactoNuevo As String, nombreNuevo As String,
                                             hayCambiosSinGuardar As Boolean) As String
        Dim destino As String = $"{clienteNuevo?.Trim()}/{If(String.IsNullOrWhiteSpace(contactoNuevo), "principal", contactoNuevo.Trim())}"
        If Not String.IsNullOrWhiteSpace(nombreNuevo) Then
            destino &= $" ({nombreNuevo.Trim()})"
        End If
        Dim texto As String = $"¿Pasar el pedido {numero} del cliente {clienteActual?.Trim()}/{contactoActual?.Trim()} al cliente {destino}?" & vbCrLf & vbCrLf &
            "Se recalcula como si el pedido fuera nuevo para ese cliente: dirección, forma y plazos de pago, CCC, IVA, vendedor, ruta, " &
            "precios de las líneas sin oferta y portes."
        If hayCambiosSinGuardar Then
            texto &= vbCrLf & vbCrLf & "OJO: hay cambios sin guardar en el pedido y se van a perder."
        End If
        Return texto
    End Function

    Public Shared Function TextoResultado(respuesta As CambiarClientePedidoRespuestaModel) As String
        If respuesta Is Nothing Then
            Return "Cliente cambiado."
        End If
        Dim texto As String = $"El pedido {respuesta.Numero} ha pasado del cliente {respuesta.ClienteAnterior}/{respuesta.ContactoAnterior} al {respuesta.Cliente}/{respuesta.Contacto}."
        If respuesta.Cambios IsNot Nothing AndAlso respuesta.Cambios.Any() Then
            texto &= vbCrLf & vbCrLf & String.Join(vbCrLf, respuesta.Cambios.Select(Function(c) "• " & c))
        End If
        Return texto
    End Function
End Class

''' <summary>NestoAPI#519: respuesta de POST PedidosVenta/{empresa}/{numero}/CambiarCliente.</summary>
Public Class CambiarClientePedidoRespuestaModel
    Public Property Empresa As String
    Public Property Numero As Integer
    Public Property ClienteAnterior As String
    Public Property ContactoAnterior As String
    Public Property Cliente As String
    Public Property Contacto As String
    Public Property Cambios As List(Of String) = New List(Of String)
End Class
