Imports Microsoft.Practices.Prism.Modularity
Imports Microsoft.Practices.Prism.UnityExtensions
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Prism.RibbonRegionAdapter
Imports Nesto.Contratos
Imports Nesto.Modulos.PlantillaVenta
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.CarteraPagos
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels
Imports Nesto.Modules.Producto

Public Class Bootstrapper
    Inherits UnityBootstrapper


    Protected Overrides Function CreateShell() As DependencyObject
        Dim shell = Container.Resolve(Of IMainWindow)()

        ' Carlos 14/07/15: estas tres líneas no sé si valen para algo
        'Dim regionManager = Me.Container.Resolve(Of IRegionManager)()
        'Microsoft.Practices.Prism.Regions.RegionManager.SetRegionManager(shell, regionManager)
        'Microsoft.Practices.Prism.Regions.RegionManager.UpdateRegions()



        Return shell
    End Function

    Protected Overrides Sub InitializeShell()
        MyBase.InitializeShell()
        Application.Current.MainWindow = CType(Me.Shell, Window)
        Application.Current.MainWindow.Show()
    End Sub

    Protected Overrides Sub ConfigureModuleCatalog()
        MyBase.ConfigureModuleCatalog()
        Dim moduleCatalog As ModuleCatalog = CType(Me.ModuleCatalog, ModuleCatalog)

        ' Módulo que tiene los botones del Ribbon de todo lo viejo que no se hizo con módulos
        moduleCatalog.AddModule(GetType(IMenuBar)) ', InitializationMode.WhenAvailable

        ' Plantilla de Ventas - 17/07/15
        moduleCatalog.AddModule(GetType(IPlantillaVenta)) ', InitializationMode.WhenAvailable

        ' Inventarios - 09/11/15
        moduleCatalog.AddModule(GetType(IInventario)) ', InitializationMode.WhenAvailable

        ' Cartera de Pagos - 19/07/16
        moduleCatalog.AddModule(GetType(ICarteraPagos)) ', InitializationMode.WhenAvailable

        ' Pedido de Venta - 26/10/16
        moduleCatalog.AddModule(GetType(IPedidoVenta))

        ' Rapports - 14/03/17
        moduleCatalog.AddModule(GetType(IRapports))

        ' Canales Externos - 03/10/17
        moduleCatalog.AddModule(GetType(ICanalesExternos))

        ' Producto - 29/05/18
        moduleCatalog.AddModule(GetType(IProducto))

        ' Cliente - 29/05/19
        moduleCatalog.AddModule(GetType(ICliente))
    End Sub

    Protected Overrides Sub ConfigureContainer()
        MyBase.ConfigureContainer()
        RegisterTypeIfMissing(GetType(RibbonRegionAdapter), GetType(RibbonRegionAdapter), True)
        RegisterTypeIfMissing(GetType(IMainWindow), GetType(MainWindow), True)
        RegisterTypeIfMissing(GetType(IMenuBar), GetType(MenuBarView), True)
        RegisterTypeIfMissing(GetType(IConfiguracion), GetType(Configuracion), True)
        RegisterTypeIfMissing(GetType(IPlantillaVenta), GetType(PlantillaVenta), False)
        RegisterTypeIfMissing(GetType(IInventario), GetType(Inventario), False)
        RegisterTypeIfMissing(GetType(ICarteraPagosService), GetType(CarteraPagosService), False)
        RegisterTypeIfMissing(GetType(ICarteraPagos), GetType(CarteraPagos), False)
        RegisterTypeIfMissing(GetType(IPedidoVentaService), GetType(PedidoVentaService), False)
        RegisterTypeIfMissing(GetType(IPedidoVenta), GetType(PedidoVenta), False)
        RegisterTypeIfMissing(GetType(IRapportService), GetType(RapportService), False)
        RegisterTypeIfMissing(GetType(IRapports), GetType(Rapports), False)
        RegisterTypeIfMissing(GetType(ICanalesExternos), GetType(Nesto.Modulos.CanalesExternos.CanalesExternos), False)
        RegisterTypeIfMissing(GetType(IAgenciaService), GetType(AgenciaService), False)
        RegisterTypeIfMissing(GetType(IProducto), GetType(Nesto.Modulos.Producto.Producto), False)
        RegisterTypeIfMissing(GetType(IProductoService), GetType(ProductoService), False)
        RegisterTypeIfMissing(GetType(ICliente), GetType(Nesto.Modulos.Cliente.Cliente), False)
    End Sub

    Protected Overrides Function ConfigureRegionAdapterMappings() As RegionAdapterMappings
        Dim mappings As RegionAdapterMappings = MyBase.ConfigureRegionAdapterMappings()
        mappings.RegisterMapping(GetType(RibbonRegionAdapter), Me.Container.Resolve(Of Prism.RibbonRegionAdapter.RibbonRegionAdapter)())
        Return mappings
    End Function

End Class
