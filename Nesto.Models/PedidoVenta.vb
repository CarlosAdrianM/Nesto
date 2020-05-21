Imports System.Collections.ObjectModel
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
    Public Class PedidoVentaDTO
        Implements INotifyPropertyChanged

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
        Public Sub New()
            Me.LineasPedido = New ObservableCollection(Of LineaPedidoVentaDTO)()
        End Sub

        Public Property empresa() As String
        Public Property numero() As Integer
        Public Property cliente() As String

        Private _contacto As String
        Public Property contacto() As String
            Get
                Return _contacto
            End Get
            Set(value As String)
                _contacto = value
                OnPropertyChanged("contacto")
            End Set
        End Property
        Public Property fecha() As Nullable(Of System.DateTime)
        Public Property formaPago() As String
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
        Public Property EsPresupuesto() As Boolean = False
        Public Property usuario() As String

        Public ReadOnly Property baseImponible As Decimal
            Get
                Dim baseImponibleParcial As Decimal
                For Each linea In LineasPedido
                    baseImponibleParcial += calcularBaseImponibleLinea(linea)
                Next
                Return baseImponibleParcial
            End Get
        End Property

        Public ReadOnly Property bruto As Decimal
            Get
                Return LineasPedido.Sum(Function(l) l.bruto)
            End Get
        End Property

        Public ReadOnly Property total As Decimal
            Get
                Dim totalParcial As Decimal
                For Each linea In LineasPedido
                    totalParcial += calcularTotalLinea(linea)
                Next
                Return Math.Round(totalParcial, 2, MidpointRounding.AwayFromZero)
            End Get
        End Property

        Public ReadOnly Property baseImponiblePicking As Decimal
            Get
                Dim baseImponibleParcial As Decimal
                For Each linea In LineasPedido.Where(Function(l) l.picking > 0)
                    baseImponibleParcial += calcularBaseImponibleLinea(linea)
                Next
                Return baseImponibleParcial
            End Get
        End Property

        Public ReadOnly Property brutoPicking As Decimal
            Get
                Return LineasPedido.Where(Function(l) l.picking > 0).Sum(Function(l) l.bruto)
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
                For Each linea In LineasPedido.Where(Function(l) l.picking > 0)
                    totalParcial += calcularTotalLinea(linea)
                Next
                Return totalParcial
            End Get
        End Property

        Private Function calcularBaseImponibleLinea(linea As LineaPedidoVentaDTO)
            Return linea.bruto * (1 - sumaDescuentos(linea))
        End Function

        Private Function calcularTotalLinea(linea As LineaPedidoVentaDTO)
            If IsNothing(Me.iva) Then
                Return calcularBaseImponibleLinea(linea)
            ElseIf Me.iva = "R10" Then
                Return calcularBaseImponibleLinea(linea) * 1.1
            Else
                Return calcularBaseImponibleLinea(linea) * 1.21
            End If
        End Function

        Public Overridable Property LineasPedido() As ObservableCollection(Of LineaPedidoVentaDTO)
        Public Overridable Property VendedoresGrupoProducto As ObservableCollection(Of VendedorGrupoProductoDTO)

        Private Sub OnPropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
        End Sub

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
        Public Property grupoProducto As String
        Public Property usuario As String
        Private Sub OnPropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
        End Sub
    End Class

End Class
