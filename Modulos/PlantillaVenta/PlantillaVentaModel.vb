Imports Microsoft.Practices.Prism.Mvvm
Imports Newtonsoft.Json

Public Class PlantillaVentaModel
    Public Class ClienteJson
        Public Property empresa() As String
        Public Property cliente() As String
        Public Property contacto() As String
        Public Property clientePrincipal() As Boolean
        Public Property nombre() As String
        Public Property direccion() As String
        Public Property poblacion() As String
        Public Property telefono() As String
        Public Property vendedor() As String
        Public Property comentarios() As String
        Public Property codigoPostal() As String
        Public Property cifNif() As String
        Public Property provincia() As String
        Public Property estado() As Integer
        Public Property iva() As String
        Public Property grupo() As String
        Public Property periodoFacturacion() As String
        Public Property ccc() As String
        Public Property ruta() As String
        Public Property copiasAlbaran() As Integer
        Public Property copiasFactura() As Integer
        Public Property comentarioRuta() As Object
        Public Property comentarioPicking() As Object
        Public Property web() As String
        Public Property albaranValorado() As Boolean
        Public Property cadena() As Object
        Public Property noComisiona() As Double
        Public Property servirJunto() As Boolean
        Public Property mantenerJunto() As Boolean



        Public ReadOnly Property rutaLogo As String
            Get
                If empresa = "1" OrElse empresa = "2" OrElse empresa = "4" OrElse empresa = "5" Then
                    Return "pack://application:,,,/PlantillaVenta;component/Images/logo" + empresa + ".jpg"
                Else
                    Return ""
                End If
            End Get
        End Property

        Public ReadOnly Property colorEstado As Brush
            Get
                If estado = -1 Then
                    Return Brushes.Red
                ElseIf estado = 7 Then
                    Return Brushes.Orange
                ElseIf estado = 0 OrElse estado = 9
                    Return Brushes.Green
                Else
                    Return Brushes.Black
                End If
            End Get
        End Property
    End Class
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

        Public ReadOnly Property textoPoblacion As String
            Get
                Return String.Format("{0} {1} ({2})", codigoPostal, poblacion, provincia)
            End Get
        End Property
    End Class
    Public Class FormaVentaDTO
        <JsonProperty("Número")>
        Public Property numero As String
        <JsonProperty("Descripción")>
        Public Property descripcion As String
    End Class
    Public Class LineaPlantillaJson
        Inherits BindableBase

        Public Property producto() As String
        Public Property texto() As String
        Private _cantidad As Integer
        Public Property cantidad As Integer
            Get
                Return _cantidad
            End Get
            Set(value As Integer)
                SetProperty(_cantidad, value)
            End Set
        End Property
        Private _cantidadOferta As Integer
        Public Property cantidadOferta As Integer
            Get
                Return _cantidadOferta
            End Get
            Set(value As Integer)
                ' No permitimos sumar oferta y descuento
                If value > 0 AndAlso cantidadOferta = 0 Then
                    aplicarDescuento = False
                ElseIf cantidadOferta > 0 AndAlso value = 0 Then
                    aplicarDescuento = aplicarDescuentoFicha
                End If
                SetProperty(_cantidadOferta, value)
            End Set
        End Property
        Public Property tamanno() As System.Nullable(Of Integer)
        Public Property unidadMedida() As String
        Public Property familia() As String
        Public Property subGrupo() As String
        Public Property estado() As Integer
        Public Property yaFacturado() As Boolean
        Public Property cantidadVendida() As Integer
        Public Property cantidadAbonada() As Integer
        Public Property fechaUltimaVenta() As System.Nullable(Of DateTime)
        Public Property iva() As String
        Public Property precio() As Decimal
        Private _aplicarDescuento As Boolean
        Public Property aplicarDescuento As Boolean
            Get
                Return _aplicarDescuento
            End Get
            Set(value As Boolean)
                If IsNothing(aplicarDescuentoFicha) Then
                    'Esta línea hay que cambiarla cuando GestorPrecios funcione bien
                    aplicarDescuentoFicha = IIf(subGrupo.ToLower = "otros aparatos", False, value)
                End If
                SetProperty(_aplicarDescuento, value)
            End Set
        End Property
        Public Property aplicarDescuentoFicha() As Boolean?
        Public Property stock As Integer
        Public Property cantidadDisponible As Integer
        Public Property stockActualizado As Boolean
        Public Property fechaInsercion As DateTime = DateTime.MaxValue
        Public Property descuento As Decimal
        Public Property descuentoProducto As Decimal
        Private _urlImagen As String
        Public Property urlImagen As String
            Get
                Return _urlImagen
            End Get
            Set(value As String)
                SetProperty(_urlImagen, value)
                OnPropertyChanged("imagen")
                OnPropertyChanged("imagenVisible")
            End Set
        End Property
        Public ReadOnly Property colorEstado As Brush
            Get
                If cantidadAbonada >= cantidadVendida Then
                    Return Brushes.Red
                ElseIf fechaUltimaVenta < DateAdd(DateInterval.Year, -1, Now)
                    Return Brushes.Orange
                ElseIf fechaUltimaVenta < DateAdd(DateInterval.Month, -6, Now) AndAlso (cantidadAbonada = 0 OrElse cantidadVendida / cantidadAbonada > 10)
                    Return Brushes.Yellow
                ElseIf fechaUltimaVenta < DateAdd(DateInterval.Month, -2, Now) AndAlso (cantidadAbonada = 0 OrElse cantidadVendida / cantidadAbonada > 10)
                    Return Brushes.YellowGreen
                ElseIf Not IsNothing(fechaUltimaVenta) Then
                    Return Brushes.Green
                Else
                    Return Brushes.Blue
                End If
            End Get
        End Property
        Public ReadOnly Property textoUnidadesVendidas As String
            Get
                If cantidadAbonada = 0 And cantidadVendida > 1 Then
                    Return String.Format("Vendidas {0} unds.", cantidadVendida)
                ElseIf cantidadAbonada = 0 And cantidadVendida = 1 Then
                    Return "Vendida 1 und."
                ElseIf fechaUltimaVenta = DateTime.MinValue Then
                    Return ""
                ElseIf (cantidadVendida = 0 AndAlso cantidadAbonada = 0)
                    Return "Ninguna unidad vendida"
                Else
                    Return String.Format("Vendidas {0} unds. (facturadas {1} y abonadas {2}).", cantidadVendida - cantidadAbonada, cantidadVendida, cantidadAbonada)
                End If
            End Get
        End Property
        Public ReadOnly Property textoFechaUltimaVenta As String
            Get
                If IsNothing(fechaUltimaVenta) OrElse fechaUltimaVenta = DateTime.MinValue Then
                    Return ""
                Else
                    Return fechaUltimaVenta.ToString
                End If
            End Get
        End Property
        Public ReadOnly Property textoNombreProducto As String
            Get
                Dim textoCompleto As String = texto
                If tamanno <> 0 Then
                    textoCompleto = textoCompleto.Trim + " " + CType(tamanno, String)
                End If
                If unidadMedida <> "" Then
                    textoCompleto = textoCompleto.Trim + " " + unidadMedida
                End If
                Return textoCompleto
            End Get
        End Property
        Public ReadOnly Property colorStock As Brush
            Get
                If Not stockActualizado Then
                    Return Brushes.Gray
                ElseIf cantidadDisponible >= cantidad + cantidadOferta Then
                    Return Brushes.Green
                ElseIf stock >= cantidad + cantidadOferta Then
                    Return Brushes.DarkOrange
                Else
                    Return Brushes.Red
                End If
            End Get
        End Property
        Public ReadOnly Property imagenVisible As Visibility
            Get
                'Return Visibility.Visible
                If urlImagen = "" Then
                    Return Visibility.Collapsed
                Else
                    Return Visibility.Visible
                End If
            End Get
        End Property
        Public ReadOnly Property imagen As BitmapImage
            Get
                If Not IsNothing(urlImagen) Then
                    Return New BitmapImage(New Uri(urlImagen, UriKind.Absolute))
                Else
                    Return Nothing
                End If
            End Get
        End Property


    End Class
    Public Class LineaPedidoVentaDTO
        Public Property almacen() As String
        Public Property aplicarDescuento() As Boolean
        Public Property cantidad() As Short
        Public Property delegacion() As String
        Public Property descuento() As Decimal
        Public Property descuentoProducto() As Decimal
        Public Property estado() As Short
        Public Property fechaEntrega() As System.DateTime
        Public Property formaVenta() As String
        Public Property iva() As String
        Public Property oferta() As Integer?
        Public Property precio() As Decimal
        Public Property producto() As String
        Public Property texto() As String
        Public Property tipoLinea() As Nullable(Of Byte)
        Public Property usuario() As String
        Public Property vistoBueno() As Boolean
        Public Function ShallowCopy() As LineaPedidoVentaDTO
            Return DirectCast(Me.MemberwiseClone(), LineaPedidoVentaDTO)
        End Function
    End Class
    Public Class PedidoVentaDTO
        Public Sub New()
            Me.LineasPedido = New HashSet(Of LineaPedidoVentaDTO)()
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
        Public Property vendedor() As String
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
        Public Property usuario() As String

        Public Overridable Property LineasPedido() As ICollection(Of LineaPedidoVentaDTO)

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
        Public Property urlImagen() As String
    End Class
    Public Class UltimasVentasProductoClienteDTO
        Public Property fecha As DateTime
        Public Property cantidad As Short
        Public Property precioBruto As Decimal
        Public Property descuentos As Decimal
        Public Property precioNeto As Decimal
    End Class
End Class
