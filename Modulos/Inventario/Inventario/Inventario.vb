Imports Microsoft.Practices.Prism.Modularity
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Nesto.Contratos
Imports Prism.RibbonRegionAdapter

Public Class Inventario
    Implements IModule, IInventario

    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, viewModel As InventarioViewModel)

        Me.container = container
        Me.regionManager = regionManager

    End Sub

    Public Sub Initialize() Implements IModule.Initialize
        container.RegisterType(Of Object, InventarioView)("InventarioView")

        Dim view = Me.container.Resolve(Of InventarioMenuBar)
        If Not IsNothing(view) Then
            'regionManager.RegisterViewWithRegion("MainMenu", GetType(PlantillaVentaView))
            'Dim regionAdapter As RibbonRegionAdapter
            'regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            'Dim regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            Dim regionAdapter = Me.container.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = Me.container.Resolve(Of IMainWindow)
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "Inventario")

            'Dim region = regionManager.Regions("MainMenu")
            region.Add(view, "MenuBar")
        End If
    End Sub
End Class
