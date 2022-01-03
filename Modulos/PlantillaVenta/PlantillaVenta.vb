Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts
Imports Prism.Ioc
Imports Prism.Modularity
Imports Prism.RibbonRegionAdapter
Imports System.Globalization

Public Class PlantillaVenta
    Implements IModule, IPlantillaVenta

    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        containerRegistry.Register(Of Object, PlantillaVentaView)("PlantillaVentaView")
    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of PlantillaVentaMenuBar)
        If Not IsNothing(view) Then
            'regionManager.RegisterViewWithRegion("MainMenu", GetType(PlantillaVentaView))
            'Dim regionAdapter As RibbonRegionAdapter
            'regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            'Dim regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            Dim regionAdapter = containerProvider.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = containerProvider.Resolve(Of IMainWindow)
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "PlantillaVenta")

            'Dim region = regionManager.Regions("MainMenu")
            region.Add(view, "MenuBar")
        End If

    End Sub
End Class


Public Class PercentageConverter
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        Dim fraction = Decimal.Parse(value.ToString())
        Return fraction.ToString("P2")
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Dim valueWithoutPercentage = value.ToString().TrimEnd(" ", "%")
        Return Decimal.Parse(valueWithoutPercentage) / 100
    End Function

    ''E.g. DB 0.042367 --> UI "4.24 %"
    'Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object


    'End Function

    ''E.g. UI "4.2367 %" --> DB 0.042367
    'Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object
    '    'Trim any trailing percentage symbol that the user MAY have included

    'End Function

End Class

