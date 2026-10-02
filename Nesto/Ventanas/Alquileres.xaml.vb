Imports Nesto.ViewModels

' Nesto#490 (4C.3): el ViewModel llega por constructor (antes, ViewModelLocator de Prism).
Partial Public Class Alquileres

    Public Sub New(viewModel As AlquileresViewModel)
        InitializeComponent()
        DataContext = viewModel
    End Sub

End Class
