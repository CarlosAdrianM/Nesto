Imports Nesto.Infrastructure.Contracts
Imports Prism.Ioc
Imports Prism.Modularity
Imports Prism.RibbonRegionAdapter

Public Class PedidoVenta
    Implements IModule, IPedidoVenta


    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        Dim unused3 = containerRegistry.Register(Of Object, PedidoVentaView)("PedidoVentaView")
        Dim unused2 = containerRegistry.Register(Of Object, DetallePedidoView)("DetallePedidoView")
        containerRegistry.RegisterDialog(Of PickingPopupView, PickingPopupViewModel)
        containerRegistry.RegisterDialog(Of FacturarRutasPopup, FacturarRutasPopupViewModel)
        containerRegistry.RegisterDialog(Of ErroresFacturacionRutasPopup, ErroresFacturacionRutasPopupViewModel)

        ' Registrar servicios del módulo
        Dim unused1 = containerRegistry.RegisterSingleton(Of Services.IServicioFacturacionRutas, Services.ServicioFacturacionRutas)
        Dim unused = containerRegistry.RegisterSingleton(Of IServicioImpresionDocumentos, ServicioImpresionDocumentos)
    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of PedidoVentaMenuBar)
        If Not IsNothing(view) Then
            Dim regionAdapter = containerProvider.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = containerProvider.Resolve(Of IMainWindow)
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "PedidoVenta")

            Dim unused = region.Add(view, "MenuBar")
        End If

    End Sub
End Class