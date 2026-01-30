Imports System.Globalization
Imports System.Windows.Input
Imports System.Windows.Threading

Public Class DetallePedidoView
    Private actualizarTotales As Boolean = False
    Private lineaEnEdicion As LineaPedidoVentaWrapper = Nothing
    ' Issue #258: Guardar el último tipoLinea usado para heredarlo en líneas nuevas
    Private ultimoTipoLineaUsado As Byte = 1

#Region "Issue #51: Refrescar columnas al cambiar visibilidad de Fecha Entrega"
    Private Sub DetallePedidoView_DataContextChanged(sender As Object, e As DependencyPropertyChangedEventArgs) Handles Me.DataContextChanged
        ' Suscribirse al PropertyChanged del ViewModel para detectar cambios en UsarFechasIndividuales
        Dim oldVm = TryCast(e.OldValue, DetallePedidoViewModel)
        Dim newVm = TryCast(e.NewValue, DetallePedidoViewModel)

        If oldVm IsNot Nothing Then
            RemoveHandler oldVm.PropertyChanged, AddressOf ViewModel_PropertyChanged
        End If

        If newVm IsNot Nothing Then
            AddHandler newVm.PropertyChanged, AddressOf ViewModel_PropertyChanged
        End If
    End Sub

    Private Sub ViewModel_PropertyChanged(sender As Object, e As ComponentModel.PropertyChangedEventArgs)
        If e.PropertyName = NameOf(DetallePedidoViewModel.UsarFechasIndividuales) Then
            ' Refrescar anchos de columnas del DataGrid después de cambiar visibilidad
            Dim unused = Dispatcher.BeginInvoke(Sub() RefrescarAnchosColumnas(), DispatcherPriority.Background)
        End If
    End Sub

    Private Sub RefrescarAnchosColumnas()
        Try
            ' Forzar recálculo de anchos de columna
            For Each columna In grdLineas.Columns
                If columna.Width.IsStar Then
                    Dim starValue = columna.Width.Value
                    columna.Width = New DataGridLength(starValue, DataGridLengthUnitType.Star)
                End If
            Next
            grdLineas.UpdateLayout()
        Catch
            ' Ignorar errores
        End Try
    End Sub
#End Region

#Region "Helper Visual Tree"
    ''' <summary>
    ''' Busca un hijo de tipo T en el árbol visual.
    ''' Issue #258: Necesario para encontrar TextBox dentro de DataGridTemplateColumn.
    ''' </summary>
    Private Shared Function FindVisualChild(Of T As DependencyObject)(parent As DependencyObject) As T
        If parent Is Nothing Then Return Nothing

        For i As Integer = 0 To VisualTreeHelper.GetChildrenCount(parent) - 1
            Dim child As DependencyObject = VisualTreeHelper.GetChild(parent, i)
            If TypeOf child Is T Then
                Return DirectCast(child, T)
            End If

            Dim result As T = FindVisualChild(Of T)(child)
            If result IsNot Nothing Then
                Return result
            End If
        Next

        Return Nothing
    End Function

    ''' <summary>
    ''' Busca un padre de tipo T en el árbol visual.
    ''' Issue #266: Necesario para encontrar DataGrid desde DataGridCell.
    ''' </summary>
    Private Shared Function FindVisualParent(Of T As DependencyObject)(child As DependencyObject) As T
        Dim parentObject As DependencyObject = VisualTreeHelper.GetParent(child)
        If parentObject Is Nothing Then Return Nothing
        Dim parent As T = TryCast(parentObject, T)
        If parent IsNot Nothing Then
            Return parent
        Else
            Return FindVisualParent(Of T)(parentObject)
        End If
    End Function
#End Region

#Region "Issue #266: Single-click edit y select-all para columna Descuento"
    ''' <summary>
    ''' Entrar en modo edición con un solo clic para DataGridTemplateColumn.
    ''' </summary>
    Private Sub DescuentoCell_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        Dim cell As DataGridCell = TryCast(sender, DataGridCell)
        If cell IsNot Nothing AndAlso Not cell.IsEditing AndAlso Not cell.IsReadOnly Then
            If Not cell.IsFocused Then
                Dim unused = cell.Focus()
            End If
            Dim dataGrid As DataGrid = FindVisualParent(Of DataGrid)(cell)
            If dataGrid IsNot Nothing Then
                If dataGrid.SelectionUnit <> DataGridSelectionUnit.FullRow Then
                    If Not cell.IsSelected Then
                        cell.IsSelected = True
                    End If
                Else
                    Dim row As DataGridRow = FindVisualParent(Of DataGridRow)(cell)
                    If row IsNot Nothing AndAlso Not row.IsSelected Then
                        row.IsSelected = True
                    End If
                End If
                dataGrid.BeginEdit()
            End If
        End If
    End Sub

    ''' <summary>
    ''' Seleccionar todo el texto cuando el TextBox se carga (al entrar en modo edición).
    ''' </summary>
    Private Sub DescuentoTextBox_Loaded(sender As Object, e As RoutedEventArgs)
        Dim textBox As TextBox = TryCast(sender, TextBox)
        If textBox IsNot Nothing Then
            Dim unused = textBox.Focus()
            textBox.SelectAll()
        End If
    End Sub
