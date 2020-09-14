Imports System.Globalization
Imports Prism.Modularity
Imports Prism.Regions
Imports Prism.Ioc
Imports Nesto.Contratos
Imports Prism.RibbonRegionAdapter

Public Class PedidoVenta
    Implements IModule, IPedidoVenta


    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        containerRegistry.Register(Of Object, PedidoVentaView)("PedidoVentaView")
        containerRegistry.Register(Of Object, DetallePedidoView)("DetallePedidoView")
    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of PedidoVentaMenuBar)
        If Not IsNothing(view) Then
            Dim regionAdapter = containerProvider.Resolve(Of RibbonRegionAdapter)()
            Dim mainWindow = containerProvider.Resolve(Of IMainWindow)
            Dim region = regionAdapter.Initialize(mainWindow.mainRibbon, "PedidoVenta")

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
End Class