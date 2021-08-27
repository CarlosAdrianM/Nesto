Imports Nesto.Contratos

Class InventarioView



    Public Sub New(viewModel As InventarioViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco inicial en Fecha
        'Keyboard.Focus(txtFecha)
        txtFecha.Focus()
    End Sub

    Dim teclaSuelta As Boolean = False

    Private Sub txtProducto_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtProducto.GotFocus
        txtProducto.SelectAll()
    End Sub



    Private Async Sub txtProducto_KeyUp(sender As Object, e As KeyEventArgs) Handles txtProducto.KeyUp
        If teclaSuelta AndAlso e.Key = Key.Return Then
            e.Handled = True
            teclaSuelta = False
            If DataContext.estaCantidadActiva Then
                txtCantidad.Focus()
            Else
                sender.GetBindingExpression(TextBox.TextProperty).UpdateSource()
                Dim vm As InventarioViewModel = DataContext
                Dim nuevoMovimiento = Await vm.InsertarProducto(sender.Text)
                If Not IsNothing(nuevoMovimiento) Then
                    If tclMovimientos.SelectedIndex = 0 Then 'Recientes
                        dgrRecientes.ScrollIntoView(nuevoMovimiento)
                    Else ' Completo
                        dgrCompleto.ScrollIntoView(nuevoMovimiento)
                    End If
                End If
            End If
            txtProducto.SelectAll()
        End If
    End Sub

    Private Sub txtCantidad_KeyUp(sender As Object, e As KeyEventArgs) Handles txtCantidad.KeyUp
        If teclaSuelta AndAlso e.Key = Key.Return Then
            e.Handled = True
            teclaSuelta = False
            sender.GetBindingExpression(TextBox.TextProperty).UpdateSource()
            DataContext.cmdInsertarProducto.Execute(txtProducto.Text)
            txtProducto.Focus()
        End If
    End Sub

    Private Sub chkCantidad_Checked(sender As Object, e As RoutedEventArgs) Handles chkCantidad.Checked
        txtCantidad.Focus()
    End Sub

    Private Sub chkCantidad_Unchecked(sender As Object, e As RoutedEventArgs) Handles chkCantidad.Unchecked
        txtProducto.Focus()
    End Sub

    Private Sub txtProducto_KeyDown(sender As Object, e As KeyEventArgs) Handles txtProducto.KeyDown
        teclaSuelta = True
    End Sub

    Private Sub txtCantidad_KeyDown(sender As Object, e As KeyEventArgs) Handles txtCantidad.KeyDown
        teclaSuelta = True
    End Sub

    Private Sub txtCantidad_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtCantidad.GotFocus
        txtCantidad.SelectAll()
    End Sub

    Private Sub tclMovimientos_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles tclMovimientos.SelectionChanged
        If tabCompleto.IsSelected Then
            DataContext.cmdActualizarMovimientos.Execute(Nothing)
        End If
    End Sub
End Class
