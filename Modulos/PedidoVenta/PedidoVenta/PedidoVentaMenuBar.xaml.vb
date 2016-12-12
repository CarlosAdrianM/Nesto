Public Class PedidoVentaMenuBar
    Public Sub New()
        ' Llamada necesaria para el diseñador.
        InitializeComponent()
        ' Agregue cualquier inicialización después de la llamada a InitializeComponent().
    End Sub

    Public Sub New(viewModel As PedidoVentaViewModel)
        ' Llamada necesaria para el diseñador.
        InitializeComponent()

        Me.DataContext = viewModel
    End Sub
End Class
