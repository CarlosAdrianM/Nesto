Imports System.Collections.ObjectModel
Imports System.Globalization
Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Input
Imports System.Windows.Threading
Imports Nesto.ViewModels

' NestoAPI#353: al abrir la ventana por primera vez, el grid del detalle calculaba los
' anchos "*" antes de que la ventana hubiera medido su layout y las columnas quedaban
' estrechas hasta cambiar de remesa. Cada vez que el binding actualiza el ItemsSource
' (el CollectionViewSource regenera la vista al cambiar listaMovimientos) se fuerza el
' recálculo de anchos DESPUÉS del layout (prioridad Loaded del Dispatcher).
Partial Public Class Remesas

    Private Sub GridDetalleRemesa_TargetUpdated(sender As Object, e As DataTransferEventArgs)
        If e.Property IsNot ItemsControl.ItemsSourceProperty Then
            Return
        End If
        Dim unused = Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            Sub()
                For Each columna As DataGridColumn In gridDetalleRemesa.Columns
                    Dim ancho = columna.Width
                    columna.Width = New DataGridLength(0)
                    columna.Width = ancho
                Next
            End Sub)
    End Sub

    ' #2: doble clic en una fila del grid de candidatos -> abrir el Extracto del cliente. El VM
    ' hace la navegación; aquí solo se reenvía la fila pulsada (patrón View -> ViewModel).
    Private Sub GridCandidatos_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs)
        Dim vm = TryCast(DataContext, RemesasViewModel)
        ' Se pasa el SelectedItem como Object (el VM, que referencia Infrastructure, lo castea).
        vm?.AbrirExtractoCliente(gridCandidatos.SelectedItem)
    End Sub

End Class

' #3: suma el Importe de los movimientos de un grupo (fecha de cargo) del detalle de la remesa,
' para mostrar "N efectos - X €" en la cabecera. Mismo patrón que GroupsToTotalConverter de
' Comisiones (los Items del grupo llegan como ReadOnlyObservableCollection(Of Object)).
Public Class SumaImporteGrupoConverter
    Implements IValueConverter

    Private Function Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If TypeOf value Is ReadOnlyObservableCollection(Of Object) Then
            Dim items = CType(value, ReadOnlyObservableCollection(Of Object))
            Dim total As Decimal = 0
            For Each movimiento In items
                total += movimiento.Importe
            Next
            Return total.ToString("c")
        End If
        Return ""
    End Function

    Private Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function
End Class
