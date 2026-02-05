Imports System.ComponentModel
Imports System.Windows.Threading

Partial Public Class PlantillaVentaView
    Public Sub New()
        ' Llamada necesaria para el diseñador.
        InitializeComponent()
    End Sub

    Private Sub SeleccionCliente_Enter(sender As Object, e As RoutedEventArgs) Handles SeleccionCliente.Enter
        Dim unused = Keyboard.Focus(txtFiltroCliente)
    End Sub

    Private Async Sub SeleccionProductos_Enter(sender As Object, e As RoutedEventArgs) Handles SeleccionProductos.Enter
        Await Task.Delay(2000)
        ' Usar el método del control para enfocar la barra de filtro integrada
        lstProductos.EnfocarBarraFiltro()
    End Sub

    Private Sub txtFiltroCliente_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltroCliente.GotFocus
        txtFiltroCliente.SelectAll()
    End Sub

    Private Sub txtFiltroCliente_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltroCliente.MouseUp
        txtFiltroCliente.SelectAll()
    End Sub

    ' Issue #266: Entrar en modo edición con un solo clic para DataGridTemplateColumn
    Private Sub DataGridCell_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
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

    ' Issue #266: Seleccionar todo el texto cuando el TextBox se carga (al entrar en modo edición)
    Private Sub TextBox_Loaded(sender As Object, e As RoutedEventArgs)
        Dim textBox As TextBox = TryCast(sender, TextBox)
        If textBox IsNot Nothing Then
            Dim unused = textBox.Focus()
            textBox.SelectAll()
        End If
    End Sub

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

    Private Sub grdListaProductos_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs) Handles grdListaProductos.CellEditEnding
        ' Issue #266: Solo procesar la columna Precio aquí
        ' La columna % Dto. usa DataGridTemplateColumn con PercentageConverter que ya maneja la conversión
        If e.Column.Header = "Precio" Then
            Dim linea As LineaPlantillaVenta = e.EditingElement.DataContext
            ' Buscar el TextBox dentro del elemento de edición
            Dim textBox As TextBox = TryCast(e.EditingElement, TextBox)
            If textBox Is Nothing Then
                ' Para DataGridTemplateColumn, buscar dentro del ContentPresenter
                Dim contentPresenter = TryCast(e.EditingElement, ContentPresenter)
                If contentPresenter IsNot Nothing Then
                    textBox = FindVisualChild(Of TextBox)(contentPresenter)
                End If
            End If

            If textBox IsNot Nothing Then
                ' Windows debería hacer que el teclado numérico escribiese coma en vez de punto
                ' pero como no lo hace, lo cambiamos nosotros
                textBox.Text = Replace(textBox.Text, ".", ",")
                Dim unused = Double.TryParse(textBox.Text, (linea.precio))
            End If
        End If

        ' Issue #266: Actualizar totales cuando se modifica Precio o % Dto.
        ' No llamamos a cmdActualizarProductosPedido porque recarga el precio/descuento del servidor
        ' Solo necesitamos refrescar los totales en la UI
        ' IMPORTANTE: Usamos Dispatcher.BeginInvoke porque CellEditEnding se dispara ANTES de que
        ' el binding actualice el valor. Si llamamos ActualizarTotales inmediatamente, la UI
        ' se refresca con el valor antiguo.
        If e.Column.Header = "Precio" OrElse e.Column.Header = "% Dto." Then
            Dim vm As PlantillaVentaViewModel = DataContext
            Dim unused = Dispatcher.BeginInvoke(Sub() vm.ActualizarTotales(), DispatcherPriority.Background)
        End If
    End Sub

    Private Shared Function FindVisualChild(Of T As DependencyObject)(parent As DependencyObject) As T
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

    Private Sub grdListaProductos_LoadingRow(sender As Object, e As DataGridRowEventArgs) Handles grdListaProductos.LoadingRow
        Dim vm As PlantillaVentaViewModel = DataContext
        If Not IsNothing(vm.ListaFiltrableProductos) AndAlso Not IsNothing(vm.ListaFiltrableProductos.ElementoSeleccionado) AndAlso CType(vm.ListaFiltrableProductos.ElementoSeleccionado, LineaPlantillaVenta).producto = e.Row.Item.producto Then
            grdListaProductos.ScrollIntoView(CType(vm.ListaFiltrableProductos.ElementoSeleccionado, LineaPlantillaVenta))
        End If

    End Sub

    Private Sub Plantilla_Loaded(sender As Object, e As RoutedEventArgs) Handles Plantilla.Loaded
        Dim vm As PlantillaVentaViewModel = DataContext
        If vm.PaginasWizard.Count = 0 Then
            vm.PaginaActual = SeleccionCliente
            vm.PaginasWizard.Add(SeleccionCliente)
            vm.PaginasWizard.Add(SeleccionProductos)
            vm.PaginasWizard.Add(SeleccionEntrega)
            vm.PaginasWizard.Add(Finalizar)
        End If
        ' Usar la BarraFiltro expuesta del control
        IndicadorOcupado.FocusAfterBusy = lstProductos.BarraFiltro

        ' Issue #94: Suscribirse a cambios para actualizar navegación del wizard
        AddHandler vm.PropertyChanged, AddressOf ViewModel_PropertyChanged
        ' Actualizar NextPage inicial
        ActualizarNavegacionPaginaProductos()
    End Sub

    ''' <summary>
    ''' Issue #94: Actualiza el NextPage de SeleccionProductos según si hay Ganavisiones disponibles.
    ''' Si no hay Ganavisiones, salta directamente a SeleccionEntrega.
    ''' </summary>
    Private Sub ViewModel_PropertyChanged(sender As Object, e As PropertyChangedEventArgs)
        If e.PropertyName = NameOf(PlantillaVentaViewModel.HayGanavisionesDisponibles) Then
            ActualizarNavegacionPaginaProductos()
        End If
    End Sub

    Private Sub ActualizarNavegacionPaginaProductos()
        Dim vm As PlantillaVentaViewModel = DataContext
        If vm IsNot Nothing Then
            If vm.HayGanavisionesDisponibles Then
                ' Con Ganavisiones: Productos -> Regalos -> Entrega
                SeleccionProductos.NextPage = SeleccionRegalos
                SeleccionEntrega.PreviousPage = SeleccionRegalos
            Else
                ' Sin Ganavisiones: Productos -> Entrega (salta Regalos)
                SeleccionProductos.NextPage = SeleccionEntrega
                SeleccionEntrega.PreviousPage = SeleccionProductos
            End If
        End If
    End Sub

    Private Sub OpcionesBusqueda_Click(sender As Object, e As RoutedEventArgs)
        Dim btn = TryCast(sender, Button)
        If btn IsNot Nothing AndAlso btn.ContextMenu IsNot Nothing Then
            btn.ContextMenu.PlacementTarget = btn
            btn.ContextMenu.IsOpen = True
        End If
    End Sub

End Class
