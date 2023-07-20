Imports Nesto.Models
Imports Prism.Mvvm

Public Class LineaPedidoVentaWrapper
    Inherits BindableBase
    Public Sub New()
        ' Este constructor solo permite la entrada desde el Datagrid
        Model = New LineaPedidoVentaDTO() With {
            .Cantidad = 1
        }
    End Sub
    Public Sub New(model As LineaPedidoVentaDTO)
        Me.Model = model
    End Sub
    Public Property Model As LineaPedidoVentaDTO
    Private _pedido As PedidoVentaWrapper
    Public Property Pedido As PedidoVentaWrapper
        Get
            Return _pedido
        End Get
        Set(value As PedidoVentaWrapper)
            SetProperty(_pedido, value)
        End Set
    End Property

    Public Property almacen() As String

    Public Property AplicarDescuento() As Boolean
        Get
            Return Model.AplicarDescuento
        End Get
        Set(value As Boolean)
            Model.AplicarDescuento = value
            RaisePropertyChanged(NameOf(AplicarDescuento))
            RaisePropertyChanged(NameOf(SumaDescuentos))
            RaisePropertyChanged(NameOf(BaseImponible))
            RaisePropertyChanged(NameOf(Total))
        End Set
    End Property

    Public Property Cantidad() As Short
        Get
            Return Model.Cantidad
        End Get
        Set(value As Short)
            Model.Cantidad = value
            RaisePropertyChanged(NameOf(Cantidad))
            RaisePropertyChanged(NameOf(BaseImponible))
            RaisePropertyChanged(NameOf(Total))
        End Set
    End Property
    Public Property delegacion() As String
    Public Property DescuentoCliente As Decimal
        Get
            Return Model.DescuentoCliente
        End Get
        Set(value As Decimal)
            Model.DescuentoCliente = value
            RaisePropertyChanged(NameOf(DescuentoCliente))
            RaisePropertyChanged(NameOf(SumaDescuentos))
            RaisePropertyChanged(NameOf(BaseImponible))
            RaisePropertyChanged(NameOf(Total))
        End Set
    End Property
    Public Property DescuentoLinea() As Decimal
        Get
            Return Model.DescuentoLinea
        End Get
        Set(value As Decimal)
            Model.DescuentoLinea = value
            RaisePropertyChanged(NameOf(DescuentoLinea))
            RaisePropertyChanged(NameOf(SumaDescuentos))
            RaisePropertyChanged(NameOf(BaseImponible))
            RaisePropertyChanged(NameOf(Total))
        End Set
    End Property

    Public Property DescuentoProducto As Decimal
        Get
            Return Model.DescuentoProducto
        End Get
        Set(value As Decimal)
            Model.DescuentoProducto = value
            RaisePropertyChanged(NameOf(DescuentoProducto))
            RaisePropertyChanged(NameOf(SumaDescuentos))
            RaisePropertyChanged(NameOf(BaseImponible))
            RaisePropertyChanged(NameOf(Total))
        End Set
    End Property
    Public ReadOnly Property BaseImponible As Decimal
        Get
            Return Model.BaseImponible
        End Get
    End Property
    Public ReadOnly Property Total As Decimal
        Get
            Return Model.Total
        End Get
    End Property
    Public Property EsPresupuesto As Boolean
    Public Property estado() As Short
        Get
            Return Model.estado
        End Get
        Set(value As Short)
            Model.estado = value
            RaisePropertyChanged(NameOf(estado))
        End Set
    End Property
    Public Property fechaEntrega() As System.DateTime
        Get
            Return Model.fechaEntrega
        End Get
        Set(value As System.DateTime)
            Model.fechaEntrega = value
            RaisePropertyChanged(NameOf(fechaEntrega))
        End Set
    End Property
    Public Property formaVenta() As String
        Get
            Return Model.formaVenta
        End Get
        Set(value As String)
            Model.formaVenta = value
        End Set
    End Property

    Public Property id() As Integer
        Get
            Return Model.id
        End Get
        Set(value As Integer)
            Model.id = value
        End Set
    End Property
    Public Property iva() As String
        Get
            Return Model.iva
        End Get
        Set(value As String)
            Model.iva = value
            RaisePropertyChanged(NameOf(iva))
            RaisePropertyChanged(NameOf(Total))
        End Set
    End Property
    Public Property oferta() As Integer?
    Public Property picking() As Integer
    Public Property PrecioUnitario() As Decimal
        Get
            Return Model.PrecioUnitario
        End Get
        Set(value As Decimal)
            Model.PrecioUnitario = value
            RaisePropertyChanged(NameOf(PrecioUnitario))
            RaisePropertyChanged(NameOf(Bruto))
            RaisePropertyChanged(NameOf(BaseImponible))
            RaisePropertyChanged(NameOf(Total))
        End Set
    End Property
    Public Property Producto() As String
        Get
            Return Model.Producto
        End Get
        Set(value As String)
            Model.Producto = value
            RaisePropertyChanged(NameOf(Producto))
        End Set
    End Property
    Public Property texto As String
        Get
            Return Model.texto
        End Get
        Set(value As String)
            Model.texto = value
            RaisePropertyChanged(NameOf(texto))
        End Set
    End Property
    Public Property tipoLinea() As Nullable(Of Byte)
        Get
            Return Model.tipoLinea
        End Get
        Set(value As Nullable(Of Byte))
            Model.tipoLinea = value
            RaisePropertyChanged(NameOf(tipoLinea))
        End Set
    End Property
    Public Property Usuario() As String
        Get
            Return Model.Usuario
        End Get
        Set(value As String)
            Model.Usuario = value
            RaisePropertyChanged(NameOf(Usuario))
        End Set
    End Property
    Public Property vistoBueno() As Boolean
        Get
            Return Model.vistoBueno
        End Get
        Set(value As Boolean)
            Model.vistoBueno = value
            RaisePropertyChanged(NameOf(vistoBueno))
        End Set
    End Property

    Public ReadOnly Property Bruto As Decimal
        Get
            Return Model.Bruto
        End Get
    End Property

    Public ReadOnly Property estaAlbaraneada() As Boolean
        Get
            Return Model.estaAlbaraneada
        End Get
    End Property

    Public ReadOnly Property estaFacturada As Boolean
        Get
            Return Model.estaFacturada
        End Get
    End Property

    Public ReadOnly Property tienePicking As Boolean
        Get
            Return Model.tienePicking
        End Get
    End Property

    Public ReadOnly Property SumaDescuentos As Decimal
        Get
            Return Model.SumaDescuentos
        End Get
    End Property
End Class
