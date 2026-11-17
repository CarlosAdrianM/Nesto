Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modulos.PedidoVenta.Models.Facturas

''' <summary>
''' Nesto#645 (Novedades 1317 y 1296): al facturar en la tienda, PRIMERO se cobra y DESPUÉS se imprime (Carlos 17/11/26: así la
''' factura no sale con «Pendiente de pago»). Creada la factura, si el usuario es de Tiendas y la factura deja algo por cobrar,
''' se pregunta «¿Quiere meter el cobro en caja ahora?»; con «Sí» se abre el cobro de Cajas con el efecto de esa factura ya
''' marcado (en efectivo, con «Entrega el cliente» y el cambio). Al terminar el cobro (o si lo cancela o dice que no) se imprime
''' la factura como siempre.
''' </summary>
Partial Public Class DetallePedidoViewModel

    ''' <summary>
    ''' Nesto#645: pregunta y, si dice que sí, abre el cobro con la factura marcada. Devuelve si se ha contabilizado el cobro.
    ''' Nunca rompe la facturación: si algo falla, lo dice y sigue.
    ''' </summary>
    Friend Async Function OfrecerCobroEnCajaAsync(resultado As CrearFacturaResponseDTO, cliente As String, contacto As String) As Task(Of Boolean)
        If resultado Is Nothing OrElse String.IsNullOrWhiteSpace(resultado.NumeroFactura) OrElse
            resultado.NumeroFactura.Trim() = Constantes.PeriodosFacturacion.FIN_DE_MES OrElse String.IsNullOrWhiteSpace(cliente) Then
            Return False
        End If
        Dim esTienda As Boolean = CobroEnCajaFactura.EsUsuarioDeTienda(configuracion)
        If Not esTienda Then
            Return False
        End If
        Dim factura As String = resultado.NumeroFactura.Trim()
        Dim empresa As String = If(String.IsNullOrWhiteSpace(resultado.Empresa), pedido?.empresa, resultado.Empresa)?.Trim()

        Dim pendiente As Decimal? = Nothing
        Try
            pendiente = Await servicio.LeerPendienteDocumento(empresa, cliente, factura)
        Catch ex As Exception
            pendiente = Nothing ' sin saberlo, se pregunta igual
        End Try
        If Not CobroEnCajaFactura.DebePreguntar(esTienda, pendiente) Then
            Return False
        End If

        Dim texto As String = $"¿Quiere meter el cobro de la factura {factura} en caja ahora?"
        If pendiente.HasValue Then
            texto &= $"{vbCrLf}Quedan {pendiente.Value.ToString("C", Globalization.CultureInfo.GetCultureInfo("es-ES"))} por cobrar."
        End If
        If Not dialogService.ShowConfirmationAnswer("Cobro en caja", texto) Then
            Return False
        End If

        Dim parametros As New ParametrosDialogo From {
            {CobroEnCajaFactura.PARAMETRO_EMPRESA, empresa},
            {CobroEnCajaFactura.PARAMETRO_CLIENTE, cliente.Trim()},
            {CobroEnCajaFactura.PARAMETRO_CONTACTO, contacto?.Trim()},
            {CobroEnCajaFactura.PARAMETRO_DOCUMENTO, factura}
        }
        Try
            Dim respuesta As ResultadoDialogo = Await dialogService.ShowDialogAsync(CobroEnCajaFactura.DIALOGO, parametros)
            Return respuesta IsNot Nothing AndAlso respuesta.Result = ResultadoBoton.OK
        Catch ex As Exception
            dialogService.ShowError($"No se ha podido abrir el cobro en caja: {ex.Message}")
            Return False
        End Try
    End Function

    ''' <summary>
    ''' Nesto#645: los bytes de la factura en papel vienen de cuando se creó, antes del cobro, y dirían «Pendiente de pago». Tras
    ''' cobrar se piden otra vez (el PDF lee lo pendiente del extracto al generarse). Si falla, se imprime la de antes.
    ''' </summary>
    Friend Async Function RefrescarFacturaEnPapelTrasCobroAsync(resultado As CrearFacturaResponseDTO) As Task
        Dim datos = resultado?.DatosImpresion
        If datos Is Nothing OrElse datos.EstaImpresoEnServidor Then
            Return
        End If
        Try
            Dim documentos As DocumentosImpresionPedidoDTO = Await servicio.ObtenerDocumentosImpresion(resultado.Empresa, resultado.NumeroPedido, resultado.NumeroFactura)
            Dim nueva = documentos?.Facturas?.FirstOrDefault(Function(f) String.Equals(f?.NumeroFactura?.Trim(), resultado.NumeroFactura?.Trim(), StringComparison.OrdinalIgnoreCase))
            If nueva?.DatosImpresion?.BytesPDF IsNot Nothing AndAlso nueva.DatosImpresion.BytesPDF.Length > 0 Then
                datos.BytesPDF = nueva.DatosImpresion.BytesPDF
            End If
        Catch ex As Exception
            ' Se imprime la que había
        End Try
    End Function

    ''' <summary>Nesto#645: lo que se hace con la factura recién creada, en este orden: avisos, cobro en caja e impresión.</summary>
    Friend Async Function TrasCrearFacturaAsync(resultado As CrearFacturaResponseDTO, cliente As String, contacto As String) As Task
        MostrarAvisosFacturacion(resultado)
        Dim cobrado As Boolean = Await OfrecerCobroEnCajaAsync(resultado, cliente, contacto)
        If cobrado Then
            Await RefrescarFacturaEnPapelTrasCobroAsync(resultado)
        End If
        Await ImprimirFacturaEnPapelAsync(resultado) ' Nesto#620
        Await ImprimirFactura(resultado.NumeroFactura)
    End Function
End Class
