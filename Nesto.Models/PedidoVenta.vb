Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Partial Public Class PedidoVenta

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
                OnPropertyChanged("total")
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


        Public ReadOnly Property bruto As Decimal
            Get
                Return (cantidad * (Decimal.Truncate(precio * 10000) / 10000))
            End Get
        End Property

        'Public ReadOnly Property baseImponible As Decimal
        '    Get
        '        Return bruto * (1 - descuento)
        '    End Get
        'End Property

        'Public ReadOnly Property sumaDescuentosLinea As Decimal
        '    Get
        '        ' (1-(1-(inserted.descuentocliente)) * (1-(inserted.descuentoproducto)) * (1-(inserted.descuento)) * (1-(inserted.descuentopp)))
        '        Return 1 - ((1 - descuento) * (1 - descuentoProducto))
        '    End Get
        'End Property
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
    Public Class PrepagoDTO
        Public Property Importe As Decimal
        Public Property Factura As String
        Public Property CuentaContable As String
        Public Property ConceptoAdicional As String
    End Class
    Public Class VendedorGrupoProductoDTO
        Implements INotifyPropertyChanged

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

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
        Private _estado As Short
        Public Property estado As Short
            Get
                Return _estado
            End Get
            Set(value As Short)
                _estado = value
                OnPropertyChanged(NameOf(estado))
            End Set
        End Property
        Public Property grupoProducto As String
        Public Property usuario As String
        Private Sub OnPropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
        End Sub
    End Class

End Class
