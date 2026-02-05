Imports System.Windows
Imports System.Windows.Media
Imports System.Windows.Media.Imaging
Imports Nesto.Infrastructure.Contracts
Imports Newtonsoft.Json


Public Class DireccionesEntregaJson
    Public Property contacto() As String
    Public Property clientePrincipal() As Boolean
    Public Property nombre() As String
    Public Property direccion() As String
    Public Property poblacion() As String
    Public Property comentarios() As String
    Public Property codigoPostal() As String
    Public Property provincia() As String
    Public Property estado() As Integer
    Public Property iva() As String
    Public Property comentarioRuta() As String
    Public Property comentarioPicking() As Object
    Public Property noComisiona() As Double
    Public Property servirJunto() As Boolean
    Public Property mantenerJunto() As Boolean
    Public Property esDireccionPorDefecto() As Boolean
    Public Property vendedor() As String
    Public Property periodoFacturacion() As String
    Public Property ccc() As String
    Public Property ruta() As String
    Public Property formaPago() As String
    Public Property plazosPago() As String
    Public Property tieneCorreoElectronico As Boolean
    Public Property tieneFacturacionElectronica As Boolean

    Public ReadOnly Property textoPoblacion As String
        Get
            Return String.Format("{0} {1} ({2})", codigoPostal, poblacion, provincia)
        End Get
    End Property
End Class
Public Class FormaPagoDTO
    Public Property formaPago As String
    Public Property descripcion As String
    Public bloquearPagos As Boolean
    Public cccObligatorio As Boolean
End Class
Public Class FormaVentaDTO
    <JsonProperty("Número")>
    Public Property numero As String
    <JsonProperty("Descripción")>
    Public Property descripcion As String
End Class
Public Class PrecioProductoDTO
    Public Property precio As Decimal
    Public Property descuento As Decimal
    Public Property aplicarDescuento As Boolean
    Public Property motivo As String
End Class
Public Class StockProductoDTO
    Public Property stock() As Integer
    Public Property cantidadDisponible() As Integer
    Public Property cantidadPendienteRecibir As Integer
    Public Property urlImagen() As String
    Public Property StockDisponibleTodosLosAlmacenes As Integer
End Class
Public Class UltimasVentasProductoClienteDTO
    Public Property fecha As DateTime
    Public Property cantidad As Short
    Public Property precioBruto As Decimal
    Public Property descuentos As Decimal
    Public Property precioNeto As Decimal
End Class

Public Class PonerStockParam
    Public Property Lineas As List(Of LineaPlantillaVenta)
    Public Property Almacen As String
    Public Property Almacenes As List(Of String)
    Public Property Ordenar As Boolean
End Class

Public Class StockAlmacenDTO
    Public Property almacen As String
    Public Property stock As Integer
    Public Property cantidadDisponible As Integer
End Class

''' <summary>
''' Respuesta del endpoint ProductosBonificables.
''' Issue #94: Sistema Ganavisiones - FASE 7
''' </summary>
Public Class ProductosBonificablesResponse
    Public Property GanavisionesDisponibles As Integer
    Public Property BaseImponibleBonificable As Decimal
    Public Property Productos As List(Of ProductoBonificableDTO)
End Class

''' <summary>
''' Producto que se puede bonificar con Ganavisiones.
''' Issue #94: Sistema Ganavisiones - FASE 7
''' </summary>
Public Class ProductoBonificableDTO
    Public Property ProductoId As String
    Public Property ProductoNombre As String
    Public Property Ganavisiones As Integer
    Public Property PVP As Decimal
    Public Property Stocks As List(Of StockAlmacenDTO)
    Public ReadOnly Property StockTotal As Integer
        Get
            If Stocks Is Nothing Then Return 0
            Return Stocks.Sum(Function(s) s.stock)
        End Get
    End Property
End Class

