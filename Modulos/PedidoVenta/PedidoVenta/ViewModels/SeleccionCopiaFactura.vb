Imports CommunityToolkit.Mvvm.ComponentModel
Imports Nesto.Modulos.PedidoVenta.Models.Rectificativas

''' <summary>
''' Nesto#638 (NestoAPI#899/#863): una línea de la factura en «Solo unas líneas o albaranes…»: si se marca, cuántas unidades y
''' si vuelve estropeada. La cantidad nunca pasa del tope (<see cref="Tope"/>: en un abono, lo que queda por abonar; en una
''' copia, lo facturado).
''' </summary>
Public Class LineaCopiaElegible
    Inherits ObservableObject

    Private ReadOnly _alCambiar As Action

    Public Sub New(linea As LineaFacturaParaCopiarDTO, tope As Integer, Optional alCambiar As Action = Nothing)
        Me.Linea = linea
        Me.Tope = Math.Max(0, tope)
        _alCambiar = alCambiar
    End Sub

    Public ReadOnly Property Linea As LineaFacturaParaCopiarDTO
    Public ReadOnly Property Tope As Integer

    Public ReadOnly Property Pedido As Integer
        Get
            Return Linea.Pedido
        End Get
    End Property
    Public ReadOnly Property NumeroOrden As Integer
        Get
            Return Linea.NumeroOrden
        End Get
    End Property
    Public ReadOnly Property Albaran As Integer?
        Get
            Return Linea.Albaran
        End Get
    End Property
    Public ReadOnly Property Producto As String
        Get
            Return Linea.Producto?.Trim()
        End Get
    End Property
    Public ReadOnly Property Texto As String
        Get
            Return Linea.Texto?.Trim()
        End Get
    End Property
    ''' <summary>Lo facturado, en positivo.</summary>
    Public ReadOnly Property Facturadas As Integer
        Get
            Return CInt(Math.Abs(Linea.Cantidad))
        End Get
    End Property
    Public ReadOnly Property YaAbonadas As Decimal
        Get
            Return Linea.YaAbonada
        End Get
    End Property
    Public ReadOnly Property Quedan As Decimal
        Get
            Return Linea.QuedanPorAbonar
        End Get
    End Property
    Public ReadOnly Property EsCuentaContable As Boolean
        Get
            Return Linea.EsCuentaContable
        End Get
    End Property
    Public ReadOnly Property SePuedeMarcar As Boolean
        Get
            Return Tope > 0
        End Get
    End Property

    Private _marcada As Boolean
    Public Property Marcada As Boolean
        Get
            Return _marcada
        End Get
        Set(value As Boolean)
            If value AndAlso Not SePuedeMarcar Then
                value = False
            End If
            If SetProperty(_marcada, value) Then
                If value AndAlso _cantidad = 0 Then
                    Cantidad = Tope ' por defecto, todo lo que se puede
                End If
                If Not value Then
                    NoVendible = False
                End If
                OnPropertyChanged(NameOf(Importe))
                _alCambiar?.Invoke()
            End If
        End Set
    End Property

    Private _cantidad As Integer
    ''' <summary>Las unidades que se copian o abonan (en positivo), entre 0 y <see cref="Tope"/>.</summary>
    Public Property Cantidad As Integer
        Get
            Return _cantidad
        End Get
        Set(value As Integer)
            Dim ajustada As Integer = Math.Min(Math.Max(0, value), Tope)
            If SetProperty(_cantidad, ajustada) Then
                OnPropertyChanged(NameOf(Importe))
                _alCambiar?.Invoke()
            ElseIf ajustada <> value Then
                OnPropertyChanged(NameOf(Cantidad)) ' la casilla vuelve a enseñar el tope
            End If
        End Set
    End Property

    Private _noVendible As Boolean
    Public Property NoVendible As Boolean
        Get
            Return _noVendible
        End Get
        Set(value As Boolean)
            Dim unused = SetProperty(_noVendible, value AndAlso _marcada)
        End Set
    End Property

    ''' <summary>La base imponible de lo marcado (proporcional a las unidades).</summary>
    Public ReadOnly Property Importe As Decimal
        Get
            If Not _marcada OrElse Facturadas = 0 Then
                Return 0D
            End If
            Return Math.Round(Math.Abs(Linea.BaseImponible) * _cantidad / Facturadas, 2, MidpointRounding.AwayFromZero)
        End Get
    End Property
End Class

''' <summary>Nesto#638: un albarán de la factura; marcarlo marca (o desmarca) sus líneas que no son de cuenta contable.</summary>
Public Class AlbaranCopiaElegible
    Inherits ObservableObject

    Private ReadOnly _alMarcar As Action(Of AlbaranCopiaElegible)

    Public Sub New(albaran As Integer, fecha As Date?, Optional alMarcar As Action(Of AlbaranCopiaElegible) = Nothing)
        Me.Albaran = albaran
        Me.Fecha = fecha
        _alMarcar = alMarcar
    End Sub

    Public ReadOnly Property Albaran As Integer
    Public ReadOnly Property Fecha As Date?
    Public ReadOnly Property Descripcion As String
        Get
            Return If(Fecha.HasValue, $"Albarán {Albaran} ({Fecha.Value:dd/MM/yy})", $"Albarán {Albaran}")
        End Get
    End Property

    Private _marcado As Boolean
    Public Property Marcado As Boolean
        Get
            Return _marcado
        End Get
        Set(value As Boolean)
            If SetProperty(_marcado, value) Then
                _alMarcar?.Invoke(Me)
            End If
        End Set
    End Property
End Class

''' <summary>Nesto#638: lo que va en la petición de CopiarFactura con lo marcado.</summary>
Public Class SeleccionParaCopiar
    Public Property Albaranes As New List(Of Integer)
    Public Property Lineas As New List(Of LineaACopiarDTO)
    Public Property LineasNoVendibles As New List(Of Integer)
    Public ReadOnly Property HayAlgo As Boolean
        Get
            Return Albaranes.Any() OrElse Lineas.Any()
        End Get
    End Property
