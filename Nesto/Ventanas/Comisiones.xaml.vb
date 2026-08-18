Imports System.Collections.ObjectModel
Imports System.Globalization
Imports Nesto.Models
Imports Nesto.ViewModels

Partial Public Class Comisiones
    Public Sub New(viewModel As ComisionesViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco en el filtro
        'txtFiltro.Focus()
    End Sub

    Private Sub dgrFamilias_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrFamilias.MouseDoubleClick
        If EsDobleClickSobreElCuerpo(e.OriginalSource) Then
            DataContext.cmdAbrirPedido.Execute(dgrFamilias.SelectedItem)
        End If
    End Sub

    Private Sub dgrFechas_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrFechas.MouseDoubleClick
        If EsDobleClickSobreElCuerpo(e.OriginalSource) Then
            DataContext.cmdAbrirPedido.Execute(dgrFechas.SelectedItem)
        End If
    End Sub

    Private Sub dgrGrupos_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrGrupos.MouseDoubleClick
        If EsDobleClickSobreElCuerpo(e.OriginalSource) Then
            DataContext.cmdAbrirPedido.Execute(dgrGrupos.SelectedItem)
        End If
    End Sub

    Private Sub dgrPendientesEntregar_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrPendientesEntregar.MouseDoubleClick
        If EsDobleClickSobreElCuerpo(e.OriginalSource) Then
            DataContext.cmdAbrirPedido.Execute(dgrPendientesEntregar.SelectedItem)
        End If
    End Sub

    ' Nesto#435: el doble clic puede caer sobre un Run (el texto de la celda), que es un
    ' ContentElement y NO un Visual: VisualTreeHelper.GetParent lanza InvalidOperationException
    ' y tiraba la aplicación (ELMAH 26/07 ×2). ArbolVisualHelper admite ambos mundos.
    Private Shared Function EsDobleClickSobreElCuerpo(originalSource As Object) As Boolean
        Dim padre As DependencyObject = Nesto.Infrastructure.Shared.ArbolVisualHelper.ObtenerPadreSeguro(originalSource)
        Return padre IsNot Nothing AndAlso padre.GetType() = GetType(ScrollContentPresenter)
    End Function

    Private Async Sub Comisiones_Loaded(sender As Object, e As RoutedEventArgs) Handles MyBase.Loaded
        Dim viewModel As ComisionesViewModel = CType(Me.DataContext, ComisionesViewModel)
        ' Ponemos e IF para que no entre cada vez que coja el foco
        If IsNothing(viewModel.vendedorActual) AndAlso IsNothing(viewModel.listaVendedores) Then
            Await viewModel.CargarDatos()
        End If
    End Sub
End Class

Public Class GroupsToTotalConverter
    Implements IValueConverter

    Private Function IValueConverter_Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If TypeOf value Is ReadOnlyObservableCollection(Of Object) Then
            Dim items = CType(value, ReadOnlyObservableCollection(Of Object))
            Dim total As Decimal = 0

            For Each gi In items
                total += gi.BaseImponible
            Next

            Return total.ToString("c")
        End If

        Return ""
    End Function

    Private Function IValueConverter_ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function
End Class

Public Class GroupsToTotalConverterTwoLevels
    Implements IValueConverter

    Private Function IValueConverter_Convert(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.Convert
        If TypeOf value Is ReadOnlyObservableCollection(Of Object) Then
            Dim items = CType(value, ReadOnlyObservableCollection(Of Object))
            Dim total As Decimal = 0

            For Each gi In items
                For Each item In gi.Items
                    total += item.BaseImponible
                Next
            Next

            Return total.ToString("c")
        End If

        Return ""
    End Function

    Private Function IValueConverter_ConvertBack(value As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return value
    End Function
End Class

