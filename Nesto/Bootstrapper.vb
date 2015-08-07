'Imports System.Windows
Imports Microsoft.Practices.Prism.Modularity
Imports Microsoft.Practices.Unity
Imports Microsoft.Practices.Prism.UnityExtensions
Imports Microsoft.Practices.Prism.Regions
'Imports Prism.RibbonRegionAdapter.RibbonRegionAdapter
Imports Prism.RibbonRegionAdapter
Imports Nesto.Contratos
Imports Nesto.Modulos.PlantillaVenta
'Imports System.Windows.Controls.Primitives
'Imports Nesto.Modulos.PlantillaVenta



Public Class Bootstrapper
    Inherits UnityBootstrapper


    Protected Overrides Function CreateShell() As DependencyObject
        Dim shell = Me.Container.Resolve(Of IMainWindow)()

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


    End Sub

    Protected Overrides Sub ConfigureContainer()
        MyBase.ConfigureContainer()
        RegisterTypeIfMissing(GetType(RibbonRegionAdapter), GetType(RibbonRegionAdapter), True)
        RegisterTypeIfMissing(GetType(IMainWindow), GetType(MainWindow), True)
        RegisterTypeIfMissing(GetType(IMenuBar), GetType(MenuBarView), True)
        RegisterTypeIfMissing(GetType(IPlantillaVenta), GetType(PlantillaVenta), False)
        'Me.Container.RegisterType(GetType(MainWindow), GetType(MainWindow), "MainWindow")
        'Me.Container.RegisterType(Of Object, MenuBarView)("MenuBarView", New TransientLifetimeManager())
    End Sub

    Protected Overrides Function ConfigureRegionAdapterMappings() As RegionAdapterMappings
        Dim mappings As RegionAdapterMappings = MyBase.ConfigureRegionAdapterMappings()
        mappings.RegisterMapping(GetType(Ribbon), Me.Container.Resolve(Of Prism.RibbonRegionAdapter.RibbonRegionAdapter)())
        Return mappings
    End Function

End Class
