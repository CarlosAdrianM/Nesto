Imports System.Windows.Controls
Imports System.Windows.Data
Imports System.Windows.Threading

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

End Class
