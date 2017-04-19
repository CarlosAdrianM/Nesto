Imports System.Globalization
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity

Public Class PedidoVentaView
    Private ReadOnly container As IUnityContainer
    Public Property scopedRegionManager As IRegionManager
    Private cargado As Boolean = False


    Public Sub New(viewModel As PedidoVentaViewModel, container As IUnityContainer)
        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel
        Me.container = container
    End Sub

    Private Sub PedidoVentaView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Not cargado Then
            Dim view As ListaPedidosVenta = Me.container.Resolve(Of ListaPedidosVenta)
            view.cambiarRegionManager(scopedRegionManager)
            Dim region As IRegion = scopedRegionManager.Regions("ListaPedidosRegion")
            region.Add(view, "ListaPedidosVenta")
            region.Activate(view)
            cargado = True
        End If
    End Sub
End Class
