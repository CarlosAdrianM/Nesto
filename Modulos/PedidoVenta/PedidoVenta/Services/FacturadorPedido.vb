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
        Private ReadOnly _resolutorReembolsos As ResolutorReembolsosDesfasados

        Public Sub New(servicioFacturacion As IServicioFacturacionRutas, servicioImpresion As IServicioImpresionDocumentos, dialogService As IServicioDialogos)
            Me.New(servicioFacturacion, servicioImpresion, dialogService, Nothing)
        End Sub

        ''' <param name="resolutorReembolsos">Nesto#615: pregunta por los envíos del reparto propio con el reembolso desfasado
        ''' de lo facturado. Nothing = no se pregunta.</param>
        Public Sub New(servicioFacturacion As IServicioFacturacionRutas, servicioImpresion As IServicioImpresionDocumentos, dialogService As IServicioDialogos,
                       resolutorReembolsos As ResolutorReembolsosDesfasados)
            _servicioFacturacion = servicioFacturacion
            _servicioImpresion = servicioImpresion
            _dialogService = dialogService
            _resolutorReembolsos = resolutorReembolsos
        End Sub

        Public Function FacturarAsync(empresa As String, pedido As Integer, imprimirDocumentos As Boolean) As Task
            Return FacturarConAsync(Function(saltarPacking) If(saltarPacking,
                                        _servicioFacturacion.FacturarPedido(empresa, pedido, saltarPacking:=True),
                                        _servicioFacturacion.FacturarPedido(empresa, pedido)),
                                    pedido, imprimirDocumentos)
        End Function

        ''' <summary>
        ''' NestoAPI#828: «Facturar al imprimir etiqueta» de Agencias con el mismo núcleo que F8 en Ariadna
        ''' (POST api/FacturacionRutas/FacturarEtiqueta): el papel del pedido se imprime aquí, para meterlo en la caja, y se
        ''' confirma a la API (Facturar rutas lo cuenta como «ya impreso en el packing», salvo en la ruta propia). Desde NestoAPI
        ''' 6698e846 (10/11/26) la ruta propia también se factura e imprime aquí (no el reparto propio de otro día o aplazado, ni
        ''' las notas, ni los retenidos). Si no sale, queda pendiente: repetir la etiqueta lo vuelve a imprimir sin facturar.
        ''' </summary>
        Public Async Function FacturarEtiquetaAsync(empresa As String, pedido As Integer, envio As Integer?, imprimirDocumentos As Boolean) As Task
            Await FacturarConAsync(Async Function(saltarPacking) As Task(Of FacturarRutasResponseDTO)
                                       Return Await _servicioFacturacion.FacturarEtiqueta(empresa, pedido, envio, saltarPacking)
                                   End Function,
                                   pedido, imprimirDocumentos)
        End Function

        Private Async Function FacturarConAsync(facturar As Func(Of Boolean, Task(Of FacturarRutasResponseDTO)), pedido As Integer, imprimirDocumentos As Boolean) As Task
            Dim response As FacturarRutasResponseDTO = Await facturar(False)
            If response Is Nothing Then
                Throw New InvalidOperationException($"La API no ha devuelto el resultado de facturar el pedido {pedido}")
            End If

            ' Nesto#586 / NestoAPI#765: una nota de entrega que no ha pasado por el packing no se procesa sin preguntar.
            If EsNotaSinPacking(response) Then
                If Not _dialogService.ShowConfirmationAnswer("Nota de entrega", PREGUNTA_SALTAR_PACKING) Then
                    Return
                End If
                response = Await facturar(True)
                If response Is Nothing Then
                    Throw New InvalidOperationException($"La API no ha devuelto el resultado de facturar el pedido {pedido}")
                End If
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

            Dim seHaImpreso As Boolean = False
            Dim resultadoImpresion As ResultadoImpresion = Nothing
            Dim falloImpresion As String = Nothing
            If imprimirDocumentos AndAlso HayAlgoParaImprimir(response) Then
                seHaImpreso = True
                Try
                    resultadoImpresion = Await _servicioImpresion.ImprimirDocumentos(response)
                Catch ex As Exception
                    falloImpresion = ex.Message
                    _dialogService.ShowError($"Error al imprimir documento: {ex.Message}")
                End Try
            End If

            ' NestoAPI#828: lo que ha salido (o no) del papel de la etiqueta se apunta en la API
            Dim etiqueta = TryCast(response, FacturarEtiquetaResponseDTO)
            Dim confirmaciones As New List(Of ImpresionDocumentoEnPapelDTO)
            If etiqueta IsNot Nothing Then
                confirmaciones = ImpresionesAConfirmar(etiqueta, seHaImpreso, resultadoImpresion, falloImpresion)
                For Each confirmacion In confirmaciones
                    Try
                        Await _servicioFacturacion.ConfirmarImpresionPapel(confirmacion)
                    Catch ex As Exception
                        ' Sin la marca, Facturar rutas no lo cuenta como impreso en el packing; la factura y el papel ya están.
                        System.Diagnostics.Debug.WriteLine($"NestoAPI#828: no se ha podido apuntar la impresión de {confirmacion.Documento}: {ex.Message}")
                    End Try
                Next
            End If

            Dim resumen = TextoResumen(response, pedido)
            If etiqueta IsNot Nothing Then
                resumen = String.Join(Environment.NewLine, {resumen, TextoPapelEtiqueta(etiqueta, confirmaciones, seHaImpreso)}.
                                      Where(Function(t) Not String.IsNullOrWhiteSpace(t)))
            End If
            If Not String.IsNullOrEmpty(resumen) Then
                _dialogService.ShowNotification("Facturación", resumen)
            End If

            ' Nesto#615 (NestoAPI#810): el envío del reparto propio que lleva un reembolso distinto de lo facturado.
            If _resolutorReembolsos IsNot Nothing Then
                Await _resolutorReembolsos.ResolverAsync(response)
            End If
        End Function

        ''' <summary>
        ''' NestoAPI#828: lo que hay que apuntar en la API del papel de la etiqueta: cada documento pendiente que Nesto ha intentado
        ''' imprimir, impreso o no (con el motivo). Si no se ha imprimido nada (la casilla de imprimir desmarcada), nada: sigue pendiente.
        ''' </summary>
        Friend Shared Function ImpresionesAConfirmar(etiqueta As FacturarEtiquetaResponseDTO, seHaImpreso As Boolean, resultado As ResultadoImpresion,
                                                     falloImpresion As String) As List(Of ImpresionDocumentoEnPapelDTO)
            Dim lista As New List(Of ImpresionDocumentoEnPapelDTO)
            If Not seHaImpreso OrElse etiqueta?.DocumentosEnPapel Is Nothing Then
                Return lista
            End If
            For Each documento In etiqueta.DocumentosEnPapel.Where(Function(d) d IsNot Nothing AndAlso d.EstaPendiente)
                Dim errorDocumento = If(resultado?.Errores, New List(Of ErrorImpresion)).
                    FirstOrDefault(Function(e) String.Equals(e?.NumeroDocumento?.Trim(), documento.Documento?.Trim(), StringComparison.OrdinalIgnoreCase))
                Dim impreso As Boolean = falloImpresion Is Nothing AndAlso errorDocumento Is Nothing
                lista.Add(New ImpresionDocumentoEnPapelDTO With {
                    .Empresa = documento.Empresa,
                    .Pedido = documento.Pedido,
                    .Envio = documento.Envio,
                    .Tipo = documento.Tipo,
                    .Documento = documento.Documento,
                    .Impreso = impreso,
                    .Mensaje = If(impreso, Nothing, If(errorDocumento?.MensajeError, falloImpresion))
                })
            Next
            Return lista
        End Function

        ''' <summary>
        ''' NestoAPI#828: lo que se dice del papel de la etiqueta: los avisos de la API, «Meta en la caja la factura …» si ha salido y,
        ''' si no, que queda pendiente y que repetir la etiqueta lo vuelve a imprimir sin facturar.
        ''' </summary>
        Friend Shared Function TextoPapelEtiqueta(etiqueta As FacturarEtiquetaResponseDTO, confirmaciones As List(Of ImpresionDocumentoEnPapelDTO),
                                                  seHaImpreso As Boolean) As String
            Dim lineas As New List(Of String)
            lineas.AddRange(If(etiqueta.AvisosPapel, New List(Of String)).Where(Function(a) Not String.IsNullOrWhiteSpace(a)).Select(Function(a) a.Trim()))
            Dim documentos = If(etiqueta.DocumentosEnPapel, New List(Of DocumentoEnPapelDTO)).Where(Function(d) d IsNot Nothing).ToList()
            Dim enLaCaja = documentos.
                Where(Function(d) (Not d.EstaPendiente AndAlso Not d.YaEstabaImpreso) OrElse
                                  If(confirmaciones, New List(Of ImpresionDocumentoEnPapelDTO)).Any(Function(c) c.Impreso AndAlso c.Documento = d.Documento)).
                Select(Function(d) Descripcion(d)).ToList()
            If enLaCaja.Any() Then
                lineas.Add($"Meta en la caja {String.Join(" y ", enLaCaja)}.")
            End If
            Dim sinSalir = documentos.
                Where(Function(d) d.EstaPendiente AndAlso Not If(confirmaciones, New List(Of ImpresionDocumentoEnPapelDTO)).Any(Function(c) c.Impreso AndAlso c.Documento = d.Documento)).
                Select(Function(d) Descripcion(d)).ToList()
            If sinSalir.Any() Then
                lineas.Add($"{Mayuscula(String.Join(" y ", sinSalir))} {If(sinSalir.Count = 1, "lleva", "llevan")} papel y no {If(sinSalir.Count = 1, "ha salido", "han salido")}" &
                           $"{If(seHaImpreso, String.Empty, " (no se ha mandado a imprimir)")}: queda pendiente. Repita la etiqueta para imprimirlo sin volver a facturar.")
            End If
            Return String.Join(Environment.NewLine, lineas)
        End Function

        Private Shared Function Descripcion(documento As DocumentoEnPapelDTO) As String
            If Not String.IsNullOrWhiteSpace(documento.Descripcion) Then
                Return documento.Descripcion.Trim()
            End If
            Select Case documento.Tipo
                Case DocumentoEnPapelDTO.TIPO_ALBARAN
                    Return $"el albarán {documento.Documento?.Trim()}"
                Case DocumentoEnPapelDTO.TIPO_NOTA_ENTREGA
                    Return $"la nota de entrega del pedido {documento.Documento?.Trim()}"
                Case Else
                    Return $"la factura {documento.Documento?.Trim()}"
            End Select
        End Function

        Private Shared Function Mayuscula(texto As String) As String
            Return If(String.IsNullOrEmpty(texto), texto, Char.ToUpper(texto(0)) & texto.Substring(1))
        End Function

        ''' <summary>Nesto#586: la pregunta antes de procesar una nota de entrega que no ha pasado por el packing.</summary>
        Public Const PREGUNTA_SALTAR_PACKING As String = "Esta nota no ha pasado por el packing: se saltará el reparto. ¿Continúa?"

        Friend Shared Function EsNotaSinPacking(response As FacturarRutasResponseDTO) As Boolean
            Return If(response?.PedidosConErrores, New List(Of PedidoConErrorDTO)).
                Any(Function(e) String.Equals(e?.Codigo, CodigosErrorFacturacion.NOTA_SIN_PACKING, StringComparison.OrdinalIgnoreCase))
        End Function

        ''' <summary>
        ''' ¿Queda algo que imprima Nesto? Nesto#620 (NestoAPI#806): lo que la API ya ha impreso en el servidor no cuenta (misma regla
        ''' que Facturar rutas: FacturarRutasResponseDTO.TotalDocumentosParaImprimir).
        ''' </summary>
        Friend Shared Function HayAlgoParaImprimir(response As FacturarRutasResponseDTO) As Boolean
            Return response IsNot Nothing AndAlso response.TotalDocumentosParaImprimir > 0
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
            ' Nesto#620 (NestoAPI#806): lo impreso en el servidor no lo imprime Nesto; se dice dónde ha salido para que lo recojan.
            Dim impresosEnServidor = response.TextoImpresosEnServidor
            If Not String.IsNullOrEmpty(impresosEnServidor) Then
                lineas.Add(impresosEnServidor)
            End If
            Return String.Join(Environment.NewLine, lineas)
        End Function
    End Class
End Namespace
