Public Class EtiquetaComisionVenta
    Implements IEtiquetaComisionVenta
    Public Property Id As Integer
    Public Property Nombre As String Implements IEtiquetaComision.Nombre
    Public Property Venta As Decimal Implements IEtiquetaComisionVenta.Venta
    Public Property Tipo As Decimal Implements IEtiquetaComision.Tipo
    Public Property Comision As Decimal Implements IEtiquetaComision.Comision

    Public Property CifraAnual As Decimal Implements IEtiquetaComision.CifraAnual ' Suma de ventas del año
    Public Property ComisionAnual As Decimal Implements IEtiquetaComision.ComisionAnual
    Public ReadOnly Property UnidadCifra As String Implements IEtiquetaComision.UnidadCifra
        Get
            Return "€"
        End Get
    End Property
    Public ReadOnly Property PorcentajeAnual As Decimal Implements IEtiquetaComision.PorcentajeAnual
        Get
            Return If(CifraAnual = 0, 0, Math.Round(ComisionAnual / CifraAnual, 4, MidpointRounding.AwayFromZero))
        End Get
    End Property

    Public ReadOnly Property EsComisionAcumulada As Boolean Implements IEtiquetaComision.EsComisionAcumulada

End Class


Public Class EtiquetaComisionClientes
    Implements IEtiquetaComisionClientes
    Public Property Id As Integer
    Public Property Nombre As String Implements IEtiquetaComision.Nombre
    Public Property Recuento As Integer Implements IEtiquetaComisionClientes.Recuento
    Public Property Tipo As Decimal Implements IEtiquetaComision.Tipo
    Public Property Comision As Decimal Implements IEtiquetaComision.Comision

    Public Property CifraAnual As Decimal Implements IEtiquetaComision.CifraAnual ' Suma de recuentos del año (como decimal)
    Public Property ComisionAnual As Decimal Implements IEtiquetaComision.ComisionAnual
    Public ReadOnly Property UnidadCifra As String Implements IEtiquetaComision.UnidadCifra
        Get
            Return "clientes"
        End Get
    End Property

    Public ReadOnly Property PorcentajeAnual As Decimal Implements IEtiquetaComision.PorcentajeAnual
        Get
            Return If(CifraAnual = 0, 0, Math.Round(ComisionAnual / CifraAnual, 4, MidpointRounding.AwayFromZero))
        End Get
    End Property

    Public ReadOnly Property EsComisionAcumulada As Boolean Implements IEtiquetaComision.EsComisionAcumulada
End Class
