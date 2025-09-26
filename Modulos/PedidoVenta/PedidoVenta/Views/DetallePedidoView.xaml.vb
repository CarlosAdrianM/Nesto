Imports System.Globalization
Imports System.Windows.Threading

Public Class DetallePedidoView
    Private actualizarTotales As Boolean = False
    Private lineaEnEdicion As LineaPedidoVentaWrapper = Nothing



    Private Sub grdLineas_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs) Handles grdLineas.CellEditEnding
        Dim unused = DataContext.cmdCeldaModificada.Execute(e)
        actualizarTotales = True
    End Sub


    Private Sub grdLineas_RowEditEnding(sender As Object, e As DataGridRowEditEndingEventArgs) Handles grdLineas.RowEditEnding
        ' Aquí comprobaremos las condiciones de precios (servicio.comprobarCondicionesPrecio)

        ' Si se cancela la edición o se confirma, verificar la línea
        If e.EditAction = DataGridEditAction.Commit Then
            Dim unused = Dispatcher.BeginInvoke(Sub() VerificarYEliminarLineaVacia(), DispatcherPriority.Background)
        End If
    End Sub

    Private Sub grdLineas_KeyUp(sender As Object, e As KeyEventArgs) Handles grdLineas.KeyUp
        If actualizarTotales Then
            Dim unused = DataContext.cmdActualizarTotales.Execute()
            actualizarTotales = False
        End If

        Dim currentRowIndex = grdLineas.Items.IndexOf(grdLineas.CurrentItem)
        If e.Key = Key.System AndAlso e.SystemKey = Key.C AndAlso currentRowIndex > 1 Then ' Alt + C
            grdLineas.CurrentCell = New DataGridCellInfo(grdLineas.Items(currentRowIndex - 1), grdLineas.Columns(4)) ' Cantidad
        End If
    End Sub

    Private Sub grdLineas_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles grdLineas.PreviewKeyDown
        If e.Key = Key.Enter Then
            If grdLineas.CurrentColumn.Header = "Cantidad" Then
                grdLineas.CurrentColumn = grdLineas.Columns(2) ' Producto
            End If
        ElseIf e.Key = Key.Delete Then
            Dim dataGrid As DataGrid = TryCast(sender, DataGrid)
            If dataGrid?.SelectedItem IsNot Nothing AndAlso TypeOf dataGrid.SelectedItem Is LineaPedidoVentaWrapper Then
                Dim selectedItem As LineaPedidoVentaWrapper = DirectCast(dataGrid.SelectedItem, LineaPedidoVentaWrapper)
                If selectedItem.estaAlbaraneada OrElse selectedItem.tienePicking Then
                    e.Handled = True
                    Return
                End If
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
                Dim unused = tb.Focus()
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
            Dim unused = DataContext.CargarProductoCommand.Execute(grdLineas.SelectedItem.Model)
        End If
    End Sub

    Private Sub grdLineasCabecera_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles grdLineasCabecera.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))

        If IsNothing(src) Then
            Return
        End If

        If src.[GetType]() = GetType(ContentPresenter) Then
            Dim vm As DetallePedidoViewModel = CType(DataContext, DetallePedidoViewModel)
            'Dim lineaSeleccionada As LineaPedidoVentaWrapper = CType(grdLineas.SelectedItem, LineaPedidoVentaWrapper)
            Dim dataGrid As DataGrid = CType(sender, DataGrid)
            Dim lineaSeleccionada As LineaPedidoVentaWrapper = CType(dataGrid.SelectedItem, LineaPedidoVentaWrapper)
            If Not IsNothing(lineaSeleccionada) Then
                vm.CargarProductoCommand.Execute(lineaSeleccionada.Model)
            End If
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

    Private Sub grdLineas_BeginningEdit(sender As Object, e As DataGridBeginningEditEventArgs)
        lineaEnEdicion = TryCast(e.Row.Item, LineaPedidoVentaWrapper)
        Dim item As LineaPedidoVentaWrapper = TryCast(e.Row.Item, LineaPedidoVentaWrapper)
        If item Is Nothing Then
            Return
        End If

        If item.estaAlbaraneada OrElse item.tienePicking Then
            e.Cancel = True
        End If
    End Sub

    Private estaEnfocanddoAutomaticamente As Boolean = False

    Private Sub TabControl_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles TabControl.SelectionChanged
        ' Solo actuar si se seleccionó la pestaña de líneas Y no estamos ya enfocando automáticamente
        If Not estaEnfocanddoAutomaticamente AndAlso e.Source Is TabControl Then
            Dim tabControl As TabControl = TryCast(sender, TabControl)
            If tabControl IsNot Nothing AndAlso tabControl.SelectedIndex = 3 Then
                ' Usar Dispatcher para asegurar que el DataGrid esté completamente renderizado
                Dim unused = Dispatcher.BeginInvoke(Sub() EnfocarColumnaProductoInicial(), DispatcherPriority.Loaded)
            End If
        End If
    End Sub

    Private Sub EnfocarColumnaProductoInicial()
        estaEnfocanddoAutomaticamente = True

        Try
            ' Encontrar la primera línea disponible para editar o crear una nueva
            Dim filaParaEditar As Integer = EncontrarOCrearFilaDisponible()

            If filaParaEditar >= 0 Then
                ' Asegurarse de que el DataGrid esté visible y listo
                grdLineas.UpdateLayout()

                ' Seleccionar la fila
                grdLineas.SelectedIndex = filaParaEditar
                grdLineas.CurrentItem = grdLineas.Items(filaParaEditar)

                ' Enfocar la columna "Producto" (índice 2)
                Dim columnaProducto As DataGridColumn = grdLineas.Columns(2)
                grdLineas.CurrentCell = New DataGridCellInfo(grdLineas.Items(filaParaEditar), columnaProducto)

                ' Enfocar el DataGrid
                Dim unused4 = grdLineas.Focus()

                ' Usar otro Dispatcher para asegurar que BeginEdit funcione correctamente
                Dim unused3 = Dispatcher.BeginInvoke(Sub()
                                                         Dim unused2 = grdLineas.BeginEdit()
                                                         ' Si hay un TextBox, enfocarlo
                                                         Dim cellContent = grdLineas.CurrentCell.Column.GetCellContent(grdLineas.CurrentCell.Item)
                                                         If cellContent IsNot Nothing Then
                                                             Dim textBox = TryCast(cellContent, TextBox)
                                                             If textBox IsNot Nothing Then
                                                                 Dim unused1 = textBox.Focus()
                                                                 textBox.SelectAll()
                                                             End If
                                                         End If
                                                     End Sub, DispatcherPriority.Background)
            End If
        Finally
            ' Resetear la bandera después de un breve delay
            Dim unused = Dispatcher.BeginInvoke(Sub()
                                                    estaEnfocanddoAutomaticamente = False
                                                End Sub, DispatcherPriority.Background)
        End Try
    End Sub

    Private Function EncontrarOCrearFilaDisponible() As Integer
        ' Primero buscar una línea vacía existente
        For i As Integer = 0 To grdLineas.Items.Count - 1
            Dim linea As LineaPedidoVentaWrapper = TryCast(grdLineas.Items(i), LineaPedidoVentaWrapper)
            If linea IsNot Nothing AndAlso Not linea.estaAlbaraneada AndAlso Not linea.tienePicking Then
                If String.IsNullOrEmpty(linea.Producto) Then
                    Return i ' Línea vacía disponible
                End If
            End If
        Next

        ' Si no hay líneas vacías, intentar crear una nueva si el DataGrid permite agregar filas
        If grdLineas.CanUserAddRows Then
            ' El DataGrid debería crear automáticamente una nueva fila
            ' Devolver la última fila (que será la nueva)
            Return grdLineas.Items.Count - 1
        End If

        ' Como última opción, usar la última fila editable
        For i As Integer = grdLineas.Items.Count - 1 To 0 Step -1
            Dim linea As LineaPedidoVentaWrapper = TryCast(grdLineas.Items(i), LineaPedidoVentaWrapper)
            If linea IsNot Nothing AndAlso Not linea.estaAlbaraneada AndAlso Not linea.tienePicking Then
                Return i
            End If
        Next

        Return -1
    End Function

    Private Sub EnfocarColumnaProducto()
        ' Si no hay líneas o la última tiene producto, crear una nueva
        If grdLineas.Items.Count = 0 OrElse UltimaLineaTieneProducto() Then
            ' Esto dependerá de cómo manejes la creación de nuevas líneas en tu ViewModel
            ' Si tienes un comando para agregar líneas, úsalo aquí
            ' Por ahora, vamos a trabajar con las líneas existentes
        End If

        ' Seleccionar la primera fila disponible para edición
        Dim filaParaEditar As Integer = EncontrarPrimeraFilaDisponible()
        If filaParaEditar >= 0 Then
            grdLineas.SelectedIndex = filaParaEditar
            grdLineas.CurrentItem = grdLineas.Items(filaParaEditar)

            ' Enfocar la columna "Producto" (índice 2 según tu estructura)
            Dim columnaProducto As DataGridColumn = grdLineas.Columns(2)
            grdLineas.CurrentCell = New DataGridCellInfo(grdLineas.Items(filaParaEditar), columnaProducto)

            ' Enfocar el DataGrid y comenzar edición
            Dim unused1 = grdLineas.Focus()
            Dim unused = grdLineas.BeginEdit()
        End If
    End Sub

    Private Function UltimaLineaTieneProducto() As Boolean
        If grdLineas.Items.Count = 0 Then Return False
        Dim ultimaLinea As LineaPedidoVentaWrapper = TryCast(grdLineas.Items(grdLineas.Items.Count - 1), LineaPedidoVentaWrapper)
        Return ultimaLinea IsNot Nothing AndAlso Not String.IsNullOrEmpty(ultimaLinea.Producto)
    End Function

    Private Function EncontrarPrimeraFilaDisponible() As Integer
        ' Buscar la primera fila que no esté albaraneada ni tenga picking
        For i As Integer = 0 To grdLineas.Items.Count - 1
            Dim linea As LineaPedidoVentaWrapper = TryCast(grdLineas.Items(i), LineaPedidoVentaWrapper)
            If linea IsNot Nothing AndAlso Not linea.estaAlbaraneada AndAlso Not linea.tienePicking Then
                If String.IsNullOrEmpty(linea.Producto) Then
                    Return i ' Línea vacía disponible
                End If
            End If
        Next

        ' Si no hay líneas vacías disponibles, usar la primera editable
        For i As Integer = 0 To grdLineas.Items.Count - 1
            Dim linea As LineaPedidoVentaWrapper = TryCast(grdLineas.Items(i), LineaPedidoVentaWrapper)
            If linea IsNot Nothing AndAlso Not linea.estaAlbaraneada AndAlso Not linea.tienePicking Then
                Return i
            End If
        Next

        Return If(grdLineas.Items.Count > 0, 0, -1)
    End Function

    Private Sub VerificarYEliminarLineaVacia()
        If lineaEnEdicion IsNot Nothing Then
            ' Verificar si la línea está vacía (sin producto y sin texto)
            If String.IsNullOrWhiteSpace(lineaEnEdicion.Producto) AndAlso String.IsNullOrWhiteSpace(lineaEnEdicion.texto) Then
                ' Verificar que no sea una línea que ya existía (tiene ID > 0 normalmente significa que ya estaba guardada)
                If lineaEnEdicion.id = 0 OrElse lineaEnEdicion.id = Nothing Then
                    Try
                        ' Eliminar la línea de la colección
                        Dim viewModel As DetallePedidoViewModel = TryCast(DataContext, DetallePedidoViewModel)
                        If viewModel IsNot Nothing AndAlso viewModel.pedido IsNot Nothing AndAlso viewModel.pedido.Lineas IsNot Nothing Then
                            If viewModel.pedido.Lineas.Contains(lineaEnEdicion) Then
                                Dim unused = viewModel.pedido.Lineas.Remove(lineaEnEdicion)
                            End If
                        End If
                    Catch ex As Exception
                        ' Si hay error al eliminar, simplemente ignorar
                    End Try
                End If
            End If
            lineaEnEdicion = Nothing
        End If
    End Sub

    Private Sub DetallePedidoView_KeyUp(sender As Object, e As KeyEventArgs) Handles Me.KeyUp
        ' Alt + L para ir a pestaña de líneas
        If e.Key = Key.System AndAlso e.SystemKey = Key.L Then
            If TabControl.SelectedIndex <> 3 Then
                TabControl.SelectedIndex = 3
            Else
                Dim unused = Dispatcher.BeginInvoke(Sub() EnfocarColumnaProductoInicial(), DispatcherPriority.Loaded)
            End If
            e.Handled = True
        End If
    End Sub

End Class
