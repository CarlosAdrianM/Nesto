Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Class RapportView

    Public Sub New(viewModel As RapportViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        DataContext = viewModel
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub
End Class