End Class

''' <summary>
''' Nesto#638: LAS REGLAS (puras) de la pantalla de «Solo unas líneas o albaranes…». Las de verdad (qué se puede copiar, no
''' abonar dos veces) siguen en la API (ReglaSeleccionCopiaFactura, ReglasAbonoCopiaFactura).
''' </summary>
Public Module ReglasSeleccionCopiaFactura

    ''' <summary>Hasta dónde se puede marcar una línea: en un abono, lo que queda por abonar; en una copia, lo facturado.</summary>
    Public Function Tope(linea As LineaFacturaParaCopiarDTO, esAbono As Boolean) As Integer
        If linea Is Nothing Then
            Return 0
        End If
        Dim maximo As Decimal = If(esAbono, linea.QuedanPorAbonar, Math.Abs(linea.Cantidad))
        Return CInt(Math.Floor(Math.Max(0D, maximo)))
    End Function

    ''' <summary>
    ''' La selección para la API. Un albarán va como albarán solo si TODAS sus líneas que no son de cuenta contable están
    ''' marcadas enteras y sin nada abonado (así el comentario del abono dice «albarán 101»); si no, sus líneas van una a una.
    ''' Las de cuenta contable (portes, comisión) van siempre como línea, y solo si se marcan. El pedido de la línea solo va si
    ''' su Nº Orden se repite en otro pedido de la factura.
    ''' </summary>
    Public Function Construir(lineas As IEnumerable(Of LineaCopiaElegible)) As SeleccionParaCopiar
        Dim todas As List(Of LineaCopiaElegible) = If(lineas, Enumerable.Empty(Of LineaCopiaElegible)()).ToList()
        Dim seleccion As New SeleccionParaCopiar
        Dim ordenesRepetidos As HashSet(Of Integer) = New HashSet(Of Integer)(
            todas.GroupBy(Function(l) l.NumeroOrden).Where(Function(g) g.Select(Function(l) l.Pedido).Distinct().Count() > 1).Select(Function(g) g.Key))

        Dim albaranesEnteros As New HashSet(Of Integer)
        For Each grupo In todas.Where(Function(l) l.Albaran.HasValue AndAlso Not l.EsCuentaContable).GroupBy(Function(l) l.Albaran.Value)
            If grupo.All(Function(l) l.Marcada AndAlso l.Cantidad = l.Facturadas AndAlso l.YaAbonadas = 0D) Then
                albaranesEnteros.Add(grupo.Key)
            End If
        Next
        seleccion.Albaranes.AddRange(albaranesEnteros.OrderBy(Function(a) a))

        For Each linea In todas.Where(Function(l) l.Marcada AndAlso l.Cantidad > 0)
            Dim vaEnSuAlbaran As Boolean = Not linea.EsCuentaContable AndAlso linea.Albaran.HasValue AndAlso albaranesEnteros.Contains(linea.Albaran.Value)
            If Not vaEnSuAlbaran Then
                seleccion.Lineas.Add(New LineaACopiarDTO With {
                    .NumeroOrden = linea.NumeroOrden,
                    .Pedido = If(ordenesRepetidos.Contains(linea.NumeroOrden), linea.Pedido, CType(Nothing, Integer?)),
                    .Cantidad = CShort(linea.Cantidad)
                })
            End If
            If linea.NoVendible Then
                seleccion.LineasNoVendibles.Add(linea.NumeroOrden)
            End If
        Next
        Return seleccion
    End Function

    ''' <summary>El total de lo marcado, a la vista: «Marcadas 3 líneas, 7 unidades, 123,45 €».</summary>
    Public Function Resumen(lineas As IEnumerable(Of LineaCopiaElegible)) As String
        Dim marcadas As List(Of LineaCopiaElegible) = If(lineas, Enumerable.Empty(Of LineaCopiaElegible)()).
            Where(Function(l) l.Marcada AndAlso l.Cantidad > 0).ToList()
        If Not marcadas.Any() Then
            Return "No hay nada marcado: marque albaranes o líneas."
        End If
        Dim unidades As Integer = marcadas.Sum(Function(l) l.Cantidad)
        Dim importe As Decimal = marcadas.Sum(Function(l) l.Importe)
        Dim noVendibles As Integer = marcadas.Where(Function(l) l.NoVendible).Count()
        Dim texto As String = $"Marcadas {marcadas.Count} {If(marcadas.Count = 1, "línea", "líneas")}, {unidades} {If(unidades = 1, "unidad", "unidades")}, {importe.ToString("N2", Globalization.CultureInfo.GetCultureInfo("es-ES"))} € (base imponible)."
        If noVendibles > 0 Then
            texto &= $" {noVendibles} {If(noVendibles = 1, "vuelve estropeada", "vuelven estropeadas")} (no vendible)."
        End If
        Return texto
    End Function
End Module
