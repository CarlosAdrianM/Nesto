Imports Prism.Ioc
Imports Prism.Regions
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared
Imports System.ComponentModel
Imports System.Windows.Threading
Imports Microsoft.Win32

Partial Class MainWindow
    Implements IMainWindow
    Implements INotifyPropertyChanged

    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly _servicioAutenticacion As IServicioAutenticacion
    Private ReadOnly tituloVentana As String
    Private _configuracion As IConfiguracion
    Private ReadOnly _novedadesService As INovedadesService
    Private ReadOnly _dialogService As Prism.Services.Dialogs.IDialogService
    Private ReadOnly _versionActual As String
    Public Property Maquina As String
    Public Property Delegacion As String
    Public ReadOnly Property Usuario As String
    Public Property TextoAdvertencia As String

    ''' <summary>
    ''' Nesto#492: estado de la conexión con el servidor que pinta la raya bajo el nombre del usuario
    ''' (token válido + conexión en tiempo real). Se actualiza por eventos, sin sondeos.
    ''' </summary>
    Public ReadOnly Property IndicadorConexion As IndicadorConexionServidor

    Public Event PropertyChanged As PropertyChangedEventHandler Implements INotifyPropertyChanged.PropertyChanged

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion, servicioAutenticacion As IServicioAutenticacion,
                   novedadesService As INovedadesService, dialogService As Prism.Services.Dialogs.IDialogService,
                   campanaNotificaciones As ControlesUsuario.Notificaciones.CampanaNotificacionesViewModel,
                   avisosEnTiempoReal As IAvisosEnTiempoReal)

        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        ' Nesto#477: la campana arranca su refresco al cargarse y se refresca al recuperar el foco
        ' (ella misma limita esta vía a un refresco cada pocos minutos)
        Campana.DataContext = campanaNotificaciones
        AddHandler Me.Activated, Sub(s, e)
                                     Dim unusedRefresco = campanaNotificaciones.AlActivarseLaVentana()
                                 End Sub

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.regionManager = regionManager
        Me._servicioAutenticacion = servicioAutenticacion
        Me._configuracion = configuracion
        Me._novedadesService = novedadesService
        Me._dialogService = dialogService

        Dim clickOnceVersion As String = Environment.GetEnvironmentVariable("ClickOnce_CurrentVersion")
        Dim version As String = If(String.IsNullOrEmpty(clickOnceVersion),
            GetType(MainWindow).Assembly.GetName().Version.ToString,
            clickOnceVersion)
        _versionActual = version
        tituloVentana = "Nesto (" + version + ")"
        Title = tituloVentana

        Usuario = Environment.UserName
        ActualizarMaquinaYDelegacion()

        AddHandler SystemEvents.SessionSwitch, AddressOf OnSessionSwitch

        ' Nesto#492: la raya de conexión con el servidor (antes, un temporizador cada 30 s)
        IndicadorConexion = New IndicadorConexionServidor(servicioAutenticacion, avisosEnTiempoReal,
                                                          Sub(accion) Dispatcher.BeginInvoke(accion))
        IndicadorConexion.Iniciar()
        AddHandler Me.Closed, Sub(s, e) IndicadorConexion.Dispose()
        ObtenerTokenAlArrancar()

        ' Nesto#372: mostrar las novedades la primera vez que se arranca tras actualizar
        AddHandler Me.Loaded, AddressOf OnMainWindowLoadedComprobarNovedades
    End Sub

    ' Nesto#372: si la versión actual es posterior a la última cuyas novedades vio el usuario,
    ' se muestran las novedades nuevas y se guarda la versión. La primera vez (sin parámetro
    ' guardado) es un bootstrap silencioso. Nunca debe bloquear ni romper el arranque.
    Private Async Sub OnMainWindowLoadedComprobarNovedades(sender As Object, e As RoutedEventArgs)
        RemoveHandler Me.Loaded, AddressOf OnMainWindowLoadedComprobarNovedades
        Try
            ' leerParametro lanza si el parámetro no existe todavía (ni siquiera para el usuario
            ' "(defecto)"): se trata como "sin versión vista" para que el GuardarParametro de más
            ' abajo lo cree y el mecanismo arranque solo.
            Dim ultimaVista As String = String.Empty
            Try
                ultimaVista = Await _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.UltimaVersionNovedades)
            Catch ex As Exception
                ultimaVista = String.Empty
            End Try
            If NovedadesHelper.DebeMostrarNovedades(_versionActual, ultimaVista) Then
                Dim novedades = Await _novedadesService.ObtenerNovedades(ultimaVista?.Trim())
                If novedades.Count > 0 Then
                    Dim parametros As New Prism.Services.Dialogs.DialogParameters From {
                        {"novedades", novedades}
                    }
                    _dialogService.ShowDialog("NovedadesDialog", parametros, Sub(r)
                                                                             End Sub)
                End If
            End If
            If ultimaVista?.Trim() <> _versionActual Then
                Await _configuracion.GuardarParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.UltimaVersionNovedades, _versionActual)
            End If
        Catch ex As Exception
            ' Las novedades nunca deben impedir arrancar Nesto
        End Try

        Await ComprobarAlmacenTitular()
    End Sub

    ' Caso real 20/08/26: quien se cambia el almacén de pedidos temporalmente (Tienda Online:
    ' AMZ para FBA ↔ ALG para cubrir rutas) puede olvidarse de volver. El servidor captura el
    ' almacén TITULAR al primer cambio, y aquí, al arrancar, si el activo difiere del titular
    ' se ofrece restaurarlo. Solo en máquinas SIN parámetros automáticos por nombre de sede
    ' (en las de sede ALG*/REI*... el arranque ya machaca el almacén y no hay nada que ofrecer).
    ' Best-effort: nunca debe impedir arrancar Nesto.
    Private Async Function ComprobarAlmacenTitular() As System.Threading.Tasks.Task
        Try
            Dim delegacionDeMaquina As String = Strings.Left(Maquina, 3)
            If Constantes.Sedes.ListaSedes.Select(Function(s) s.Codigo).Contains(delegacionDeMaquina) Then
                Return
            End If
            Dim servicio = ContainerLocator.Container.Resolve(Of ControlesUsuario.Services.IServicioParametrosEditables)()
            Dim editables = Await servicio.LeerEditables()
            Dim almacen = editables?.FirstOrDefault(Function(p) p.Clave = Parametros.Claves.AlmacenPedidoVta)
            If almacen Is Nothing OrElse String.IsNullOrWhiteSpace(almacen.ValorTitular) OrElse
                almacen.ValorTitular = almacen.ValorActual Then
                Return
            End If
            Dim respuesta = MessageBox.Show(
                $"Tu almacén de pedidos titular es {almacen.ValorTitular}, pero tienes activo {almacen.ValorActual}." &
                vbCrLf & vbCrLf & $"¿Quieres volver a {almacen.ValorTitular}?",
                "Almacén de pedidos", MessageBoxButton.YesNo, MessageBoxImage.Question)
            If respuesta = MessageBoxResult.Yes Then
                Dim unused = Await servicio.Cambiar(almacen.Clave, almacen.ValorTitular)
            End If
        Catch ex As Exception
            ' Best-effort: sin conexión o sin permisos, no se molesta ni se rompe el arranque
        End Try
    End Function

    Private Sub ActualizarMaquinaYDelegacion()
        Maquina = RdpClientInfo.GetCurrentClientName()
        Delegacion = _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto)
        Dim delegacionDeMaquina As String = Strings.Left(Maquina, 3)
        If Constantes.Sedes.ListaSedes.Select(Function(s) s.Codigo).Contains(delegacionDeMaquina) Then
            TextoAdvertencia = String.Empty
            If delegacionDeMaquina <> Delegacion Then
                Delegacion = delegacionDeMaquina
                _configuracion.GuardarParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.DelegacionDefecto, delegacionDeMaquina)
                _configuracion.GuardarParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta, delegacionDeMaquina)
                _configuracion.GuardarParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenInventario, delegacionDeMaquina)
            End If
        Else
            TextoAdvertencia = "¡Nombre de equipo no válido para parámetros automáticos!"
        End If
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Maquina)))
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(Delegacion)))
        RaiseEvent PropertyChanged(Me, New PropertyChangedEventArgs(NameOf(TextoAdvertencia)))
    End Sub

    Private Sub OnSessionSwitch(sender As Object, e As SessionSwitchEventArgs)
        If e.Reason = SessionSwitchReason.RemoteConnect Then
            ActualizarMaquinaYDelegacion()
        End If
    End Sub

    ''' <summary>
    ''' Obtiene el token proactivamente al iniciar; al llegar, el indicador se repinta solo (TokenCambiado).
    ''' </summary>
    Private Async Sub ObtenerTokenAlArrancar()
        Try
            If _servicioAutenticacion IsNot Nothing Then
                Await _servicioAutenticacion.ObtenerTokenValidoAsync()
            End If
        Catch ex As Exception
            ' Sin servidor la raya se queda en rojo; no debe romper el arranque
        End Try
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

