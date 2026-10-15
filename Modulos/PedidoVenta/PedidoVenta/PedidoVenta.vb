Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Services
Imports Nesto.Infrastructure.Composicion
Imports Nesto.Infrastructure.Contracts
Imports Prism.Ioc
Imports Prism.Modularity

Public Class PedidoVenta
    Implements IModule, IModuloNesto, IPedidoVenta

    ' Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
    Private Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        Registrar(New RegistroPrism(containerRegistry))
    End Sub

    Private Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Inicializar(New ResolutorPrism(containerProvider))
    End Sub

    Public Sub Registrar(registro As IRegistroNesto) Implements IModuloNesto.Registrar
        registro.Vista(Of PedidoVentaView)("PedidoVentaView")
        registro.Vista(Of DetallePedidoView)("DetallePedidoView")
        registro.RegistrarDialogo(Of PickingPopupView, PickingPopupViewModel)
        ' Sugerencia 575: pedidos que podrían ir por nuestra ruta, antes de sacar el picking
        registro.RegistrarDialogo(Of SugerenciasRutaPickingPopup, SugerenciasRutaPickingPopupViewModel)(SugerenciasRutaPickingPopupViewModel.NOMBRE)
        registro.RegistrarDialogo(Of FacturarRutasPopup, FacturarRutasPopupViewModel)
        registro.RegistrarDialogo(Of ErroresFacturacionRutasPopup, ErroresFacturacionRutasPopupViewModel)
        registro.RegistrarDialogo(Of CopiarFacturaView, CopiarFacturaViewModel)
        ' NestoAPI#352: pregunta el grupo de comisión de las líneas de inmovilizado al guardar
        registro.RegistrarDialogo(Of SelectorGrupoComisionDialog, SelectorGrupoComisionDialogViewModel)

        ' Registrar servicios del módulo
        registro.Singleton(Of Services.IServicioFacturacionRutas, Services.ServicioFacturacionRutas)()
        registro.Singleton(Of IServicioImpresionDocumentos, ServicioImpresionDocumentos)()

        ' Issue #263: Servicios de autocomplete
        registro.Transitorio(Of ServicioBusquedaProductos)()
        registro.Transitorio(Of ServicioBusquedaCuentas)()

        ' Nesto#490 (4C.5): lo que Unity construía sin registrar. PedidoVentaView y ListaPedidosVenta también se piden
        ' por su tipo (PedidoVentaViewModel.CargarPedido y el maestro-detalle de PedidoVentaView).
        registro.Transitorio(Of PedidoVentaMenuBar)()
        registro.Transitorio(Of PedidoVentaViewModel)()
        registro.Transitorio(Of PedidoVentaView)()
        registro.Transitorio(Of ListaPedidosVenta)()
        ' Los ViewModels de sus vistas
        registro.Transitorio(Of ListaPedidosVentaViewModel)()
        registro.Transitorio(Of DetallePedidoViewModel)()
    End Sub

    Public Sub Inicializar(resolutor As IResolutorNesto) Implements IModuloNesto.Inicializar
        Dim view = resolutor.Resolver(Of PedidoVentaMenuBar)()
        If Not IsNothing(view) Then
            resolutor.Resolver(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
