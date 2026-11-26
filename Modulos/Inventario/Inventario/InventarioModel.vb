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
        ' 20/11/26: Integer como en la base de datos. Con Short, sumar una cantidad disparatada (una referencia leída en «Cantidad») desbordaba.
        Public Property StockReal As Integer
        Public Property Estado As Byte
        <JsonProperty("Descripción")>
        Public Property Descripcion As String
        Public Property Grupo As String
        Public Property Subgrupo As String
        Public Property Familia As String
        Public Property StockCalculado As Integer
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
        ''' <summary>
        ''' Novedad 1456 (Paloma, 25/11/26): la API dice si quien pregunta puede borrar esta fila pendiente (la misma regla que
        ''' «Borrar esta fila» de las comprobaciones). Ausente (API que aún no lo manda) o False: sin botón. Solo de lectura: no se manda.
        ''' </summary>
        Public Property SePuedeBorrar As Boolean
        Public Function ShouldSerializeSePuedeBorrar() As Boolean
            Return False
        End Function
        ''' <summary>
        ''' 25/11/26: fila pendiente de un día ANTERIOR al elegido que la API añade al final de «Completo» (incluirPendientesAnteriores),
        ''' para poder borrarla. Su día va en <see cref="Fecha"/>. Ausente (API antigua): False, como antes. Solo de lectura.
        ''' </summary>
        Public Property PendienteDeDiaAnterior As Boolean
        Public Function ShouldSerializePendienteDeDiaAnterior() As Boolean
            Return False
        End Function
        ''' <summary>«pendiente del 24/11» en las de un día anterior; vacío en las del día elegido.</summary>
        <JsonIgnore>
        Public ReadOnly Property TextoDia As String
            Get
                Return If(PendienteDeDiaAnterior, $"pendiente del {Fecha:dd/MM}", String.Empty)
            End Get
        End Property
        ''' <summary>Nesto#596: «código» o «tecleado» para la rejilla.</summary>
        <JsonIgnore>
        Public ReadOnly Property Lectura As String
            Get
                Return TextoLectura(PorCodigoBarras)
            End Get
        End Property
        ''' <summary>Almacén con ubicaciones: «001-002-005» o «sin ubicación» para la rejilla (en las tiendas no se ve).</summary>
        <JsonIgnore>
        Public ReadOnly Property Hueco As String
            Get
                Return TextoHueco(Pasillo, Fila, Columna)
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
        ''' <summary>Almacén con ubicaciones: el hueco donde se ha contado («001-002-005»).</summary>
        Public Property Hueco As String
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

    ''' <summary>
    ''' 25/11/26 (Paloma rellenó sin querer 3.032 filas de Alcobendas): GET api/Inventarios/Rellenar/Previa. Lo que haría el
    ''' rellenado (sin hacerlo) y si lo ya rellenado ese día se puede deshacer.
    ''' </summary>
    Public Class PreviaRellenarInventarioDTO
        Public Property Almacen As String
        Public Property NombreAlmacen As String
        Public Property Fecha As Date?
        Public Property FilasQueSeCrearian As Integer
        Public Property FilasPendientesYa As Integer
        ''' <summary>«Va a crear 3.032 filas de inventario en Alcobendas con fecha 25/11/2026.»</summary>
        Public Property Mensaje As String
        Public Property PuedeDeshacer As Boolean
        Public Property MotivoNoPuedeDeshacer As String
    End Class

    ''' <summary>25/11/26: respuesta de DELETE api/Inventarios/Rellenar (deshacer el rellenado de un almacén y día).</summary>
    Public Class DeshacerRellenarInventarioRespuestaDTO
        Public Property Almacen As String
        Public Property Fecha As Date?
        Public Property FilasBorradas As Integer
        Public Property FilasContadas As Integer
        Public Property UnidadesContadas As Integer
    End Class

    ''' <summary>NestoAPI#784: el campo Codigo del 409 de Rellenar, RellenarParcial, RellenarUbicacion y GET/POST api/Inventarios.</summary>
    Public NotInheritable Class CodigosConflictoInventario
        ''' <summary>Ya hay filas pendientes ese día: no se rellena otra vez (ni con OtraVez); se puede contar igual.</summary>
        Public Const YA_RELLENADO As String = "YaRellenado"
        ''' <summary>Ya se contó y contabilizó ese día: se puede repetir con OtraVez = true («Contar otra vez»).</summary>
        Public Const YA_CONTABILIZADO As String = "YaContabilizado"
        ''' <summary>GET: el producto tiene más de una fila pendiente ese día; hay que dejar una.</summary>
        Public Const FILAS_DUPLICADAS As String = "FilasDuplicadas"
        ''' <summary>GET sin ubicación: filas pendientes en varias ubicaciones (Algete).</summary>
        Public Const VARIAS_UBICACIONES As String = "VariasUbicaciones"
        ''' <summary>POST: el producto ya tiene fila pendiente ese día; se suma a esa (PUT).</summary>
        Public Const YA_TIENE_FILA As String = "YaTieneFila"
        ''' <summary>Sugerencia 1185: «a cero» en Algete con filas del producto sin ubicación; se borran con QuitarFilasSinUbicacion.</summary>
        Public Const FILAS_SIN_UBICACION As String = "FilasSinUbicacion"
    End Class

    ''' <summary>Sugerencia 1185: una línea de POST api/Inventarios/DejarACero (una ubicación, o la fila sin ubicación de una tienda).</summary>
    Public Class LineaDejarACeroDTO
        Public Const CREAR As String = "Crear"
        Public Const PONER_A_CERO As String = "PonerACero"
        Public Const YA_ESTABA_A_CERO As String = "YaEstabaACero"

        Public Property Pasillo As String
        Public Property Fila As String
        Public Property Columna As String
        Public Property StockCalculado As Integer
        Public Property ContadoAntes As Integer
        Public Property NumeroOrden As Integer?
        Public Property Accion As String
    End Class

    ''' <summary>Sugerencia 1185: respuesta de POST api/Inventarios/DejarACero.</summary>
    Public Class DejarACeroInventarioRespuestaDTO
        Public Property Almacen As String
        Public Property Fecha As Date
        Public Property Producto As String
        Public Property Descripcion As String
        Public Property PorUbicaciones As Boolean
        Public Property Simulado As Boolean
        Public Property StockAlmacen As Integer
        Public Property UnidadesSinUbicar As Integer
        Public Property Lineas As New List(Of LineaDejarACeroDTO)
        Public Property FilasQuitadas As New List(Of LineaInventarioContadaDTO)
        Public Property FilasCreadas As Integer
        Public Property FilasPuestasACero As Integer
        Public Property Unidades As Integer
        Public Property Usuario As String
        Public Property Aviso As String
    End Class

    ''' <summary>NestoAPI#784: el cuerpo de los 409 de api/Inventarios (el de Rellenar trae además Pendientes y Contabilizadas).</summary>
    Public Class ConflictoInventarioDTO
        Public Property Message As String
        Public Property Codigo As String
        Public Property PuedeContarOtraVez As Boolean
        Public Property Lineas As New List(Of LineaInventarioContadaDTO)
        Public Property Contabilizadas As New List(Of ContabilizadasInventarioUsuarioDTO)
        ''' <summary>Nesto#647: en el 409 del código de barras de varios productos, los candidatos para preguntar cuál es.</summary>
        Public Property Productos As New List(Of ControlesUsuario.Models.ProductoCodigoBarrasDuplicado)
    End Class

    ''' <summary>NestoAPI#784: una fila del inventario en el cuerpo de un 409 (Traspaso solo en las ya contabilizadas).</summary>
    Public Class LineaInventarioContadaDTO
        Public Property NumeroOrden As Integer
        Public Property Producto As String
        Public Property Descripcion As String
        Public Property Familia As String
        Public Property Pasillo As String
        Public Property Fila As String
        Public Property Columna As String
        Public Property StockCalculado As Integer
        Public Property StockReal As Integer
        Public Property Usuario As String
        Public Property Fecha As Date
        Public Property PorCodigoBarras As Boolean?
        Public Property Traspaso As Integer?
    End Class

    ''' <summary>NestoAPI#784: lo ya contabilizado ese día en el almacén, por usuario y traspaso (409 YaContabilizado de Rellenar).</summary>
    Public Class ContabilizadasInventarioUsuarioDTO
        Public Property Usuario As String
        Public Property Filas As Integer
        Public Property Traspaso As Integer?
    End Class

    Public Const LECTURA_CODIGO As String = "código"
    Public Const LECTURA_TECLEADO As String = "tecleado"

    ''' <summary>«001-002-005» o «sin ubicación».</summary>
    Public Shared Function TextoHueco(pasillo As String, fila As String, columna As String) As String
        If String.IsNullOrWhiteSpace(pasillo) AndAlso String.IsNullOrWhiteSpace(fila) AndAlso String.IsNullOrWhiteSpace(columna) Then
            Return "sin ubicación"
        End If
        Return $"{If(pasillo, String.Empty).Trim()}-{If(fila, String.Empty).Trim()}-{If(columna, String.Empty).Trim()}"
    End Function

    Public Shared Function TextoLectura(porCodigoBarras As Boolean?) As String
        If Not porCodigoBarras.HasValue Then
            Return String.Empty
        End If
        Return If(porCodigoBarras.Value, LECTURA_CODIGO, LECTURA_TECLEADO)
    End Function
End Class
