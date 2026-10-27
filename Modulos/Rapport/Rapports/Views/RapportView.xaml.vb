Imports System.Windows.Controls
Imports System.Windows.Input
Imports System.Windows.Media
Imports Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO

Public Class RapportView

    Public Sub New(viewModel As RapportViewModel)

        ' Esta llamada es exigida por el diseñador.
        InitializeComponent()
        DataContext = viewModel
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub

    ' Nesto#558: los chips de resultado son una ListBox de selección única; una ListBox no desmarca al volver a pulsar el
    ' marcado, así que lo hacemos aquí (solo vista: no toca el ViewModel, el binding de SelectedValue lleva el Nothing).
    Private Sub lstResultadoRapport_PreviewMouseLeftButtonDown(sender As Object, e As MouseButtonEventArgs)
        Dim lista As ListBox = TryCast(sender, ListBox)
        If lista Is Nothing OrElse e Is Nothing Then
            Return
        End If
        Dim elemento As ListBoxItem = BuscarElemento(TryCast(e.OriginalSource, DependencyObject))
        If elemento IsNot Nothing AndAlso elemento.IsSelected Then
            lista.SelectedItem = Nothing
            e.Handled = True
        End If
    End Sub

    ' Supr o Retroceso sobre los chips: sin resultado.
    Private Sub lstResultadoRapport_PreviewKeyDown(sender As Object, e As KeyEventArgs)
        Dim lista As ListBox = TryCast(sender, ListBox)
        If lista Is Nothing OrElse e Is Nothing Then
            Return
        End If
        If e.Key = Key.Delete OrElse e.Key = Key.Back Then
            lista.SelectedItem = Nothing
            e.Handled = True
        End If
    End Sub

    Private Shared Function BuscarElemento(origen As DependencyObject) As ListBoxItem
        Dim actual As DependencyObject = origen
        While actual IsNot Nothing AndAlso TypeOf actual IsNot ListBoxItem AndAlso TypeOf actual IsNot ListBox
            actual = If(TypeOf actual Is Visual OrElse TypeOf actual Is System.Windows.Media.Media3D.Visual3D,
                        VisualTreeHelper.GetParent(actual),
                        LogicalTreeHelper.GetParent(actual))
        End While
        Return TryCast(actual, ListBoxItem)
    End Function
End Class
