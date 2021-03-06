﻿Imports System.Windows.Markup
Imports System.Globalization
Imports Prism.Ioc
Imports Prism.Modularity
Imports Nesto.Contratos
Imports Prism
Imports Prism.RibbonRegionAdapter
Imports Nesto.Modulos
Imports Nesto.Modulos.PlantillaVenta
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.CarteraPagos
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels
Imports Nesto.Modules.Producto
Imports Nesto.Modulos.Cliente
Imports Prism.Regions
Imports Unity
Imports Prism.Mvvm
Imports ControlesUsuario
Imports ControlesUsuario.Dialogs
Imports Microsoft.Identity.Client

Partial Public Class Application

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        FrameworkElement.LanguageProperty.OverrideMetadata(GetType(FrameworkElement), New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))
        MyBase.OnStartup(e)
    End Sub

    Protected Overrides Sub RegisterTypes(containerRegistry As IContainerRegistry)
        containerRegistry.RegisterSingleton(GetType(RibbonRegionAdapter), GetType(RibbonRegionAdapter))
        containerRegistry.RegisterSingleton(GetType(IMainWindow), GetType(MainWindow))
        containerRegistry.RegisterSingleton(GetType(IMenuBar), GetType(MenuBarView))
        containerRegistry.RegisterSingleton(GetType(IConfiguracion), GetType(Configuracion))

        Dim clientId = "d287e79a-5e01-4642-ac29-9b568dd39f67"
        Dim tenantId = "16d9b0cd-12c6-4639-8c26-779abc0dc0ad"
        Dim msGraphApp As IPublicClientApplication = PublicClientApplicationBuilder.Create(clientId).WithTenantId(tenantId).WithRedirectUri("https://login.microsoftonline.com/common/oauth2/nativeclient").Build()
        containerRegistry.RegisterInstance(GetType(IPublicClientApplication), msGraphApp)

        containerRegistry.Register(GetType(IPlantillaVenta), GetType(PlantillaVenta))
        containerRegistry.Register(GetType(IPlantillaVentaService), GetType(PlantillaVentaService))
        containerRegistry.Register(GetType(IInventario), GetType(Inventario))
        containerRegistry.Register(GetType(ICarteraPagosService), GetType(CarteraPagosService))
        containerRegistry.Register(GetType(ICarteraPagos), GetType(CarteraPagos))
        containerRegistry.Register(GetType(IPedidoVentaService), GetType(PedidoVentaService))
        containerRegistry.Register(GetType(IPedidoVenta), GetType(PedidoVenta))
        containerRegistry.Register(GetType(IRapportService), GetType(RapportService))
        containerRegistry.Register(GetType(IRapports), GetType(Rapports))
        containerRegistry.Register(GetType(ICanalesExternos), GetType(Nesto.Modulos.CanalesExternos.CanalesExternos))
        containerRegistry.Register(GetType(IAgenciaService), GetType(AgenciaService))
        containerRegistry.Register(GetType(IProducto), GetType(Nesto.Modulos.Producto.Producto))
        containerRegistry.Register(GetType(IProductoService), GetType(ProductoService))
        containerRegistry.Register(GetType(ICliente), GetType(Nesto.Modulos.Cliente.Cliente))
        containerRegistry.Register(GetType(IClienteService), GetType(ClienteService))

        containerRegistry.RegisterDialog(Of ConfirmationDialog, ConfirmationDialogViewModel)
        containerRegistry.RegisterDialog(Of NotificationDialog, NotificationDialogViewModel)
    End Sub

    Protected Overrides Function CreateShell() As Window
        Return Container.Resolve(Of IMainWindow)
    End Function

    Protected Overrides Sub ConfigureModuleCatalog(moduleCatalog As IModuleCatalog)
        MyBase.ConfigureModuleCatalog(moduleCatalog)
        ' Módulo que tiene los botones del Ribbon de todo lo viejo que no se hizo con módulos
        moduleCatalog.AddModule(GetType(IMenuBar)) ', InitializationMode.WhenAvailable

        '' Plantilla de Ventas - 17/07/15
        moduleCatalog.AddModule(GetType(IPlantillaVenta)) ', InitializationMode.WhenAvailable

        '' Inventarios - 09/11/15
        moduleCatalog.AddModule(GetType(IInventario)) ', InitializationMode.WhenAvailable

        '' Cartera de Pagos - 19/07/16
        moduleCatalog.AddModule(GetType(ICarteraPagos)) ', InitializationMode.WhenAvailable

        '' Pedido de Venta - 26/10/16
        moduleCatalog.AddModule(GetType(IPedidoVenta))

        '' Rapports - 14/03/17
        moduleCatalog.AddModule(GetType(IRapports))

        '' Canales Externos - 03/10/17
        moduleCatalog.AddModule(Of Nesto.Modulos.CanalesExternos.CanalesExternos)

        '' Producto - 29/05/18
        moduleCatalog.AddModule(GetType(IProducto))

        '' Cliente - 29/05/19
        moduleCatalog.AddModule(GetType(ICliente))
    End Sub

    Protected Overrides Sub ConfigureRegionAdapterMappings(regionAdapterMappings As Prism.Regions.RegionAdapterMappings)
        MyBase.ConfigureRegionAdapterMappings(regionAdapterMappings)
        regionAdapterMappings.RegisterMapping(GetType(RibbonRegionAdapter), Me.Container.Resolve(Of Prism.RibbonRegionAdapter.RibbonRegionAdapter)())
    End Sub

    Protected Overrides Sub ConfigureViewModelLocator()
        MyBase.ConfigureViewModelLocator()
        ViewModelLocationProvider.Register(GetType(Remesas).ToString, GetType(RemesasViewModel))
        ViewModelLocationProvider.Register(GetType(Alquileres).ToString, GetType(AlquileresViewModel))
        ViewModelLocationProvider.Register(GetType(CRInforme).ToString, GetType(CRInformeViewModel))
    End Sub
End Class
