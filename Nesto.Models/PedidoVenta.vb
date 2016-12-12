Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Public Class PedidoVenta
    Public Class LineaPedidoVentaDTO
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
                OnPropertyChanged("aplicarDescuento")
            End Set
        End Property
        Public Property cantidad() As Short = 1
        Public Property delegacion() As String
        Private _descuento As Decimal
        Public Property descuento() As Decimal
            Get
                Return _descuento
            End Get
            Set(value As Decimal)
                _descuento = value
                OnPropertyChanged("descuento")
            End Set
        End Property
        Public Property descuentoProducto() As Decimal
        Public Property estado() As Short = 1
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
        Public Property iva() As String
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
            End Set
        End Property
        Private _producto As String
        Public Property producto() As String
            Get
                Return _producto
            End Get
            Set(value As String)
                _producto = value
                OnPropertyChanged("producto")
            End Set
        End Property
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
        Public Property usuario() As String
        Public Property vistoBueno() As Boolean


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
    Public Class PedidoVentaDTO
        Public Sub New()
            Me.LineasPedido = New ObservableCollection(Of LineaPedidoVentaDTO)()
        End Sub

        Public Property empresa() As String
        Public Property numero() As Integer
        Public Property cliente() As String
        Public Property contacto() As String
        Public Property fecha() As Nullable(Of System.DateTime)
        Public Property formaPago() As String
        Public Property plazosPago() As String
        Public Property primerVencimiento() As Nullable(Of System.DateTime)
        Public Property iva() As String
        Public Property vendedor() As String
        Public Property comentarios() As String
        Public Property comentarioPicking() As String
        Public Property periodoFacturacion() As String
        Public Property ruta() As String
        Public Property serie() As String
        Public Property ccc() As String
        Public Property origen() As String
        Public Property contactoCobro() As String
        Public Property noComisiona() As Decimal
        Public Property vistoBuenoPlazosPago() As Boolean
        Public Property mantenerJunto() As Boolean
        Public Property servirJunto() As Boolean
        Public Property usuario() As String

        Public Overridable Property LineasPedido() As ObservableCollection(Of LineaPedidoVentaDTO)

    End Class

End Class