''' <summary>
''' Request para validar si se puede desmarcar ServirJunto.
''' Issue #94: Sistema Ganavisiones - FASE 9
''' </summary>
Public Class ValidarServirJuntoRequest
    Public Property Almacen As String
    Public Property ProductosBonificados As List(Of String)
End Class

''' <summary>
''' Respuesta de la validacion de ServirJunto.
''' Issue #94: Sistema Ganavisiones - FASE 9
''' </summary>
Public Class ValidarServirJuntoResponse
    Public Property PuedeDesmarcar As Boolean
    Public Property ProductosProblematicos As List(Of ProductoSinStockDTO)
    Public Property Mensaje As String
End Class

''' <summary>
''' Producto bonificado que no tiene stock en el almacen del pedido.
''' Issue #94: Sistema Ganavisiones - FASE 9
''' </summary>
Public Class ProductoSinStockDTO
    Public Property ProductoId As String
    Public Property ProductoNombre As String
    Public Property AlmacenConStock As String
End Class

''' <summary>
''' Linea de regalo para seleccion en el wizard.
''' Issue #94: Sistema Ganavisiones - FASE 7
''' Compatible con SelectorLineasPlantillaVenta.
''' </summary>
Public Class LineaRegalo
    Inherits Prism.Mvvm.BindableBase
    Implements IFiltrableItem, ILineaConCantidad

    Public Property producto As String
    Public Property texto As String
    Public Property precio As Decimal
    Public Property ganavisiones As Integer
    Public Property stocks As List(Of StockAlmacenDTO)

    Private _urlImagen As String
    ''' <summary>
    ''' URL de la imagen del producto (cargada cuando se selecciona cantidad).
    ''' </summary>
    Public Property urlImagen As String
        Get
            Return _urlImagen
        End Get
        Set(value As String)
            SetProperty(_urlImagen, value)
            RaisePropertyChanged(NameOf(imagen))
            RaisePropertyChanged(NameOf(imagenVisible))
            RaisePropertyChanged(NameOf(clasificacionVisible))
        End Set
    End Property

    Private _cantidad As Integer
    Public Property cantidad As Integer Implements ILineaConCantidad.cantidad
        Get
            Return _cantidad
        End Get
        Set(value As Integer)
            SetProperty(_cantidad, value)
            RaisePropertyChanged(NameOf(colorStock))
            RaisePropertyChanged(NameOf(textoUnidadesDisponibles))
        End Set
    End Property

    Public ReadOnly Property StockTotal As Integer
        Get
            If stocks Is Nothing Then Return 0
            Return stocks.Sum(Function(s) s.stock)
        End Get
    End Property

    ''' <summary>
    ''' Texto con nombre y precio del producto (para mostrar el valor al cliente).
    ''' </summary>
    Public ReadOnly Property textoConPrecio As String
        Get
            Return $"{texto} (Valor: {precio:C2})"
        End Get
    End Property

#Region "Propiedades para compatibilidad con SelectorLineasPlantillaVenta"

    ''' <summary>
    ''' Nombre del producto para mostrar en el selector.
    ''' </summary>
    Public ReadOnly Property textoNombreProducto As String
        Get
            Return $"{texto} (Valor: {precio:C2})"
        End Get
    End Property

    ''' <summary>
    ''' Visibilidad de la imagen - visible si hay urlImagen.
    ''' </summary>
    Public ReadOnly Property imagenVisible As Visibility
        Get
            If String.IsNullOrEmpty(urlImagen) Then
                Return Visibility.Collapsed
            Else
                Return Visibility.Visible
            End If
        End Get
    End Property

    ''' <summary>
    ''' Muestra los ganavisiones en lugar de la imagen cuando no hay imagen.
    ''' </summary>
    Public ReadOnly Property clasificacionVisible As Visibility
        Get
            If String.IsNullOrEmpty(urlImagen) Then
                Return Visibility.Visible
            Else
                Return Visibility.Collapsed
            End If
        End Get
    End Property

    ''' <summary>
    ''' Color del indicador - verde para productos disponibles.
    ''' </summary>
    Public ReadOnly Property colorEstado As Brush
        Get
            If StockTotal > 0 Then
                Return Brushes.Green
            Else
                Return Brushes.Gray
            End If
        End Get
    End Property

    ''' <summary>
    ''' Muestra los ganavisiones como clasificacion.
    ''' </summary>
    Public ReadOnly Property clasificacionMasVendidos As Integer
        Get
            Return ganavisiones
        End Get
    End Property

    ''' <summary>
    ''' No se usa cantidadOferta para regalos.
    ''' </summary>
    Public Property cantidadOferta As Integer = 0 Implements ILineaConCantidad.cantidadOferta

    ''' <summary>
    ''' No aplica descuento en regalos.
    ''' </summary>
    Public Property aplicarDescuentoFicha As Boolean? = False Implements ILineaConCantidad.aplicarDescuentoFicha

    ''' <summary>
    ''' Texto de unidades disponibles basado en stock total.
    ''' </summary>
    Public ReadOnly Property textoUnidadesDisponibles As String
        Get
            If StockTotal = 0 Then
                Return "Sin stock"
            ElseIf StockTotal < cantidad Then
                Return $"Solo {StockTotal} und. disponibles"
            ElseIf StockTotal = 1 Then
                Return "1 und. disponible"
            Else
                Return $"{StockTotal} und. disponibles"
            End If
        End Get
    End Property

    ''' <summary>
    ''' Color del stock basado en disponibilidad.
    ''' </summary>
    Public ReadOnly Property colorStock As Brush
        Get
            If StockTotal = 0 Then
                Return Brushes.Red
            ElseIf StockTotal < cantidad Then
                Return Brushes.Orange
            Else
                Return Brushes.Green
            End If
        End Get
    End Property

    ''' <summary>
    ''' Detalle de stocks por almacen.
    ''' </summary>
    Public ReadOnly Property textoStocksPorAlmacen As String
        Get
            If stocks Is Nothing OrElse stocks.Count = 0 Then
                Return String.Empty
            End If
            Return String.Join(" | ", stocks.Select(Function(s) $"{s.almacen}:{s.stock}"))
        End Get
    End Property

    ''' <summary>
    ''' No aplica para regalos.
    ''' </summary>
    Public ReadOnly Property textoFechaUltimaVenta As String
        Get
            Return String.Empty
        End Get
    End Property

    ''' <summary>
    ''' No aplica para regalos.
    ''' </summary>
    Public ReadOnly Property textoUnidadesVendidas As String
        Get
            Return String.Empty
        End Get
    End Property

    ''' <summary>
    ''' Muestra "Regalo" como familia.
    ''' </summary>
    Public ReadOnly Property familia As String
        Get
            Return "Regalo"
        End Get
    End Property

    ''' <summary>
    ''' Muestra los ganavisiones como subgrupo.
    ''' </summary>
    Public ReadOnly Property subGrupo As String
        Get
            Return $"{ganavisiones} Ganavisiones"
        End Get
    End Property

    ''' <summary>
    ''' Imagen del producto (cargada desde urlImagen).
    ''' </summary>
    Public ReadOnly Property imagen As BitmapImage
        Get
            If Not String.IsNullOrEmpty(urlImagen) Then
                Return New BitmapImage(New Uri(urlImagen, UriKind.Absolute))
            Else
                Return Nothing
            End If
        End Get
    End Property

#End Region

    ''' <summary>
    ''' Implementación de IFiltrableItem para permitir filtrar los regalos.
    ''' </summary>
    Public Function Contains(filtro As String) As Boolean Implements IFiltrableItem.Contains
        Return (Not String.IsNullOrEmpty(producto) AndAlso producto.ToLower.Contains(filtro)) OrElse
               (Not String.IsNullOrEmpty(texto) AndAlso texto.ToLower.Contains(filtro))
    End Function

End Class
