Imports Microsoft.Practices.Prism.Modularity
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Nesto.Contratos
Imports Prism.RibbonRegionAdapter

Public Class Rapports
    Implements IModule, IRapports

    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, viewModel As ListaRapportsViewModel)

        Me.container = container
        Me.regionManager = regionManager

    End Sub
    Public Sub Initialize() Implements IModule.Initialize
        container.RegisterType(Of Object, ListaRapportsView)("ListaRapportsView")
        container.RegisterType(Of Object, RapportView)("RapportView")

        Dim view = Me.container.Resolve(Of RapportsMenuBar)
        If Not IsNothing(view) Then
            Dim regionAdapter = Me.container.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = Me.container.Resolve(Of IMainWindow)
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "ListaRapports")

            region.Add(view, "MenuBar")
        End If
    End Sub
End Class
