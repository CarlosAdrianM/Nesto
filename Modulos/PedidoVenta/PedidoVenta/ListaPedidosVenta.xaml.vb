Imports Prism.Regions

Public Class ListaPedidosVenta
    Public Sub New(viewModel As ListaPedidosVentaViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        viewModel.cmdCargarListaPedidos.Execute(Nothing)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco en el filtro
        txtFiltro.Focus()
    End Sub


    Private Sub txtFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltro.GotFocus
        txtFiltro.SelectAll()
    End Sub

    Private Sub txtFiltro_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltro.PreviewMouseUp
        txtFiltro.SelectAll()
    End Sub

    Private Sub txtFiltro_KeyUp(sender As Object, e As KeyEventArgs) Handles txtFiltro.KeyUp
        If e.Key = System.Windows.Input.Key.Enter Then
            Dim Binding = txtFiltro.GetBindingExpression(TextBox.TextProperty)
            Binding.UpdateSource()
            txtFiltro.SelectAll()
        End If
    End Sub

    Public Sub cambiarRegionManager(newRegionManager As IRegionManager)
        Me.DataContext.scopedRegionManager = newRegionManager
    End Sub

    Private Sub ListaPedidosVenta_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        txtFiltro.Focus()
    End Sub
End Class
