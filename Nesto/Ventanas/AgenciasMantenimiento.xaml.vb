Imports Nesto.ViewModels

' Nesto#490 (4C.3): el ViewModel llega por constructor (antes, ViewModelLocator de Prism).
Partial Public Class AgenciasMantenimiento

    Public Sub New(viewModel As AgenciasMantenimientoViewModel)
        InitializeComponent()
        DataContext = viewModel
    End Sub

End Class
