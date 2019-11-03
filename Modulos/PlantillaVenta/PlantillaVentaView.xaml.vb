Imports System.Globalization
Imports Nesto.Modulos.PlantillaVenta.PlantillaVentaModel
Imports Xceed.Wpf.Toolkit

Public Class PlantillaVentaView
    Public Sub New(viewModel As PlantillaVentaViewModel)
        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        Me.DataContext = viewModel
    End Sub

    Private Sub SeleccionCliente_Enter(sender As Object, e As RoutedEventArgs) Handles SeleccionCliente.Enter
        Keyboard.Focus(txtFiltroCliente)
    End Sub

    Private Async Sub SeleccionProductos_Enter(sender As Object, e As RoutedEventArgs) Handles SeleccionProductos.Enter
        Await Task.Delay(500)
        Keyboard.Focus(txtFiltroProducto)
    End Sub

    Private Sub txtFiltroProducto_KeyUp(sender As Object, e As KeyEventArgs) Handles txtFiltroProducto.KeyUp
        If e.Key = System.Windows.Input.Key.Enter Then
            txtFiltroProducto.SelectAll()
        End If
        If e.Key = Key.D1 AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso lstProductos.Items.Count > 0 Then
            lstProductos.SelectedItem = lstProductos.Items(0)
        End If
        If e.Key = Key.D2 AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso lstProductos.Items.Count > 1 Then
            lstProductos.SelectedItem = lstProductos.Items(1)
        End If
        If e.Key = Key.D3 AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control AndAlso lstProductos.Items.Count > 2 Then
            lstProductos.SelectedItem = lstProductos.Items(2)
        End If
        If e.Key = Key.OemPlus AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control Then
            If IsNothing(lstProductos.SelectedItem) Then
                lstProductos.SelectedItem = lstProductos.Items(0)
            End If
            Dim linea As LineaPlantillaJson = lstProductos.SelectedItem
            linea.cantidad += 1
        End If
        If e.Key = Key.OemPlus AndAlso e.KeyboardDevice.Modifiers = (ModifierKeys.Control Or ModifierKeys.Shift) Then
            If IsNothing(lstProductos.SelectedItem) Then
                lstProductos.SelectedItem = lstProductos.Items(0)
            End If
            Dim linea As LineaPlantillaJson = lstProductos.SelectedItem
            If linea.aplicarDescuentoFicha Then
                linea.cantidadOferta += 1
            End If
        End If
        If e.Key = Key.D6 AndAlso e.KeyboardDevice.Modifiers = ModifierKeys.Control Then
            If IsNothing(lstProductos.SelectedItem) Then
                lstProductos.SelectedItem = lstProductos.Items(0)
            End If
            Dim linea As LineaPlantillaJson = lstProductos.SelectedItem
            If linea.aplicarDescuentoFicha Then
                linea.cantidad = 6
                linea.cantidadOferta = 1
            End If
        End If
    End Sub

    Private Sub txtFiltroProducto_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltroProducto.GotFocus
        txtFiltroProducto.SelectAll()
    End Sub

    Private Sub txtFiltroProducto_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltroProducto.MouseUp
        txtFiltroProducto.SelectAll()
    End Sub

    Private Sub txtFiltroCliente_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltroCliente.GotFocus
        txtFiltroCliente.SelectAll()
    End Sub

    Private Sub txtFiltroCliente_MouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltroCliente.MouseUp
        txtFiltroCliente.SelectAll()
    End Sub

    Private Sub grdListaProductos_CellEditEnding(sender As Object, e As DataGridCellEditEndingEventArgs) Handles grdListaProductos.CellEditEnding
        If e.Column.Header = "Precio" OrElse e.Column.Header = "% Dto." Then
            Dim linea As LineaPlantillaJson = e.EditingElement.DataContext
            Dim textBox As TextBox = e.EditingElement
            ' Windows debería hacer que el teclado numérico escribiese coma en vez de punto
            ' pero como no lo hace, lo cambiamos nosotros
            textBox.Text = Replace(textBox.Text, ".", ",")
            If e.Column.Header = "Precio" Then
                If Not Double.TryParse(textBox.Text, linea.precio) Then
                    Return
                End If
            Else
                If Not Double.TryParse(textBox.Text, linea.descuento) Then
                    Return
                Else
                    linea.descuento = linea.descuento / 100
                End If
            End If
        End If
    End Sub

    Private Sub grdListaProductos_LoadingRow(sender As Object, e As DataGridRowEventArgs) Handles grdListaProductos.LoadingRow
        If Not IsNothing(DataContext.productoSeleccionado) AndAlso DataContext.productoSeleccionado.producto = e.Row.Item.producto Then
            grdListaProductos.ScrollIntoView(DataContext.productoSeleccionado)
        End If

    End Sub

    Private Sub Plantilla_Loaded(sender As Object, e As RoutedEventArgs) Handles Plantilla.Loaded
        Dim vm As PlantillaVentaViewModel = DataContext
        vm.PaginaActual = SeleccionCliente
        If vm.PaginasWizard.Count = 0 Then
            vm.PaginasWizard.Add(SeleccionCliente)
            vm.PaginasWizard.Add(SeleccionProductos)
            vm.PaginasWizard.Add(SeleccionEntrega)
            vm.PaginasWizard.Add(Finalizar)
        End If
    End Sub

    Private Async Sub btnBuscar_Click(sender As Object, e As RoutedEventArgs) Handles btnBuscar.Click
        Await Task.Delay(500)
        Keyboard.Focus(txtFiltroProducto)
    End Sub
End Class

