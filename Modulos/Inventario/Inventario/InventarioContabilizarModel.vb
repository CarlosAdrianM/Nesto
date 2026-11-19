Imports Newtonsoft.Json

' Nesto#597 (gemela de NestoAPI#790): lo que devuelven Comprobaciones, Contabilizar y Diferencias de api/Inventarios.
Partial Public Class InventarioModel

    ''' <summary>NestoAPI#790: GET api/Inventarios/Comprobaciones (y el cuerpo del 409 de POST api/Inventarios/Contabilizar).</summary>
    Public Class ComprobacionesInventarioDTO
        Public Property Almacen As String
        Public Property Fecha As Date
        Public Property ControlUbicaciones As Boolean
        ''' <summary>
        ''' Novedad 1364: estas comprobaciones cuentan las filas pendientes de días anteriores como del día (pedidas con
        ''' incluirDiasAnteriores). Al contabilizar se manda IncluirDiasAnteriores = True.
        ''' </summary>
        Public Property IncluyeDiasAnteriores As Boolean
        Public Property DiasAnterioresIncluidos As New List(Of Date)
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
        ''' <summary>
        ''' Novedad 1364: solo en OtraFecha, todas sus filas son de días anteriores al del inventario: se pueden contabilizar
        ''' junto con las del día («Contabilizar también la del dd/MM»).
        ''' </summary>
        Public Property SePuedeIncluir As Boolean
        Public Property Filas As New List(Of FilaComprobacionInventarioDTO)
        ''' <summary>«Título (N filas)» para la cabecera de cada comprobación.</summary>
        <JsonIgnore>
        Public ReadOnly Property Cabecera As String
            Get
                Dim n = If(Filas Is Nothing, 0, Filas.Count)
                Return $"{Titulo} ({TextoFilas(n)})"
            End Get
        End Property

        <JsonIgnore>
        Public ReadOnly Property EsOtraFecha As Boolean
            Get
                Return String.Equals(Codigo, CodigosComprobacionInventario.OTRA_FECHA, StringComparison.OrdinalIgnoreCase)
            End Get
        End Property

        ''' <summary>Novedad 1364: se enseña «Contabilizar también…» (OtraFecha con todas sus filas de días anteriores).</summary>
        <JsonIgnore>
        Public ReadOnly Property MostrarIncluir As Boolean
            Get
                Return EsOtraFecha AndAlso SePuedeIncluir
            End Get
        End Property

        ''' <summary>Novedad 1364: los días de las filas, «18/11» o «17/11 y 18/11».</summary>
        <JsonIgnore>
        Public ReadOnly Property TextoDias As String
            Get
                Return InventarioModel.TextoDias(If(Filas, New List(Of FilaComprobacionInventarioDTO)).
                    Where(Function(f) f IsNot Nothing AndAlso f.Fecha.HasValue).Select(Function(f) f.Fecha.Value))
            End Get
        End Property

        ''' <summary>Novedad 1364: «Contabilizar también la del 18/11» (o «las del 17/11 y 18/11» con varias filas).</summary>
        <JsonIgnore>
        Public ReadOnly Property TextoIncluir As String
            Get
                Dim n = If(Filas Is Nothing, 0, Filas.Count)
                Return $"Contabilizar también {If(n = 1, "la", "las")} del {TextoDias}"
            End Get
        End Property

        ''' <summary>
        ''' Novedad 1364 (Paloma, ALC): en OtraFecha, qué salidas hay y qué hace cada una, para no dejar a nadie sin saber qué hacer.
        ''' Vacío en los demás bloqueantes.
        ''' </summary>
        <JsonIgnore>
        Public ReadOnly Property ExplicacionSalidas As String
            Get
                If Not EsOtraFecha Then
                    Return String.Empty
                End If
                Dim hayBorrables = Filas IsNot Nothing AndAlso Filas.Any(Function(f) f IsNot Nothing AndAlso f.SePuedeBorrar)
                If SePuedeIncluir Then
                    Dim texto = $"«{TextoIncluir}»: se cuentan junto con las de este día y se contabilizan todas a la vez (antes se vuelven a comprobar)."
                    If hayBorrables Then
                        texto += " «Borrar esta fila»: la quita del recuento; ese producto no se contabiliza y se queda con el stock que diga el sistema."
                    End If
                    Return texto
                End If
                Dim posteriores = InventarioModel.TextoDias(If(Filas, New List(Of FilaComprobacionInventarioDTO)).
                    Where(Function(f) f IsNot Nothing AndAlso f.Fecha.HasValue).Select(Function(f) f.Fecha.Value))
                Dim aviso = $"Hay filas de otro día ({posteriores}) que no se pueden contabilizar en este. Cierre esta ventana, elija en la fecha " +
                    "el día más reciente y vuelva a contabilizar: allí podrá contabilizar también las de días anteriores."
                If hayBorrables Then
                    aviso += " También puede borrar las que tienen el botón (el producto se queda con el stock que diga el sistema)."
                End If
                Return aviso
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
        ''' <summary>Filas pendientes de otro día (el SP contabiliza todas las del usuario). Novedad 1364: se pueden incluir o borrar.</summary>
        Public Const OTRA_FECHA As String = "OtraFecha"
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
        ''' Incidencia 1186: lo decide la API (GET api/Inventarios/Comprobaciones); solo con True sale «Borrar esta fila». Ausente
        ''' (API de antes) o False, sin botón. Borrar NO pone a 0: deja el producto sin contar.
        ''' </summary>
        Public Property SePuedeBorrar As Boolean
    End Class

    Public Class RecuentoUsuarioInventarioDTO
        Public Property Usuario As String
        Public Property Filas As Integer
        ''' <summary>Novedad 1364: los días de sus filas (más de uno solo si se incluyen días anteriores).</summary>
        Public Property Dias As New List(Of Date)
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

    ''' <summary>Novedad 1364: «18/11», «17/11 y 18/11», «16/11, 17/11 y 18/11» (sin repetir, en orden).</summary>
    Public Shared Function TextoDias(dias As IEnumerable(Of Date)) As String
        Dim lista = If(dias, Enumerable.Empty(Of Date)()).Select(Function(d) d.Date).Distinct().OrderBy(Function(d) d).
            Select(Function(d) d.ToString("dd/MM", Globalization.CultureInfo.InvariantCulture)).ToList()
        If lista.Count <= 1 Then
            Return String.Join(String.Empty, lista)
        End If
        Return String.Join(", ", lista.Take(lista.Count - 1)) + " y " + lista.Last()
    End Function

    ''' <summary>«1 usuario», «3 usuarios».</summary>
    Public Shared Function TextoUsuarios(n As Integer) As String
        Return $"{n} {If(n = 1, "usuario", "usuarios")}"
    End Function
End Class
