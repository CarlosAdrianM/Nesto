Imports Nesto.ViewModels
Imports ControlesUsuario.Dialogs
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared

Public Class MenuBarView
    Implements IModuloNesto, IMenuBar

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
        ' Nesto#490 (4C.5, tramo 3c): sin IUnityContainer. Las ventanas de VentanasPorNombre se crean con el resolutor propio
        ' (el mismo que recibe el módulo) y Novedades y Parámetros llegan por sus interfaces.
        Dim navegacion = resolutor.Resolver(Of IServicioNavegacion)()
        Dim configuracion = resolutor.Resolver(Of IConfiguracion)()
        Dim servicioAutenticacion = resolutor.Resolver(Of IServicioAutenticacion)()
        Dim abridorNovedades = resolutor.Resolver(Of IAbridorNovedades)()
        Dim servicioDialogos = resolutor.Resolver(Of IServicioDialogos)()

        Dim viewModel As New MenuBarViewModel(resolutor, navegacion, configuracion, servicioAutenticacion, abridorNovedades, servicioDialogos)
        For Each ventana In VentanasPorNombre
            viewModel.RegistrarTipoVista(ventana.Key, ventana.Value)
        Next

        Me.DataContext = viewModel

        resolutor.Resolver(Of IServicioCinta)().AnadirMenu(Me)
    End Sub
End Class
