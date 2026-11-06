Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models

''' <summary>
''' Nesto#629 (NestoAPI#722): al aplazar un pedido cuyo envío ya tiene la agencia y esta no admite el cambio
''' (PEDIDO_YA_EN_AGENCIA), se enseña el motivo de la API y, si lo permite, el botón «Mandar correo a logística». Si lo
''' pulsa, se pide un comentario opcional, la API manda el correo (con copia al vendedor) y se enseña lo que conteste.
''' El pedido no se ha guardado: se queda en pantalla con sus cambios. Nunca se promete que se pueda aplazar.
''' </summary>
Public NotInheritable Class AvisoLogisticaAplazamiento
    Private Sub New()
    End Sub

    Public Const TITULO As String = "El pedido ya lo tiene la agencia"
    Public Const TEXTO_BOTON_CORREO As String = "Mandar correo a logística"
    Public Const TEXTO_BOTON_CERRAR As String = "Cerrar"
    Public Const TITULO_COMENTARIO As String = "Mandar correo a logística"
    Public Const TEXTO_COMENTARIO As String = "Si quiere, añada un comentario para logística (opcional):"

    ''' <summary>
    ''' Enseña el motivo y, si se puede avisar a logística, ofrece el botón. Devuelve True si se llegó a mandar el correo.
    ''' </summary>
    Public Shared Async Function OfrecerAsync(dialogService As IServicioDialogos, servicio As IPedidoVentaService,
                                              empresa As String, pedido As Integer,
                                              rechazo As PedidoYaEnAgenciaException) As Task(Of Boolean)
        If rechazo Is Nothing Then
            Return False
        End If
        If Not rechazo.PuedeAvisarLogistica OrElse Not rechazo.FechaEntrega.HasValue Then
            dialogService.ShowError(rechazo.Message)
            Return False
        End If

        Dim mandar As Boolean = ConfirmacionConBotones.Preguntar(dialogService, TITULO, rechazo.Message, TEXTO_BOTON_CORREO, TEXTO_BOTON_CERRAR)
        If Not mandar Then
            Return False
        End If
        Dim comentario As String = dialogService.GetText(TITULO_COMENTARIO, TEXTO_COMENTARIO)
        If comentario Is Nothing Then
            Return False ' ha cancelado el cuadro del comentario: no se manda nada
        End If
        Try
            Dim respuesta As String = Await servicio.AvisarLogisticaAplazamiento(empresa?.Trim(), pedido, rechazo.FechaEntrega.Value,
                                                                                  rechazo.Envio, comentario)
            dialogService.ShowNotification(TEXTO_BOTON_CORREO, respuesta)
            Return True
        Catch ex As Exception
            dialogService.ShowError("No se ha mandado el correo a logística:" & vbCrLf & ex.Message)
            Return False
        End Try
    End Function
End Class
