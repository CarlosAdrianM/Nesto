Imports System.Threading.Tasks
Imports Nesto.ViewModels

Public Class Agencias

    Private Sub txtNumeroPedido_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNumeroPedido.GotFocus
        txtNumeroPedido.SelectAll()
    End Sub

    Private Sub txtNumeroBultos_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNumeroBultos.GotFocus
        txtNumeroBultos.SelectAll()
    End Sub

    Private Sub txtNumeroPedido_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNumeroPedido.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub

    Private Sub txtNumeroBultos_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNumeroBultos.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub


    Private Sub txtNombreFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNombreFiltro.GotFocus
        txtNombreFiltro.SelectAll()
    End Sub

    Public Sub New()

        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub

    Public Sub New(viewModel As AgenciasViewModel)
        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        Me.DataContext = viewModel
    End Sub

    Private Async Sub Agencias_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim viewModel As AgenciasViewModel = CType(Me.DataContext, AgenciasViewModel)
        ' Ponemos e IF para que no entre cada vez que coja el foco
        If IsNothing(viewModel.numeroPedido) OrElse viewModel.numeroPedido.Trim = "" Then
            Await viewModel.cmdCargarDatos.Execute()
            Await Task.Delay(1000)
            txtPedidosNumero.Focus()
            txtPedidosNumero.SelectAll()
        End If
    End Sub

    Private Async Sub InsertarEnvioPendienteButton_Click(sender As Object, e As RoutedEventArgs) Handles InsertarEnvioPendienteButton.Click
        Await Task.Delay(300)
        EnvioPendientePedidoTextBox.Focus()
    End Sub

    Private Sub EnvioPendientePedidoTextBox_KeyUp(sender As Object, e As KeyEventArgs) Handles EnvioPendientePedidoTextBox.KeyUp
        If e.Key = Key.Enter Then
            Dim Binding = sender.GetBindingExpression(TextBox.TextProperty)
            Binding.UpdateSource()
        End If
    End Sub

    Private Sub txtPedidosNumero_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtPedidosNumero.GotFocus
        txtPedidosNumero.SelectAll()
    End Sub

    Private Sub txtPedidosNumero_KeyUp(sender As Object, e As KeyEventArgs) Handles txtPedidosNumero.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub

    Private Sub EnvioPendientePedidoTextBox_GotFocus(sender As Object, e As RoutedEventArgs) Handles EnvioPendientePedidoTextBox.GotFocus
        EnvioPendientePedidoTextBox.SelectAll()
    End Sub

    Private Sub txtPedidosNumero_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtPedidosNumero.PreviewMouseUp
        txtPedidosNumero.SelectAll()
    End Sub

    Private Sub txtNumeroPedido_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtNumeroPedido.PreviewMouseUp
        txtNumeroPedido.SelectAll()
    End Sub

    Private Sub txtNumeroBultos_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtNumeroBultos.PreviewMouseUp
        txtNumeroBultos.SelectAll()
    End Sub

    Private Sub txtEnlaceSeguimiento_MouseRightButtonUp(sender As Object, e As MouseButtonEventArgs) Handles txtEnlaceSeguimiento.MouseRightButtonUp
        Dim vm As AgenciasViewModel = DataContext
        Clipboard.SetText(vm.EnlaceSeguimientoEnvio)
    End Sub
End Class
