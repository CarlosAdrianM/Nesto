﻿Imports System.Globalization

Public Class DetallePedidoView
    Private actualizarTotales As Boolean = False

    Public Sub New(viewModel As DetallePedidoViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco en el filtro
    End Sub

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

        Dim currentRowIndex = grdLineas.Items.IndexOf(grdLineas.CurrentItem)
        If (e.Key = 156 OrElse e.Key = 120) AndAlso currentRowIndex > 1 Then ' Alt + C
            grdLineas.CurrentCell = New DataGridCellInfo(grdLineas.Items(currentRowIndex - 1), grdLineas.Columns(4)) ' Cantidad
        End If



    End Sub


    Private Sub grdLineas_PreviewKeyDown(sender As Object, e As KeyEventArgs) Handles grdLineas.PreviewKeyDown
        If e.Key = Key.Enter Then
            If grdLineas.CurrentColumn.Header = "Cantidad" Then
                grdLineas.CurrentColumn = grdLineas.Columns(2) ' Producto
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


End Class