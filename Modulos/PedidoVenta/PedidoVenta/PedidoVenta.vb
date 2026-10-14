Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Services
Imports Nesto.Infrastructure.Contracts
Imports Prism.Ioc
Imports Prism.Modularity

Public Class PedidoVenta
    Implements IModule, IPedidoVenta


    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        Dim unused3 = containerRegistry.Register(Of Object, PedidoVentaView)("PedidoVentaView")
        Dim unused2 = containerRegistry.Register(Of Object, DetallePedidoView)("DetallePedidoView")
        containerRegistry.RegistrarDialogo(Of PickingPopupView, PickingPopupViewModel)
        ' Sugerencia 575: pedidos que podrían ir por nuestra ruta, antes de sacar el picking
        containerRegistry.RegistrarDialogo(Of SugerenciasRutaPickingPopup, SugerenciasRutaPickingPopupViewModel)(SugerenciasRutaPickingPopupViewModel.NOMBRE)
        containerRegistry.RegistrarDialogo(Of FacturarRutasPopup, FacturarRutasPopupViewModel)
        containerRegistry.RegistrarDialogo(Of ErroresFacturacionRutasPopup, ErroresFacturacionRutasPopupViewModel)
        containerRegistry.RegistrarDialogo(Of CopiarFacturaView, CopiarFacturaViewModel)
        ' NestoAPI#352: pregunta el grupo de comisión de las líneas de inmovilizado al guardar
        containerRegistry.RegistrarDialogo(Of SelectorGrupoComisionDialog, SelectorGrupoComisionDialogViewModel)

        ' Registrar servicios del módulo
        Dim unused1 = containerRegistry.RegisterSingleton(Of Services.IServicioFacturacionRutas, Services.ServicioFacturacionRutas)
        Dim unused = containerRegistry.RegisterSingleton(Of IServicioImpresionDocumentos, ServicioImpresionDocumentos)

        ' Issue #263: Servicios de autocomplete
        containerRegistry.Register(Of ServicioBusquedaProductos)
        containerRegistry.Register(Of ServicioBusquedaCuentas)
    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of PedidoVentaMenuBar)
        If Not IsNothing(view) Then
            containerProvider.Resolve(Of IServicioCinta)().AnadirMenu(view)
        End If

    End Sub
End Class