Imports System.Globalization

Public Class PedidoVentaView
    Public Sub New(viewModel As PedidoVentaViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        viewModel.cmdCargarListaPedidos.Execute(Nothing)
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco en el filtro
        txtFiltro.Focus()
    End Sub

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
            DataContext.cmdActualizarTotales.Execute(Nothing)
            actualizarTotales = False
        End If
    End Sub

    Private Sub txtFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltro.GotFocus
        txtFiltro.SelectAll()
    End Sub

    Private Sub txtFiltro_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltro.PreviewMouseUp
        txtFiltro.SelectAll()
    End Sub

    Private Sub txtFiltro_KeyUp(sender As Object, e As KeyEventArgs) Handles txtFiltro.KeyUp
        If e.Key = System.Windows.Input.Key.Enter Then
            Dim Binding = txtFiltro.GetBindingExpression(TextBox.TextProperty)
            Binding.UpdateSource()
            txtFiltro.SelectAll()
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
