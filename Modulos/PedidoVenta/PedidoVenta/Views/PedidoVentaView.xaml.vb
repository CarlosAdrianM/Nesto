Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta.PedidoVentaModel
Imports Unity

' Nesto#490 (4C.4, 7.º tramo): la navegación de su ámbito (sus regiones de lista y detalle) le llega por
' IConAmbitoNavegacion al abrirla con AbrirVistaConAmbito, en vez del scopedRegionManager de Prism.
Public Class PedidoVentaView
    Implements IConAmbitoNavegacion

    Private ReadOnly container As IUnityContainer
    Public Property NavegacionAmbito As IServicioNavegacion Implements IConAmbitoNavegacion.NavegacionAmbito
    Private cargado As Boolean = False


    Public Sub New(container As IUnityContainer, viewModel As PedidoVentaViewModel)
        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        DataContext = viewModel
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.container = container
    End Sub

    Private Async Sub PedidoVentaView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        If Not cargado AndAlso NavegacionAmbito IsNot Nothing Then
            Dim view As ListaPedidosVenta = container.Resolve(Of ListaPedidosVenta)
            Dim viewModel As ListaPedidosVentaViewModel = CType(view.DataContext, ListaPedidosVentaViewModel)
            ' Add con su nombre y Activate, como antes (la región de un ContentControl ya activaba sola la primera vista,
            ' así que activarla antes de elegir el pedido no cambia nada). La lista recibe esta misma navegación
            ' (IConAmbitoNavegacion) y navega al detalle de esta pestaña. Se marca como cargada antes del Await: si
            ' llegara otro Loaded mientras se lee el pedido por defecto, no se añade la lista otra vez.
            NavegacionAmbito.AbrirVistaNueva("ListaPedidosRegion", view, "ListaPedidosVenta")
            cargado = True
            If Me.DataContext.empresaInicial <> "" AndAlso Me.DataContext.pedidoInicial <> 0 Then
                Dim resumen As ResumenPedido = New ResumenPedido With {
                    .empresa = Me.DataContext.empresaInicial,
                    .numero = Me.DataContext.pedidoInicial
                    }
                viewModel.ListaPedidos.ElementoSeleccionado = resumen
            Else
                viewModel.ListaPedidos.ElementoSeleccionado = Await viewModel.cargarPedidoPorDefecto()
            End If
        End If
    End Sub
End Class
