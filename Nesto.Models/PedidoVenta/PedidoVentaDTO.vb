Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Runtime.CompilerServices


Public Class PedidoVentaDTO
    Inherits PedidoBase(Of LineaPedidoVentaDTO)
    Implements INotifyPropertyChanged

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
    Public Sub New()
        Me.Lineas = New ObservableCollection(Of LineaPedidoVentaDTO)()
        Me.Prepagos = New ObservableCollection(Of PrepagoDTO)
        Me.Efectos = New ListaEfectos()
    End Sub

    Public Property empresa() As String
    Public Property numero() As Integer
    Private _cliente As String
    Public Property cliente() As String
        Get
            Return _cliente
        End Get
        Set(value As String)
            _cliente = value
            OnPropertyChanged(NameOf(cliente))
        End Set
    End Property

    Private _contacto As String
    Public Property contacto() As String
        Get
            Return _contacto
        End Get
        Set(value As String)
            _contacto = value
            OnPropertyChanged(NameOf(contacto))
        End Set
    End Property
    Public Property fecha() As Nullable(Of System.DateTime)
    Private _formaPago As String
    Public Property formaPago() As String
        Get
            Return _formaPago
        End Get
        Set(value As String)
            If _formaPago <> value Then
                _formaPago = value
                OnPropertyChanged(NameOf(formaPago))
                Efectos.FormaPagoCliente = value
            End If
        End Set
    End Property
    Public Property plazosPago() As String
    Public Property primerVencimiento() As Nullable(Of System.DateTime)
    Public Property iva() As String

    Private _vendedor As String
    Public Property vendedor As String
        Get
            Return _vendedor
        End Get
        Set(value As String)
            _vendedor = value
            OnPropertyChanged("vendedor")
        End Set
    End Property
    Public Property comentarios() As String
    Public Property comentarioPicking() As String
    Private _crearEfectosManualmente As Boolean
    Public Property CrearEfectosManualmente As Boolean
        Get
            Return _crearEfectosManualmente
        End Get
        Set(value As Boolean)
            If _crearEfectosManualmente <> value Then
                _crearEfectosManualmente = value
                OnPropertyChanged(NameOf(CrearEfectosManualmente))
            End If
        End Set
    End Property
    Public Property periodoFacturacion() As String
    Public Property ruta() As String
    Public Property serie() As String
    Private _ccc As String
    Public Property ccc() As String
        Get
            Return _ccc
        End Get
        Set(value As String)
            If _ccc <> value Then
                _ccc = value
                OnPropertyChanged(NameOf(ccc))
                Efectos.CccCliente = value
            End If
        End Set
    End Property
    Public Property origen() As String
    Public Property contactoCobro() As String
    Public Property noComisiona() As Decimal
    Public Property vistoBuenoPlazosPago() As Boolean
    Public Property mantenerJunto() As Boolean
    Public Property servirJunto() As Boolean
    Public Property EsPresupuesto() As Boolean = False
    Public Property notaEntrega As Boolean
    Private _descuentoPP As Decimal
    Public Property DescuentoPP As Decimal
        Get
            Return _descuentoPP
        End Get
        Set(value As Decimal)
            _descuentoPP = value
            OnPropertyChanged(NameOf(DescuentoPP))
            OnPropertyChanged(NameOf(BaseImponible))
            OnPropertyChanged(NameOf(baseImponiblePicking))
            OnPropertyChanged(NameOf(Total))
            OnPropertyChanged(NameOf(totalPicking))
        End Set
    End Property
    'Public Property Usuario() As String

    'Public ReadOnly Property BaseImponible As Decimal
    '    Get
    '        Dim baseImponibleParcial As Decimal
    '        For Each linea In Lineas
    '            baseImponibleParcial += calcularBaseImponibleLinea(linea)
    '        Next
    '        Return baseImponibleParcial
    '    End Get
    'End Property

    Public ReadOnly Property bruto As Decimal
        Get
            Return Lineas.Sum(Function(l) l.bruto)
        End Get
    End Property

    'Public ReadOnly Property Total As Decimal
    '    Get
    '        Dim totalParcial As Decimal
    '        For Each linea In Lineas
    '            totalParcial += calcularTotalLinea(linea)
    '        Next
    '        Return Math.Round(totalParcial, 2, MidpointRounding.AwayFromZero)
    '    End Get
    'End Property

    Public ReadOnly Property baseImponiblePicking As Decimal
        Get
            Dim baseImponibleParcial As Decimal
            For Each linea In Lineas.Where(Function(l) l.picking > 0)
                baseImponibleParcial += calcularBaseImponibleLinea(linea)
            Next
            Return baseImponibleParcial
        End Get
    End Property

    Public ReadOnly Property brutoPicking As Decimal
        Get
            Return Lineas.Where(Function(l) l.picking > 0).Sum(Function(l) l.bruto)
        End Get
    End Property

    Public ReadOnly Property sumaDescuentos(linea As LineaPedidoVentaDTO) As Decimal
        Get
            ' Si no está marcado el aplicar descuento, solo aplica el de la propia línea
            If Not linea.aplicarDescuento Then
                Return linea.descuento
            End If

            ' falta añadir el descuento del cliente y el descuento PP
            Return 1 - ((1 - linea.descuento) * (1 - linea.descuentoProducto))
        End Get
    End Property

    Public ReadOnly Property totalPicking As Decimal
        Get
            Dim totalParcial As Decimal
            For Each linea In Lineas.Where(Function(l) l.picking > 0)
                totalParcial += calcularTotalLinea(linea)
            Next
            Return totalParcial
        End Get
    End Property

    Private Function calcularBaseImponibleLinea(linea As LineaPedidoVentaDTO)
        ' Una vez veamos que funciona, eliminar esta función y usar el campo directamente
        'Return linea.bruto * (1 - sumaDescuentos(linea)) * (1 - DescuentoPP)
        Return linea.BaseImponible
    End Function

    Private Function calcularTotalLinea(linea As LineaPedidoVentaDTO)
        ' Una vez veamos que funciona, eliminar esta función y usar el campo directamente
        'If IsNothing(Me.iva) OrElse (iva = "EX") Then
        '    Return calcularBaseImponibleLinea(linea)
        'ElseIf Me.iva = "R10" Then
        '    Return calcularBaseImponibleLinea(linea) * 1.1
        'ElseIf Me.iva = "SR4" Then
        '    Return calcularBaseImponibleLinea(linea) * 1.04
        'Else
        '    Return calcularBaseImponibleLinea(linea) * 1.21
        'End If
        Return linea.Total
    End Function

    'Public Overridable Property Lineas() As ObservableCollection(Of LineaPedidoVentaDTO)
    Public Overridable Property Efectos As ListaEfectos
    Public Overridable Property Prepagos() As ObservableCollection(Of PrepagoDTO)
    Public Overridable Property VendedoresGrupoProducto As ObservableCollection(Of VendedorGrupoProductoDTO)

    Private Sub OnPropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub

End Class

