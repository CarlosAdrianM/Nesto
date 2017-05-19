Imports Nesto.ViewModels
Imports Xceed.Wpf.DataGrid

Public Class Comisiones
    Public Sub New(viewModel As ComisionesViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco en el filtro
        'txtFiltro.Focus()
    End Sub

    Private Sub dgrEntregados_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrEntregados.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))

        If src.[GetType]() = GetType(CellContentPresenter) Then
            DataContext.cmdAbrirPedido.Execute(dgrEntregados.SelectedItem)
        End If
    End Sub

    Private Sub dgrPendientesEntregar_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrPendientesEntregar.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))

        If src.[GetType]() = GetType(CellContentPresenter) Then
            DataContext.cmdAbrirPedido.Execute(dgrPendientesEntregar.SelectedItem)
        End If
    End Sub
End Class
