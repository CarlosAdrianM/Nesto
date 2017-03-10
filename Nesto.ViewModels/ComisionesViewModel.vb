Imports System.Collections.ObjectModel
Imports Nesto.Models
Imports System.ComponentModel
Imports System.Windows
Imports System.Windows.Input
Imports System.Globalization
'Imports Nesto.Models.Nesto.Models.EF

Public Class ComisionesViewModel
    Inherits ViewModelBase

    Private Shared DbContext As NestoEntities

    Dim mainModel As New Nesto.Models.MainModel
    Private vendedor As String = mainModel.leerParametro("1", "Vendedor")

    Public Sub New()
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        DbContext = New NestoEntities
        If vendedor = "" Then
            listaVendedores = New ObservableCollection(Of Vendedores)(From c In DbContext.Vendedores Where c.Empresa = "1" And (c.Estado = 0 OrElse c.Estado = 4))
        Else
            listaVendedores = New ObservableCollection(Of Vendedores)(From c In DbContext.Vendedores Where c.Empresa = "1" And (c.Estado = 0 OrElse c.Estado = 4) And c.Número.Trim = vendedor)
        End If

        colMeses = New Collection(Of String)
        For i = 12 To 1 Step -1
            _colMeses.Add(Now.AddMonths(i).ToString("MMMM"))
        Next

        mesActual = colMeses(0)
        vendedorActual = listaVendedores.FirstOrDefault

        Titulo = "Comisiones"




    End Sub

    Private _listaVendedores As ObservableCollection(Of Vendedores)
    Public Property listaVendedores As ObservableCollection(Of Vendedores)
        Get
            Return _listaVendedores
        End Get
        Set(value As ObservableCollection(Of Vendedores))
            _listaVendedores = value
            OnPropertyChanged("listaVendedores")
        End Set
    End Property

    Private _vendedorActual As Vendedores
    Public Property vendedorActual As Vendedores
        Get
            Return _vendedorActual
        End Get
        Set(value As Vendedores)
            _vendedorActual = value
            OnPropertyChanged("vendedorActual")
            comisionesActual = DbContext.Comisiones("1", fechaDesde, fechaHasta, vendedorActual.Número, 0).FirstOrDefault
            listaPedidos = New ObservableCollection(Of vstLinPedidoVtaConVendedor)(From l In DbContext.vstLinPedidoVtaConVendedor
                                                                                   Where (l.Empresa = "1" Or l.Empresa = "3") And l.Estado >= -1 And l.Estado <= 1 And l.Vendedor = vendedorActual.Número
                                                                                   Order By l.Número, l.Nº_Orden
                                                                                   Select l)
            listaVentasComision = New ObservableCollection(Of vstLinPedidoVtaComisiones)(From l In DbContext.vstLinPedidoVtaComisiones
                                                                                         Where (((l.Estado = 4 AndAlso l.Fecha_Factura >= fechaDesde AndAlso l.Fecha_Factura <= fechaHasta) OrElse
                                                                                             (l.Estado = 2 AndAlso l.Fecha_Albarán >= fechaDesde AndAlso l.Fecha_Albarán <= fechaHasta)) AndAlso
                                                                                             l.Vendedor = vendedorActual.Número)
                                                                                         Order By l.Grupo, l.Dirección
                                                                                         Select l)
        End Set
    End Property

    Private _comisionesActual As Comisiones_Result
    Public Property comisionesActual As Comisiones_Result
        Get
            Return _comisionesActual
        End Get
        Set(value As Comisiones_Result)
            _comisionesActual = value
            OnPropertyChanged("comisionesActual")
        End Set
    End Property

    Private _colMeses
    Public Property colMeses As Collection(Of String)
        Get
            Return _colMeses
        End Get
        Set(value As Collection(Of String))
            _colMeses = value
        End Set
    End Property

    Private _mesActual
    Public Property mesActual As String
        Get
            Return _mesActual
        End Get
        Set(value As String)
            _mesActual = value
            fechaDesde = DateSerial(Year(Now), DateTime.ParseExact(value, "MMMM", CultureInfo.CurrentCulture).Month, 1)
            If fechaDesde > Now Then
                fechaDesde = fechaDesde.AddYears(-1)
            End If
            fechaHasta = (fechaDesde.AddMonths(1)).AddDays(-1)
            If vendedorActual IsNot Nothing Then
                comisionesActual = DbContext.Comisiones("1", fechaDesde, fechaHasta, vendedorActual.Número, 0).FirstOrDefault
                listaVentasComision = New ObservableCollection(Of vstLinPedidoVtaComisiones)(From l In DbContext.vstLinPedidoVtaComisiones
                                                                                             Where (((l.Estado = 4 AndAlso l.Fecha_Factura >= fechaDesde AndAlso l.Fecha_Factura <= fechaHasta) OrElse
                                                                                             (l.Estado = 2 AndAlso l.Fecha_Albarán >= fechaDesde AndAlso l.Fecha_Albarán <= fechaHasta)) AndAlso
                                                                                             l.Vendedor = vendedorActual.Número)
                                                                                             Order By l.Grupo, l.Dirección
                                                                                             Select l)
            End If
        End Set
    End Property

    Private _fechaDesde As Date
    Public Property fechaDesde As Date
        Get
            Return _fechaDesde
        End Get
        Set(value As Date)
            _fechaDesde = value
        End Set
    End Property


    Private _fechaHasta As Date
    Public Property fechaHasta As Date
        Get
            Return _fechaHasta
        End Get
        Set(value As Date)
            _fechaHasta = value
        End Set
    End Property

    Private _listaPedidos As ObservableCollection(Of vstLinPedidoVtaConVendedor)
    Public Property listaPedidos As ObservableCollection(Of vstLinPedidoVtaConVendedor)
        Get
            Return _listaPedidos
        End Get
        Set(value As ObservableCollection(Of vstLinPedidoVtaConVendedor))
            _listaPedidos = value
            OnPropertyChanged("listaPedidos")
        End Set
    End Property

    Private _listaVentasComision As ObservableCollection(Of vstLinPedidoVtaComisiones)
    Public Property listaVentasComision As ObservableCollection(Of vstLinPedidoVtaComisiones)
        Get
            Return _listaVentasComision
        End Get
        Set(value As ObservableCollection(Of vstLinPedidoVtaComisiones))
            _listaVentasComision = value
            OnPropertyChanged("listaVentasComision")
        End Set
    End Property





#Region "Comandos"
    'Private _cmdCargarComisiones As ICommand
    'Public ReadOnly Property cmdCargarComisiones() As ICommand
    '    Get
    '        If _cmdCargarComisiones Is Nothing Then
    '            _cmdCargarComisiones = New RelayCommand(AddressOf CargarComisiones, AddressOf CanCargarComisiones)
    '        End If
    '        Return _cmdCargarComisiones
    '    End Get
    'End Property
    'Private Function CanCargarComisiones(ByVal param As Object) As Boolean
    '    Return True
    'End Function
    'Private Sub CargarComisiones(ByVal param As Object)
    '    comisionesActual = DbContext.Comisiones("1", "01/01/13", "31/01/13", vendedorActual.Número, 0).FirstOrDefault
    'End Sub

#End Region

End Class




