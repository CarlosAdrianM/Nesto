Imports ControlesUsuario.Dialogs
Imports Nesto.Modulos.PedidoVenta
Imports Unity

''' <summary>NestoAPI#555: abre el pedido en una pestaña, igual que desde Canales externos.</summary>
Public Class AbridorPedidos
    Implements IAbridorPedidos

    Private ReadOnly _container As IUnityContainer

    Public Sub New(container As IUnityContainer)
        _container = container
    End Sub

    Public Sub Abrir(empresa As String, pedido As Integer) Implements IAbridorPedidos.Abrir
        PedidoVentaViewModel.CargarPedido(empresa, pedido, _container)
    End Sub
End Class
