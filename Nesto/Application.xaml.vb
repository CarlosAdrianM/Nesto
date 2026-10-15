Imports System.Globalization
Imports System.Windows.Markup
Imports Azure.Identity
Imports CommunityToolkit.Mvvm.Messaging
Imports ControlesUsuario
Imports ControlesUsuario.Dialogs
Imports ControlesUsuario.Services
Imports ControlesUsuario.ViewModels
Imports Nesto.Infrastructure.Composicion
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Services
Imports Nesto.Infrastructure.Shared
Imports Nesto.Modules.Producto
Imports Nesto.Modules.Producto.ViewModels
Imports Nesto.Modulos
Imports Nesto.Modulos.Cajas
Imports Nesto.Modulos.Cajas.Interfaces
Imports Nesto.Modulos.Cajas.Services
Imports Nesto.Modulos.Ganavisiones
Imports Nesto.Modulos.Ganavisiones.Interfaces
Imports Nesto.Modulos.Ganavisiones.Services
Imports Nesto.Modulos.OfertasCombinadas
Imports Nesto.Modulos.OfertasCombinadas.Interfaces
Imports Nesto.Modulos.OfertasCombinadas.Services
Imports Nesto.Modulos.CanalesExternos.Interfaces
Imports Nesto.Modulos.CanalesExternos.Services
Imports Nesto.Modulos.CarteraPagos
Imports Nesto.Modulos.Cliente
Imports Nesto.Modulos.Inventario
Imports Nesto.Modulos.PedidoVenta
Imports Nesto.Modulos.PlantillaVenta
Imports Nesto.Modulos.Producto.Views
Imports Nesto.Modulos.Rapports
Imports Nesto.ViewModels
Imports Prism
Imports Prism.Ioc
Imports Prism.Modularity
Imports Prism.Mvvm
Imports Prism.Regions
Imports Unity

