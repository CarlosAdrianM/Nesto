Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Class RapportView

    Public Sub New(viewModel As RapportViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel
    End Sub

    Private Sub RapportView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        cmbEstadosRapport.ItemsSource = [Enum].GetValues(GetType(EstadoSeguimientoDTO)).Cast(Of EstadoSeguimientoDTO)()
        cmbTiposCentro.ItemsSource = [Enum].GetValues(GetType(TiposCentro)).Cast(Of TiposCentro)()
    End Sub
End Class
