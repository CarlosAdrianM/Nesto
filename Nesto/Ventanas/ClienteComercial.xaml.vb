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

    Private Sub txtReclamarDeudaCorreo_KeyUp(sender As Object, e As KeyEventArgs) Handles txtReclamarDeudaCorreo.KeyUp
        If e.Key = Key.Enter Then
            txtReclamarDeudaMovil.Focus()
        End If
    End Sub

    Private Sub txtReclamarDeudaMovil_KeyUp(sender As Object, e As KeyEventArgs) Handles txtReclamarDeudaMovil.KeyUp
        If e.Key = Key.Enter Then
            txtReclamarDeudaImporte.Focus()
        End If
    End Sub

    Private Sub txtReclamarDeudaImporte_KeyUp(sender As Object, e As KeyEventArgs) Handles txtReclamarDeudaImporte.KeyUp
        If e.Key = Key.Enter Then
            txtReclamarDeudaAsunto.Focus()
        End If
    End Sub

    Private Sub txtReclamarDeudaAsunto_KeyUp(sender As Object, e As KeyEventArgs) Handles txtReclamarDeudaAsunto.KeyUp
        If e.Key = Key.Enter Then
            txtReclamarDeudaNombre.Focus()
        End If
    End Sub

    Private Sub txtReclamarDeudaNombre_KeyUp(sender As Object, e As KeyEventArgs) Handles txtReclamarDeudaNombre.KeyUp
        If e.Key = Key.Enter Then
            btnReclamarDeuda.Focus()
        End If
    End Sub

    Private Sub txtReclamarDeudaAsunto_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtReclamarDeudaAsunto.GotFocus
        txtReclamarDeudaAsunto.SelectAll()
    End Sub

    Private Sub txtReclamarDeudaCorreo_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtReclamarDeudaCorreo.GotFocus
        txtReclamarDeudaCorreo.SelectAll()
    End Sub

    Private Sub txtReclamarDeudaImporte_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtReclamarDeudaImporte.GotFocus
        txtReclamarDeudaImporte.SelectAll()
    End Sub

    Private Sub txtReclamarDeudaMovil_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtReclamarDeudaMovil.GotFocus
        txtReclamarDeudaMovil.SelectAll()
    End Sub

    Private Sub txtReclamarDeudaNombre_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtReclamarDeudaNombre.GotFocus
        txtReclamarDeudaNombre.SelectAll()
    End Sub

    Private Sub Run_MouseRightButtonUp(sender As Object, e As MouseButtonEventArgs)
        Dim vm As ClientesViewModel = DataContext
        Clipboard.SetText(vm.EnlaceReclamarDeuda)
    End Sub
End Class
