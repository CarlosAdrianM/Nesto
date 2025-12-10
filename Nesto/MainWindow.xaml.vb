Imports Prism.Regions
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports System.ComponentModel
Imports System.Windows.Threading

Partial Class MainWindow
    Implements IMainWindow
    Implements INotifyPropertyChanged

    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion
    Private ReadOnly tituloVentana As String
    Private _timerVerificacion As DispatcherTimer
    Public ReadOnly Property Maquina As String
    Public ReadOnly Property Delegacion As String
    Public ReadOnly Property Usuario As String
    Public ReadOnly Property TextoAdvertencia As String

    Private _estaAutenticado As Boolean = False
    ''' <summary>
    ''' Indica si el usuario está autenticado correctamente con la API.
    ''' Se actualiza periódicamente y cuando hay errores 401.
    ''' </summary>
    Public Property EstaAutenticado As Boolean
        Get
            Return _estaAutenticado
        End Get
        Set(value As Boolean)
            If _estaAutenticado <> value Then
                _estaAutenticado = value
                RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(EstaAutenticado)))
            End If
        End Set
    End Property

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion)

        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.regionManager = regionManager
        Me._servicioAutenticacion = servicioAutenticacion

        tituloVentana = "Nesto (" + GetType(MainWindow).Assembly.GetName().Version.ToString + ")"
        Title = tituloVentana

        Maquina = Environment.GetEnvironmentVariable("CLIENTNAME")
        Delegacion = configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto)
        Usuario = Environment.UserName
        Dim delegacionDeMaquina As String = Strings.Left(Maquina, 3)
        If Constantes.Sedes.ListaSedes.Select(Function(s) s.Codigo).Contains(delegacionDeMaquina) Then
            TextoAdvertencia = String.Empty
            If delegacionDeMaquina <> Delegacion Then
                Delegacion = delegacionDeMaquina
                configuracion.GuardarParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto, delegacionDeMaquina)
                configuracion.GuardarParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta, delegacionDeMaquina)
                configuracion.GuardarParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenInventario, delegacionDeMaquina)
            End If
        Else
            TextoAdvertencia = "¡Nombre de equipo no válido para parámetros automáticos!"
        End If

        ' Inicializar verificación de autenticación
        InicializarVerificacionAutenticacion()
    End Sub

    ''' <summary>
    ''' Configura el timer para verificar periódicamente el estado de autenticación.
    ''' </summary>
    Private Async Sub InicializarVerificacionAutenticacion()
        ' Obtener token proactivamente al iniciar
        If _servicioAutenticacion IsNot Nothing Then
            Await _servicioAutenticacion.ObtenerTokenValidoAsync()
        End If

        ' Verificar estado inicial
        VerificarEstadoAutenticacion()

        ' Configurar timer para verificar cada 30 segundos
        _timerVerificacion = New DispatcherTimer()
        _timerVerificacion.Interval = TimeSpan.FromSeconds(30)
        AddHandler _timerVerificacion.Tick, AddressOf OnTimerVerificacion
        _timerVerificacion.Start()
    End Sub

    Private Sub OnTimerVerificacion(sender As Object, e As EventArgs)
        VerificarEstadoAutenticacion()
    End Sub

    ''' <summary>
    ''' Verifica si hay un token válido y actualiza el indicador visual.
    ''' </summary>
    Public Sub VerificarEstadoAutenticacion()
        If _servicioAutenticacion IsNot Nothing Then
            EstaAutenticado = _servicioAutenticacion.TieneTokenValido()
        Else
            EstaAutenticado = False
        End If
    End Sub

    Public Property regionRibbon As Controls.Ribbon.Ribbon Implements IMainWindow.mainRibbon
        Get
            Return MainMenu
        End Get
        Set(value As Controls.Ribbon.Ribbon)
            MainMenu = value
        End Set
    End Property



End Class