Partial Public Class Application

    ' Nesto#490 (4C.4, tramo 3): piloto de la región propia (parámetro RegionesPropias). Lo usan RegisterTypes (la
    ' navegación) y CreateShell (la propiedad adjunta de las regiones y el gestor del shell), que tienen que ir por el mismo lado.
    Private _conmutadorRegiones As Nesto.Infrastructure.Navegacion.ConmutadorRegiones

    ' Application-level events, such as Startup, Exit, and DispatcherUnhandledException
    ' can be handled in this file.
    Protected Overrides Sub OnStartup(e As StartupEventArgs)
        FrameworkElement.LanguageProperty.OverrideMetadata(GetType(FrameworkElement), New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))
        ' Los Run/Span de los TextBlock son FrameworkCONTENTElement, no FrameworkElement: sin este
        ' override, un StringFormat=c dentro de un Run sale en dólares (en-US) aunque la línea de
        ' arriba esté puesta (el total de Cartera viva en Remesas; Comisiones lo parcheaba a mano).
        ' ⚠️ NO usar GetType(FrameworkContentElement): esa clase ya registra la propiedad con
        ' metadata propia y OverrideMetadata LANZA en el arranque ("PropertyMetadata ya está
        ' registrado") — fue el crash al abrir de la 1.10.14.1. Sobre el tipo DERIVADO TextElement
        ' (padre de Run/Span/Bold...) sí está permitido y los Run lo heredan por búsqueda de metadata.
        FrameworkContentElement.LanguageProperty.OverrideMetadata(GetType(Documents.TextElement), New FrameworkPropertyMetadata(XmlLanguage.GetLanguage(CultureInfo.CurrentCulture.IetfLanguageTag)))

        ' Manejadores globales: cualquier excepción no controlada se registra en ELMAH (vía NestoAPI)
        ' en lugar de cerrar la aplicación silenciosamente sin dejar rastro.
        AddHandler Me.DispatcherUnhandledException, AddressOf OnDispatcherUnhandledException
        AddHandler AppDomain.CurrentDomain.UnhandledException, AddressOf OnAppDomainUnhandledException
        AddHandler TaskScheduler.UnobservedTaskException, AddressOf OnUnobservedTaskException

        ' Carlos 29/09/26: Tab + escribir en las celdas con AutocompleteBehavior/CuentaContableBehavior (sin ratón)
        ControlesUsuario.Behaviors.EdicionCeldaConTeclado.Registrar()

        MyBase.OnStartup(e)
    End Sub

    Private Sub OnDispatcherUnhandledException(sender As Object, e As System.Windows.Threading.DispatcherUnhandledExceptionEventArgs)
        RegistrarError(e.Exception, "DispatcherUnhandledException")
        Try
            Dim unused = MessageBox.Show(
                "Se ha producido un error inesperado. Se ha registrado para su revisión." & vbCrLf & vbCrLf & e.Exception.Message,
                "Nesto", MessageBoxButton.OK, MessageBoxImage.Error)
        Catch
        End Try
        ' Evita que la aplicación se cierre por una excepción en el hilo de UI.
        e.Handled = True
    End Sub

    Private Sub OnAppDomainUnhandledException(sender As Object, e As UnhandledExceptionEventArgs)
        Dim ex = TryCast(e.ExceptionObject, Exception)
        If ex IsNot Nothing Then
            RegistrarError(ex, "AppDomain.UnhandledException")
        End If
    End Sub

    Private Sub OnUnobservedTaskException(sender As Object, e As System.Threading.Tasks.UnobservedTaskExceptionEventArgs)
        RegistrarError(e.Exception, "UnobservedTaskException")
        e.SetObserved()
    End Sub

    ' Envía el error al servidor de forma robusta. Se ejecuta en el threadpool para no capturar
    ' el SynchronizationContext de UI (evita deadlocks al esperar) y nunca propaga excepciones.
    Private Sub RegistrarError(ex As Exception, contexto As String)
        Try
            Dim servicio = Container?.Resolve(Of IServicioRegistroErrores)()
            If servicio IsNot Nothing Then
                Dim unused = Task.Run(Function() servicio.RegistrarErrorAsync(ex, contexto)).Wait(TimeSpan.FromSeconds(10))
            End If
        Catch
            ' Nunca propagar desde el manejador de errores.
        End Try
    End Sub

    Protected Overrides Sub RegisterTypes(containerRegistry As IContainerRegistry)
        ' Nesto#490 (4C.5): todo lo de la aplicación se registra en ComposicionNesto, sin tipos de Prism (la prueba sombra
        ' lo monta igual sobre Microsoft.Extensions.DependencyInjection). Aquí solo queda lo de las regiones y la
        ' navegación, que se va con el tramo 4 de 4C.4.
        ComposicionNesto.Registrar(New RegistroPrism(containerRegistry))

        ' Nesto#490 (4C.4): navegación propia. Tramo 3 (piloto): con el parámetro RegionesPropias = "1" va por la región
        ' propia (ServicioNavegacionNesto sobre el GestorRegiones del shell, las vistas creadas por su nombre con los mismos
        ' registros, y las navegaciones fallidas a la traza y a ELMAH); sin él, o si no se puede leer, por el IRegionManager
        ' de Prism como siempre. El parámetro se lee una vez, en CreateShell.
        _conmutadorRegiones = New Nesto.Infrastructure.Navegacion.ConmutadorRegiones(
            Function() Container.Resolve(Of IConfiguracion)().LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.RegionesPropias),
            Sub(ex, contexto) Container.Resolve(Of IServicioRegistroErrores)().RegistrarErrorAsync(ex, contexto))
        Dim conmutadorRegiones = _conmutadorRegiones
        Dim unusedNavegacion = containerRegistry.RegisterSingleton(Of IServicioNavegacion)(
            Function(provider) conmutadorRegiones.CrearNavegacion(
                Function() New Nesto.Infrastructure.Navegacion.ServicioNavegacionPrism(provider.Resolve(Of IRegionManager)()),
                Function(nombre) provider.Resolve(GetType(Object), nombre)))
        ' Nesto#490 (4C.4): el cargador de vistas de la navegación de Prism, salvo que nunca reutiliza las vistas de un
        ' IReceptorNavegacionPestanaNueva (lo que antes hacía IsNavigationTarget = False). Sustituye al que registra Prism.
        Dim unusedCargadorVistas = containerRegistry.RegisterSingleton(Of IRegionNavigationContentLoader, Nesto.Infrastructure.Navegacion.CargadorVistasNavegacion)()
    End Sub

    Protected Overrides Function CreateShell() As Window
        ' Nesto#490 (4C.4, tramo 3): la propiedad adjunta de las regiones tiene que saber si va el piloto antes de leer el XAML
        _conmutadorRegiones.AntesDeCrearShell()
        Dim shell As Window = CType(Container.Resolve(Of IMainWindow)(), Window)
        _conmutadorRegiones.AlCrearShell(shell)
        Return shell
    End Function

    Protected Overrides Sub ConfigureModuleCatalog(moduleCatalog As IModuleCatalog)
        MyBase.ConfigureModuleCatalog(moduleCatalog)
        ' Nesto#490 (4C.5): la lista de módulos (con sus fechas) está en ComposicionNesto, para que la prueba sombra cargue los mismos
        For Each tipoModulo In ComposicionNesto.Modulos
            Dim unused = moduleCatalog.AddModule(tipoModulo)
        Next
    End Sub

    ' Memory leak: al quitar una vista de cualquier región, limpiamos su DataContext para
    ' que WPF desmonte los BindingExpression que, si no, la mantienen viva (junto con su VM).
    ' Behavior global → aplica a todas las ventanas. Ver LimpiarVistaAlQuitarRegionBehavior.
    Protected Overrides Sub ConfigureDefaultRegionBehaviors(regionBehaviors As IRegionBehaviorFactory)
        MyBase.ConfigureDefaultRegionBehaviors(regionBehaviors)
        regionBehaviors.AddIfMissing(LimpiarVistaAlQuitarRegionBehavior.BehaviorKey, GetType(LimpiarVistaAlQuitarRegionBehavior))
        ' Nesto#490 (4C.4): la navegación llega a los ViewModels por IReceptorNavegacion, sin tipos de Prism
        regionBehaviors.AddIfMissing(Nesto.Infrastructure.Navegacion.ReceptorNavegacionRegionBehavior.BehaviorKey,
                                     GetType(Nesto.Infrastructure.Navegacion.ReceptorNavegacionRegionBehavior))
    End Sub

    Protected Overrides Sub OnInitialized()
        MyBase.OnInitialized()

        Dim authService = Container.Resolve(Of IServicioAutenticacion)()
        Dim unused = Task.Run(
            Async Function()
                Dim token = Await authService.ObtenerTokenWindowsAsync()
                If token IsNot Nothing Then
                    System.Diagnostics.Debug.WriteLine("Token obtenido exitosamente al iniciar la aplicación")
                End If
                Await IniciarAvisosEnTiempoReal()
            End Function)
    End Sub

    ' NestoAPI#536: conecta la campana a los avisos por SignalR, salvo que el parámetro AvisosTiempoReal sea "NO".
    ' Si no se puede leer el parámetro, se conecta igual (la conexión no molesta aunque falle: reintenta sola).
    Private Async Function IniciarAvisosEnTiempoReal() As Task
        Try
            Dim avisos = TryCast(Container.Resolve(Of IAvisosEnTiempoReal)(), AvisosEnTiempoRealSignalR)
            If avisos Is Nothing Then
                Return
            End If
            Dim valor As String = Nothing
            Try
                Dim cfg = Container.Resolve(Of IConfiguracion)()
                valor = Await cfg.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AvisosTiempoReal)
            Catch
                ' Sin parámetro legible se queda el valor por defecto (conectada).
            End Try
            If String.Equals(If(valor, String.Empty).Trim(), "NO", StringComparison.OrdinalIgnoreCase) Then
                Return
            End If
            avisos.Iniciar()
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"No se pudieron iniciar los avisos en tiempo real: {ex.Message}")
        End Try
    End Function

    Protected Overrides Sub OnExit(e As ExitEventArgs)
        Try
            TryCast(Container?.Resolve(Of IAvisosEnTiempoReal)(), AvisosEnTiempoRealSignalR)?.Detener()
        Catch
            ' Al cerrar, nada debe impedirlo.
        End Try
        MyBase.OnExit(e)
    End Sub
End Class
