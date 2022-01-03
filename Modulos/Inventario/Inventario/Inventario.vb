﻿Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts
Imports Prism.Ioc
Imports Prism.Modularity
Imports Prism.RibbonRegionAdapter

Public Class Inventario
    Implements IModule, IInventario

    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        containerRegistry.Register(Of Object, InventarioView)("InventarioView")
    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of InventarioMenuBar)
        If Not IsNothing(view) Then
            'regionManager.RegisterViewWithRegion("MainMenu", GetType(PlantillaVentaView))
            'Dim regionAdapter As RibbonRegionAdapter
            'regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            'Dim regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            Dim regionAdapter = containerProvider.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = containerProvider.Resolve(Of IMainWindow)
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "Inventario")

            'Dim region = regionManager.Regions("MainMenu")
            region.Add(view, "MenuBar")
        End If
    End Sub
End Class
