Imports System.Globalization
Imports Nesto.Modulos.PlantillaVenta.PlantillaVentaModel

Public Class PlantillaVentaView
    Public Sub New(viewModel As PlantillaVentaViewModel)
        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        Me.DataContext = viewModel
    End Sub

    Private Sub Wizard_Finish(sender As Object, e As RoutedEventArgs)
        Me.DataContext.cmdCrearPedido.Execute(Nothing)
    End Sub

    Private Sub SeleccionCliente_Enter(sender As Object, e As RoutedEventArgs) Handles SeleccionCliente.Enter
        Keyboard.Focus(txtFiltroCliente)
    End Sub

    Private Sub SeleccionProductos_Enter(sender As Object, e As RoutedEventArgs) Handles SeleccionProductos.Enter
        Keyboard.Focus(txtFiltroProducto)
    End Sub

    Private Sub txtFiltroProducto_KeyUp(sender As Object, e As KeyEventArgs) Handles txtFiltroProducto.KeyUp
        If e.Key = System.Windows.Input.Key.Enter Then
            txtFiltroProducto.SelectAll()
        End If
    End Sub

    Private Sub txtFiltroProducto_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltroProducto.GotFocus
        txtFiltroProducto.SelectAll()
    End Sub

    Private Sub listViewClientes_SelectionChanged(sender As Object, e As SelectionChangedEventArgs) Handles listViewClientes.SelectionChanged
        WizardPlantilla.CurrentPage = SeleccionProductos
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

            DataContext.cmdComprobarCondicionesPrecio.Execute(linea)
        End If
    End Sub


End Class