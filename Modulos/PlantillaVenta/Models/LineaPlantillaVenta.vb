Imports Prism.Mvvm
Imports Nesto.Infrastructure.Contracts

Public Class LineaPlantillaVenta
    Inherits BindableBase
    Implements IFiltrableItem, ILineaConCantidad

    Public Property producto() As String
    Public Property texto() As String
    Private _cantidad As Integer
    Public Property cantidad As Integer Implements ILineaConCantidad.cantidad
        Get
            Return _cantidad
        End Get
        Set(value As Integer)
            SetProperty(_cantidad, value)
            RaisePropertyChanged(NameOf(colorStock))
            RaisePropertyChanged(NameOf(baseImponible))
        End Set
    End Property
    Private _cantidadOferta As Integer
    Public Property cantidadOferta As Integer Implements ILineaConCantidad.cantidadOferta
        Get
            Return _cantidadOferta
        End Get
        Set(value As Integer)
            ' No permitimos sumar oferta y descuento. OJO (Nesto#401): se asigna el CAMPO privado,
            ' no la propiedad, para que poner una oferta nunca inicialice aplicarDescuentoFicha.
            ' Al recargar un borrador, Json.NET ejecuta este setter ANTES de asignar la ficha
            ' (cantidadOferta va antes en el JSON) y la dejaba False para siempre, deshabilitando
            ' el control de cantidad oferta sin forma de quitar la oferta.
            If value > 0 AndAlso cantidadOferta = 0 Then
                _aplicarDescuento = False
                RaisePropertyChanged(NameOf(aplicarDescuento))
            ElseIf cantidadOferta > 0 AndAlso value = 0 AndAlso aplicarDescuentoFicha.HasValue Then
                _aplicarDescuento = aplicarDescuentoFicha.Value
                RaisePropertyChanged(NameOf(aplicarDescuento))
            End If
            SetProperty(_cantidadOferta, value)
            RaisePropertyChanged(NameOf(colorStock))
            RaisePropertyChanged(NameOf(personalizarOfertaVisible))
            RaisePropertyChanged(NameOf(personalizarInputsVisible))
            RaisePropertyChanged(NameOf(puedeEditarCantidadOferta))
        End Set
    End Property

    ''' <summary>
    ''' Nesto#371: permite que la unidad "de oferta" (cantidadOferta) no vaya gratis, sino a un precio
    ''' y descuento concretos (típico "2ª unidad al 50 %": precio de tarifa + 50 % de descuento).
    ''' Solo tiene sentido cuando cantidadOferta &gt; 0.
    ''' </summary>
    Private _personalizarOferta As Boolean
    Public Property personalizarOferta As Boolean
        Get
            Return _personalizarOferta
        End Get
        Set(value As Boolean)
            SetProperty(_personalizarOferta, value)
            ' Nesto#375 (hardening): al desmarcar, resetear precio/dto de oferta a su valor por
            ' defecto (gratis) para no dejar valores residuales si se vuelve a marcar.
            If Not value Then
                precioOferta = 0
                descuentoOferta = 0
                RaisePropertyChanged(NameOf(precioOferta))
                RaisePropertyChanged(NameOf(descuentoOferta))
            End If
            RaisePropertyChanged(NameOf(personalizarInputsVisible))
        End Set
    End Property
    ''' <summary>Nesto#371: precio de la unidad de oferta cuando se personaliza (0 = gratis).</summary>
    Public Property precioOferta As Decimal
    ''' <summary>Nesto#371: descuento (0..1) de la unidad de oferta cuando se personaliza.</summary>
    Public Property descuentoOferta As Decimal
    ''' <summary>
    ''' Nesto#375: True si se personaliza la oferta pero NO aporta beneficio: la unidad de oferta
    ''' sale igual o más cara que la de pago (precio efectivo oferta &gt;= precio efectivo normal).
    ''' Una "oferta" así es falsa (p. ej. 6+6 con los 12 al mismo precio): deben meterse todas las
    ''' unidades sin oferta. Solo aplica con personalizarOferta y cantidadOferta &gt; 0.
    ''' </summary>
    Public ReadOnly Property ofertaPersonalizadaSinBeneficio As Boolean
        Get
            If Not personalizarOferta OrElse cantidadOferta <= 0 Then Return False
            Dim precioEfectivoNormal As Decimal = precio * (1D - descuento)
            Dim precioEfectivoOferta As Decimal = precioOferta * (1D - descuentoOferta)
            Return precioEfectivoOferta >= precioEfectivoNormal
        End Get
    End Property
    ''' <summary>Nesto#371: el check "personalizar oferta" solo se ve si hay cantidad de oferta.</summary>
    Public ReadOnly Property personalizarOfertaVisible As Visibility
        Get
            Return If(cantidadOferta > 0, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property
    ''' <summary>Nesto#371: los inputs de precio/dto se ven al marcar personalizar (con cantidad oferta).</summary>
    Public ReadOnly Property personalizarInputsVisible As Visibility
        Get
            Return If(cantidadOferta > 0 AndAlso personalizarOferta, Visibility.Visible, Visibility.Collapsed)
        End Get
    End Property
    Public Property tamanno() As System.Nullable(Of Integer)
    Public Property unidadMedida() As String
    Public Property familia() As String
    Public Property subGrupo() As String
    ''' <summary>
    ''' Grupo del producto (ej: COS, ACC, PEL, APA).
    ''' Issue #94: Sistema Ganavisiones - necesario para calcular base imponible bonificable.
    ''' </summary>
    Public Property grupo() As String
    Public Property codigoBarras As String
    Public Property estado() As Integer
    Public Property yaFacturado() As Boolean
    Public Property cantidadVendida() As Integer
    Public Property cantidadAbonada() As Integer
    Public Property fechaUltimaVenta() As System.Nullable(Of DateTime)
    Public Property iva() As String
    Private _precio As Decimal
    Public Property precio() As Decimal
        Get
            Return _precio
        End Get
        Set(value As Decimal)
            SetProperty(_precio, value)
            RaisePropertyChanged(NameOf(baseImponible))
        End Set
    End Property
    Private _aplicarDescuento As Boolean
    Public Property aplicarDescuento As Boolean
        Get
            Return _aplicarDescuento
        End Get
        Set(value As Boolean)
            If IsNothing(aplicarDescuentoFicha) Then
                'Esta línea hay que cambiarla cuando GestorPrecios funcione bien
                'aplicarDescuentoFicha = IIf(subGrupo.ToLower = "otros aparatos", False, value)
                aplicarDescuentoFicha = value
            End If
            SetProperty(_aplicarDescuento, value)
        End Set
    End Property
    Private _aplicarDescuentoFicha As Boolean?
    Public Property aplicarDescuentoFicha() As Boolean? Implements ILineaConCantidad.aplicarDescuentoFicha
        Get
            Return _aplicarDescuentoFicha
        End Get
        Set(value As Boolean?)
            Dim unused = SetProperty(_aplicarDescuentoFicha, value)
            RaisePropertyChanged(NameOf(puedeEditarCantidadOferta))
        End Set
    End Property

    ''' <summary>
    ''' Nesto#401: habilita el control de cantidad oferta. Con oferta YA puesta siempre se puede
    ''' editar (para reducirla o quitarla, que nunca añade beneficio no autorizado — la validación
    ''' del servidor sigue mandando); sin oferta, solo si la ficha del producto admite descuentos.
    ''' </summary>
    Public ReadOnly Property puedeEditarCantidadOferta As Boolean Implements ILineaConCantidad.puedeEditarCantidadOferta
        Get
            Return aplicarDescuentoFicha.GetValueOrDefault(False) OrElse cantidadOferta > 0
        End Get
    End Property

    ''' <summary>
    ''' Nesto#370: en el selector compartido enlaza IsEnabled del control de cantidad. Los productos
    ''' normales siempre se pueden seleccionar (solo los regalos bloqueados, en LineaRegalo, no).
    ''' </summary>
    Public ReadOnly Property puedeSeleccionar As Boolean
        Get
            Return True
        End Get
    End Property

    ''' <summary>
    ''' Nesto#370: solo aplica a regalos bloqueados (LineaRegalo). En productos siempre vacío/oculto.
    ''' </summary>
    Public ReadOnly Property textoDesbloqueo As String
        Get
            Return String.Empty
        End Get
    End Property
    Public ReadOnly Property desbloqueoVisible As Visibility
        Get
            Return Visibility.Collapsed
        End Get
    End Property
    Public Property stock As Integer
    Private _cantidadDisponible As Integer
    Public Property cantidadDisponible As Integer
        Get
            Return _cantidadDisponible
        End Get
        Set(value As Integer)
            SetProperty(_cantidadDisponible, value)
            RaisePropertyChanged(NameOf(colorStock))
        End Set
    End Property
    Public Property cantidadPendienteRecibir As Integer
    Public Property StockDisponibleTodosLosAlmacenes As Integer
    Private _stockActualizado As Boolean
    Public Property stockActualizado As Boolean
        Get
            Return _stockActualizado
        End Get
        Set(value As Boolean)
            SetProperty(_stockActualizado, value)
            RaisePropertyChanged(NameOf(colorStock))
        End Set
    End Property
    Public Property fechaInsercion As DateTime = DateTime.MaxValue
    Private _descuento As Decimal
    Public Property descuento As Decimal
        Get
            Return _descuento
        End Get
        Set(value As Decimal)
            SetProperty(_descuento, value)
            RaisePropertyChanged(NameOf(baseImponible))
        End Set
    End Property
    Public Property descuentoProducto As Decimal
    Public Property clasificacionMasVendidos As Integer
    ' Nesto#390: notificante para que al cambiar la preferencia de almacenes (toggle) se
    ' refresquen los textos derivados sin recargar la plantilla entera.
    Private _stocks As List(Of StockAlmacenDTO)
    Public Property stocks As List(Of StockAlmacenDTO)
        Get
            Return _stocks
        End Get
        Set(value As List(Of StockAlmacenDTO))
            SetProperty(_stocks, value)
            RaisePropertyChanged(NameOf(textoStocksPorAlmacen))
            RaisePropertyChanged(NameOf(stockTotalTodosAlmacenes))
        End Set
    End Property

    ''' <summary>
    ''' Nesto#397: ids de LinPedidoVta originales cuando la plantilla está en modo edición de un
    ''' pedido existente (0/Nothing = línea nueva). Permiten que el guardado haga PUT actualizando
    ''' las líneas en vez de recrearlas. En borradores normales quedan a 0 (ignorados).
    ''' </summary>
    Public Property idLineaPedido As Integer
    Public Property idLineaPedidoOferta As Integer?
    Private _urlImagen As String
    Public Property urlImagen As String
        Get
            Return _urlImagen
        End Get
        Set(value As String)
            SetProperty(_urlImagen, value)
            RaisePropertyChanged(NameOf(imagen))
            RaisePropertyChanged(NameOf(imagenVisible))
        End Set
    End Property
    Public ReadOnly Property colorEstado As Brush
        Get
            Dim color As Brush
            If cantidadAbonada >= cantidadVendida Then
                color = Brushes.Red
            ElseIf fechaUltimaVenta < DateAdd(DateInterval.Year, -1, Now) Then
                color = Brushes.Orange
            ElseIf fechaUltimaVenta < DateAdd(DateInterval.Month, -6, Now) AndAlso (cantidadAbonada = 0 OrElse cantidadVendida / cantidadAbonada > 10) Then
                color = Brushes.Yellow
            ElseIf fechaUltimaVenta < DateAdd(DateInterval.Month, -2, Now) AndAlso (cantidadAbonada = 0 OrElse cantidadVendida / cantidadAbonada > 10) Then
                color = Brushes.YellowGreen
            ElseIf Not IsNothing(fechaUltimaVenta) Then
                color = Brushes.Green
            Else
                color = Brushes.Blue
            End If
            If color IsNot Brushes.Green AndAlso estado = 0 Then
                color = Brushes.DarkGreen
            End If
            ' Issue #357: productos a extinguir (estado de producto = 4) en púrpura. Prevalece sobre
            ' el histórico de ventas para que el vendedor no los meta sin ver que van a desaparecer.
            ' Aquí 'estado' es el estado del PRODUCTO (no la línea); 4 = a extinguir.
            If estado = 4 Then
                color = Brushes.Purple
            End If
            Return color
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
            ElseIf (cantidadVendida = 0 AndAlso cantidadAbonada = 0) Then
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
            ElseIf StockDisponibleTodosLosAlmacenes < cantidad + cantidadOferta Then
                Return Brushes.Red
            ElseIf cantidadDisponible >= cantidad + cantidadOferta Then
                Return Brushes.Green
            ElseIf StockDisponibleTodosLosAlmacenes >= cantidad + cantidadOferta Then
                Return Brushes.DeepPink
            ElseIf cantidadDisponible + cantidadPendienteRecibir >= cantidad + cantidadOferta Then
                Return Brushes.Blue
            ElseIf stock >= cantidad + cantidadOferta Then
                Return Brushes.DarkOrange
            Else
                Return Brushes.Red
            End If
        End Get
    End Property
    Public ReadOnly Property imagenVisible As Visibility
        Get
            RaisePropertyChanged(NameOf(clasificacionVisible))
            'Return Visibility.Visible
            If urlImagen = "" Then
                Return Visibility.Collapsed
            Else
                Return Visibility.Visible
            End If
        End Get
    End Property
    Public ReadOnly Property clasificacionVisible As Visibility
        Get
            'Return Visibility.Visible
            If urlImagen = "" Then
                Return Visibility.Visible
            Else
                Return Visibility.Collapsed
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
    Public ReadOnly Property esSobrePedido As Boolean
        Get
            Return Not (Me.estado = 0 OrElse
                (stockActualizado AndAlso cantidadDisponible >= cantidad + cantidadOferta))
        End Get
    End Property
    Public ReadOnly Property colorSobrePedido As Brush
        Get
            If Not stockActualizado Then
                Return Brushes.LightGray
            End If
            If esSobrePedido Then
                Return Brushes.Yellow
            Else
                Return Brushes.White
            End If
        End Get
    End Property
    Public ReadOnly Property precioHabilitado As Boolean
        Get
            Return aplicarDescuento OrElse subGrupo = "Productos depilación ceras"
        End Get
    End Property
    Public ReadOnly Property baseImponible As Decimal
        Get
            ' Cálculo coherente con GestorPedidosVenta.CalcularImportesLinea (Issue #242/#243)
            ' Bruto = Cantidad * Precio (sin redondear)
            ' ImporteDto = ROUND(Bruto * Descuento, 2)
            ' BaseImponible = ROUND(Bruto, 2) - ImporteDto
            Dim bruto As Decimal = cantidad * precio
            Dim importeDescuento As Decimal = Math.Round(bruto * descuento, 2, MidpointRounding.AwayFromZero)
            Return Math.Round(bruto, 2, MidpointRounding.AwayFromZero) - importeDescuento
        End Get
    End Property

    ''' <summary>
    ''' Nesto#371: base imponible que aporta la unidad de oferta cuando se personaliza (precio/dto en
    ''' vez de ir gratis). 0 si la oferta va gratis. Se calcula igual que la base de la línea cobrada,
    ''' para que los totales del pedido (base, total, portes, bonificable) la tengan en cuenta.
    ''' </summary>
    Public ReadOnly Property baseImponibleOferta As Decimal
        Get
            If Not personalizarOferta OrElse cantidadOferta <= 0 Then Return 0
            Dim bruto As Decimal = cantidadOferta * precioOferta
            Dim importeDescuento As Decimal = Math.Round(bruto * descuentoOferta, 2, MidpointRounding.AwayFromZero)
            Return Math.Round(bruto, 2, MidpointRounding.AwayFromZero) - importeDescuento
        End Get
    End Property
    Public ReadOnly Property textoUnidadesDisponibles As String
        Get
            Dim cantidadMenor As Integer = If(Not stockActualizado OrElse cantidadDisponible <= StockDisponibleTodosLosAlmacenes, cantidadDisponible, StockDisponibleTodosLosAlmacenes)
            If cantidadMenor = 0 Then
                Return String.Empty
            ElseIf cantidadMenor < 0 Then
                Return String.Format("Falta stock ({0} und.)", -cantidadMenor)
            ElseIf cantidadMenor = 1 Then
                Return "Solo 1 und. disponible"
            Else
                Return String.Format("{0} und. disponibles", cantidadMenor)
            End If
        End Get
    End Property
    Public ReadOnly Property textoStocksPorAlmacen As String
        Get
            If stocks Is Nothing OrElse stocks.Count = 0 Then
                Return String.Empty
            End If
            Return String.Join(" | ", stocks.Select(Function(s) $"{s.almacen}:{s.cantidadDisponible}"))
        End Get
    End Property
    Public ReadOnly Property stockTotalTodosAlmacenes As Integer
        Get
            If stocks Is Nothing OrElse stocks.Count = 0 Then
                Return cantidadDisponible
            End If
            Return stocks.Sum(Function(s) s.cantidadDisponible)
        End Get
    End Property

    Public Property esLineaPortes As Boolean = False

    ' Issue #159: marca la línea "virtual" de comisión contra reembolso (análoga a esLineaPortes).
    Public Property esLineaReembolso As Boolean = False

    Public Function Contains(filtro As String) As Boolean Implements IFiltrableItem.Contains
        Return (Not IsNothing(producto) AndAlso producto.ToLower.Contains(filtro)) OrElse
                (Not IsNothing(texto) AndAlso texto.ToLower.Contains(filtro)) OrElse
                (Not IsNothing(familia) AndAlso familia.ToLower.Contains(filtro)) OrElse
                (Not IsNothing(subGrupo) AndAlso subGrupo.ToLower.Contains(filtro)) OrElse
                (Not IsNothing(codigoBarras) AndAlso codigoBarras.ToLower.Contains(filtro))
    End Function
End Class
