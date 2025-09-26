Public Interface IEtiquetaComision
    ReadOnly Property Nombre As String
    Property Tipo As Decimal
    Property Comision As Decimal
    ReadOnly Property EsComisionAcumulada As Boolean

    ' Propiedades genéricas para totales anuales
    ReadOnly Property CifraAnual As Decimal      ' Puede ser venta anual o recuento anual
    ReadOnly Property ComisionAnual As Decimal   ' Comisión acumulada del año
    ReadOnly Property UnidadCifra As String      ' "€" para ventas, "clientes" para recuentos
    ReadOnly Property PorcentajeAnual As Decimal
End Interface

Public Interface IEtiquetaComisionVenta
    Inherits IEtiquetaComision
    Property Venta As Decimal
    ' CifraAnual será la suma de ventas del año
    ' UnidadCifra será "€"
End Interface

Public Interface IEtiquetaComisionClientes
    Inherits IEtiquetaComision
    Property Recuento As Integer
    ' CifraAnual será la suma de recuentos del año
    ' UnidadCifra será "clientes" (o "unidades", "conseguidos", etc.)
End Interface
