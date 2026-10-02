Imports Nesto.ViewModels

' Nesto#490 (4C.3): el ViewModel llega por constructor (antes, ViewModelLocator de Prism).
Partial Public Class FamiliasMantenimiento

    Public Sub New(viewModel As FamiliasMantenimientoViewModel)
        InitializeComponent()
        DataContext = viewModel
    End Sub

End Class
