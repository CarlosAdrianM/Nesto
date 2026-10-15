Imports Nesto.ViewModels
Imports Prism.Modularity
Imports Prism.Ioc
Imports Unity
Imports Nesto.Infrastructure.Composicion
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared

<[Module](ModuleName:="MenuBarView")>
Public Class MenuBarView
    Implements IModule, IModuloNesto, IMenuBar

    ''' <summary>
    ''' Las ventanas que abre MenuBarViewModel en una pestaña nueva, por el nombre con el que las pide. Se crean con el
    ''' contenedor por su tipo, así que (Nesto#490, 4C.5) se registran en <see cref="Registrar"/>: MS.DI no construye
    ''' clases sin registrar, como hacía Unity.
    ''' </summary>
    Public Shared ReadOnly Property VentanasPorNombre As IReadOnlyDictionary(Of String, Type) = New Dictionary(Of String, Type) From {
        {"Clientes", GetType(Clientes)},
        {"Alquileres", GetType(Alquileres)},
        {"Remesas", GetType(Remesas)},
        {"Agencias", GetType(Agencias)},
        {"AgenciasMantenimiento", GetType(AgenciasMantenimiento)},
        {"FamiliasMantenimiento", GetType(FamiliasMantenimiento)},
        {"Comisiones", GetType(Comisiones)},
        {"ClienteComercial", GetType(ClienteComercial)},
        {"PlanesVentajas", GetType(PlanesVentajas)}
    }

    ' Nesto#490 (4C.5): Prism sigue cargando el módulo por IModule, que delega en IModuloNesto
    Private Sub RegisterTypes(containerRegistry As IContainerRegistry) Implements IModule.RegisterTypes
        Registrar(New RegistroPrism(containerRegistry))
    End Sub

    Private Sub OnInitialized(containerProvider As IContainerProvider) Implements IModule.OnInitialized
        Inicializar(New ResolutorPrism(containerProvider))
    End Sub

    Public Sub Registrar(registro As IRegistroNesto) Implements IModuloNesto.Registrar
        For Each tipoVentana In VentanasPorNombre.Values
            registro.Transitorio(tipoVentana, tipoVentana)
        Next
        ' Y los ViewModels que reciben esas ventanas por constructor
        registro.Transitorio(Of ClientesViewModel)()
        registro.Transitorio(Of AlquileresViewModel)()
        registro.Transitorio(Of RemesasViewModel)()
        registro.Transitorio(Of AgenciasViewModel)()
        registro.Transitorio(Of AgenciasMantenimientoViewModel)()
        registro.Transitorio(Of FamiliasMantenimientoViewModel)()
        registro.Transitorio(Of ComisionesViewModel)()
        registro.Transitorio(Of PlanesVentajasViewModel)()
    End Sub

    Public Sub Inicializar(resolutor As IResolutorNesto) Implements IModuloNesto.Inicializar
        ' MenuBarViewModel todavía recibe el IUnityContainer (se va en el tramo 3 de 4C.5)
        Dim container = resolutor.Resolver(Of IUnityContainer)()
        Dim navegacion = resolutor.Resolver(Of IServicioNavegacion)()
        Dim configuracion = resolutor.Resolver(Of IConfiguracion)()
        Dim servicioAutenticacion = resolutor.Resolver(Of IServicioAutenticacion)()

        Dim viewModel As New MenuBarViewModel(container, navegacion, configuracion, servicioAutenticacion)
        For Each ventana In VentanasPorNombre
            viewModel.RegistrarTipoVista(ventana.Key, ventana.Value)
        Next

        Me.DataContext = viewModel

        resolutor.Resolver(Of IServicioCinta)().AnadirMenu(Me)
    End Sub
End Class
