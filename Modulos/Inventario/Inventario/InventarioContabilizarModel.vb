Imports Newtonsoft.Json

' Nesto#597 (gemela de NestoAPI#790): lo que devuelven Comprobaciones, Contabilizar y Diferencias de api/Inventarios.
Partial Public Class InventarioModel

    ''' <summary>NestoAPI#790: GET api/Inventarios/Comprobaciones (y el cuerpo del 409 de POST api/Inventarios/Contabilizar).</summary>
    Public Class ComprobacionesInventarioDTO
        Public Property Almacen As String
        Public Property Fecha As Date
        Public Property ControlUbicaciones As Boolean
        Public Property Bloqueantes As New List(Of ComprobacionInventarioDTO)
        Public Property Avisos As New List(Of ComprobacionInventarioDTO)
        ''' <summary>Quién ha metido inventario ese día en ese almacén (filas pendientes de cada uno).</summary>
        Public Property Recuento As New List(Of RecuentoUsuarioInventarioDTO)
    End Class

    Public Class ComprobacionInventarioDTO
        Public Property Codigo As String
        Public Property Titulo As String
        ''' <summary>
        ''' NestoAPI#784: un bloqueante que el usuario puede confirmar (hoy solo RecontadaSinContar): se contabiliza con
        ''' ConfirmarContadoCero = true. Los demás bloqueantes no se saltan nunca.
        ''' </summary>
        Public Property SeConfirma As Boolean
        Public Property Filas As New List(Of FilaComprobacionInventarioDTO)
        ''' <summary>«Título (N filas)» para la cabecera de cada comprobación.</summary>
        <JsonIgnore>
        Public ReadOnly Property Cabecera As String
            Get
                Dim n = If(Filas Is Nothing, 0, Filas.Count)
                Return $"{Titulo} ({TextoFilas(n)})"
            End Get
        End Property
    End Class

    ''' <summary>NestoAPI#784: los códigos de comprobación que Nesto trata aparte (el resto se enseña tal cual).</summary>
    Public NotInheritable Class CodigosComprobacionInventario
        ''' <summary>El producto tiene varias filas pendientes ese día: bloquea y no se confirma nunca.</summary>
        Public Const FILAS_DUPLICADAS As String = "FilasDuplicadas"
        ''' <summary>Ya contabilizado ese día, vuelto a abrir y con contado 0 y teórico distinto de 0: bloquea, pero se confirma.</summary>
        Public Const RECONTADA_SIN_CONTAR As String = "RecontadaSinContar"
        ''' <summary>Contado 0 con stock: solo aviso.</summary>
        Public Const CONTADO_CERO_CON_STOCK As String = "ContadoCeroConStock"
        ''' <summary>Ubicaciones con stock sin inventariar: sus líneas son de Ubicaciones, no filas de inventario (no se borran).</summary>
        Public Const UBICACION_SIN_RELLENAR As String = "UbicacionSinRellenar"
    End Class

    Public Class FilaComprobacionInventarioDTO
        ''' <summary>La fila de Inventarios; Nothing en UbicacionSinRellenar (es una fila de Ubicaciones).</summary>
        <JsonProperty("NºOrden")>
        Public Property NumOrden As Integer?
        Public Property Producto As String
        Public Property Descripcion As String
        Public Property StockCalculado As Integer?
        Public Property StockReal As Integer?
        Public Property Pasillo As String
        Public Property Fila As String
        Public Property Columna As String
        Public Property Usuario As String
        Public Property Fecha As Date?
        <JsonIgnore>
        Public ReadOnly Property Ubicacion As String
            Get
                Return TextoUbicacion(Pasillo, Fila, Columna)
            End Get
        End Property
        ''' <summary>
        ''' Incidencia 1186: se enseña «Borrar esta fila» (lo decide la ventana de comprobaciones: fila de Inventarios con Nº orden
        ''' y comprobación que no sea UbicacionSinRellenar).
        ''' </summary>
        <JsonIgnore>
        Public Property SePuedeBorrar As Boolean
    End Class

    Public Class RecuentoUsuarioInventarioDTO
        Public Property Usuario As String
        Public Property Filas As Integer
    End Class

    ''' <summary>NestoAPI#790: el cuerpo 200 de POST api/Inventarios/Contabilizar (un traspaso por usuario).</summary>
    Public Class ContabilizacionInventarioDTO
        Public Property Almacen As String
        Public Property Fecha As Date
        Public Property Contabilizados As New List(Of UsuarioContabilizadoInventarioDTO)
        Public Property Errores As New List(Of ErrorContabilizarInventarioDTO)
    End Class

    Public Class UsuarioContabilizadoInventarioDTO
        Public Property Usuario As String
        Public Property Filas As Integer
        Public Property Traspaso As Integer?
    End Class

    Public Class ErrorContabilizarInventarioDTO
        Public Property Usuario As String
        Public Property Mensaje As String
    End Class

    ''' <summary>
    ''' Nesto#597: lo que vuelve de contabilizar: o la contabilización (200) o, si la API ha encontrado bloqueantes (409),
    ''' sus comprobaciones y no se ha tocado nada.
    ''' </summary>
    Public Class ResultadoContabilizarInventario
        Public Property Contabilizacion As ContabilizacionInventarioDTO
        Public Property Bloqueado As ComprobacionesInventarioDTO
    End Class

    ''' <summary>NestoAPI#790: GET api/Inventarios/Diferencias. Valoracion «PVP» o «Coste»: la decide la API por rol.</summary>
    Public Class DiferenciasInventarioDTO
        Public Property Almacen As String
        Public Property Fecha As Date?
        Public Property Traspasos As New List(Of Integer)
        Public Property Valoracion As String
        Public Property Lineas As New List(Of LineaDiferenciaInventarioDTO)
        Public Property Totales As New TotalesDiferenciasInventarioDTO
        Public Property PorGrupo As New List(Of TotalGrupoDiferenciasInventarioDTO)
        Public Property PorFamilia As New List(Of TotalFamiliaDiferenciasInventarioDTO)
    End Class

    Public Class LineaDiferenciaInventarioDTO
        Public Property Producto As String
        Public Property Descripcion As String
        Public Property Grupo As String
        Public Property Familia As String
        Public Property Pasillo As String
        Public Property Fila As String
        Public Property Columna As String
        Public Property StockCalculado As Integer
        Public Property StockReal As Integer
        ''' <summary>StockReal − StockCalculado: positiva, sobra; negativa, falta.</summary>
        Public Property Diferencia As Integer
        Public Property ValorUnitario As Decimal
        Public Property ValorDiferencia As Decimal
        Public Property Usuario As String
        <JsonIgnore>
        Public ReadOnly Property Ubicacion As String
            Get
                Return TextoUbicacion(Pasillo, Fila, Columna)
            End Get
        End Property
    End Class

    Public Class TotalesDiferenciasInventarioDTO
        Public Property Filas As Integer
        Public Property Unidades As Integer
        Public Property Valor As Decimal
    End Class

    Public Class TotalGrupoDiferenciasInventarioDTO
        Public Property Grupo As String
        Public Property Filas As Integer
        Public Property Unidades As Integer
        Public Property Valor As Decimal
    End Class

    Public Class TotalFamiliaDiferenciasInventarioDTO
        Public Property Familia As String
        Public Property Filas As Integer
        Public Property Unidades As Integer
        Public Property Valor As Decimal
    End Class

    ''' <summary>«A01-002-003», o vacío si no hay ubicación.</summary>
    Public Shared Function TextoUbicacion(pasillo As String, fila As String, columna As String) As String
        Dim partes = {pasillo, fila, columna}.Where(Function(p) Not String.IsNullOrWhiteSpace(p)).Select(Function(p) p.Trim())
        Return String.Join("-", partes)
    End Function

    ''' <summary>«1 fila», «12 filas».</summary>
    Public Shared Function TextoFilas(n As Integer) As String
        Return $"{n} {If(n = 1, "fila", "filas")}"
    End Function

    ''' <summary>«1 usuario», «3 usuarios».</summary>
    Public Shared Function TextoUsuarios(n As Integer) As String
        Return $"{n} {If(n = 1, "usuario", "usuarios")}"
    End Function
End Class
