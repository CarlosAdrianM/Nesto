Imports Prism.Modularity
Imports Nesto.Contratos
Imports Prism.RibbonRegionAdapter
Imports Prism.Ioc

Public Class Rapports
    Implements IModule, IRapports

    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        containerRegistry.Register(Of Object, ListaRapportsView)("ListaRapportsView")
        containerRegistry.Register(Of Object, RapportView)("RapportView")

    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of RapportsMenuBar)
        If Not IsNothing(view) Then
            Dim regionAdapter = containerProvider.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = containerProvider.Resolve(Of IMainWindow)
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "ListaRapports")

            region.Add(view, "MenuBar")
        End If
    End Sub
End Class
