''' <summary>
''' Nesto#631 (prueba): la botonera de <see cref="DetallePedidoView"/> con WPF-UI. Solo pinta: los comandos, CanExecute y
''' visibilidades son los del DetallePedidoViewModel (heredado de la vista).
''' </summary>
Public Class BotoneraDetallePedido

    Public Sub New()
        ' Antes de InitializeComponent, para que los estilos implícitos de WPF-UI (y el BasedOn de BotonBarra) ya estén
        Resources.MergedDictionaries.Add(RecursosWpfUi.Diccionario)
        TextElement.SetFontSize(Me, RecursosWpfUi.TAMANO_WPF_UI)
        InitializeComponent()
        ' El menú «Más» va en un popup, fuera del árbol de la botonera: sus recursos aparte (el mismo diccionario compartido)
        MenuMas.Resources.MergedDictionaries.Add(RecursosWpfUi.Diccionario)
        TextElement.SetFontSize(MenuMas, RecursosWpfUi.TAMANO_WPF_UI)
    End Sub

    ''' <summary>
    ''' El DropDownButton de WPF-UI 4.3 solo abre su menú al soltar el botón izquierdo del ratón (PreviewMouseLeftButtonUp):
    ''' con el teclado (Espacio o Intro sobre «Más») no hacía nada. Click llega con ratón y con teclado; si el ratón ya lo ha
    ''' abierto, no se hace nada.
    ''' </summary>
    Private Sub BotonMas_Click(sender As Object, e As RoutedEventArgs) Handles BotonMas.Click
        AbrirMenuMas()
    End Sub

    Friend Sub AbrirMenuMas()
        If MenuMas.IsOpen Then
            Return
        End If
        MenuMas.MinWidth = BotonMas.ActualWidth
        MenuMas.PlacementTarget = BotonMas
        MenuMas.Placement = Primitives.PlacementMode.Bottom
        MenuMas.IsOpen = True
    End Sub

End Class
