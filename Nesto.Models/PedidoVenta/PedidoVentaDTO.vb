Imports System.Collections.ObjectModel
Imports System.Runtime.Serialization


''' <summary>
''' DTO para pedidos de venta. Implementa IEquatable para detectar cambios sin guardar.
''' </summary>
Public Class PedidoVentaDTO
    Inherits PedidoBase(Of LineaPedidoVentaDTO)
    Implements IEquatable(Of PedidoVentaDTO)

    Public Sub New()
        Lineas = New List(Of LineaPedidoVentaDTO)()
        Prepagos = New ObservableCollection(Of PrepagoDTO)
        Efectos = New ListaEfectos()
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
    Public Property CreadoSinPasarValidacion As Boolean

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

#Region "IEquatable - Comparación para detectar cambios sin guardar (Issue #254)"

    ''' <summary>
    ''' Compara los campos editables del pedido que afectan a la facturación.
    ''' No compara las líneas (solo cabecera) ni campos de solo lectura.
    ''' </summary>
    Public Overloads Function Equals(other As PedidoVentaDTO) As Boolean Implements IEquatable(Of PedidoVentaDTO).Equals
        If other Is Nothing Then Return False
        If ReferenceEquals(Me, other) Then Return True

        ' Comparamos solo los campos que el usuario puede modificar y afectan a facturación
        Return String.Equals(formaPago, other.formaPago, StringComparison.Ordinal) AndAlso
               String.Equals(plazosPago, other.plazosPago, StringComparison.Ordinal) AndAlso
               String.Equals(ccc, other.ccc, StringComparison.Ordinal) AndAlso
               String.Equals(iva, other.iva, StringComparison.Ordinal) AndAlso
               String.Equals(vendedor, other.vendedor, StringComparison.Ordinal) AndAlso
               String.Equals(periodoFacturacion, other.periodoFacturacion, StringComparison.Ordinal) AndAlso
               String.Equals(ruta, other.ruta, StringComparison.Ordinal) AndAlso
               String.Equals(serie, other.serie, StringComparison.Ordinal) AndAlso
               String.Equals(contacto, other.contacto, StringComparison.Ordinal) AndAlso
               String.Equals(contactoCobro, other.contactoCobro, StringComparison.Ordinal) AndAlso
               String.Equals(comentarios, other.comentarios, StringComparison.Ordinal) AndAlso
               String.Equals(comentarioPicking, other.comentarioPicking, StringComparison.Ordinal) AndAlso
               Nullable.Equals(primerVencimiento, other.primerVencimiento) AndAlso
               noComisiona = other.noComisiona AndAlso
               mantenerJunto = other.mantenerJunto AndAlso
               servirJunto = other.servirJunto AndAlso
               notaEntrega = other.notaEntrega
    End Function

    Public Overrides Function Equals(obj As Object) As Boolean
        Return Equals(TryCast(obj, PedidoVentaDTO))
    End Function

    Public Overrides Function GetHashCode() As Integer
        ' Usamos XOR para evitar overflow en las operaciones
        Dim hash As Integer = 17
        hash = hash Xor If(formaPago IsNot Nothing, formaPago.GetHashCode(), 0)
        hash = hash Xor If(ccc IsNot Nothing, ccc.GetHashCode(), 0)
        hash = hash Xor If(plazosPago IsNot Nothing, plazosPago.GetHashCode(), 0)
        hash = hash Xor If(iva IsNot Nothing, iva.GetHashCode(), 0)
        Return hash
    End Function

    ''' <summary>
    ''' Crea una copia superficial del pedido para guardar como snapshot.
    ''' Solo copia los campos comparados en Equals.
    ''' </summary>
    Public Function CrearSnapshot() As PedidoVentaDTO
        Return New PedidoVentaDTO() With {
            .empresa = Me.empresa,
            .numero = Me.numero,
            .cliente = Me.cliente,
            .contacto = Me.contacto,
            .formaPago = Me.formaPago,
            .plazosPago = Me.plazosPago,
            .primerVencimiento = Me.primerVencimiento,
            .ccc = Me.ccc,
            .iva = Me.iva,
            .vendedor = Me.vendedor,
            .periodoFacturacion = Me.periodoFacturacion,
            .ruta = Me.ruta,
            .serie = Me.serie,
            .contactoCobro = Me.contactoCobro,
            .comentarios = Me.comentarios,
            .comentarioPicking = Me.comentarioPicking,
            .noComisiona = Me.noComisiona,
            .mantenerJunto = Me.mantenerJunto,
            .servirJunto = Me.servirJunto,
            .notaEntrega = Me.notaEntrega
        }
    End Function

#End Region

End Class

