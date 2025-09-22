Imports Nesto.Infrastructure.Contracts
Imports Nesto.Models
Imports Prism.Mvvm

Public Class PedidoVentaModel
    Public Class ResumenPedido
        Inherits BindableBase
        Implements IFiltrableItem
        Public Property empresa As String
        Public Property numero As Integer
        Public Property cliente As String
        Public Property contacto As String
        Public Property nombre As String
        Public Property direccion As String
        Public Property codPostal As String
        Public ReadOnly Property noTieneProductos As Boolean
            Get
                Return Not tieneProductos
            End Get
        End Property
        Public Property poblacion As String
        Public Property provincia As String
        Public Property fecha As Date
        Private _tieneProductos As Boolean
        Public Property tieneProductos As Boolean
            Get
                Return _tieneProductos
            End Get
            Set(value As Boolean)
                Dim unused = SetProperty(_tieneProductos, value)
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
        Public Property vendedor As String
        Private _ultimoSeguimiento As String
        Public Property ultimoSeguimiento As String
            Get
                Return _ultimoSeguimiento
            End Get
            Set(value As String)
                Dim unused = SetProperty(_ultimoSeguimiento, value)
            End Set
        End Property

        Public Property esNuevo As Boolean = False

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
