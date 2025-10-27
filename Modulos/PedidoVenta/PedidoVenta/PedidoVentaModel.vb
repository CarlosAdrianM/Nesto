Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Prism.Mvvm

Public Class PedidoVentaModel
    Public Class ResumenPedido
        Inherits BindableBase
        Implements IFiltrableItem
        Private _empresa As String
        Public Property empresa As String
            Get
                Return _empresa
            End Get
            Set(value As String)
                Dim unused = SetProperty(_empresa, value)
            End Set
        End Property

        Private _numero As Integer
        Public Property numero As Integer
            Get
                Return _numero
            End Get
            Set(value As Integer)
                Dim unused = SetProperty(_numero, value)
            End Set
        End Property

        Private _cliente As String
        Public Property cliente As String
            Get
                Return _cliente
            End Get
            Set(value As String)
                Dim unused = SetProperty(_cliente, value)
            End Set
        End Property

        Private _contacto As String
        Public Property contacto As String
            Get
                Return _contacto
            End Get
            Set(value As String)
                Dim unused = SetProperty(_contacto, value)
            End Set
        End Property

        Private _nombre As String
        Public Property nombre As String
            Get
                Return _nombre
            End Get
            Set(value As String)
                Dim unused = SetProperty(_nombre, value)
            End Set
        End Property

        Private _direccion As String
        Public Property direccion As String
            Get
                Return _direccion
            End Get
            Set(value As String)
                Dim unused = SetProperty(_direccion, value)
            End Set
        End Property

        Private _codPostal As String
        Public Property codPostal As String
            Get
                Return _codPostal
            End Get
            Set(value As String)
                Dim unused = SetProperty(_codPostal, value)
            End Set
        End Property

        Public ReadOnly Property noTieneProductos As Boolean
            Get
                Return Not tieneProductos
            End Get
        End Property

        Private _poblacion As String
        Public Property poblacion As String
            Get
                Return _poblacion
            End Get
            Set(value As String)
                Dim unused = SetProperty(_poblacion, value)
            End Set
        End Property

        Private _provincia As String
        Public Property provincia As String
            Get
                Return _provincia
            End Get
            Set(value As String)
                Dim unused = SetProperty(_provincia, value)
            End Set
        End Property

        Private _fecha As Date
        Public Property fecha As Date
            Get
                Return _fecha
            End Get
            Set(value As Date)
                Dim unused = SetProperty(_fecha, value)
            End Set
        End Property
        Private _tieneProductos As Boolean
        Public Property tieneProductos As Boolean
            Get
                Return _tieneProductos
            End Get
            Set(value As Boolean)
                If SetProperty(_tieneProductos, value) Then
                    RaisePropertyChanged(NameOf(noTieneProductos))
                End If
            End Set
        End Property
        Private _tienePendientes As Boolean
        Public Property tienePendientes As Boolean
            Get
                Return _tienePendientes
            End Get
            Set(value As Boolean)
                Dim unused = SetProperty(_tienePendientes, value)
            End Set
        End Property
        Private _tienePicking As Boolean
        Public Property tienePicking As Boolean
            Get
                Return _tienePicking
            End Get
            Set(value As Boolean)
                Dim unused = SetProperty(_tienePicking, value)
            End Set
        End Property
        Private _tieneFechasFuturas As Boolean
        Public Property tieneFechasFuturas As Boolean
            Get
                Return _tieneFechasFuturas
            End Get
            Set(value As Boolean)
                Dim unused = SetProperty(_tieneFechasFuturas, value)
            End Set
        End Property
        Private _tienePresupuesto As Boolean
        Public Property tienePresupuesto As Boolean
            Get
                Return _tienePresupuesto
            End Get
            Set(value As Boolean)
                Dim unused = SetProperty(_tienePresupuesto, value)
            End Set
        End Property
        Public ReadOnly Property tieneSeguimiento As Boolean
            Get
                Return Not String.IsNullOrEmpty(ultimoSeguimiento)
            End Get
        End Property

        Private _baseImponible As Decimal
        Public Property baseImponible As Decimal
            Get
                Return _baseImponible
            End Get
            Set(value As Decimal)
                Dim unused = SetProperty(_baseImponible, value)
            End Set
        End Property
        Private _total As Decimal
        Public Property total As Decimal
            Get
                Return _total
            End Get
            Set(value As Decimal)
                Dim unused = SetProperty(_total, value)
            End Set
        End Property

        Private _vendedor As String
        Public Property vendedor As String
            Get
                Return _vendedor
            End Get
            Set(value As String)
                Dim unused = SetProperty(_vendedor, value)
            End Set
        End Property

        Private _ultimoSeguimiento As String
        Public Property ultimoSeguimiento As String
            Get
                Return _ultimoSeguimiento
            End Get
            Set(value As String)
                Dim unused = SetProperty(_ultimoSeguimiento, value)
            End Set
        End Property

        Private _esNuevo As Boolean = False
        Public Property esNuevo As Boolean
            Get
                Return _esNuevo
            End Get
            Set(value As Boolean)
                Dim unused = SetProperty(_esNuevo, value)
            End Set
        End Property

        Public Function Contains(filtro As String) As Boolean Implements IFiltrableItem.Contains
            Return (Not IsNothing(direccion) AndAlso direccion.ToLower.Contains(filtro.ToLower)) OrElse
                    (Not IsNothing(nombre) AndAlso nombre.ToLower.Contains(filtro.ToLower)) OrElse
                    (Not IsNothing(cliente) AndAlso cliente.Trim.ToLower.Equals(filtro.ToLower)) OrElse
                    (Not IsNothing(vendedor) AndAlso vendedor.Trim.ToLower.Equals(filtro.ToLower)) OrElse
                    (numero = convertirCadenaInteger(filtro))
        End Function

        Private Function convertirCadenaInteger(texto As String) As Integer
            Dim valor As Integer
            Return IIf(Integer.TryParse(texto, valor), valor, Nothing)
        End Function
    End Class

    Public Class Producto
        Public Property producto() As String
        Public Property nombre() As String
        Public Property precio() As Decimal
        Public Property aplicarDescuento() As Boolean
        Public Property Stock() As Integer
        Public Property CantidadReservada() As Integer
        Public Property CantidadDisponible() As Integer
        Public Property descuento() As Decimal
        Public Property iva() As String
    End Class

    Public Class EnvioAgenciaDTO
        Public Property Fecha As Date
        Public Property AgenciaNombre As String
        Public Property EnlaceSeguimiento As String
        Public Property Estado As Short
    End Class

    Public Class ParametroStringIntInt
        Public Property Empresa As String
        Public Property NumeroPedidoOriginal As Integer
        Public Property NumeroPedidoAmpliacion As Integer
    End Class

    Public Class ParametroStringIntPedido
        Public Property Empresa As String
        Public Property NumeroPedidoOriginal As Integer
        Public Property PedidoAmpliacion As PedidoVentaDTO
    End Class
End Class
