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
    ''' Numero de factura a copiar (ej: "NV26/001234").
    ''' Para una sola factura. Se ignora si NumerosFactura tiene elementos.
    ''' </summary>
    Public Property NumeroFactura As String

    ''' <summary>
    ''' Lista de numeros de factura a copiar (para seleccion multiple).
    ''' Si tiene elementos, se ignora NumeroFactura.
    ''' Issue #279 - SelectorFacturas
    ''' </summary>
    Public Property NumerosFactura As List(Of String)

    ''' <summary>
    ''' Si true, agrupa todas las facturas en una sola rectificativa.
    ''' Si false, crea una rectificativa por cada factura.
    ''' Solo aplica cuando NumerosFactura tiene mas de un elemento.
    ''' Issue #279
    ''' </summary>
    Public Property AgruparEnUnaRectificativa As Boolean = True

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

    ''' <summary>
    ''' Si true, crea DOS operaciones en un solo clic:
    ''' 1. Rectificativa (abono) al cliente ORIGEN de la factura
    ''' 2. Factura nueva (cargo) al cliente DESTINO seleccionado
    ''' Requiere ClienteDestino y ContactoDestino informados.
    ''' </summary>
    Public Property CrearAbonoYCargo As Boolean

    ''' <summary>
    ''' Comentarios a anadir en el pedido creado.
    ''' Se concatenan con el comentario automatico de trazabilidad.
    ''' </summary>
    Public Property Comentarios As String
End Class
