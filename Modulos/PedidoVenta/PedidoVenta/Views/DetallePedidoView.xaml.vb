Imports System.Globalization

Public Class DetallePedidoView
    Private actualizarTotales As Boolean = False

    Private Sub grdLineas_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs) Handles grdLineas.CellEditEnding
        DataContext.cmdCeldaModificada.Execute(e)
        actualizarTotales = True
    End Sub


    Private Sub grdLineas_RowEditEnding(sender As Object, e As DataGridRowEditEndingEventArgs) Handles grdLineas.RowEditEnding
        ' Aquí comprobaremos las condiciones de precios (servicio.comprobarCondicionesPrecio)
    End Sub

    Private Sub grdLineas_KeyUp(sender As Object, e As KeyEventArgs) Handles grdLineas.KeyUp
        If actualizarTotales Then
            DataContext.cmdActualizarTotales.Execute()
            actualizarTotales = False
        End If

        Dim currentRowIndex = grdLineas.Items.IndexOf(grdLineas.CurrentItem)
        If (e.Key = 156 OrElse e.Key = 120) AndAlso currentRowIndex > 1 Then ' Alt + C
            grdLineas.CurrentCell = New DataGridCellInfo(grdLineas.Items(currentRowIndex - 1), grdLineas.Columns(4)) ' Cantidad
        End If
    End Sub

    Private Sub grdLineas_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles grdLineas.PreviewKeyDown
        If e.Key = Key.Enter Then
            If grdLineas.CurrentColumn.Header = "Cantidad" Then
                grdLineas.CurrentColumn = grdLineas.Columns(2) ' Producto
            End If
        End If
    End Sub

    Private Sub txtDescuentoPedido_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtDescuentoPedido.GotFocus
        txtDescuentoPedido.SelectAll()
    End Sub

    Private Sub txtDescuentoPedido_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles txtDescuentoPedido.MouseDoubleClick
        txtDescuentoPedido.SelectAll()
    End Sub

    Private Sub txtDescuentoPedido_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs) Handles txtDescuentoPedido.PreviewMouseLeftButtonDown
        Dim tb As TextBox = sender
        If Not IsNothing(tb) Then
            If Not tb.IsKeyboardFocusWithin Then
                e.Handled = True
                tb.Focus()
            End If
        End If
    End Sub

    Private Sub txtDescuentoPedido_KeyUp(sender As Object, e As KeyEventArgs) Handles txtDescuentoPedido.KeyUp
        If e.Key = Key.Enter Then
            Dim prop As DependencyProperty = TextBox.TextProperty
            Dim binding As BindingExpression = BindingOperations.GetBindingExpression(txtDescuentoPedido, prop)
            'DataContext.cmdPonerDescuentoPedido.Execute(Nothing)
            If Not IsNothing(binding) Then
                binding.UpdateSource()
            End If
        End If
    End Sub

    Private Sub grdLineas_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles grdLineas.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))

        If IsNothing(src) Then
            Return
        End If

        If src.[GetType]() = GetType(ScrollContentPresenter) Then
            DataContext.CargarProductoCommand.Execute(grdLineas.SelectedItem)
        End If
    End Sub

    Private Sub grdLineasCabecera_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles grdLineasCabecera.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))

        If IsNothing(src) Then
            Return
        End If

        If src.[GetType]() = GetType(ContentPresenter) Then
            DataContext.CargarProductoCommand.Execute(grdLineas.SelectedItem)
        End If
    End Sub

    Public Class porcentajeConverter
        Implements IValueConverter
        Public Function Convert(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.Convert
            Dim fraccion As Decimal = Decimal.Parse(value.ToString)
            Return fraccion.ToString("P2")
        End Function

        Public Function ConvertBack(ByVal value As Object, ByVal targetType As Type, ByVal parameter As Object, ByVal culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
            Dim valueWithoutPercentage As String = value.ToString().TrimEnd(" ", "%")
            Return Decimal.Parse(valueWithoutPercentage) / 100
        End Function
    End Class
End Class
