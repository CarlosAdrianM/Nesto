Imports System.Windows
Imports Microsoft.Practices.Prism.Modularity
Imports Microsoft.Practices.Unity
Imports Microsoft.Practices.Prism.UnityExtensions
Imports Microsoft.Practices.Prism.Regions
Imports Prism.RibbonRegionAdapter.RibbonRegionAdapter
Imports Prism.RibbonRegionAdapter
Imports System.Windows.Controls.Primitives


Public Class Bootstrapper
    Inherits UnityBootstrapper

    Protected Overrides Function CreateShell() As DependencyObject
        Dim shell = Me.Container.Resolve(Of MainWindow)()

        ' Carlos 14/07/15: estas tres líneas no sé si valen para algo
        Dim regionManager = Me.Container.Resolve(Of RegionManager)()
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
        ' Carlos: aquí se añadirían los módulos
        moduleCatalog.AddModule(GetType(MenuBarView)) ', InitializationMode.WhenAvailable
    End Sub

    Protected Overrides Sub ConfigureContainer()
        MyBase.ConfigureContainer()
        RegisterTypeIfMissing(GetType(RibbonRegionAdapter), GetType(RibbonRegionAdapter), True)
        RegisterTypeIfMissing(GetType(MainWindow), GetType(MainWindow), True)
        'Me.Container.RegisterInstance(Of RibbonRegionAdapter
        'builder.RegisterAssemblyTypes(GetType(RibbonRegionAdapter).Assembly).InNamespaceOf(Of RibbonRegionAdapter)().AsSelf().SingleInstance()
    End Sub

    Protected Overrides Function ConfigureRegionAdapterMappings() As RegionAdapterMappings
        'Dim regionAdapterMappings As RegionAdapterMappings = Container.TryResolve(Of RegionAdapterMappings)()
        'Dim selector As SelectorRegionAdapter = Me.Container.Resolve(Of SelectorRegionAdapter)()
        'If regionAdapterMappings IsNot Nothing Then
        '    regionAdapterMappings.RegisterMapping(GetType(Selector), selector)
        '    regionAdapterMappings.RegisterMapping(GetType(ItemsControl), Me.Container.Resolve(Of ItemsControlRegionAdapter)())
        '    regionAdapterMappings.RegisterMapping(GetType(ContentControl), Me.Container.Resolve(Of ContentControlRegionAdapter)())
        'End If

        'Dim tipo As SelectorRegionAdapter = regionAdapterMappings.GetMapping(GetType(Selector))
        'Dim mappings = regionAdapterMappings 

        Dim mappings = MyBase.ConfigureRegionAdapterMappings()
        mappings.RegisterMapping(GetType(Ribbon), Me.Container.Resolve(Of Prism.RibbonRegionAdapter.RibbonRegionAdapter)())
        Return mappings
    End Function

End Class
