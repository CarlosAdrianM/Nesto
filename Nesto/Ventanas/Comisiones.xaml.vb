Imports Nesto.ViewModels

Public Class Comisiones
    Public Sub New(viewModel As ComisionesViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco en el filtro
        'txtFiltro.Focus()
    End Sub
End Class
