

Partial Public Class ControlPedidos
    Partial Public Class ControlPedidosDataTable
        Private Sub ControlPedidosDataTable_ColumnChanging(sender As Object, e As DataColumnChangeEventArgs) Handles Me.ColumnChanging
            If (e.Column.ColumnName = Me.FamiliaColumn.ColumnName) Then
                'Agregar código de usuario aquí
            End If

        End Sub

    End Class
End Class
