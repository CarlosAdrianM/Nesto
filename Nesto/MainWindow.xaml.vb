Imports Prism.Regions
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Shared

Partial Class MainWindow
    Implements IMainWindow

    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly tituloVentana As String
    Public ReadOnly Property Maquina As String
    Public ReadOnly Property Delegacion As String
    Public ReadOnly Property Usuario As String
    Public ReadOnly Property TextoAdvertencia As String

    Public Sub New(regionManager As IRegionManager, configuracion As IConfiguracion)

        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.regionManager = regionManager
        'Me.DataContext = New MainViewModel(container, regionManager)
        'Try
        '    tituloVentana = "Nesto (" + ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString() + ")"
        'Catch ex As InvalidDeploymentException
        '    tituloVentana = "Nesto"
        'End Try

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

