Public Class EtiquetaComisionVenta
    Implements IEtiquetaComisionVenta
    Public Property Id As Integer
    Public Property Nombre As String Implements IEtiquetaComision.Nombre
    Public Property Venta As Decimal Implements IEtiquetaComisionVenta.Venta
    Public Property Tipo As Decimal Implements IEtiquetaComision.Tipo
    Public Property Comision As Decimal Implements IEtiquetaComision.Comision
End Class


Public Class EtiquetaComisionClientes
    Implements IEtiquetaComisionClientes
    Public Property Id As Integer
    Public Property Nombre As String Implements IEtiquetaComision.Nombre
    Public Property Recuento As Integer Implements IEtiquetaComisionClientes.Recuento
    Public Property Tipo As Decimal Implements IEtiquetaComision.Tipo
    Public Property Comision As Decimal Implements IEtiquetaComision.Comision
End Class
