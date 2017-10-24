Imports Microsoft.Practices.Prism.Modularity
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Nesto.Contratos
Imports Prism.RibbonRegionAdapter

Public Class CarteraPagos
    Implements IModule, ICarteraPagos

    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, viewModel As CarteraPagosViewModel)

        Me.container = container
        Me.regionManager = regionManager

    End Sub

    Public Sub Initialize() Implements IModule.Initialize
        container.RegisterType(Of Object, CarteraPagosView)("CarteraPagosView")

        Dim view = Me.container.Resolve(Of CarteraPagosMenuBar)
        If Not IsNothing(view) Then
            'regionManager.RegisterViewWithRegion("MainMenu", GetType(PlantillaVentaView))
            'Dim regionAdapter As RibbonRegionAdapter
            'regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            'Dim regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            Dim regionAdapter = Me.container.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = Me.container.Resolve(Of IMainWindow)
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "CarteraPagos")

            'Dim region = regionManager.Regions("MainMenu")
            region.Add(view, "MenuBar")
        End If
    End Sub
End Class
