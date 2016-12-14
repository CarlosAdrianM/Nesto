Public Class PedidoVentaView
    Public Sub New(viewModel As PedidoVentaViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        viewModel.cmdCargarListaPedidos.Execute(Nothing)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

    End Sub

    Private actualizarTotales As Boolean = False

    Private Sub grdLineas_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs) Handles grdLineas.CellEditEnding
        DataContext.cmdCeldaModificada.Execute(e)
        'actualizarTotales = True
    End Sub

    'Private Sub grdLineas_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles grdLineas.SelectionChanged
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_RowEditEnding(sender As Object, e As DataGridRowEditEndingEventArgs) Handles grdLineas.RowEditEnding
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_CurrentCellChanged(sender As Object, e As EventArgs) Handles grdLineas.CurrentCellChanged
    '    'If actualizarTotales Then
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    '    '    actualizarTotales = False
    '    'End If
    'End Sub

    'Private Sub grdLineas_AddingNewItem(sender As Object, e As AddingNewItemEventArgs) Handles grdLineas.AddingNewItem
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_BeginningEdit(sender As Object, e As DataGridBeginningEditEventArgs) Handles grdLineas.BeginningEdit
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_InitializingNewItem(sender As Object, e As InitializingNewItemEventArgs) Handles grdLineas.InitializingNewItem
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_LoadingRow(sender As Object, e As DataGridRowEventArgs) Handles grdLineas.LoadingRow
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_LoadingRowDetails(sender As Object, e As DataGridRowDetailsEventArgs) Handles grdLineas.LoadingRowDetails
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_LostFocus(sender As Object, e As RoutedEventArgs) Handles grdLineas.LostFocus
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_ManipulationCompleted(sender As Object, e As ManipulationCompletedEventArgs) Handles grdLineas.ManipulationCompleted
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_PreparingCellForEdit(sender As Object, e As DataGridPreparingCellForEditEventArgs) Handles grdLineas.PreparingCellForEdit
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_PreviewTextInput(sender As Object, e As TextCompositionEventArgs) Handles grdLineas.PreviewTextInput
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    'Private Sub grdLineas_SelectedCellsChanged(sender As Object, e As SelectedCellsChangedEventArgs) Handles grdLineas.SelectedCellsChanged
    '    DataContext.cmdActualizarTotales.Execute(Nothing)
    'End Sub

    Private Sub grdLineas_KeyUp(sender As Object, e As KeyEventArgs) Handles grdLineas.KeyUp
        DataContext.cmdActualizarTotales.Execute(Nothing)
    End Sub
End Class
