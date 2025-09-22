Imports System.Collections.ObjectModel
Imports System.Collections.Specialized
Imports System.ComponentModel
Imports Nesto.Models
Imports Prism.Mvvm

Public Class PedidoVentaWrapper
    Inherits BindableBase

    Public Event IvaCambiado(nuevoIva As String)

    Public Sub New(pedido As PedidoVentaDTO)
        If IsNothing(pedido) Then
            Return
        End If
        Model = pedido
        Lineas = New ObservableCollection(Of LineaPedidoVentaWrapper)
        AddHandler Lineas.CollectionChanged, AddressOf ContentCollectionChanged
        AddHandler Prepagos.CollectionChanged, AddressOf PrepagosCollectionChanged

        For Each linea In Model.Lineas
            Dim lineaNueva As New LineaPedidoVentaWrapper(linea) With {
                .Pedido = Me
            }
            Lineas.Add(lineaNueva)
        Next
    End Sub

    Public Property Model As PedidoVentaDTO

    Private _lineas As ObservableCollection(Of LineaPedidoVentaWrapper)
    Public Property Lineas As ObservableCollection(Of LineaPedidoVentaWrapper)
        Get
            Return _lineas
        End Get
        Set(value As ObservableCollection(Of LineaPedidoVentaWrapper))
            For Each detail In value
                detail.Pedido = Me
            Next
            Dim unused = SetProperty(_lineas, value)
        End Set
    End Property

    Public Property empresa() As String
        Get
            Return Model.empresa
        End Get
        Set(value As String)
            Model.empresa = value
        End Set
    End Property
    Public Property numero() As Integer
        Get
            Return Model.numero
        End Get
        Set(value As Integer)
            Model.numero = value
        End Set
    End Property

    Public Property cliente() As String
        Get
            Return Model.cliente
        End Get
        Set(value As String)
            Model.cliente = value
            RaisePropertyChanged(NameOf(cliente))
        End Set
    End Property

    Public Property contacto() As String
        Get
            Return Model.contacto
        End Get
        Set(value As String)
            Model.contacto = value
            RaisePropertyChanged(NameOf(contacto))
        End Set
    End Property
    Public Property fecha() As Nullable(Of System.DateTime)
        Get
            Return Model.fecha
        End Get
        Set(value As Nullable(Of System.DateTime))
            Model.fecha = value
        End Set
    End Property
    Public Property formaPago() As String
        Get
            Return Model.formaPago
        End Get
        Set(value As String)
            Model.formaPago = value
            RaisePropertyChanged(NameOf(formaPago))
        End Set
    End Property
    Public Property plazosPago() As String
        Get
            Return Model.plazosPago
        End Get
        Set(value As String)
            Model.plazosPago = value
            RaisePropertyChanged(NameOf(plazosPago))
        End Set
    End Property
    Public Property primerVencimiento() As Nullable(Of System.DateTime)
        Get
            Return Model.primerVencimiento
        End Get
        Set(value As Nullable(Of System.DateTime))
            Model.primerVencimiento = value
            RaisePropertyChanged(NameOf(primerVencimiento))
        End Set
    End Property
    Public Property iva() As String
        Get
            Return Model.iva
        End Get
        Set(value As String)
            Model.iva = value
            RaisePropertyChanged(NameOf(iva))
            RaiseEvent IvaCambiado(value)
        End Set
    End Property

    Public Property vendedor As String
        Get
            Return Model.vendedor
        End Get
        Set(value As String)
            Model.vendedor = value
            RaisePropertyChanged(NameOf(vendedor))
        End Set
    End Property
    Public Property comentarios() As String
        Get
            Return Model.comentarios
        End Get
        Set(value As String)
            Model.comentarios = value
        End Set
    End Property
    Public Property comentarioPicking() As String
        Get
            Return Model.comentarioPicking
        End Get
        Set(value As String)
            Model.comentarioPicking = value
        End Set
    End Property
    Public Property CrearEfectosManualmente As Boolean
        Get
            Return Model.CrearEfectosManualmente
        End Get
        Set(value As Boolean)
            If Model.CrearEfectosManualmente <> value Then
                Model.CrearEfectosManualmente = value
                RaisePropertyChanged(NameOf(CrearEfectosManualmente))
            End If
        End Set
    End Property
    Public Property periodoFacturacion() As String
        Get
            Return Model.periodoFacturacion
        End Get
        Set(value As String)
            Model.periodoFacturacion = value
        End Set
    End Property
    Public Property ruta() As String
        Get
            Return Model.ruta
        End Get
        Set(value As String)
            Model.ruta = value
        End Set
    End Property
    Public Property serie() As String
        Get
            Return Model.serie
        End Get
        Set(value As String)
            Model.serie = value
        End Set
    End Property
    Public Property ccc() As String
        Get
            Return Model.ccc
        End Get
        Set(value As String)
            If Model.ccc <> value Then
                Model.ccc = value
                RaisePropertyChanged(NameOf(ccc))
            End If
        End Set
    End Property
    Public Property origen() As String
        Get
            Return Model.origen
        End Get
        Set(value As String)
            Model.origen = value
        End Set
    End Property
    Public Property contactoCobro() As String
        Get
            Return Model.contactoCobro
        End Get
        Set(value As String)
            Model.contactoCobro = value
        End Set
    End Property
    Public Property noComisiona() As Decimal
        Get
            Return Model.noComisiona
        End Get
        Set(value As Decimal)
            Model.noComisiona = value
        End Set
    End Property
    Public Property vistoBuenoPlazosPago() As Boolean
        Get
            Return Model.vistoBuenoPlazosPago
        End Get
        Set(value As Boolean)
            Model.vistoBuenoPlazosPago = value
        End Set
    End Property
    Public Property mantenerJunto() As Boolean
        Get
            Return Model.mantenerJunto
        End Get
        Set(value As Boolean)
            Model.mantenerJunto = value
        End Set
    End Property
    Public Property servirJunto() As Boolean
        Get
            Return Model.servirJunto
        End Get
        Set(value As Boolean)
            Model.servirJunto = value
        End Set
    End Property
    Public Property EsPresupuesto() As Boolean
        Get
            Return Model.EsPresupuesto
        End Get
        Set(value As Boolean)
            Model.EsPresupuesto = value
        End Set
    End Property
    Public Property notaEntrega As Boolean
        Get
            Return Model.notaEntrega
        End Get
        Set(value As Boolean)
            Model.notaEntrega = value
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
    Public Property DescuentoPP As Decimal
        Get
            Return Model.DescuentoPP
        End Get
        Set(value As Decimal)
            Model.DescuentoPP = value
            RaisePropertyChanged(NameOf(DescuentoPP))
            RaisePropertyChanged(NameOf(BaseImponible))
            RaisePropertyChanged(NameOf(baseImponiblePicking))
            RaisePropertyChanged(NameOf(Total))
            RaisePropertyChanged(NameOf(totalPicking))
        End Set
    End Property

    Public ReadOnly Property Bruto As Decimal
        Get
            Return Model.Bruto
        End Get
    End Property

    Public ReadOnly Property baseImponiblePicking As Decimal
        Get
            Return Model.baseImponiblePicking
        End Get
    End Property

    Public ReadOnly Property brutoPicking As Decimal
        Get
            Return Model.brutoPicking
        End Get
    End Property

    Public ReadOnly Property sumaDescuentos(linea As LineaPedidoVentaDTO) As Decimal
        Get
            Return Model.sumaDescuentos(linea)
        End Get
    End Property

    Public ReadOnly Property totalPicking As Decimal
        Get
            Return Model.totalPicking
        End Get
    End Property

    Public Overridable Property Efectos As ListaEfectos
        Get
            Return Model.Efectos
        End Get
        Set(value As ListaEfectos)
            Model.Efectos = value
        End Set
    End Property
    Public Overridable Property Prepagos() As ObservableCollection(Of PrepagoDTO)
        Get
            Return Model.Prepagos
        End Get
        Set(value As ObservableCollection(Of PrepagoDTO))
            Model.Prepagos = value
        End Set
    End Property
    Public Overridable Property VendedoresGrupoProducto As ObservableCollection(Of VendedorGrupoProductoDTO)
        Get
            Return Model.VendedoresGrupoProducto
        End Get
        Set(value As ObservableCollection(Of VendedorGrupoProductoDTO))
            Model.VendedoresGrupoProducto = value
        End Set
    End Property



    Private Sub ContentCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        If e.NewItems IsNot Nothing Then
            For Each item As LineaPedidoVentaWrapper In e.NewItems
                If item IsNot Nothing Then
                    AddHandler item.PropertyChanged, AddressOf LineaOnPropertyChanged
                    If item.id = 0 Then
                        item.Pedido = Me
                        Dim posicion As Integer = Lineas.IndexOf(item)
                        Dim lista = DirectCast(Model.Lineas, IList(Of LineaPedidoVentaDTO))
                        lista.Insert(posicion, item.Model)
                    End If
                End If
            Next
        End If

        If e.OldItems IsNot Nothing Then
            For Each item As LineaPedidoVentaWrapper In e.OldItems
                If item IsNot Nothing Then
                    RemoveHandler item.PropertyChanged, AddressOf LineaOnPropertyChanged
                    Dim unused = Model.Lineas.Remove(item.Model)
                End If
            Next
            RaisePropertyChanged(String.Empty)
        End If
    End Sub

    Private Sub PrepagosCollectionChanged(sender As Object, e As NotifyCollectionChangedEventArgs)
        If e.NewItems IsNot Nothing Then
            'For Each item As PrepagoDTO In e.NewItems
            '    If item IsNot Nothing Then

            '    End If
            'Next
        End If

        If e.OldItems IsNot Nothing Then
            'For Each item As PrepagoDTO In e.OldItems
            '    If item IsNot Nothing Then

            '    End If
            'Next
            RaisePropertyChanged(String.Empty)
        End If
    End Sub

    Private Sub LineaOnPropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(BaseImponible) Then
            RaisePropertyChanged(NameOf(BaseImponible))
        End If
        If e.PropertyName = NameOf(Total) Then
            RaisePropertyChanged(NameOf(Total))
        End If
    End Sub


End Class
