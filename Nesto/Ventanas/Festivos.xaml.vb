Imports Nesto.ViewModels

' Nesto#616 (NestoAPI#779): el ViewModel llega por constructor, como FamiliasMantenimiento.
Partial Public Class Festivos

    Public Sub New(viewModel As FestivosViewModel)
        InitializeComponent()
        DataContext = viewModel
    End Sub

End Class
