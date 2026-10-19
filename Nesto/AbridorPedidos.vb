Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Modulos.PedidoVenta

''' <summary>
''' NestoAPI#555: abre el pedido en una pestaña nueva (PedidoVentaViewModel.CargarPedido).
''' Nesto#490 (4C.5, tramo 3a): es lo que reciben por el constructor las pantallas que abren un pedido (Clientes,
''' Comisiones, Bancos, Canales externos, la plantilla, el detalle del pedido y los errores de facturación de rutas) y la
''' campana, en lugar del IUnityContainer. Lo crea ComposicionNesto con el resolutor propio (IResolutorNesto).
''' </summary>
Public Class AbridorPedidos
    Implements IAbridorPedidos

    Private ReadOnly _resolutor As IResolutorNesto

    Public Sub New(resolutor As IResolutorNesto)
        If resolutor Is Nothing Then
            Throw New ArgumentNullException(NameOf(resolutor))
        End If
        _resolutor = resolutor
    End Sub

    Public Sub Abrir(empresa As String, pedido As Integer) Implements IAbridorPedidos.Abrir
        PedidoVentaViewModel.CargarPedido(empresa, pedido, _resolutor)
    End Sub
End Class
