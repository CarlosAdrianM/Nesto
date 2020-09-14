Imports Prism.Mvvm
Imports Prism.Regions

Public Class ViewModelBase
    Inherits BindableBase
    Implements INavigationAware

    Private _titulo As String
    Public Property Titulo As String
        Get
            Return _titulo
        End Get
        Set(ByVal value As String)
            SetProperty(_titulo, value)
        End Set
    End Property

    Public Sub OnNavigatedFrom(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedFrom

    End Sub

    Public Sub OnNavigatedTo(navigationContext As NavigationContext) Implements INavigationAware.OnNavigatedTo

    End Sub

    Public Overridable Function IsNavigationTarget(navigationContext As NavigationContext) As Boolean Implements INavigationAware.IsNavigationTarget
        Return False
    End Function
End Class
