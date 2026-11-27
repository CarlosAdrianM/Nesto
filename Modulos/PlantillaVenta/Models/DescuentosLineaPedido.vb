Imports Nesto.Models
Imports Newtonsoft.Json

''' <summary>
''' Incidencia 1504 (María José, pedido 931500, 27/11/26): «Modificar con plantilla» enseñaba todas las líneas al 0 % y con
''' la base sin descontar. Los descuentos de campaña y el 100 % de las muestras van en LinPedidoVta.DescuentoProducto (el
''' descuento de línea es 0), y ParaPlantilla solo trae el de línea. Peor aún: al guardar, la línea viajaba con
''' DescuentoProducto 0 y el PUT de NestoAPI lo copia tal cual, así que una línea SIN picking que nadie había tocado perdía
''' su descuento.
'''
''' Estos son los descuentos con los que está GRABADA una línea del pedido que se modifica (la de pago). Sirven para dos
''' cosas: enseñar en la plantilla el descuento que de verdad lleva (<see cref="DescuentoMostrado"/>) y, si la línea no se
''' toca, mandarla en el PUT exactamente como está (<see cref="SiguenIntactos"/>).
''' </summary>
Public Class DescuentosLineaPedido
    ''' <summary>LinPedidoVta.Descuento.</summary>
    Public Property DescuentoLinea As Decimal
    ''' <summary>LinPedidoVta.DescuentoProducto.</summary>
    Public Property DescuentoProducto As Decimal
    ''' <summary>LinPedidoVta.Aplicar_Dto.</summary>
    Public Property AplicarDescuento As Boolean
    ''' <summary>LinPedidoVta.Precio.</summary>
    Public Property Precio As Decimal

    ''' <summary>
    ''' El descuento que enseña la plantilla («% Dto.», del que salen la base de la línea y los totales): el de línea y el de
    ''' producto compuestos con la misma función que el pedido (<see cref="LineaPedidoVentaDTO.SumaDescuentos"/>), sin el
    ''' del cliente ni el de pronto pago, que la plantilla tampoco enseña en un pedido nuevo.
    ''' </summary>
    <JsonIgnore>
    Public ReadOnly Property DescuentoMostrado As Decimal
        Get
            Dim linea As New LineaPedidoVentaDTO With {
                .DescuentoLinea = DescuentoLinea,
                .DescuentoProducto = DescuentoProducto,
                .AplicarDescuento = AplicarDescuento
            }
            Return linea.SumaDescuentos
        End Get
    End Property

    ''' <summary>El descuento «calculado» que guarda la línea de la plantilla (el de producto, si se aplican descuentos).</summary>
    <JsonIgnore>
    Public ReadOnly Property DescuentoProductoPlantilla As Decimal
        Get
            Return If(AplicarDescuento, DescuentoProducto, 0D)
        End Get
    End Property

    ''' <summary>
    ''' La línea de la plantilla sigue con el precio, el descuento y el «aplicar descuento» con los que se cargó: entonces se
    ''' manda con los descuentos grabados, sin convertirlos. Si se ha cambiado algo de eso (a mano o porque al cambiar la
    ''' cantidad se ha vuelto a pedir el precio), se manda como cualquier línea de la plantilla.
    ''' </summary>
    Public Function SiguenIntactos(precio As Decimal, descuento As Decimal, aplicarDescuento As Boolean) As Boolean
        Return precio = Me.Precio AndAlso descuento = DescuentoMostrado AndAlso aplicarDescuento = Me.AplicarDescuento
    End Function

    ''' <summary>
    ''' Pone a cada línea de <paramref name="pedidoPlantilla"/> que no lo trae el descuento de producto de su línea de pago en
    ''' <paramref name="pedidoCompleto"/> (GET api/PedidosVenta), buscándola por su id. Devuelve cuántas ha completado. Una
    ''' línea que no aparece se queda sin él (Nothing).
    ''' </summary>
    Public Shared Function Completar(pedidoPlantilla As PedidoParaPlantillaModel, pedidoCompleto As PedidoVentaDTO) As Integer
        If pedidoPlantilla?.Lineas Is Nothing OrElse pedidoCompleto?.Lineas Is Nothing Then
            Return 0
        End If
        Dim completadas As Integer = 0
        For Each linea In pedidoPlantilla.Lineas
            If linea Is Nothing OrElse linea.IdLineaPago = 0 OrElse linea.DescuentoProducto.HasValue Then
                Continue For
            End If
            Dim original As LineaPedidoVentaDTO = pedidoCompleto.Lineas.FirstOrDefault(Function(l) l IsNot Nothing AndAlso l.id = linea.IdLineaPago)
            If original IsNot Nothing Then
                linea.DescuentoProducto = original.DescuentoProducto
                completadas += 1
            End If
        Next
        Return completadas
    End Function

    ''' <summary>¿Alguna línea de pago del pedido viene sin el descuento de producto? (Entonces hay que leer el pedido entero.)</summary>
    Public Shared Function FaltaAlguno(pedidoPlantilla As PedidoParaPlantillaModel) As Boolean
        Return If(pedidoPlantilla?.Lineas, New List(Of LineaParaPlantillaModel)).
            Any(Function(l) l IsNot Nothing AndAlso l.IdLineaPago <> 0 AndAlso Not l.DescuentoProducto.HasValue)
    End Function

    ''' <summary>Los descuentos grabados de la línea de pago, o Nothing si no se sabe su descuento de producto (o es una línea nueva).</summary>
    Public Shared Function DeLinea(linea As LineaParaPlantillaModel) As DescuentosLineaPedido
        If linea Is Nothing OrElse linea.IdLineaPago = 0 OrElse Not linea.DescuentoProducto.HasValue Then
            Return Nothing
        End If
        Return New DescuentosLineaPedido With {
            .DescuentoLinea = linea.Descuento,
            .DescuentoProducto = linea.DescuentoProducto.Value,
            .AplicarDescuento = linea.AplicarDescuento,
            .Precio = linea.Precio
        }
    End Function
End Class
