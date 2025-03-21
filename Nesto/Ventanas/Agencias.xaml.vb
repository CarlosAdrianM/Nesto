﻿Imports System.Threading.Tasks
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
        If viewModel IsNot Nothing Then
            AddHandler viewModel.SolicitarFocoNumeroPedido, AddressOf OnSolicitarFocoNumeroPedido
        End If
        ' Ponemos e IF para que no entre cada vez que coja el foco
        If IsNothing(viewModel.numeroPedido) OrElse viewModel.numeroPedido.Trim = "" Then
            viewModel.cmdCargarDatos.Execute() ' Await 
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

    Private Sub txtNumClienteContabilizar_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNumClienteContabilizar.KeyUp
        If e.Key = Key.Return Then
            btnContabilizarReembolso.Focus()
        End If
    End Sub

    Private Sub txtNombreFiltro_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtNombreFiltro.PreviewMouseUp
        txtNombreFiltro.SelectAll()
    End Sub

    Private Sub txtNombreFiltro_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNombreFiltro.KeyUp
        If e.Key = Key.Return Then
            txtClienteFiltro.Focus()
        End If
    End Sub

    Private Sub txtClienteFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtClienteFiltro.GotFocus
        txtClienteFiltro.SelectAll()
    End Sub

    Private Sub txtClienteFiltro_KeyUp(sender As Object, e As KeyEventArgs) Handles txtClienteFiltro.KeyUp
        If e.Key = Key.Return Then
            txtNombreFiltro.Focus()
        End If
    End Sub

    Private Sub txtClienteFiltro_PreviewMouseDown(sender As Object, e As MouseButtonEventArgs) Handles txtClienteFiltro.PreviewMouseDown
        txtClienteFiltro.SelectAll()
    End Sub

    Private Sub txtPeso_KeyUp(sender As Object, e As KeyEventArgs) Handles txtPeso.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub

    Private Sub txtPeso_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtPeso.GotFocus
        txtPeso.SelectAll()
    End Sub

    Private Sub txtPeso_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles txtPeso.PreviewKeyDown
        Dim textBox As TextBox = TryCast(sender, TextBox)

        ' Verificar si la tecla presionada es la tecla del punto en el teclado numérico
        If e.Key = Key.Decimal Then
            e.Handled = True

            Dim caretIndex As Integer = textBox.CaretIndex
            textBox.Text = textBox.Text.Insert(caretIndex, ",")
            textBox.CaretIndex = caretIndex + 1
        End If
    End Sub

    Private Sub txtPeso_PreviewMouseLeftButtonUp(sender As Object, e As MouseButtonEventArgs) Handles txtPeso.PreviewMouseLeftButtonUp
        txtPeso.SelectAll()
    End Sub

    Private Sub OnSolicitarFocoNumeroPedido(sender As Object, e As EventArgs)
        txtNumeroPedido.Dispatcher.InvokeAsync(Sub()
                                                   txtNumeroPedido.Focus()
                                                   txtNumeroPedido.SelectAll()
                                               End Sub)
    End Sub

    Private Sub Agencias_Unloaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Unloaded
        Dim viewModel As AgenciasViewModel = CType(Me.DataContext, AgenciasViewModel)
        RemoveHandler viewModel.SolicitarFocoNumeroPedido, AddressOf OnSolicitarFocoNumeroPedido
    End Sub
End Class
