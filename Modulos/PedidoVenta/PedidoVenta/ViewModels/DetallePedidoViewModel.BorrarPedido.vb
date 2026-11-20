Imports CommunityToolkit.Mvvm.Input
Imports CommunityToolkit.Mvvm.Messaging
Imports Nesto.Infrastructure.Events
Imports Nesto.Models

''' <summary>
''' Novedades 1283 (Laura): «Borrar pedido» en el menú «Más» del detalle, como en Nesto viejo. Pregunta con el número y el cliente (Intro =
''' «No»), llama a DELETE api/PedidosVenta y, si la API pregunta por las inscripciones gratuitas a eventos que justifica el pedido, lo
''' pregunta y repite con anularInscripcionesEventos. Si la API no lo deja borrar (picking, albarán, factura, envío, prepago…) se enseña
''' su mensaje tal cual. Al terminar avisa a las listas de pedidos (<see cref="PedidoBorradoMensaje"/>): lo quitan y cierran el detalle.
''' </summary>
Partial Public Class DetallePedidoViewModel

    Public Const TITULO_BORRAR_PEDIDO As String = "Borrar pedido"

    Public Property BorrarPedidoCommand As AsyncRelayCommand

    Private _estaBorrandoPedido As Boolean

    ''' <summary>Cualquiera que pueda modificar el pedido (lo que no se puede borrar lo dice la API).</summary>
    Public ReadOnly Property PuedeBorrarPedido As Boolean
        Get
            Return pedido IsNot Nothing AndAlso pedido.numero > 0 AndAlso Not _estaBorrandoPedido
        End Get
    End Property

    ''' <summary>PURA. «¿Desea borrar el pedido 930001 del cliente 15191/0 (EL EDÉN)? Se borra entero y no se puede deshacer.»</summary>
    Public Shared Function PreguntaBorrarPedido(numero As Integer, cliente As String, contacto As String, nombre As String) As String
        Dim quien As String = cliente?.Trim()
        If Not String.IsNullOrWhiteSpace(contacto) Then
            quien &= "/" & contacto.Trim()
        End If
        If Not String.IsNullOrWhiteSpace(nombre) Then
            quien &= $" ({nombre.Trim()})"
        End If
        Return $"¿Desea borrar el pedido {numero} del cliente {quien}? Se borra entero, con todas sus líneas, y no se puede deshacer."
    End Function

    Friend Async Function BorrarPedidoAsync() As Task
        If Not PuedeBorrarPedido Then
            Return
        End If
        Dim empresa As String = pedido.empresa?.Trim()
        Dim numero As Integer = pedido.numero
        Dim nombre As String = If(DireccionEntregaSeleccionada?.nombre, ClienteCompleto?.nombre)
        If Not dialogService.ShowConfirmationAnswer(TITULO_BORRAR_PEDIDO, PreguntaBorrarPedido(numero, pedido.cliente, pedido.contacto, nombre), True) Then
            Return
        End If

        _estaBorrandoPedido = True
        BorrarPedidoCommand.NotifyCanExecuteChanged()
        Try
            Dim avisoInscripciones As InscripcionesEventoAnularException = Nothing
            Try
                Await servicio.BorrarPedido(empresa, numero, False)
            Catch ex As InscripcionesEventoAnularException
                avisoInscripciones = ex ' en VB no se puede esperar el diálogo dentro del Catch
            End Try
            If avisoInscripciones IsNot Nothing Then
                If Not dialogService.ShowConfirmationAnswer(InscripcionesEventoAnularException.TITULO, avisoInscripciones.Message, True) Then
                    Return ' no se borra nada
                End If
                Await servicio.BorrarPedido(empresa, numero, True)
            End If
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return
        Finally
            _estaBorrandoPedido = False
            BorrarPedidoCommand.NotifyCanExecuteChanged()
        End Try

        dialogService.ShowNotification(TITULO_BORRAR_PEDIDO, $"Se ha borrado el pedido {numero}.")
        messenger.Send(New PedidoBorradoMensaje(empresa, numero))
    End Function

End Class
