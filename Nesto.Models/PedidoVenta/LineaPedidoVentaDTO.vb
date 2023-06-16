Imports System.ComponentModel
Imports System.Runtime.CompilerServices


Public Class LineaPedidoVentaDTO
    Inherits LineaPedidoBase
    Implements INotifyPropertyChanged


    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Property almacen() As String
    Private _aplicarDescuento As Boolean
    Public Property aplicarDescuento() As Boolean
        Get
            Return _aplicarDescuento
        End Get
        Set(value As Boolean)
            _aplicarDescuento = value
            OnPropertyChanged()
            OnPropertyChanged("total")
        End Set
    End Property

    Private _cantidad As Short = 1
    Public Property cantidad() As Short
        Get
            Return _cantidad
        End Get
        Set(value As Short)
            _cantidad = value
            OnPropertyChanged()
            OnPropertyChanged("total")
        End Set
    End Property
    Public Property delegacion() As String
    Private _descuento As Decimal
    Public Property descuento() As Decimal
        Get
            Return _descuento
        End Get
        Set(value As Decimal)
            _descuento = value
            OnPropertyChanged("descuento")
            OnPropertyChanged("total")
        End Set
    End Property
    Private _descuentoProducto As Decimal
    Public Property descuentoProducto As Decimal
        Get
            Return _descuentoProducto
        End Get
        Set(value As Decimal)
            _descuentoProducto = value
            OnPropertyChanged()
            OnPropertyChanged("total") '??? 15/06/23: ese campo no existe
        End Set
    End Property
    Public Property EsPresupuesto As Boolean
    Private _estado As Short = 1
    Public Property estado() As Short
        Get
            Return _estado
        End Get
        Set(value As Short)
            _estado = value
            OnPropertyChanged("estado")
        End Set
    End Property
    Private _fechaEntrega As System.DateTime
    Public Property fechaEntrega() As System.DateTime
        Get
            Return _fechaEntrega
        End Get
        Set(value As System.DateTime)
            _fechaEntrega = value
            OnPropertyChanged("fechaEntrega")
        End Set
    End Property
    Public Property formaVenta() As String
    Public Property id() As Integer
    Private _iva As String
    Public Property iva() As String
        Get
            Return _iva
        End Get
        Set(value As String)
            _iva = value
            OnPropertyChanged()
            OnPropertyChanged("total")
        End Set
    End Property
    Public Property oferta() As Integer?
    Public Property picking() As Integer
    Private _precio As Decimal
    Public Property precio() As Decimal
        Get
            Return _precio
        End Get
        Set(value As Decimal)
            _precio = value
            OnPropertyChanged("precio")
            OnPropertyChanged("total")
        End Set
    End Property
    'Private _producto As String
    'Public Property Producto() As String
    '    Get
    '        Return _producto
    '    End Get
    '    Set(value As String)
    '        _producto = value
    '        OnPropertyChanged(NameOf(Producto))
    '    End Set
    'End Property
    Private _texto As String
    Public Property texto As String
        Get
            Return _texto
        End Get
        Set(value As String)
            _texto = value
            OnPropertyChanged("texto")
        End Set
    End Property
    Public Property tipoLinea() As Nullable(Of Byte) = 1
    'Public Property Usuario() As String
    Public Property vistoBueno() As Boolean


    Public ReadOnly Property bruto As Decimal
        Get
            Return (cantidad * (Decimal.Truncate(precio * 10000) / 10000))
        End Get
    End Property

    Public ReadOnly Property estaAlbaraneada() As Boolean
        Get
            Return estado >= 2
        End Get
    End Property

    Public ReadOnly Property estaFacturada As Boolean
        Get
            Return estado >= 4
        End Get
    End Property

    Public ReadOnly Property tienePicking As Boolean
        Get
            Return picking <> 0
        End Get
    End Property

    Public Function ShallowCopy() As LineaPedidoVentaDTO
        Return DirectCast(Me.MemberwiseClone(), LineaPedidoVentaDTO)
    End Function

    ' This method is called by the Set accessor of each property.
    ' The CallerMemberName attribute that is applied to the optional propertyName
    ' parameter causes the property name of the caller to be substituted as an argument.
    Private Sub OnPropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
    End Sub
End Class



