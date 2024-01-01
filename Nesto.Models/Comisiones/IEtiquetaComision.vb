Public Interface IEtiquetaComision
    ReadOnly Property Nombre As String
    Property Tipo As Decimal
    Property Comision As Decimal
End Interface

Public Interface IEtiquetaComisionVenta
    Inherits IEtiquetaComision
    Property Venta As Decimal
End Interface

Public Interface IEtiquetaComisionClientes
    Inherits IEtiquetaComision
    Property Recuento As Integer
End Interface
