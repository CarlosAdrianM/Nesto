Imports System.Windows
Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports Nesto.Modulos.PedidoVenta.Models.Facturas
Imports Prism.Services.Dialogs

Public Class ErroresFacturacionRutasPopup

    Private Sub BtnCerrar_Click(sender As Object, e As RoutedEventArgs)
        ' Cerrar el diálogo usando el ViewModel
        Dim viewModel = TryCast(DataContext, ErroresFacturacionRutasPopupViewModel)
        If viewModel IsNot Nothing Then
            viewModel.Cerrar()
        End If
    End Sub

    Private Sub DataGrid_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
        System.Diagnostics.Debug.WriteLine("=== DataGrid_MouseDoubleClick ejecutado ===")

        Dim dataGrid As DataGrid = DirectCast(sender, DataGrid)
        Dim errorSeleccionado As PedidoConErrorDTO = TryCast(dataGrid.SelectedItem, PedidoConErrorDTO)

        System.Diagnostics.Debug.WriteLine($"Item seleccionado: {If(errorSeleccionado Is Nothing, "Nothing", $"Pedido {errorSeleccionado.NumeroPedido}")}")

        If Not IsNothing(errorSeleccionado) AndAlso Not IsNothing(DataContext) Then
            System.Diagnostics.Debug.WriteLine($"Ejecutando comando cmdAbrirPedido para pedido {errorSeleccionado.NumeroPedido}")
            Try
                Dim viewModel = DirectCast(DataContext, ErroresFacturacionRutasPopupViewModel)
                If viewModel.cmdAbrirPedido.CanExecute(errorSeleccionado) Then
                    viewModel.cmdAbrirPedido.Execute(errorSeleccionado)
                    System.Diagnostics.Debug.WriteLine("Comando ejecutado correctamente")
                Else
                    System.Diagnostics.Debug.WriteLine("ERROR: El comando CanExecute devolvió False")
                End If
            Catch ex As Exception
                System.Diagnostics.Debug.WriteLine($"ERROR al ejecutar comando: {ex.Message}")
                System.Diagnostics.Debug.WriteLine($"StackTrace: {ex.StackTrace}")
            End Try
        Else
            If IsNothing(errorSeleccionado) Then
                System.Diagnostics.Debug.WriteLine("ERROR: No hay item seleccionado")
            End If
            If IsNothing(DataContext) Then
                System.Diagnostics.Debug.WriteLine("ERROR: DataContext es Nothing")
            End If
        End If
    End Sub

    Private Sub CopiarErrorCompleto_Click(sender As Object, e As RoutedEventArgs)
        ' Copiar todos los detalles del error al portapapeles
        Dim menuItem As MenuItem = DirectCast(sender, MenuItem)
        Dim contextMenu As ContextMenu = DirectCast(menuItem.Parent, ContextMenu)
        Dim dataGrid As DataGrid = DirectCast(contextMenu.PlacementTarget, DataGrid)
        Dim errorSeleccionado As PedidoConErrorDTO = TryCast(dataGrid.SelectedItem, PedidoConErrorDTO)

        If Not IsNothing(errorSeleccionado) Then
            Dim textoCompleto As String = String.Format(
                "Pedido: {0}" & vbCrLf &
                "Cliente: {1} ({2})" & vbCrLf &
                "Ruta: {3}" & vbCrLf &
                "Periodo: {4}" & vbCrLf &
                "Fecha Entrega: {5:dd/MM/yyyy}" & vbCrLf &
                "Total: {6:N2} €" & vbCrLf &
                "Tipo de Error: {7}" & vbCrLf &
                "Mensaje: {8}",
                errorSeleccionado.NumeroPedido,
                errorSeleccionado.Cliente,
                errorSeleccionado.NombreCliente,
                errorSeleccionado.Ruta,
                errorSeleccionado.PeriodoFacturacion,
                errorSeleccionado.FechaEntrega,
                errorSeleccionado.Total,
                errorSeleccionado.TipoError,
                errorSeleccionado.MensajeError)

            Clipboard.SetText(textoCompleto)
        End If
    End Sub

    Private Sub CopiarSoloMensaje_Click(sender As Object, e As RoutedEventArgs)
        ' Copiar solo el mensaje de error al portapapeles
        Dim menuItem As MenuItem = DirectCast(sender, MenuItem)
        Dim contextMenu As ContextMenu = DirectCast(menuItem.Parent, ContextMenu)
        Dim dataGrid As DataGrid = DirectCast(contextMenu.PlacementTarget, DataGrid)
        Dim errorSeleccionado As PedidoConErrorDTO = TryCast(dataGrid.SelectedItem, PedidoConErrorDTO)

        If Not IsNothing(errorSeleccionado) AndAlso Not String.IsNullOrEmpty(errorSeleccionado.MensajeError) Then
            Clipboard.SetText(errorSeleccionado.MensajeError)
        End If
    End Sub

    Private Sub CopiarNumeroPedido_Click(sender As Object, e As RoutedEventArgs)
        ' Copiar solo el número de pedido al portapapeles
        Dim menuItem As MenuItem = DirectCast(sender, MenuItem)
        Dim contextMenu As ContextMenu = DirectCast(menuItem.Parent, ContextMenu)
        Dim dataGrid As DataGrid = DirectCast(contextMenu.PlacementTarget, DataGrid)
        Dim errorSeleccionado As PedidoConErrorDTO = TryCast(dataGrid.SelectedItem, PedidoConErrorDTO)

        If Not IsNothing(errorSeleccionado) Then
            Clipboard.SetText(errorSeleccionado.NumeroPedido.ToString())
        End If
    End Sub

End Class
