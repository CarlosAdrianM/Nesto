Namespace Models.Rectificativas
    ''' <summary>
    ''' NestoAPI#899/#863 (Nesto#638): GET api/PedidosVenta/CopiarFactura/Lineas: las líneas de la factura para elegir qué se
    ''' copia o se abona, con lo ya abonado.
    ''' </summary>
    Public Class LineasFacturaParaCopiarDTO
        Public Property Empresa As String
        Public Property Factura As String
        Public Property Albaranes As List(Of Integer) = New List(Of Integer)
        Public Property Lineas As List(Of LineaFacturaParaCopiarDTO) = New List(Of LineaFacturaParaCopiarDTO)
    End Class

    Public Class LineaFacturaParaCopiarDTO
        Public Property Pedido As Integer
        Public Property NumeroOrden As Integer
        Public Property Albaran As Integer?
        Public Property FechaAlbaran As Date?
        Public Property TipoLinea As Byte?
        ''' <summary>Cuenta contable (portes, comisión…): por albarán no entra, solo si se marca la línea.</summary>
        Public Property EsCuentaContable As Boolean
        ''' <summary>Portes o comisión contra reembolso.</summary>
        Public Property EsGastosEnvio As Boolean
        Public Property Producto As String
        Public Property Texto As String
        ''' <summary>La cantidad facturada (negativa en una rectificativa).</summary>
        Public Property Cantidad As Decimal
        Public Property YaAbonada As Decimal
        ''' <summary>|Cantidad| − YaAbonada (nunca negativo): lo que aún se puede abonar.</summary>
        Public Property QuedanPorAbonar As Decimal
        Public Property Precio As Decimal
        Public Property BaseImponible As Decimal
    End Class

    ''' <summary>NestoAPI#899/#863: una línea que se copia (o abona) y cuántas unidades (en positivo; Nothing = entera).</summary>
    Public Class LineaACopiarDTO
        Public Property NumeroOrden As Integer
        ''' <summary>Solo si la factura agrupa varios pedidos y ese Nº Orden está en más de uno.</summary>
        Public Property Pedido As Integer?
        Public Property Cantidad As Short?
    End Class
End Namespace
