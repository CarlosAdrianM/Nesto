Imports Microsoft.Xaml.Behaviors
Imports Prism.Regions

Public Class CloseTabAction
    Inherits TriggerAction(Of Button)

    Protected Overrides Sub Invoke(parameter As Object)
        Dim args = TryCast(parameter, RoutedEventArgs)

        If IsNothing(args) Then
            Return
        End If

        Dim tabItem = FindParent(Of TabItem)(TryCast(args.OriginalSource, DependencyObject))

        If IsNothing(tabItem) Then
            Return
        End If

        Dim tabControl = FindParent(Of TabControl)(tabItem)
        If IsNothing(tabControl) Then
            Return
        End If

        'tabControl.Items.Remove(tabItem.Content)
        Dim region As IRegion = RegionManager.GetObservableRegion(tabControl).Value

        If IsNothing(region) Then
            Return
        End If

        If region.Views.Contains(tabItem.Content) Then
            region.Remove(tabItem.Content)
        End If


    End Sub

    Private Shared Function FindParent(Of T As DependencyObject)(child As DependencyObject) As T
        Dim parentObject As DependencyObject = VisualTreeHelper.GetParent(child)

        If IsNothing(parentObject) Then
            Return Nothing
        End If

        Dim parent = TryCast(parentObject, T)
        If Not IsNothing(parent) Then
            Return parent
        End If

        Return FindParent(Of T)(parentObject)

    End Function
End Class
