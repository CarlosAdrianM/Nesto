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

    ' Modelo (0.25) -> UI ("25,00 %")
    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If value Is Nothing Then Return "0,00 %"
        Dim fraction As Decimal
        If Decimal.TryParse(value.ToString(), fraction) Then
            Return fraction.ToString("P2", culture)
        End If
        Return "0,00 %"
    End Function

    ' UI ("30" o "30 %" o "30,00 %") -> Modelo (0.30)
    ' El usuario escribe valores entre 0 y 100, internamente se guarda entre 0 y 1
    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        If value Is Nothing OrElse String.IsNullOrWhiteSpace(value.ToString()) Then
            Return 0D
        End If

        Dim valueString = value.ToString().Trim()

        ' Quitar el símbolo de porcentaje si existe
        valueString = valueString.Replace("%", "").Trim()

        Dim parsedValue As Decimal
        If Not Decimal.TryParse(valueString, NumberStyles.Any, culture, parsedValue) Then
            ' Si falla con la cultura actual, intentar con cultura invariante
            If Not Decimal.TryParse(valueString, NumberStyles.Any, CultureInfo.InvariantCulture, parsedValue) Then
                Return 0D
            End If
        End If

        ' El usuario escribe 30 para representar 30%, internamente guardamos 0.30
        Return parsedValue / 100D
    End Function

End Class

