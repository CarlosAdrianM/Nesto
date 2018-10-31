Imports Nesto.ViewModels

Public Class PlanesVentajas

    Public Sub New(viewModel As PlanesVentajasViewModel)
        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        Me.DataContext = viewModel
    End Sub


    Private Sub txtFiltro_KeyUp(sender As Object, e As KeyEventArgs) Handles txtFiltro.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub

    Private Sub txtFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtFiltro.GotFocus
        txtFiltro.SelectAll()
    End Sub

    Private Async Function Planes_LoadedAsync(sender As Object, e As RoutedEventArgs) As System.Threading.Tasks.Task Handles Planes.Loaded
        Dim viewModel As PlanesVentajasViewModel = CType(Me.DataContext, PlanesVentajasViewModel)
        ' Ponemos e IF para que no entre cada vez que coja el foco
        If IsNothing(viewModel.listaEmpresas) Then
            Await viewModel.CargarDatos
        End If
    End Function
End Class
