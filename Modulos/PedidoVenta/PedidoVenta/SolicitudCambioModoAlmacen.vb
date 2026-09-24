Imports ControlesUsuario.Dialogs
Imports Nesto.Models
Imports Prism.Services.Dialogs

''' <summary>
''' Nesto#489 / NestoAPI#533: cuando la API no deja cambiar el modo de entrega porque el pedido ya tiene picking
''' (o ha salido parte hoy), se enseña su motivo y se ofrece «Pedir el cambio a almacén». Si el usuario acepta,
''' la API manda el correo a almacén y se enseña lo que conteste. Nunca se promete que se pueda: lo decide almacén.
''' Compartido por el detalle de pedido y la plantilla (al modificar un pedido).
''' </summary>
Public NotInheritable Class SolicitudCambioModoAlmacen
    Private Sub New()
    End Sub

    Public Const TITULO As String = "Pedir el cambio a almacén"

    Friend Shared Function TextoPregunta(motivo As String, modoDeseado As Byte) As String
        Return motivo & vbCrLf & vbCrLf &
            $"¿Quieres pedirle a almacén que lo cambien a «{ModosServicio.Nombre(modoDeseado)}»? " &
            "Lo intentarán, pero puede que ya no llegue a tiempo."
    End Function

    ''' <summary>
    ''' Enseña el motivo, pregunta y, si acepta, pide el cambio. Devuelve True si se llegó a pedir (la API lo aceptó).
    ''' </summary>
    Public Shared Async Function OfrecerAsync(dialogService As IDialogService, servicio As IPedidoVentaService,
                                              empresa As String, pedido As Integer, modoDeseado As Byte,
                                              motivo As String) As Task(Of Boolean)
        Dim pedir As Boolean = Await dialogService.ShowConfirmationAsync(TITULO, TextoPregunta(motivo, modoDeseado))
        If Not pedir Then
            Return False
        End If
        Try
            Dim respuesta As String = Await servicio.SolicitarCambioModo(empresa?.Trim(), pedido, modoDeseado, Nothing)
            dialogService.ShowNotification(TITULO, respuesta)
            Return True
        Catch ex As Exception
            dialogService.ShowError(ex.Message)
            Return False
        End Try
    End Function
End Class
