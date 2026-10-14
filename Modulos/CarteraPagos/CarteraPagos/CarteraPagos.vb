Imports Prism.Modularity
Imports Nesto.Contratos
Imports Prism.Ioc
Imports Nesto.Infrastructure.Contracts

Public Class CarteraPagos
    Implements IModule, ICarteraPagos

    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        containerRegistry.Register(Of Object, CarteraPagosView)("CarteraPagosView")
    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of CarteraPagosMenuBar)
        If Not IsNothing(view) Then
            containerProvider.Resolve(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
