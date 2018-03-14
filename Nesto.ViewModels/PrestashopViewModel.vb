Imports Nesto.Models
Imports System.Windows.Input
Imports System.ComponentModel
Imports System.Windows
Imports System.Data.Objects
Imports System.Collections.ObjectModel
'Imports Nesto.Models.Nesto.Models.EF
Imports System.Windows.Controls

Public Class PrestashopViewModel
    Inherits ViewModelBase

    Private Shared DbContext As New NestoEntities

    Public Sub New()
        If DesignerProperties.GetIsInDesignMode(New DependencyObject()) Then
            Return
        End If
        Titulo = "Prestashop"
    End Sub


    Private _Producto As ObservableCollection(Of PrestashopProductos)
    Public Property Producto As ObservableCollection(Of PrestashopProductos)
        Get
            Return _Producto
        End Get
        Set(value As ObservableCollection(Of PrestashopProductos))
            _Producto = value
            OnPropertyChanged("Producto")
        End Set
    End Property

    Private _LineaSeleccionada As PrestashopProductos
    Public Property LineaSeleccionada As PrestashopProductos
        Get
            Return _LineaSeleccionada
        End Get
        Set(value As PrestashopProductos)
            _LineaSeleccionada = value
            OnPropertyChanged("LineaSeleccionada")
        End Set
    End Property

    Private Property _productoBuscar As String
    Public Property productoBuscar As String
        Get
            Return _productoBuscar
        End Get
        Set(value As String)
            _productoBuscar = value
            OnPropertyChanged("productoBuscar")
        End Set
    End Property

    Private _PestañaSeleccionada As TabItem
    Public Property PestañaSeleccionada As TabItem
        Get
            Return _PestañaSeleccionada
        End Get
        Set(value As TabItem)
            If value.Name = "tabProductos" Then
                Producto = New ObservableCollection(Of PrestashopProductos)(From x In DbContext.PrestashopProductos Select x)
            ElseIf value.Name = "tabRevisar" Then
                Producto = New ObservableCollection(Of PrestashopProductos)(From x In DbContext.PrestashopProductos Where (x.VistoBueno = False Or x.VistoBueno Is Nothing) Select x)
            End If
            _PestañaSeleccionada = value
            OnPropertyChanged("PestañaSeleccionada")
        End Set
    End Property


#Region "Comandos"
    Private _cmdGuardarCambios As ICommand
    Public ReadOnly Property cmdGuardarCambios() As ICommand
        Get
            If _cmdGuardarCambios Is Nothing Then
                _cmdGuardarCambios = New RelayCommand(AddressOf GuardarCambios, AddressOf CanGuardarCambios)
            End If
            Return _cmdGuardarCambios
        End Get
    End Property
    Private Function CanGuardarCambios(ByVal param As Object) As Boolean
        Dim changes As IEnumerable(Of System.Data.Objects.ObjectStateEntry) = DbContext.ObjectStateManager.GetObjectStateEntries(System.Data.EntityState.Added Or System.Data.EntityState.Modified Or System.Data.EntityState.Deleted)
        Return changes.Any
    End Function
    Private Sub GuardarCambios(ByVal param As Object)
        DbContext.SaveChanges()
    End Sub

    Private _cmdBuscar As ICommand
    Public ReadOnly Property cmdBuscar() As ICommand
        Get
            If _cmdBuscar Is Nothing Then
                _cmdBuscar = New RelayCommand(AddressOf Buscar, AddressOf CanBuscar)
            End If
            Return _cmdBuscar
        End Get
    End Property
    Private Function CanBuscar(ByVal param As Object) As Boolean
        Return True
    End Function
    Private Sub Buscar(ByVal param As Object)
        LineaSeleccionada = Producto.Where(Function(f) f.Número.Contains(productoBuscar)).FirstOrDefault
    End Sub

    Private _cmdAñadir As ICommand
    Public ReadOnly Property cmdAñadir() As ICommand
        Get
            If _cmdAñadir Is Nothing Then
                _cmdAñadir = New RelayCommand(AddressOf Añadir, AddressOf CanAñadir)
            End If
            Return _cmdAñadir
        End Get
    End Property
    Private Function CanAñadir(ByVal param As Object) As Boolean
        If productoBuscar Is Nothing Then
            Return False
        End If
        Return productoBuscar.Trim <> ""
    End Function
    Private Sub Añadir(ByVal param As Object)
        Dim psprod As New PrestashopProductos
        psprod.Empresa = "1"
        psprod.Número = productoBuscar
        psprod.VistoBueno = (System.Environment.UserName = "Laura") Or (System.Environment.UserName = "Carlos")
        DbContext.AddToPrestashopProductos(psprod)
        Producto.Add(psprod)
        LineaSeleccionada = Producto.Where(Function(f) f.Número.Contains(productoBuscar)).FirstOrDefault
    End Sub



    Private _cmdVistoBueno As ICommand
    Public ReadOnly Property cmdVistoBueno() As ICommand
        Get
            If _cmdVistoBueno Is Nothing Then
                _cmdVistoBueno = New RelayCommand(AddressOf VistoBueno, AddressOf CanVistoBueno)
            End If
            Return _cmdVistoBueno
        End Get
    End Property
    Private Function CanVistoBueno(ByVal param As Object) As Boolean
        If LineaSeleccionada Is Nothing Then
            Return False
        Else
            If LineaSeleccionada.VistoBueno Is Nothing Then
                Dim nombreUsuario As String = System.Environment.UserName.ToLower
                Return (nombreUsuario = "laura") Or (nombreUsuario = "carlos") Or (nombreUsuario = "enrique")
            Else
                Return Not LineaSeleccionada.VistoBueno
            End If
        End If
    End Function
    Private Sub VistoBueno(ByVal param As Object)
        LineaSeleccionada.VistoBueno = True
    End Sub



#End Region





End Class
