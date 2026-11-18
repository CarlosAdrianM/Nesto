Imports Nesto.Models

''' <summary>
''' Incidencia 1328 (María José, pedido 930358, 18/11/26): «Modificar con plantilla» mandaba en el PUT TODAS las líneas
''' con la fecha única de la plantilla. Al cargar un pedido con líneas del 11/11, la plantilla sube la fecha a la mínima
''' de hoy (18/11) y las líneas que ya tenían PICKING viajaban con la fecha nueva: NestoAPI lo trataba como un
''' aplazamiento del pedido (avisos a almacén, etiquetas, o rechazo si la agencia ya lo tenía) sin que nadie lo pidiera.
''' LA REGLA (pura): una línea que YA EXISTE en el pedido (id &lt;&gt; 0) y que, según el pedido recién leído, tiene picking y
''' no está en albarán ni factura, se manda con la fecha de entrega que tenía. Las líneas nuevas (id 0) y las existentes
''' sin picking siguen llevando la fecha de la plantilla. Aplazar a propósito se hace desde el detalle del pedido.
''' </summary>
Public NotInheritable Class FechaEntregaLineasConPicking
    Private Const ESTADO_ALBARAN As Short = 2

    Private Sub New()
    End Sub

    ''' <summary>¿El pedido recién leído (ParaPlantilla) tiene alguna línea con picking? Si no, no hace falta leer las fechas.</summary>
    Public Shared Function HayLineasConPicking(estadoFresco As PedidoParaPlantillaModel) As Boolean
        If estadoFresco Is Nothing Then
            Return False
        End If
        Return If(estadoFresco.Lineas, New List(Of LineaParaPlantillaModel)).Any(Function(l) l IsNot Nothing AndAlso (l.PagoTienePicking OrElse l.OfertaTienePicking)) OrElse
               If(estadoFresco.Regalos, New List(Of RegaloParaPlantillaModel)).Any(Function(r) r IsNot Nothing AndAlso r.TienePicking)
    End Function

    ''' <summary>
    ''' Pone a cada línea existente con picking del pedido que se va a enviar la fecha de entrega que tiene en
    ''' <paramref name="pedidoOriginal"/> (el pedido tal como está en el servidor). Devuelve cuántas líneas ha corregido.
    ''' </summary>
    Public Shared Function ConservarFechaOriginal(pedidoAEnviar As PedidoVentaDTO, pedidoOriginal As PedidoVentaDTO) As Integer
        If pedidoAEnviar?.Lineas Is Nothing OrElse pedidoOriginal?.Lineas Is Nothing Then
            Return 0
        End If

        Dim originalesPorId As New Dictionary(Of Integer, LineaPedidoVentaDTO)
        For Each original In pedidoOriginal.Lineas
            If original IsNot Nothing AndAlso original.id <> 0 AndAlso Not originalesPorId.ContainsKey(original.id) Then
                originalesPorId.Add(original.id, original)
            End If
        Next

        Dim corregidas As Integer = 0
        For Each linea In pedidoAEnviar.Lineas
            If linea Is Nothing OrElse linea.id = 0 Then
                Continue For ' línea nueva: la fecha de la plantilla
            End If
            Dim original As LineaPedidoVentaDTO = Nothing
            If Not originalesPorId.TryGetValue(linea.id, original) Then
                Continue For
            End If
            If Not original.tienePicking OrElse original.estado >= ESTADO_ALBARAN OrElse original.fechaEntrega = Date.MinValue Then
                Continue For ' sin picking (o ya servida, o sin fecha conocida): la fecha de la plantilla
            End If
            If linea.fechaEntrega <> original.fechaEntrega Then
                linea.fechaEntrega = original.fechaEntrega
                corregidas += 1
            End If
        Next
        Return corregidas
    End Function
End Class
