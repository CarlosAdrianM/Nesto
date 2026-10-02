Imports System.Threading.Tasks
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta.Models.Facturas

Namespace Services
    ''' <summary>
    ''' NestoAPI#592: factura UN pedido por EXACTAMENTE el mismo camino que la
    ''' facturación de rutas (POST api/FacturacionRutas/FacturarPedido → GestorFacturacionRutas): visto bueno,
    ''' notas de entrega (estado -2 y baja de stock de lo «de carpeta»), albarán, traspaso, factura según el
    ''' periodo y documentos para imprimir. Si un pedido se factura aquí ya no lo coge la facturación de rutas,
    ''' y al revés. Antes Agencias llamaba por su cuenta a CrearAlbaran + CrearFactura, y con una nota de
    ''' entrega el albarán daba «El pedido es nota de entrega».
    ''' Imprime con ServicioImpresionDocumentos.ImprimirDocumentos, lo mismo que el popup de Facturar rutas.
    ''' Lo usan «Facturar al imprimir etiqueta» (Agencias) y, con las notas de entrega, los botones de albarán del
    ''' detalle del pedido (el albarán de una nota de entrega daba «El pedido es nota de entrega»).
    ''' </summary>
    Public Class FacturadorPedido
        Private ReadOnly _servicioFacturacion As IServicioFacturacionRutas
        Private ReadOnly _servicioImpresion As IServicioImpresionDocumentos
        Private ReadOnly _dialogService As IServicioDialogos

        Public Sub New(servicioFacturacion As IServicioFacturacionRutas, servicioImpresion As IServicioImpresionDocumentos, dialogService As IServicioDialogos)
            _servicioFacturacion = servicioFacturacion
            _servicioImpresion = servicioImpresion
            _dialogService = dialogService
        End Sub

        Public Async Function FacturarAsync(empresa As String, pedido As Integer, imprimirDocumentos As Boolean) As Task
            Dim response As FacturarRutasResponseDTO = Await _servicioFacturacion.FacturarPedido(empresa, pedido)
            If response Is Nothing Then
                Throw New InvalidOperationException($"La API no ha devuelto el resultado de facturar el pedido {pedido}")
            End If

            ' NestoAPI#327: los avisos de facturación (p. ej. NIF no registrado en la AEAT) le tienen que
            ' saltar al que factura.
            For Each factura In If(response.Facturas, New List(Of FacturaCreadaDTO))
                For Each aviso In If(factura.Avisos, New List(Of String))
                    _dialogService.ShowError(aviso)
                Next
            Next

            Dim errores = If(response.PedidosConErrores, New List(Of PedidoConErrorDTO))
            Dim graves = errores.Where(Function(e) e.Severidad <> NivelSeveridad.Warning).ToList()
            Dim leves = errores.Where(Function(e) e.Severidad = NivelSeveridad.Warning).ToList()
            If graves.Any() Then
                _dialogService.ShowError(String.Join(Environment.NewLine, graves.Select(Function(e) $"{e.TipoError}: {e.MensajeError}")))
            End If
            If leves.Any() Then
                _dialogService.ShowNotification("Aviso", String.Join(Environment.NewLine, leves.Select(Function(e) e.MensajeError)))
            End If

            If imprimirDocumentos AndAlso HayAlgoParaImprimir(response) Then
                Try
                    Dim unused = Await _servicioImpresion.ImprimirDocumentos(response)
                Catch ex As Exception
                    _dialogService.ShowError($"Error al imprimir documento: {ex.Message}")
                End Try
            End If

            Dim resumen = TextoResumen(response, pedido)
            If Not String.IsNullOrEmpty(resumen) Then
                _dialogService.ShowNotification("Facturación", resumen)
            End If
        End Function

        Private Shared Function HayAlgoParaImprimir(response As FacturarRutasResponseDTO) As Boolean
            Return If(response.Facturas, New List(Of FacturaCreadaDTO)).Any(Function(f) f.DatosImpresion IsNot Nothing) OrElse
                If(response.Albaranes, New List(Of AlbaranCreadoDTO)).Any(Function(a) a.DatosImpresion IsNot Nothing) OrElse
                If(response.NotasEntrega, New List(Of NotaEntregaCreadaDTO)).Any(Function(n) n.DatosImpresion IsNot Nothing)
        End Function

        Friend Shared Function TextoResumen(response As FacturarRutasResponseDTO, pedido As Integer) As String
            Dim lineas As New List(Of String)
            Dim albaranes = If(response.Albaranes, New List(Of AlbaranCreadoDTO)).Select(Function(a) a.NumeroAlbaran.ToString()).ToList()
            Dim facturas = If(response.Facturas, New List(Of FacturaCreadaDTO)).Select(Function(f) f.NumeroFactura).ToList()
            If facturas.Any() Then
                lineas.Add($"Pedido {pedido} facturado en la factura {String.Join(", ", facturas)}" &
                           If(albaranes.Any(), $" (albarán {String.Join(", ", albaranes)})", ""))
            ElseIf albaranes.Any() Then
                lineas.Add($"Albarán del pedido {pedido} creado correctamente en albarán {String.Join(", ", albaranes)}")
            End If
            If If(response.NotasEntrega, New List(Of NotaEntregaCreadaDTO)).Any() Then
                lineas.Add($"Nota de entrega del pedido {pedido} procesada")
            End If
            Return String.Join(Environment.NewLine, lineas)
        End Function
    End Class
End Namespace
