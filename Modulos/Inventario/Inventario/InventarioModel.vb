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
        ''' <summary>NestoAPI#788: True si se leyó por código de barras, False si se tecleó la referencia, Nothing si no consta.</summary>
        Public Property PorCodigoBarras As Boolean?
        ''' <summary>Nesto#596: «código» o «tecleado» para la rejilla.</summary>
        <JsonIgnore>
        Public ReadOnly Property Lectura As String
            Get
                Return TextoLectura(PorCodigoBarras)
            End Get
        End Property
    End Class

    Public Class Movimiento
        Public Property Producto As String
        Public Property Descripcion As String
        Public Property Familia As String
        Public Property Grupo As String
        Public Property Subgrupo As String
        Public Property Cantidad As Integer
        Public Property Color As Brush
        ''' <summary>Nesto#596: «código» o «tecleado», cómo se leyó esta vez.</summary>
        Public Property Lectura As String
    End Class

    ''' <summary>NestoAPI#788: GET api/Inventarios/Pendientes, por usuario y día.</summary>
    Public Class InventarioPendienteDTO
        Public Property Usuario As String
        Public Property Fecha As Date
        Public Property Filas As Integer
        Public Property StockReal As Integer
        Public Property StockCalculado As Integer
        Public Property FilasPorCodigoBarras As Integer
        Public Property FilasTecleadas As Integer
        Public Property FilasSinMarca As Integer
    End Class

    ''' <summary>NestoAPI#789: respuesta de POST api/Inventarios/Rellenar.</summary>
    Public Class RellenarInventarioRespuestaDTO
        Public Property Almacen As String
        Public Property Fecha As Date
        Public Property Filas As Integer
        Public Property Unidades As Integer
        Public Property Usuario As String
    End Class

    Public Const LECTURA_CODIGO As String = "código"
    Public Const LECTURA_TECLEADO As String = "tecleado"

    Public Shared Function TextoLectura(porCodigoBarras As Boolean?) As String
        If Not porCodigoBarras.HasValue Then
            Return String.Empty
        End If
        Return If(porCodigoBarras.Value, LECTURA_CODIGO, LECTURA_TECLEADO)
    End Function
End Class
