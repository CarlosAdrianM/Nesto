Imports Microsoft.Practices.Unity
Imports Microsoft.Practices.Prism.Regions
Imports Nesto.Contratos
Imports System.Deployment.Application

Class MainWindow
    Implements IMainWindow

    Private ReadOnly container As IUnityContainer
    Private ReadOnly regionManager As IRegionManager
    Private ReadOnly tituloVentana As String

    Public Sub New(container As IUnityContainer, regionManager As IRegionManager)

        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.container = container
        Me.regionManager = regionManager
        'Me.DataContext = New MainViewModel(container, regionManager)
        Try
            tituloVentana = "Nesto (" + ApplicationDeployment.CurrentDeployment.CurrentVersion.ToString() + ")"
        Catch ex As InvalidDeploymentException
            tituloVentana = "Nesto"
        End Try

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

    'Private Sub btnPruebaBorrar_Click(sender As Object, e As RoutedEventArgs) Handles btnPruebaBorrar.Click
    '    Dim view = Me.container.Resolve(GetType(MenuBarView))
    '    If Not IsNothing(view) Then
    '        'Dim regionAdapter = New RibbonRegionAdapter(Me.container.Resolve(GetType(RegionBehaviorFactory)))
    '        Dim regionAdapter = Me.container.Resolve(GetType(RibbonRegionAdapter))
    '        Dim region = regionAdapter.Initialize(MainMenu, "MainMenuQueNoExiste")
    '        'Dim region = regionManager.Regions("MainMenu")
    '        region.Add(view, "MenuBar")
    '    End If
    'End Sub
End Class

