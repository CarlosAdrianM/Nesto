Imports Prism.Regions
Imports Nesto.Infrastructure.Contracts

Class MainWindow
    Implements IMainWindow

    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly tituloVentana As String

    Public Sub New(regionManager As IRegionManager)

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

        Me.Title = tituloVentana
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

