Imports Microsoft.Xaml.Behaviors
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Navegacion
Imports Prism.Ioc

Public Class CloseTabAction
    Inherits TriggerAction(Of Button)

    ' Nesto#490 (4C.4, shell): la región de la que se quita la pestaña. Antes se sacaba de Prism a partir del TabControl
    ' (RegionManager.GetObservableRegion); la X solo está en las pestañas de MainRegion (MainWindow).
    Public Property NombreRegion As String = CierrePestanas.REGION_PRINCIPAL

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


        ' Pregunta al ViewModel si quiere confirmar el cierre (ITabCloseConfirmation) y, si se puede, quita la vista de la región
        Dim navegacion = Nesto.Infrastructure.Contracts.Contenedor.Actual.Resolver(Of IServicioNavegacion)()
        Dim unused = CierrePestanas.Cerrar(navegacion, NombreRegion, tabItem.Content)
    End Sub

    Private Shared Function FindParent(Of T As DependencyObject)(child As DependencyObject) As T
        Dim parentObject As DependencyObject = VisualTreeHelper.GetParent(child)

        If IsNothing(parentObject) Then
            Return Nothing
        End If

        Dim parent = TryCast(parentObject, T)
        Return If(Not IsNothing(parent), parent, FindParent(Of T)(parentObject))

    End Function
End Class
