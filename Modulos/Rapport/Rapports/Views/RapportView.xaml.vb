Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Class RapportView

    Public Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub

    Private Sub RapportView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        cmbEstadosRapport.ItemsSource = [Enum].GetValues(GetType(EstadoSeguimientoDTO)).Cast(Of EstadoSeguimientoDTO)()
    End Sub
End Class
