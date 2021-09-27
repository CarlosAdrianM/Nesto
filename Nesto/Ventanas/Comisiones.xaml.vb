Imports System.Collections.ObjectModel
Imports System.Globalization
Imports Nesto.ViewModels

Public Class Comisiones
    Public Sub New(viewModel As ComisionesViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
        Me.DataContext = viewModel

        ' Ponemos el foco en el filtro
        'txtFiltro.Focus()
    End Sub

    Private Sub dgrFamilias_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrFamilias.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))
        If IsNothing(src) Then
            Return
        End If
        If src.[GetType]() = GetType(ScrollContentPresenter) Then
            DataContext.cmdAbrirPedido.Execute(dgrFamilias.SelectedItem)
        End If
    End Sub

    Private Sub dgrFechas_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrFechas.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))
        If IsNothing(src) Then
            Return
        End If
        If src.[GetType]() = GetType(ScrollContentPresenter) Then
            DataContext.cmdAbrirPedido.Execute(dgrFechas.SelectedItem)
        End If
    End Sub

    Private Sub dgrGrupos_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrGrupos.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))
        If IsNothing(src) Then
            Return
        End If
        If src.[GetType]() = GetType(ScrollContentPresenter) Then
            DataContext.cmdAbrirPedido.Execute(dgrGrupos.SelectedItem)
        End If
    End Sub

    Private Sub dgrPendientesEntregar_MouseDoubleClick(sender As Object, e As MouseButtonEventArgs) Handles dgrPendientesEntregar.MouseDoubleClick
        Dim src As DependencyObject = VisualTreeHelper.GetParent(DirectCast(e.OriginalSource, DependencyObject))
        If IsNothing(src) Then
            Return
        End If
        If src.[GetType]() = GetType(ScrollContentPresenter) Then
            DataContext.cmdAbrirPedido.Execute(dgrPendientesEntregar.SelectedItem)
        End If
    End Sub

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
                total += gi.Base_Imponible
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
                    total += item.Base_Imponible
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