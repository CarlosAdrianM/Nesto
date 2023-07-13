Imports System.Collections.ObjectModel
Imports System.ComponentModel
Imports System.Runtime.CompilerServices
Imports System.Runtime.Serialization


Public Class PedidoVentaDTO
    Inherits PedidoBase(Of LineaPedidoVentaDTO)
    Public Sub New()
        Me.Lineas = New List(Of LineaPedidoVentaDTO)()
        Me.Prepagos = New ObservableCollection(Of PrepagoDTO)
        Me.Efectos = New ListaEfectos()
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

    Public Property vendedor As String
    Public Property comentarios() As String
    Public Property comentarioPicking() As String
    Public Property CrearEfectosManualmente As Boolean
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

    Public ReadOnly Property Bruto As Decimal
        Get
            Return Lineas.Sum(Function(l) l.Bruto)
        End Get
    End Property
    Public ReadOnly Property baseImponiblePicking As Decimal
        Get
            Dim baseImponibleParcial As Decimal
            For Each linea In Lineas.Where(Function(l) l.picking > 0)
                baseImponibleParcial += linea.BaseImponible
            Next
            Return baseImponibleParcial
        End Get
    End Property

    Public ReadOnly Property brutoPicking As Decimal
        Get
            Return Lineas.Where(Function(l) l.picking > 0).Sum(Function(l) l.Bruto)
        End Get
    End Property

    Public ReadOnly Property sumaDescuentos(linea As LineaPedidoVentaDTO) As Decimal
        Get
            ' Si no está marcado el aplicar descuento, solo aplica el de la propia línea
            If Not linea.aplicarDescuento Then
                Return linea.DescuentoLinea
            End If

            ' falta añadir el descuento del cliente y el descuento PP
            Return 1 - ((1 - linea.DescuentoLinea) * (1 - linea.DescuentoProducto))
        End Get
    End Property

    Public ReadOnly Property totalPicking As Decimal
        Get
            Dim totalParcial As Decimal
            For Each linea In Lineas.Where(Function(l) l.picking > 0)
                totalParcial += linea.Total
            Next
            Return totalParcial
        End Get
    End Property

    'Private Function calcularBaseImponibleLinea(linea As LineaPedidoVentaDTO)
    '    ' Una vez veamos que funciona, eliminar esta función y usar el campo directamente
    '    'Return linea.bruto * (1 - sumaDescuentos(linea)) * (1 - DescuentoPP)
    '    Return linea.BaseImponible
    'End Function

    'Private Function calcularTotalLinea(linea As LineaPedidoVentaDTO)
    '    ' Una vez veamos que funciona, eliminar esta función y usar el campo directamente
    '    'If IsNothing(Me.iva) OrElse (iva = "EX") Then
    '    '    Return calcularBaseImponibleLinea(linea)
    '    'ElseIf Me.iva = "R10" Then
    '    '    Return calcularBaseImponibleLinea(linea) * 1.1
    '    'ElseIf Me.iva = "SR4" Then
    '    '    Return calcularBaseImponibleLinea(linea) * 1.04
    '    'Else
    '    '    Return calcularBaseImponibleLinea(linea) * 1.21
    '    'End If
    '    Return linea.Total
    'End Function

    Public Overridable Property Efectos As ListaEfectos
    Public Overridable Property Prepagos() As ObservableCollection(Of PrepagoDTO)
    Public Overridable Property VendedoresGrupoProducto As ObservableCollection(Of VendedorGrupoProductoDTO)

    <OnDeserialized>
    Private Sub OnDeserializedMethod(context As StreamingContext)
        ' Establecer la referencia Pedido en cada línea después de la deserialización
        For Each linea As LineaPedidoVentaDTO In Lineas
            linea.Pedido = Me
        Next
    End Sub

End Class

