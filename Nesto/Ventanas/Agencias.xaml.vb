Imports System.Threading.Tasks
Imports Nesto.ViewModels

Public Class Agencias

    Private Sub txtNumeroPedido_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNumeroPedido.GotFocus
        txtNumeroPedido.SelectAll()
    End Sub

    Private Sub txtNumeroBultos_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNumeroBultos.GotFocus
        txtNumeroBultos.SelectAll()
    End Sub

    Private Sub txtNumeroPedido_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNumeroPedido.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub

    Private Sub txtNumeroBultos_KeyUp(sender As Object, e As KeyEventArgs) Handles txtNumeroBultos.KeyUp
        If e.Key = Key.Return Then
            e.OriginalSource.MoveFocus(New TraversalRequest(FocusNavigationDirection.Next))
        End If
    End Sub


    Private Sub txtNombreFiltro_GotFocus(sender As Object, e As RoutedEventArgs) Handles txtNombreFiltro.GotFocus
        txtNombreFiltro.SelectAll()
    End Sub

    Public Sub New()

        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().

    End Sub

    Public Sub New(viewModel As AgenciasViewModel)
        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        Me.DataContext = viewModel
    End Sub

    Private Async Sub Agencias_Loaded(sender As Object, e As RoutedEventArgs) Handles Me.Loaded
        Await CType(Me.DataContext, AgenciasViewModel).cmdCargarDatos.Execute()
    End Sub
End Class
