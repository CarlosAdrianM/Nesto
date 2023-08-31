Imports Newtonsoft.Json


Public Class DireccionesEntregaJson
    Public Property contacto() As String
    Public Property clientePrincipal() As Boolean
    Public Property nombre() As String
    Public Property direccion() As String
    Public Property poblacion() As String
    Public Property comentarios() As String
    Public Property codigoPostal() As String
    Public Property provincia() As String
    Public Property estado() As Integer
    Public Property iva() As String
    Public Property comentarioRuta() As String
    Public Property comentarioPicking() As Object
    Public Property noComisiona() As Double
    Public Property servirJunto() As Boolean
    Public Property mantenerJunto() As Boolean
    Public Property esDireccionPorDefecto() As Boolean
    Public Property vendedor() As String
    Public Property periodoFacturacion() As String
    Public Property ccc() As String
    Public Property ruta() As String
    Public Property formaPago() As String
    Public Property plazosPago() As String
    Public Property tieneCorreoElectronico As Boolean
    Public Property tieneFacturacionElectronica As Boolean

    Public ReadOnly Property textoPoblacion As String
        Get
            Return String.Format("{0} {1} ({2})", codigoPostal, poblacion, provincia)
        End Get
    End Property
End Class
Public Class FormaPagoDTO
    Public Property formaPago As String
    Public Property descripcion As String
    Public bloquearPagos As Boolean
    Public cccObligatorio As Boolean
End Class
Public Class FormaVentaDTO
    <JsonProperty("Número")>
    Public Property numero As String
    <JsonProperty("Descripción")>
    Public Property descripcion As String
End Class
Public Class PrecioProductoDTO
    Public Property precio As Decimal
    Public Property descuento As Decimal
    Public Property aplicarDescuento As Boolean
    Public Property motivo As String
End Class
Public Class StockProductoDTO
    Public Property stock() As Integer
    Public Property cantidadDisponible() As Integer
    Public Property cantidadPendienteRecibir As Integer
    Public Property urlImagen() As String
End Class
Public Class UltimasVentasProductoClienteDTO
    Public Property fecha As DateTime
    Public Property cantidad As Short
    Public Property precioBruto As Decimal
    Public Property descuentos As Decimal
    Public Property precioNeto As Decimal
End Class

Public Class PonerStockParam
    Public Property Lineas As List(Of LineaPlantillaVenta)
    Public Property Almacen As String
    Public Property Ordenar As Boolean
End Class
