Imports System.Collections
Imports System.ComponentModel
Imports System.Globalization
Imports System.Windows.Data
Imports Nesto.Modulos.Rapports

Partial Public Class ListaRapportsView
    Public Sub New()

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub

    Private Async Sub ListaRapportsView_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Dim unused1 = DataContext.cmdCargarListaRapports.Execute(Nothing)
        Dim unused = txtSelectorCliente.Focus()
    End Sub

    Private Sub txtFiltro_KeyEnterUpdate(sender As Object, e As KeyEventArgs) Handles txtFiltro.KeyUp
        If e.Key = Key.Enter Then
            Dim tBox As TextBox = CType(sender, TextBox)
            Dim prop As DependencyProperty = TextBox.TextProperty
            Dim binding As BindingExpression = BindingOperations.GetBindingExpression(tBox, prop)
            binding?.UpdateSource()
        End If
    End Sub

    Private Sub txtFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltro.GotFocus
        txtFiltro.SelectAll()
    End Sub

    Private Sub txtFiltro_PreviewMouseUp(sender As Object, e As MouseButtonEventArgs) Handles txtFiltro.PreviewMouseUp
        txtFiltro.SelectAll()
    End Sub

    Private Sub OnResumenVentasDoubleClick(sender As Object, e As MouseButtonEventArgs)
        Dim dg = TryCast(sender, DataGrid)
        If dg Is Nothing OrElse dg.SelectedItem Is Nothing Then Exit Sub
        Dim item = TryCast(dg.SelectedItem, VentaClienteResumenDTO)
        If item Is Nothing Then Exit Sub
        Dim vm = TryCast(DataContext, ListaRapportsViewModel)
        If vm IsNot Nothing AndAlso vm.VerDetalleVentasCommand.CanExecute(item) Then
            vm.VerDetalleVentasCommand.Execute(item)
        End If
    End Sub

    Private Sub OnDetalleVentasDoubleClick(sender As Object, e As MouseButtonEventArgs)
        Dim dg = TryCast(sender, DataGrid)
        If dg Is Nothing OrElse dg.SelectedItem Is Nothing Then Exit Sub
        Dim item = TryCast(dg.SelectedItem, VentaClienteResumenDTO)
        If item Is Nothing Then Exit Sub
        Dim vm = TryCast(DataContext, ListaRapportsViewModel)
        If vm IsNot Nothing AndAlso vm.AbrirFichaProductoCommand.CanExecute(item) Then
            vm.AbrirFichaProductoCommand.Execute(item)
        End If
    End Sub

    Private Sub OnVentasSorting(sender As Object, e As DataGridSortingEventArgs)
        e.Handled = True
        Dim dg = DirectCast(sender, DataGrid)
        Dim column = e.Column

        Dim direction = If(column.SortDirection <> ListSortDirection.Ascending,
                           ListSortDirection.Ascending,
                           ListSortDirection.Descending)
        column.SortDirection = direction

        For Each col In dg.Columns
            If col IsNot column Then col.SortDirection = Nothing
        Next

        Dim view = TryCast(CollectionViewSource.GetDefaultView(dg.ItemsSource), ListCollectionView)
        If view IsNot Nothing Then
            view.CustomSort = New TotalUltimoComparer(column.SortMemberPath, direction)
        End If
    End Sub
End Class

Public Class TotalUltimoComparer
    Implements IComparer

    Private ReadOnly _propertyName As String
    Private ReadOnly _direction As ListSortDirection

    Public Sub New(propertyName As String, direction As ListSortDirection)
        _propertyName = propertyName
        _direction = direction
    End Sub

    Public Function Compare(x As Object, y As Object) As Integer Implements IComparer.Compare
        Dim itemX = TryCast(x, VentaClienteResumenDTO)
        Dim itemY = TryCast(y, VentaClienteResumenDTO)
        If itemX Is Nothing OrElse itemY Is Nothing Then Return 0

        If itemX.Nombre = "TOTAL" Then Return 1
        If itemY.Nombre = "TOTAL" Then Return -1

        Dim valueX = GetPropertyValue(itemX)
        Dim valueY = GetPropertyValue(itemY)

        Dim result = Comparer.Default.Compare(valueX, valueY)
        Return If(_direction = ListSortDirection.Descending, -result, result)
    End Function

    Private Function GetPropertyValue(item As VentaClienteResumenDTO) As Object
        Select Case _propertyName
            Case "Nombre" : Return item.Nombre
            Case "UnidadesAnnoActual" : Return item.UnidadesAnnoActual
            Case "UnidadesAnnoAnterior" : Return item.UnidadesAnnoAnterior
            Case "VentaAnnoActual" : Return item.VentaAnnoActual
            Case "VentaAnnoAnterior" : Return item.VentaAnnoAnterior
            Case "Diferencia" : Return item.Diferencia
            Case "Ratio" : Return item.Ratio
            Case Else : Return item.Nombre
        End Select
    End Function
End Class

Public Class StringEqualityConverter
    Implements IMultiValueConverter

    Public Function Convert(values() As Object, targetType As Type, parameter As Object, culture As CultureInfo) As Object Implements IMultiValueConverter.Convert
        Return values.Length >= 2 AndAlso values(0) IsNot Nothing AndAlso values(1) IsNot Nothing AndAlso values(0).ToString() = values(1).ToString()
    End Function

    Public Function ConvertBack(value As Object, targetTypes() As Type, parameter As Object, culture As CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Return targetTypes.Select(Function(t) Binding.DoNothing).ToArray()
    End Function
End Class