#End Region

    Private Sub grdLineas_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs) Handles grdLineas.CellEditEnding
        Dim unused = DataContext.cmdCeldaModificada.Execute(e)
        actualizarTotales = True

        ' Issue #258: Guardar el tipoLinea usado y mover foco a Texto si es tipo 0
        Dim linea As LineaPedidoVentaWrapper = TryCast(e.Row.Item, LineaPedidoVentaWrapper)
        If linea IsNot Nothing AndAlso linea.tipoLinea.HasValue Then
            ultimoTipoLineaUsado = linea.tipoLinea.Value

            ' Si se cambió la columna Tipo a 0 (Texto), mover foco a columna Texto
            If e.Column.Header?.ToString() = "Tipo" AndAlso linea.tipoLinea.Value = 0 Then
                Dim unused2 = Dispatcher.BeginInvoke(Sub() MoverAColumnaTexto(), DispatcherPriority.Background)
            End If
        End If
    End Sub

    ''' <summary>
    ''' Mueve el foco a la columna Texto de la fila actual y entra en modo edición.
    ''' Issue #258: Llamado automáticamente cuando TipoLinea cambia a 0.
    ''' </summary>
    Private Sub MoverAColumnaTexto()
        Try
            Dim filaActual = grdLineas.Items.IndexOf(grdLineas.CurrentItem)
            If filaActual >= 0 Then
                Dim columnaTexto = grdLineas.Columns(3) ' Texto es la columna 3 (después de Estado, Tipo, Producto)
                grdLineas.CurrentCell = New DataGridCellInfo(grdLineas.Items(filaActual), columnaTexto)
                Dim unused = grdLineas.BeginEdit()
            End If
        Catch
            ' Ignorar errores de visual tree
        End Try
    End Sub

#Region "Issue #258: Valores por defecto en líneas nuevas"
    ''' <summary>
    ''' Al crear una nueva línea, hereda tipoLinea del último usado y establece estado=1.
    ''' Issue #258: Mejora UX - si vas a meter tres líneas de texto, solo cambias TipoLinea una vez.
    ''' </summary>
    Private Sub grdLineas_InitializingNewItem(sender As Object, e As InitializingNewItemEventArgs) Handles grdLineas.InitializingNewItem
        Dim nuevaLinea As LineaPedidoVentaWrapper = TryCast(e.NewItem, LineaPedidoVentaWrapper)
        If nuevaLinea Is Nothing Then Return

        ' Establecer estado = 1 (en curso) por defecto
        nuevaLinea.estado = 1

        ' Heredar tipoLinea del último usado (guardado en CellEditEnding)
        nuevaLinea.tipoLinea = ultimoTipoLineaUsado

        ' Si el tipo es 0 (Texto), mover automáticamente a la columna Texto
        If ultimoTipoLineaUsado = 0 Then
            Dim unused = Dispatcher.BeginInvoke(Sub() MoverAColumnaTexto(), DispatcherPriority.Background)
        End If
    End Sub
