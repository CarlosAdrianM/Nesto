Imports System.Globalization
Imports System.Windows.Markup
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared

Partial Public Class Application

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

        ' PrismApplication también llamaba primero a la base (el evento Startup) y después arrancaba
        MyBase.OnStartup(e)

        Arrancar()
    End Sub

    ''' <summary>
    ''' Nesto#490 (4C.5, tramo 4): el arranque sin PrismApplication, en el mismo orden que PrismApplication.Initialize +
    ''' OnInitialized: el contenedor (MS.DI, validado al construirlo), el shell, los módulos, mostrar el shell y, por
    ''' último, el token y los avisos en tiempo real. Los pasos están en ArranqueNesto, que también usa la prueba de
    ''' arranque sombra.
    ''' </summary>
    Private Sub Arrancar()
        Dim arranque = ArranqueNesto.Componer()
        ' Antes de crear nada: el shell, los menús y los controles creados desde XAML ya lo usan (con Prism quedaba listo
        ' en RegisterTypes, antes del shell y de los módulos)
        Contenedor.Establecer(arranque.Resolutor)

        Dim shell = arranque.CrearShell()
        ' Lo que hacía PrismApplication.InitializeShell: la ventana principal es el shell aunque se haya creado otra antes
        MainWindow = shell
        arranque.InicializarModulos()
        ' Nesto#490 (panel lateral): con el piloto NavegacionLateral, el menú lateral en lugar de la cinta
        arranque.MontarNavegacion(shell)
        ' Lo que hacía PrismApplication.OnInitialized: mostrarlo cuando los módulos ya han puesto su menú en la cinta
        shell.Show()

        IniciarEscanerRemoto()
        ObtenerTokenEIniciarAvisos()
        IniciarGlobos()
    End Sub

    ''' <summary>
    ''' Nesto#592 (NestoAPI#774): los globos («consejos») de lo nuevo. Una sola pieza para todas las vistas: al cargarse una, pide los
    ''' suyos (una vez por sesión) y los enseña junto a su control (Globo.Ancla) o centrados. Si algo falla, no hay globos.
    ''' </summary>
    Private Sub IniciarGlobos()
        Try
            ControlesUsuario.Globos.PresentadorGlobos.Iniciar(Contenedor.Actual.Resolver(Of IServicioGlobos)())
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"No se pudieron iniciar los globos: {ex.Message}")
        End Try
    End Sub

    ' Nesto#589 (NestoAPI#768): el receptor de lo leído con Ariadna («Escanear para Nesto»). Vive toda la aplicación.
    Private _receptorEscanerRemoto As ControlesUsuario.EscanerRemoto.ReceptorEscanerRemoto

    ''' <summary>
    ''' Nesto#589 (NestoAPI#768, Carlos 30/10/26): escáner remoto. Lo leído con Ariadna por el mismo usuario llega por la conexión
    ''' de la campana (AvisosHub) y se escribe en el campo con el foco si acepta códigos de barras (si no, aviso «Último código
    ''' leído»). Cada vez que Nesto pasa a primer plano se lo dice a la API: con varios Nesto abiertos del mismo usuario, el código
    ''' va al último que tuvo el foco. Sin temporizadores.
    ''' </summary>
    Private Sub IniciarEscanerRemoto()
        Try
            Dim escaner = TryCast(Contenedor.Actual.Resolver(Of IAvisosEnTiempoReal)(), IEscanerRemoto)
            If escaner Is Nothing Then
                Return
            End If
            _receptorEscanerRemoto = New ControlesUsuario.EscanerRemoto.ReceptorEscanerRemoto(escaner, Dispatcher)
            AddHandler Activated, Sub(s, a) escaner.NotificarActividad(True)
            AddHandler Deactivated, Sub(s, a) escaner.NotificarActividad(False)
            escaner.NotificarActividad(Windows.Cast(Of Window)().Any(Function(w) w.IsActive))
        Catch ex As Exception
            System.Diagnostics.Debug.WriteLine($"No se pudo iniciar el escáner remoto: {ex.Message}")
        End Try
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
            ' Nesto#490 (4C.5, tramo 4): Contenedor.Actual en lugar del Container de PrismApplication. Es null si el error
            ' llega antes de construir el contenedor, igual que el Container de Prism: entonces no se registra.
            Dim servicio = Contenedor.Actual?.Resolver(Of IServicioRegistroErrores)()
            If servicio IsNot Nothing Then
                Dim unused = Task.Run(Function() servicio.RegistrarErrorAsync(ex, contexto)).Wait(TimeSpan.FromSeconds(10))
            End If
        Catch
            ' Nunca propagar desde el manejador de errores.
        End Try
    End Sub

    ' Lo que se hacía en OnInitialized con Prism, ya con el shell en pantalla: el token de Windows y, después, la campana
    ' en tiempo real
    Private Sub ObtenerTokenEIniciarAvisos()
        Dim authService = Contenedor.Actual.Resolver(Of IServicioAutenticacion)()
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
            Dim avisos = TryCast(Contenedor.Actual.Resolver(Of IAvisosEnTiempoReal)(), AvisosEnTiempoRealSignalR)
            If avisos Is Nothing Then
                Return
            End If
            Dim valor As String = Nothing
            Try
                Dim cfg = Contenedor.Actual.Resolver(Of IConfiguracion)()
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
            TryCast(Contenedor.Actual?.Resolver(Of IAvisosEnTiempoReal)(), AvisosEnTiempoRealSignalR)?.Detener()
        Catch
            ' Al cerrar, nada debe impedirlo.
        End Try
        MyBase.OnExit(e)
    End Sub
End Class
