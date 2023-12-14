Imports Newtonsoft.Json

Public Class InventarioModel
    Public Class InventarioDTO
        <JsonProperty("NºOrden")>
        Public Property NumOrden As Integer
        Public Property Empresa As String
        <JsonProperty("Almacén")>
        Public Property Almacen As String
        <JsonProperty("Número")>
        Public Property Producto As String
        Public Property Fecha As System.DateTime
        Public Property StockReal As Short
        Public Property Estado As Byte
        <JsonProperty("Descripción")>
        Public Property Descripcion As String
        Public Property Grupo As String
        Public Property Subgrupo As String
        Public Property Familia As String
        Public Property StockCalculado As Short
        Public Property Valor As Decimal
        <JsonProperty("NºTraspaso")>
        Public Property Traspaso As Nullable(Of Integer)
        Public Property Aplicacion As String
        Public Property Pasillo As String
        Public Property Fila As String
        Public Property Columna As String
        Public Property Usuario As String
    End Class

    Public Class Movimiento
        Public Property Producto As String
        Public Property Descripcion As String
        Public Property Familia As String
        Public Property Grupo As String
        Public Property Subgrupo As String
        Public Property Cantidad As Integer
        Public Property Color As Brush
    End Class
End Class