#End Region


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

        ' Issue #263: Asegurar modo edición después de Enter en Producto.
        ' El comportamiento por defecto del DataGrid ya movió a la siguiente fila,
        ' solo necesitamos asegurar que entramos en modo edición.
        If e.Key = Key.Enter Then
            Dim columnaActual = grdLineas.CurrentColumn?.Header?.ToString()
            If columnaActual = "Producto" Then
                Dim unused2 = Dispatcher.BeginInvoke(Sub()
                                                         grdLineas.BeginEdit()
                                                         ' Enfocar el TextBox
                                                         Try
                                                             Dim cellContent = grdLineas.CurrentColumn.GetCellContent(grdLineas.CurrentItem)
                                                             If cellContent IsNot Nothing Then
                                                                 Dim textBox = FindVisualChild(Of TextBox)(cellContent)
                                                                 If textBox IsNot Nothing Then
                                                                     Dim unused3 = textBox.Focus()
                                                                     textBox.SelectAll()
                                                                 End If
                                                             End If
                                                         Catch
                                                         End Try
                                                     End Sub, DispatcherPriority.Background)
            End If
        End If
    End Sub

    Private Sub grdLineas_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles grdLineas.PreviewKeyDown
        ' Issue #258: Mejorar navegación con DataGridTemplateColumn
        If e.Key = Key.Enter Then
            Dim columnaActual = grdLineas.CurrentColumn?.Header?.ToString()

            If columnaActual = "Cantidad" Then
                ' Desde Cantidad, Enter va a la siguiente línea
                e.Handled = True
                grdLineas.CommitEdit(DataGridEditingUnit.Cell, True)
                MoverASiguienteLineaProducto()
            ElseIf columnaActual = "Tipo" Then
                ' Issue #258: Desde Tipo, ir a Producto o Texto según el tipo seleccionado
                e.Handled = True
                grdLineas.CommitEdit(DataGridEditingUnit.Cell, True)
                ' El foco se mueve automáticamente en CellEditEnding si tipo=0
                If ultimoTipoLineaUsado <> 0 Then
                    MoverAColumnaProducto()
                End If
            ElseIf columnaActual = "Producto" Then
                ' Issue #263: NO manejar Enter aquí para permitir que AutocompleteBehavior
                ' actualice el texto antes del commit. El commit y navegación se harán
                ' en CellEditEnding o dejando el comportamiento por defecto del DataGrid.
                ' La navegación a la siguiente línea se hace en el evento KeyUp.
                ' (No hacer nada aquí - dejar que el evento llegue al TextBox)
            ElseIf columnaActual = "Texto" AndAlso ultimoTipoLineaUsado = 0 Then
                ' Issue #258: Desde Texto en línea de texto, Enter va a Texto de la siguiente línea
                e.Handled = True
                grdLineas.CommitEdit(DataGridEditingUnit.Cell, True)
                grdLineas.CommitEdit(DataGridEditingUnit.Row, True)
                Dim unused = Dispatcher.BeginInvoke(Sub() MoverASiguienteLineaProducto(), DispatcherPriority.Background)
            End If
        ElseIf e.Key = Key.Tab Then
            Dim columnaActual = grdLineas.CurrentColumn?.Header?.ToString()

            If columnaActual = "Tipo" Then
                ' Desde Tipo, Tab va a Producto y entra en modo edición
                e.Handled = True
                grdLineas.CommitEdit(DataGridEditingUnit.Cell, True)
                MoverAColumnaProducto()
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
        ElseIf e.Key = Key.Escape Then
            ' Issue #258: Manejar Escape manualmente para evitar crash con DataGridTemplateColumn
            ' El error "'{0}' no es un Visual ni un Visual3D" ocurre cuando WPF intenta cancelar
            ' la edición en un DataGridTemplateColumn que tiene un Behavior adjunto.
            ' La solución es marcar el evento como manejado y usar Dispatcher para cancelar
            ' de forma segura después de que el visual tree se estabilice.
            e.Handled = True
            Dim unused = Dispatcher.BeginInvoke(
                Sub()
                    Try
                        ' Primero commit (no cancel) para guardar el estado actual
                        grdLineas.CommitEdit(DataGridEditingUnit.Cell, True)
                        grdLineas.CommitEdit(DataGridEditingUnit.Row, True)
                        ' Mover el foco al DataGrid
                        Dim unused2 = grdLineas.Focus()
                    Catch
                        ' Si falla, simplemente ignorar
                    End Try
                End Sub, DispatcherPriority.Background)
        End If
    End Sub

#Region "Issue #258: CancelEdit Command Handlers"
    ''' <summary>
    ''' Maneja CanExecute del comando CancelEdit para evitar crash con DataGridTemplateColumn.
    ''' Issue #258: El error "'{0}' no es un Visual ni un Visual3D" ocurre cuando WPF intenta
    ''' cancelar la edición en una celda con DataGridTemplateColumn que tiene Behaviors.
    ''' </summary>
    Private Sub CancelEditCommand_CanExecute(sender As Object, e As CanExecuteRoutedEventArgs)
        ' Siempre permitir el comando, pero manejarlo nosotros en Executed
        e.CanExecute = True
        e.Handled = True
    End Sub

    ''' <summary>
    ''' Maneja la ejecución del comando CancelEdit de forma segura.
    ''' </summary>
    Private Sub CancelEditCommand_Executed(sender As Object, e As ExecutedRoutedEventArgs)
        Try
            ' Intentar commit en lugar de cancel para evitar problemas con el visual tree
            grdLineas.CommitEdit(DataGridEditingUnit.Cell, True)
            grdLineas.CommitEdit(DataGridEditingUnit.Row, True)
        Catch
            ' Si falla, simplemente mover el foco
        End Try

        Try
            ' Mover el foco al DataGrid
            Dim unused = grdLineas.Focus()
        Catch
            ' Ignorar errores
        End Try

        e.Handled = True
    End Sub
