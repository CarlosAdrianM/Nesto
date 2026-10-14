Imports Nesto.ViewModels
Imports Prism.Modularity
Imports Prism.Ioc
Imports Prism.Unity
Imports Unity
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared

<[Module](ModuleName:="MenuBarView")>
Public Class MenuBarView
    Implements IModule, IMenuBar

    Public Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes

    End Sub

    Public Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Dim container = containerProvider.GetContainer()
        Dim navegacion = container.Resolve(Of IServicioNavegacion)()
        Dim configuracion = container.Resolve(Of Configuracion)
        Dim servicioAutenticacion = container.Resolve(Of IServicioAutenticacion)()

        Dim viewModel As New MenuBarViewModel(container, navegacion, configuracion, servicioAutenticacion)
        viewModel.RegistrarTipoVista("Clientes", GetType(Clientes))
        viewModel.RegistrarTipoVista("Alquileres", GetType(Alquileres))
        viewModel.RegistrarTipoVista("Remesas", GetType(Remesas))
        viewModel.RegistrarTipoVista("Agencias", GetType(Agencias))
        viewModel.RegistrarTipoVista("AgenciasMantenimiento", GetType(AgenciasMantenimiento))
        viewModel.RegistrarTipoVista("FamiliasMantenimiento", GetType(FamiliasMantenimiento))
        viewModel.RegistrarTipoVista("Comisiones", GetType(Comisiones))
        viewModel.RegistrarTipoVista("ClienteComercial", GetType(ClienteComercial))
        viewModel.RegistrarTipoVista("PlanesVentajas", GetType(PlanesVentajas))

        Me.DataContext = viewModel

        Dim view = Me
        If Not IsNothing(view) Then
            containerProvider.Resolve(Of IServicioCinta)().AnadirMenu(view)
        End If
    End Sub
End Class
