Imports System.Drawing.Printing
Imports System.IO
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports PdfiumViewer

''' <summary>
''' Implementación del servicio de impresión de documentos usando PdfiumViewer.
''' Toma bytes de PDFs desde la API y los envía directamente a la impresora.
''' </summary>
Public Class ServicioImpresionDocumentos
    Implements IServicioImpresionDocumentos

    ''' <summary>
    ''' Imprime facturas que contienen bytes del PDF.
    ''' </summary>
    Public Async Function ImprimirFacturas(facturas As IEnumerable(Of FacturaCreadaDTO)) As Task(Of ResultadoImpresion) Implements IServicioImpresionDocumentos.ImprimirFacturas
        Dim resultado As New ResultadoImpresion()

        If facturas Is Nothing OrElse Not facturas.Any() Then
            Return resultado
        End If

        ' Filtrar solo las que tienen datos de impresión
        Dim facturasParaImprimir = facturas.Where(Function(f) f.DatosImpresion IsNot Nothing).ToList()

        For Each factura In facturasParaImprimir
            Try
                ImprimirDocumento(factura.DatosImpresion.BytesPDF, factura.DatosImpresion.NumeroCopias, factura.DatosImpresion.TipoBandeja)
                resultado.DocumentosImpresos += 1
            Catch ex As Exception
                resultado.DocumentosConError += 1
                resultado.Errores.Add(New ErrorImpresion With {
                    .TipoDocumento = "Factura",
                    .NumeroDocumento = factura.NumeroFactura,
                    .MensajeError = ex.Message
                })
            End Try
        Next

        Return resultado
    End Function

    ''' <summary>
    ''' Imprime albaranes que contienen bytes del PDF.
    ''' </summary>
    Public Async Function ImprimirAlbaranes(albaranes As IEnumerable(Of AlbaranCreadoDTO)) As Task(Of ResultadoImpresion) Implements IServicioImpresionDocumentos.ImprimirAlbaranes
        Dim resultado As New ResultadoImpresion()

        If albaranes Is Nothing OrElse Not albaranes.Any() Then
            Return resultado
        End If

        ' Filtrar solo los que tienen datos de impresión
        Dim albaranesParaImprimir = albaranes.Where(Function(a) a.DatosImpresion IsNot Nothing).ToList()

        For Each albaran In albaranesParaImprimir
            Try
                ImprimirDocumento(albaran.DatosImpresion.BytesPDF, albaran.DatosImpresion.NumeroCopias, albaran.DatosImpresion.TipoBandeja)
                resultado.DocumentosImpresos += 1
            Catch ex As Exception
                resultado.DocumentosConError += 1
                resultado.Errores.Add(New ErrorImpresion With {
                    .TipoDocumento = "Albarán",
                    .NumeroDocumento = albaran.NumeroAlbaran.ToString(),
                    .MensajeError = ex.Message
                })
            End Try
        Next

        Return resultado
    End Function

    ''' <summary>
    ''' Imprime notas de entrega que contienen bytes del PDF.
    ''' </summary>
    Public Async Function ImprimirNotasEntrega(notasEntrega As IEnumerable(Of NotaEntregaCreadaDTO)) As Task(Of ResultadoImpresion) Implements IServicioImpresionDocumentos.ImprimirNotasEntrega
        Dim resultado As New ResultadoImpresion()

        If notasEntrega Is Nothing OrElse Not notasEntrega.Any() Then
            Return resultado
        End If

        ' Filtrar solo las que tienen datos de impresión
        Dim notasParaImprimir = notasEntrega.Where(Function(n) n.DatosImpresion IsNot Nothing).ToList()

        For Each nota In notasParaImprimir
            Try
                ImprimirDocumento(nota.DatosImpresion.BytesPDF, nota.DatosImpresion.NumeroCopias, nota.DatosImpresion.TipoBandeja)
                resultado.DocumentosImpresos += 1
            Catch ex As Exception
                resultado.DocumentosConError += 1
                resultado.Errores.Add(New ErrorImpresion With {
                    .TipoDocumento = "Nota de Entrega",
                    .NumeroDocumento = nota.NumeroPedido.ToString(),
                    .MensajeError = ex.Message
                })
            End Try
        Next

        Return resultado
    End Function

    ''' <summary>
    ''' Imprime todos los documentos (facturas, albaranes y notas de entrega) de una respuesta.
    ''' </summary>
    Public Async Function ImprimirDocumentos(response As FacturarRutasResponseDTO) As Task(Of ResultadoImpresion) Implements IServicioImpresionDocumentos.ImprimirDocumentos
        Dim resultadoTotal As New ResultadoImpresion()

        If response Is Nothing Then
            Return resultadoTotal
        End If

        ' Imprimir facturas
        If response.Facturas IsNot Nothing AndAlso response.Facturas.Any() Then
            Dim resultadoFacturas = Await ImprimirFacturas(response.Facturas)
            resultadoTotal.DocumentosImpresos += resultadoFacturas.DocumentosImpresos
            resultadoTotal.DocumentosConError += resultadoFacturas.DocumentosConError
            resultadoTotal.Errores.AddRange(resultadoFacturas.Errores)
        End If

        ' Imprimir albaranes
        If response.Albaranes IsNot Nothing AndAlso response.Albaranes.Any() Then
            Dim resultadoAlbaranes = Await ImprimirAlbaranes(response.Albaranes)
            resultadoTotal.DocumentosImpresos += resultadoAlbaranes.DocumentosImpresos
            resultadoTotal.DocumentosConError += resultadoAlbaranes.DocumentosConError
            resultadoTotal.Errores.AddRange(resultadoAlbaranes.Errores)
        End If

        ' Imprimir notas de entrega
        If response.NotasEntrega IsNot Nothing AndAlso response.NotasEntrega.Any() Then
            Dim resultadoNotas = Await ImprimirNotasEntrega(response.NotasEntrega)
            resultadoTotal.DocumentosImpresos += resultadoNotas.DocumentosImpresos
            resultadoTotal.DocumentosConError += resultadoNotas.DocumentosConError
            resultadoTotal.Errores.AddRange(resultadoNotas.Errores)
        End If

        Return resultadoTotal
    End Function

    ''' <summary>
    ''' Imprime un documento PDF a partir de sus bytes.
    ''' Usa PdfiumViewer para cargar el PDF y enviarlo a la impresora.
    ''' </summary>
    ''' <param name="pdfBytes">Bytes del documento PDF</param>
    ''' <param name="numeroCopias">Número de copias a imprimir</param>
    ''' <param name="tipoBandeja">Tipo estándar de bandeja (Upper, Middle, Lower, etc.)</param>
    Private Sub ImprimirDocumento(pdfBytes As Byte(), numeroCopias As Integer, tipoBandeja As TipoBandejaImpresion)
        If pdfBytes Is Nothing OrElse pdfBytes.Length = 0 Then
            Throw New ArgumentException("Los bytes del PDF están vacíos o son nulos")
        End If

        Using stream As New MemoryStream(pdfBytes)
            Using document As PdfDocument = PdfDocument.Load(stream)
                Using printDocument = document.CreatePrintDocument()
                    ' Configurar número de copias
                    If numeroCopias > 1 Then
                        printDocument.PrinterSettings.Copies = CShort(numeroCopias)
                    End If

                    ' Configurar bandeja de impresión usando el tipo estándar (PaperSourceKind)
                    ' DEBUG: Enumerar todas las bandejas disponibles
                    System.Diagnostics.Debug.WriteLine($"=== BANDEJAS DISPONIBLES EN LA IMPRESORA ===")
                    System.Diagnostics.Debug.WriteLine($"Impresora: {printDocument.PrinterSettings.PrinterName}")
                    For i As Integer = 0 To printDocument.PrinterSettings.PaperSources.Count - 1
                        Dim src As PaperSource = printDocument.PrinterSettings.PaperSources(i)
                        System.Diagnostics.Debug.WriteLine($"  [{i}] SourceName='{src.SourceName}' | Kind={src.Kind} | RawKind={src.RawKind}")
                    Next
                    System.Diagnostics.Debug.WriteLine($"Buscando tipo de bandeja: {tipoBandeja} (valor={CInt(tipoBandeja)})")
                    System.Diagnostics.Debug.WriteLine($"===========================================")

                    ' Buscar la bandeja por Kind (tipo estándar independiente del fabricante)
                    Dim bandejaEncontrada As Boolean = False
                    Dim targetKind As PaperSourceKind = CType(tipoBandeja, PaperSourceKind)

                    For Each source As PaperSource In printDocument.PrinterSettings.PaperSources
                        ' Comparar por RawKind (que contiene el valor numérico del PaperSourceKind)
                        If source.RawKind = CInt(tipoBandeja) OrElse source.Kind = targetKind Then
                            printDocument.DefaultPageSettings.PaperSource = source
                            bandejaEncontrada = True
                            System.Diagnostics.Debug.WriteLine($"✓ Bandeja configurada: '{source.SourceName}' (Kind={source.Kind}, RawKind={source.RawKind})")
                            Exit For
                        End If
                    Next

                    If Not bandejaEncontrada Then
                        System.Diagnostics.Debug.WriteLine($"⚠ ADVERTENCIA: No se encontró una bandeja con Kind={tipoBandeja}. Se usará la bandeja predeterminada.")
                    End If

                    ' Enviar a impresora
                    printDocument.Print()
                End Using
            End Using
        End Using
    End Sub
End Class
