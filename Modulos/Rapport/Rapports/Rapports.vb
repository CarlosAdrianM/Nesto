Imports ControlesUsuario.Dialogs
Imports Prism.Modularity
Imports Nesto.Contratos
Imports Prism.Ioc
Imports Nesto.Infrastructure.Contracts

Public Class Rapports
    Implements IModule, IRapports

    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        containerRegistry.Register(Of Object, ListaRapportsView)("ListaRapportsView")
        containerRegistry.Register(Of Object, RapportView)("RapportView")
        containerRegistry.RegistrarDialogo(Of CopiarSeguimientosView, CopiarSeguimientosViewModel)
    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim view = containerProvider.Resolve(Of RapportsMenuBar)
        If Not IsNothing(view) Then
            containerProvider.Resolve(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