#End Region

    ''' <summary>
    ''' Mueve el foco a la columna Producto de la fila actual y entra en modo edición.
    ''' Issue #258: Necesario para DataGridTemplateColumn.
    ''' </summary>
    Private Sub MoverAColumnaProducto()
        Try
            Dim filaActual = grdLineas.Items.IndexOf(grdLineas.CurrentItem)
            If filaActual >= 0 Then
                Dim columnaProducto = grdLineas.Columns(2) ' Producto es la columna 2
                grdLineas.CurrentCell = New DataGridCellInfo(grdLineas.Items(filaActual), columnaProducto)
                Dim unused = grdLineas.BeginEdit()
                ' Enfocar el TextBox dentro de la celda
                Dim unused2 = Dispatcher.BeginInvoke(Sub()
                                                         Try
                                                             Dim cellContent = columnaProducto.GetCellContent(grdLineas.Items(filaActual))
                                                             If cellContent IsNot Nothing Then
                                                                 Dim textBox = FindVisualChild(Of TextBox)(cellContent)
                                                                 If textBox IsNot Nothing Then
                                                                     Dim unused3 = textBox.Focus()
                                                                     textBox.SelectAll()
                                                                 End If
                                                             End If
                                                         Catch
                                                             ' Ignorar errores de visual tree
                                                         End Try
                                                     End Sub, DispatcherPriority.Background)
            End If
        Catch
            ' Ignorar errores si el visual tree fue destruido
        End Try
    End Sub

    ''' <summary>
    ''' Mueve el foco a la columna apropiada de la siguiente línea y entra en modo edición.
    ''' Issue #258: Para tipoLinea=0 va a Texto, para otros va a Producto.
    ''' </summary>
    Private Sub MoverASiguienteLineaProducto()
        Try
            Dim filaActual = grdLineas.Items.IndexOf(grdLineas.CurrentItem)
            Dim siguienteFila = filaActual + 1

            ' Si no hay siguiente fila, intentar crear una nueva (si CanUserAddRows)
            If siguienteFila >= grdLineas.Items.Count Then
                If grdLineas.CanUserAddRows Then
                    siguienteFila = grdLineas.Items.Count - 1 ' La última fila es la de nueva entrada
                Else
                    Return
                End If
            End If

            grdLineas.SelectedIndex = siguienteFila
            grdLineas.CurrentItem = grdLineas.Items(siguienteFila)

            ' Issue #258: Elegir columna según el tipo de línea
            ' Si ultimoTipoLineaUsado=0 (Texto), ir a columna Texto (3)
            ' Si no, ir a columna Producto (2)
            Dim columnaDestino = If(ultimoTipoLineaUsado = 0, grdLineas.Columns(3), grdLineas.Columns(2))
            grdLineas.CurrentCell = New DataGridCellInfo(grdLineas.Items(siguienteFila), columnaDestino)
            Dim unused = grdLineas.Focus()
            Dim unused2 = grdLineas.BeginEdit()

            ' Enfocar el TextBox dentro de la celda
            Dim unused3 = Dispatcher.BeginInvoke(Sub()
                                                     Try
                                                         Dim cellContent = columnaDestino.GetCellContent(grdLineas.Items(siguienteFila))
                                                         If cellContent IsNot Nothing Then
                                                             Dim textBox = FindVisualChild(Of TextBox)(cellContent)
                                                             If textBox IsNot Nothing Then
                                                                 Dim unused4 = textBox.Focus()
                                                                 textBox.SelectAll()
                                                             End If
                                                         End If
                                                     Catch
                                                         ' Ignorar errores de visual tree
                                                     End Try
                                                 End Sub, DispatcherPriority.Background)
        Catch
            ' Ignorar errores si el visual tree fue destruido
        End Try
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
            Return
        End If

        ' Issue #258: Cuando TipoLinea=0 (Texto), solo permitir editar Tipo y Texto
        ' Esto mejora la UX al hacer evidente que esos campos no aplican a líneas de texto
        If item.tipoLinea.HasValue AndAlso item.tipoLinea.Value = 0 Then
            Dim columnHeader = e.Column.Header?.ToString()
            ' Columnas permitidas para TipoLinea=0: "Tipo" y "Texto"
            Dim columnasPermitidas = {"Tipo", "Texto", "Estado"}
            If Not columnasPermitidas.Contains(columnHeader) Then
                e.Cancel = True
            End If
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
                                                         ' Issue #258: Buscar TextBox en el árbol visual (soporta DataGridTemplateColumn)
                                                         Dim cellContent = grdLineas.CurrentCell.Column.GetCellContent(grdLineas.CurrentCell.Item)
                                                         If cellContent IsNot Nothing Then
                                                             ' Primero intentar cast directo (DataGridTextColumn)
                                                             Dim textBox = TryCast(cellContent, TextBox)
                                                             ' Si no es TextBox directamente, buscar dentro (DataGridTemplateColumn)
                                                             If textBox Is Nothing Then
                                                                 textBox = FindVisualChild(Of TextBox)(cellContent)
                                                             End If
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
