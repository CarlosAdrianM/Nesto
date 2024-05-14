Imports Prism.Mvvm
Imports Nesto.Infrastructure.Contracts

Public Class LineaPlantillaVenta
    Inherits BindableBase
    Implements IFiltrableItem

    Public Property producto() As String
    Public Property texto() As String
    Private _cantidad As Integer
    Public Property cantidad As Integer
        Get
            Return _cantidad
        End Get
        Set(value As Integer)
            SetProperty(_cantidad, value)
            RaisePropertyChanged(NameOf(colorStock))
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
            RaisePropertyChanged(NameOf(colorStock))
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
                'aplicarDescuentoFicha = IIf(subGrupo.ToLower = "otros aparatos", False, value)
                aplicarDescuentoFicha = value
            End If
            SetProperty(_aplicarDescuento, value)
        End Set
    End Property
    Public Property aplicarDescuentoFicha() As Boolean?
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
    Public Property stockActualizado As Boolean
    Public Property fechaInsercion As DateTime = DateTime.MaxValue
    Public Property descuento As Decimal
    Public Property descuentoProducto As Decimal
    Public Property clasificacionMasVendidos As Integer
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
    Public ReadOnly Property textoUnidadesDisponibles As String
        Get
            Dim cantidadMenor As Integer = If(cantidadDisponible <= StockDisponibleTodosLosAlmacenes, cantidadDisponible, StockDisponibleTodosLosAlmacenes)
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

    Public Function Contains(filtro As String) As Boolean Implements IFiltrableItem.Contains
        Return (Not IsNothing(producto) AndAlso producto.ToLower.Contains(filtro)) OrElse
                (Not IsNothing(texto) AndAlso texto.ToLower.Contains(filtro)) OrElse
                (Not IsNothing(familia) AndAlso familia.ToLower.Contains(filtro)) OrElse
                (Not IsNothing(subGrupo) AndAlso subGrupo.ToLower.Contains(filtro))
    End Function
End Class
