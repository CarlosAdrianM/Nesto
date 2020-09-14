Imports System.Windows.Markup
Imports System.Globalization
Imports Prism.Unity
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
Imports CommonServiceLocator

Partial Public Class Application
    Inherits PrismApplication
    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        FrameworkElement.LanguageProperty.OverrideMetadata(GetType(FrameworkElement), New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))
        MyBase.OnStartup(e)
        'Dim bootstrapper As Bootstrapper = New Bootstrapper
        'Bootstrapper.Run()
    End Sub

    Protected Overrides Sub RegisterTypes(containerRegistry As IContainerRegistry)
        containerRegistry.RegisterSingleton(GetType(RibbonRegionAdapter), GetType(RibbonRegionAdapter))
        containerRegistry.RegisterSingleton(GetType(IMainWindow), GetType(MainWindow))
        containerRegistry.RegisterSingleton(GetType(IMenuBar), GetType(MenuBarView))
        containerRegistry.RegisterSingleton(GetType(IConfiguracion), GetType(Configuracion))
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
        'Dim fluentRibbonRegionAdapter = ServiceLocator.Current.GetInstance(Of RibbonRegionAdapter)()
        'regionAdapterMappings.RegisterMapping(GetType(RibbonRegionAdapter), fluentRibbonRegionAdapter)

        'Dim sl = Container.Resolve(Of IServiceLocator)
        'Dim rbf = New RegionBehaviorFactory(sl)
        'Dim resultado = New RibbonRegionAdapter(rbf)
        'regionAdapterMappings.RegisterMapping(GetType(RibbonRegionAdapter), resultado)

        regionAdapterMappings.RegisterMapping(GetType(RibbonRegionAdapter), Me.Container.Resolve(Of Prism.RibbonRegionAdapter.RibbonRegionAdapter)())
    End Sub
End Class
