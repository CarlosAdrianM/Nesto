Imports Nesto.Contratos
Imports Microsoft.Practices.Prism.Modularity
Imports Microsoft.Practices.Prism.Regions
Imports Microsoft.Practices.Unity
Imports Prism.RibbonRegionAdapter
Imports System.Globalization

Public Class PlantillaVenta
    Implements IModule, IPlantillaVenta

    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager, viewModel As PlantillaVentaViewModel)

        Me.container = container
        Me.regionManager = regionManager

    End Sub

    Public Sub Initialize() Implements IModule.Initialize
        container.RegisterType(Of Object, PlantillaVentaView)("PlantillaVentaView")

        Dim view = Me.container.Resolve(Of PlantillaVentaMenuBar)
        If Not IsNothing(view) Then
            'regionManager.RegisterViewWithRegion("MainMenu", GetType(PlantillaVentaView))
            'Dim regionAdapter As RibbonRegionAdapter
            'regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            'Dim regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
            Dim regionAdapter = Me.container.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = Me.container.Resolve(Of IMainWindow)
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
