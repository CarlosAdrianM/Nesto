Imports Nesto.Models
Imports Prism.Mvvm

Public Class LineaPedidoVentaWrapper
    Inherits BindableBase
    Public Sub New()
        ' Este constructor solo permite la entrada desde el Datagrid
        Model = New LineaPedidoVentaDTO() With {
            .Cantidad = 1,
            .tipoLinea = 1
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
            Dim unused = SetProperty(_pedido, value)
        End Set
    End Property

    Public Property Almacen() As String
        Get
            Return Model.almacen
        End Get
        Set(value As String)
            Model.almacen = value
            RaisePropertyChanged(NameOf(Almacen))
        End Set
    End Property

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
            ' Issue #258: En líneas de texto (TipoLinea=0), Cantidad debe ser 0
            If tipoLinea.HasValue AndAlso tipoLinea.Value = 0 AndAlso value <> 0 Then
                Return ' No permitir cantidad distinta de 0 en líneas de texto
            End If
            Model.Cantidad = value
            RaisePropertyChanged(NameOf(Cantidad))
            RaisePropertyChanged(NameOf(BaseImponible))
            RaisePropertyChanged(NameOf(Total))
        End Set
    End Property
    Public Property delegacion() As String
        Get
            Return Model.delegacion
        End Get
        Set(value As String)
            Model.delegacion = value
            RaisePropertyChanged(NameOf(delegacion))
        End Set
    End Property
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
    Public Property Factura As String
        Get
            Return Model.Factura
        End Get
        Set(value As String)
            Model.Factura = value
            RaisePropertyChanged(NameOf(Factura))
        End Set
    End Property
    Public Property Albaran As Integer?
        Get
            Return Model.Albaran
        End Get
        Set(value As Integer?)
            Model.Albaran = value
            RaisePropertyChanged(NameOf(Albaran))
        End Set
    End Property
    Public Property fechaEntrega() As Date
        Get
            Return Model.fechaEntrega
        End Get
        Set(value As Date)
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
            Dim parametroIva = Pedido.Model.ParametrosIva.SingleOrDefault(Function(p) p.CodigoIvaProducto.ToLower() = value.ToLower())
            If Not IsNothing(parametroIva) Then
                Model.iva = parametroIva.CodigoIvaProducto
                Model.PorcentajeIva = parametroIva.PorcentajeIvaProducto
                Model.PorcentajeRecargoEquivalencia = parametroIva.PorcentajeIvaRecargoEquivalencia
            Else
                Model.iva = value
                Model.PorcentajeIva = 0
                Model.PorcentajeRecargoEquivalencia = 0
            End If

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
            ' Issue #258: En líneas de texto (TipoLinea=0), Precio debe ser 0
            If tipoLinea.HasValue AndAlso tipoLinea.Value = 0 AndAlso value <> 0 Then
                Return ' No permitir precio distinto de 0 en líneas de texto
            End If
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
            ' Issue #258: En líneas de texto (TipoLinea=0), Producto debe estar vacío
            If tipoLinea.HasValue AndAlso tipoLinea.Value = 0 Then
                If Not String.IsNullOrEmpty(value) Then
                    Return ' No permitir establecer un producto en líneas de texto
                End If
            End If
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
            Dim valorAnterior = Model.tipoLinea
            Model.tipoLinea = value
            RaisePropertyChanged(NameOf(tipoLinea))

            ' Issue #258: Aplicar reglas según TipoLinea
            If value.HasValue AndAlso value.Value <> valorAnterior Then
                AplicarReglasTipoLinea(value.Value)
            End If
        End Set
    End Property

    ' Issue #258: Aplica las reglas correspondientes al cambiar el tipo de línea
    ' Esto evita estados incoherentes como tener un código de producto en una línea de cuenta contable
    Private Sub AplicarReglasTipoLinea(tipo As Byte)
        Select Case tipo
            Case 0 ' Línea de texto: solo el campo texto es válido
                Producto = String.Empty
                texto = String.Empty ' El usuario escribirá el texto que quiera
                Cantidad = 0
                PrecioUnitario = 0
                DescuentoLinea = 0
                DescuentoProducto = 0
                AplicarDescuento = False
            Case 1 ' Producto: limpiar para que el usuario busque el producto
                ' Si venía de cuenta contable (8 dígitos numéricos), limpiar
                If Not String.IsNullOrEmpty(Producto) AndAlso Producto.Length = 8 AndAlso Producto.All(Function(c) Char.IsDigit(c)) Then
                    Producto = String.Empty
                    texto = String.Empty
                    PrecioUnitario = 0
                End If
                ' Los productos sí pueden tener descuentos
                AplicarDescuento = True
            Case 2 ' Cuenta contable: limpiar datos de producto
                ' Si venía de producto, limpiar (los códigos de producto no son cuentas válidas)
                If Not String.IsNullOrEmpty(Producto) AndAlso (Producto.Length <> 8 OrElse Not Producto.All(Function(c) Char.IsDigit(c))) Then
                    Producto = String.Empty
                    texto = String.Empty
                    PrecioUnitario = 0
                End If
                ' Las cuentas contables no tienen descuentos
                DescuentoLinea = 0
                DescuentoProducto = 0
                AplicarDescuento = False
            Case 3 ' Inmovilizado: similar a cuenta contable
                If Not String.IsNullOrEmpty(Producto) AndAlso (Producto.Length <> 8 OrElse Not Producto.All(Function(c) Char.IsDigit(c))) Then
                    Producto = String.Empty
                    texto = String.Empty
                    PrecioUnitario = 0
                End If
                DescuentoLinea = 0
                DescuentoProducto = 0
                AplicarDescuento = False
        End Select
    End Sub
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

    ' Carlos 09/12/25: Issue #253/#52 - Indica si el producto es ficticio
    Public ReadOnly Property EsFicticio As Boolean
        Get
            Return Model.EsFicticio
        End Get
    End Property
End Class
