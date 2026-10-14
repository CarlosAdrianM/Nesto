Imports Nesto.Contratos
Imports Nesto.Infrastructure.Contracts
Imports Prism.Ioc
Imports Prism.Modularity

Public Class PlantillaVenta
    Implements IModule, IPlantillaVenta

    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        containerRegistry.Register(Of Object, PlantillaVentaView)("PlantillaVentaView")
    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of PlantillaVentaMenuBar)
        If Not IsNothing(view) Then
            containerProvider.Resolve(Of IServicioCinta)().AnadirMenu(view)
        End If

    End Sub
End Class
