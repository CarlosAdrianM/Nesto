Imports Nesto.ViewModels

Public Class ClienteComercial
    Public Sub New(viewModel As ClientesViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco en el filtro
        'txtFiltro.Focus()
    End Sub

    Private Sub txtFiltro_KeyUp(sender As Object, e As KeyEventArgs) Handles txtFiltro.KeyUp
        If e.Key = Key.Enter Then
            DataContext.filtro = txtFiltro.Text
            DataContext.actualizarFiltro(txtFiltro.Text)
            txtFiltro.SelectAll()
            listaIzda.Focus()
        End If
    End Sub

    Private Sub Comercial_Loaded(sender As Object, e As RoutedEventArgs) Handles Comercial.Loaded
        txtFiltro.Focus()
    End Sub

    Private Sub txtFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltro.GotFocus
        txtFiltro.SelectAll()
    End Sub

    'Private Sub txtFiltro_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltro.MouseUp
    '    txtFiltro.SelectAll()
    'End Sub

    Private Sub txtFiltro_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltro.PreviewMouseUp
        txtFiltro.SelectAll()
    End Sub
End Class
