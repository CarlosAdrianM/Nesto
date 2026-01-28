Public Class CopiarFacturaRequestDTO
    ''' <summary>
    ''' Empresa de la factura origen
    ''' </summary>
    Public Property Empresa As String

    ''' <summary>
    ''' Cliente de la factura origen
    ''' </summary>
    Public Property Cliente As String

    ''' <summary>
    ''' Numero de factura a copiar (ej: "NV26/001234")
    ''' </summary>
    Public Property NumeroFactura As String

    ''' <summary>
    ''' Si true, invierte el signo de las cantidades (para crear rectificativa/abono)
    ''' </summary>
    Public Property InvertirCantidades As Boolean

    ''' <summary>
    ''' Si true, anade las lineas al pedido original en lugar de crear uno nuevo
    ''' </summary>
    Public Property AnadirAPedidoOriginal As Boolean

    ''' <summary>
    ''' Si true, mantiene precios/descuentos/IVA originales.
    ''' Si false, recalcula segun condiciones del cliente destino.
    ''' </summary>
    Public Property MantenerCondicionesOriginales As Boolean = True

    ''' <summary>
    ''' Si true, despues de copiar crea albaran y factura automaticamente.
    ''' </summary>
    Public Property CrearAlbaranYFactura As Boolean

    ''' <summary>
    ''' Cliente destino. Si es null o vacio, se usa el mismo cliente origen.
    ''' </summary>
    Public Property ClienteDestino As String

    ''' <summary>
    ''' Contacto del cliente destino.
    ''' </summary>
    Public Property ContactoDestino As String
End Class
