Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Runtime.CompilerServices

Partial Public Class PedidoVenta
    Public Class PedidoVentaDTO
        Implements INotifyPropertyChanged

        Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged
        Public Sub New()
            Me.LineasPedido = New ObservableCollection(Of LineaPedidoVentaDTO)()
            Me.Prepagos = New ObservableCollection(Of PrepagoDTO)
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
        Public Property notaEntrega As Boolean
        Private _descuentoPP As Decimal
        Public Property DescuentoPP As Decimal
            Get
                Return _descuentoPP
            End Get
            Set(value As Decimal)
                _descuentoPP = value
                OnPropertyChanged(NameOf(DescuentoPP))
                OnPropertyChanged(NameOf(baseImponible))
                OnPropertyChanged(NameOf(baseImponiblePicking))
                OnPropertyChanged(NameOf(total))
                OnPropertyChanged(NameOf(totalPicking))
            End Set
        End Property
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
            Return linea.bruto * (1 - sumaDescuentos(linea)) * (1 - DescuentoPP)
        End Function

        Private Function calcularTotalLinea(linea As LineaPedidoVentaDTO)
            If IsNothing(Me.iva) Then
                Return calcularBaseImponibleLinea(linea)
            ElseIf Me.iva = "R10" Then
                Return calcularBaseImponibleLinea(linea) * 1.1
            ElseIf Me.iva = "SR4" Then
                Return calcularBaseImponibleLinea(linea) * 1.04
            Else
                Return calcularBaseImponibleLinea(linea) * 1.21
            End If
        End Function

        Public Overridable Property LineasPedido() As ObservableCollection(Of LineaPedidoVentaDTO)
        Public Overridable Property Prepagos() As ObservableCollection(Of PrepagoDTO)
        Public Overridable Property VendedoresGrupoProducto As ObservableCollection(Of VendedorGrupoProductoDTO)

        Private Sub OnPropertyChanged(<CallerMemberName()> Optional ByVal propertyName As String = Nothing)
            RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(propertyName))
        End Sub

    End Class

End Class
