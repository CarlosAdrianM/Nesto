Imports System.Threading.Tasks
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta.Models.Facturas

Namespace Services
    ''' <summary>
    ''' Nesto#615 (NestoAPI#810): núcleo común de todo lo que factura por rutas (popup de Facturar rutas y FacturadorPedido, que
    ''' usan Agencias y el detalle del pedido). Por cada envío del reparto propio con el reembolso desfasado de lo facturado
    ''' PREGUNTA (decisión de Carlos 04/11: no se cambia solo) y, si se acepta, llama a PUT api/EnviosAgencias/{id}/Reembolso
    ''' y enseña el resultado. Si se rechaza, no toca nada. Nunca lanza: un error se enseña y se sigue con el siguiente.
    ''' </summary>
    Public Class ResolutorReembolsosDesfasados
        Public Const TITULO As String = "Reembolso del envío"

        Private ReadOnly _servicio As IServicioReembolsoEnvios
        Private ReadOnly _dialogService As IServicioDialogos

        Public Sub New(servicio As IServicioReembolsoEnvios, dialogService As IServicioDialogos)
            _servicio = servicio
            _dialogService = dialogService
        End Sub

        Public Async Function ResolverAsync(response As FacturarRutasResponseDTO) As Task
            Dim desfasados = If(response?.ReembolsosDesfasados, New List(Of ReembolsoDesfasadoDTO)).Where(Function(d) d IsNot Nothing).ToList()
            For Each desfasado In desfasados
                If Not _dialogService.ShowConfirmationAnswer(TITULO, Pregunta(desfasado)) Then
                    Continue For
                End If
                Dim resultado As ReembolsoEnvioCambiadoDTO
                Try
                    resultado = Await _servicio.CambiarReembolso(desfasado.Envio, New CambiarReembolsoEnvioDTO With {
                        .Reembolso = desfasado.ReembolsoFacturado,
                        .Observaciones = $"Ajustado al facturar el pedido {desfasado.Pedido}"
                    })
                Catch ex As Exception
                    _dialogService.ShowError($"No se ha podido actualizar el reembolso del envío RP{desfasado.Envio}: {ex.Message}")
                    Continue For
                End Try
                _dialogService.ShowNotification(TITULO, TextoResultado(desfasado, resultado))
            Next
        End Function

        Public Shared Function Pregunta(desfasado As ReembolsoDesfasadoDTO) As String
            Return $"El envío RP{desfasado.Envio} del pedido {desfasado.Pedido} lleva un reembolso de {desfasado.ReembolsoActual:C} " &
                $"y lo facturado es {desfasado.ReembolsoFacturado:C}. ¿Actualizar el reembolso del envío?"
        End Function

        Friend Shared Function TextoResultado(desfasado As ReembolsoDesfasadoDTO, resultado As ReembolsoEnvioCambiadoDTO) As String
            If resultado Is Nothing Then
                Return $"Se ha actualizado el reembolso del envío RP{desfasado.Envio} a {desfasado.ReembolsoFacturado:C}."
            End If
            If Not resultado.Cambiado Then
                Return $"El envío RP{desfasado.Envio} ya llevaba un reembolso de {resultado.Reembolso:C}: no se ha cambiado nada."
            End If
            Return $"El envío RP{desfasado.Envio} lleva ahora un reembolso de {resultado.Reembolso:C} (antes {resultado.ReembolsoAnterior:C})."
        End Function
    End Class
End Namespace
