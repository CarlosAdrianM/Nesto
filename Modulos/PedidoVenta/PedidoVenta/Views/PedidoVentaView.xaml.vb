Imports Prism.Regions
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Prism.Ioc
Imports Unity

Public Class PedidoVentaView
    Private ReadOnly container As IUnityContainer
    Public Property scopedRegionManager As IRegionManager
    Private cargado As Boolean = False


    Public Sub New(container As IUnityContainer)
        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.container = container
    End Sub

    Private Async Sub PedidoVentaView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Not cargado Then
            Dim view As ListaPedidosVenta = container.Resolve(Of ListaPedidosVenta)
            Dim viewModel As ListaPedidosVentaViewModel = CType(view.DataContext, ListaPedidosVentaViewModel)
            view.cambiarRegionManager(scopedRegionManager)
            Dim region As IRegion = scopedRegionManager.Regions("ListaPedidosRegion")
            region.Add(view, "ListaPedidosVenta")
            If Me.DataContext.empresaInicial <> "" AndAlso Me.DataContext.pedidoInicial <> 0 Then
                Dim resumen As ResumenPedido = New ResumenPedido With {
                    .empresa = Me.DataContext.empresaInicial,
                    .numero = Me.DataContext.pedidoInicial
                    }
                viewModel.ListaPedidos.ElementoSeleccionado = resumen
            Else
                viewModel.ListaPedidos.ElementoSeleccionado = Await viewModel.cargarPedidoPorDefecto()
            End If
            region.Activate(view)
            cargado = True
        End If
    End Sub
End Class
