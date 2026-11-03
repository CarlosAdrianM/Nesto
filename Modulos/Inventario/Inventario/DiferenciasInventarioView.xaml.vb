''' <summary>
''' Nesto#597 (NestoAPI#790): «Diferencias de inventario». La lógica, en <see cref="DiferenciasInventarioViewModel"/>, que se
''' lee a sí mismo al llegar (IReceptorNavegacionPestanaNueva).
''' </summary>
Public Class DiferenciasInventarioView
    Public Sub New(viewModel As DiferenciasInventarioViewModel)
        InitializeComponent()
        DataContext = viewModel
    End Sub
End Class
