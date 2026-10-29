Imports System.Reflection
Imports System.Runtime.CompilerServices
Imports System.Threading

''' <summary>
''' Nesto#490 (panel lateral, tramo 1). Hallazgo de la prueba previa (spike): el primer icono de WPF-UI (SymbolIcon, que
''' usa el propio NavigationView en su plantilla) crea un Wpf.Ui.Controls.TextBlock, y su constructor estático cambia PARA
''' TODO EL PROCESO el tamaño por defecto de System.Windows.Controls.TextBlock: de 12 (el de Windows) a 14. No es un estilo
''' (aislarlo en el panel no sirve): es metadata del tipo (TextBlockMetadata, con OverrideMetadata).
''' <para>
''' Nesto#560: la primera compensación (dar a cada ventana un tamaño LOCAL de 12 para que lo heredaran) NO bastaba. WPF
''' guarda como «por defecto» (no como heredado) el valor que llega a un Control cuando coincide con SU defecto (12), y un
''' TextBlock cuyo padre está «por defecto» no hereda: usa su propio defecto, 14. Así que por debajo del primer Control (el
''' TabControl MainRegion, cada UserControl, Button, DataGrid, DataGridCell...) los textos salían a 14: con el panel lateral
''' las rejillas, las pestañas y las etiquetas eran más grandes y las filas más altas (~22 px frente a ~19).
''' </para>
''' <para>
''' Ahora se deshace el cambio en su origen: se fuerza el constructor estático de WPF-UI y se devuelve el defecto de
''' TextBlock al de Windows (SystemFonts.MessageFontSize), así todo se ve EXACTAMENTE como con la cinta. El panel, que sí
''' quiere el 14 de WPF-UI, lo fija para lo suyo (PanelNavegacionLateral). Si un día WPF cambiara por dentro y no se pudiera
''' (no hay API pública para cambiar un defecto ya sellado), queda la compensación antigua, mejor que nada.
''' </para>
''' </summary>
Public NotInheritable Class CompensacionTextoWpfUi

    ''' <summary>El tamaño de letra que usa el panel lateral (el de los controles de WPF-UI).</summary>
    Public Const TAMANO_WPF_UI As Double = 14

    Private Shared _registrada As Integer

    Private Sub New()
    End Sub

    ''' <summary>
    ''' Deja el tamaño por defecto de TextBlock como sin WPF-UI. Si no puede, fija el tamaño en la ventana principal y
    ''' registra (una sola vez) el resto, como antes.
    ''' </summary>
    Public Shared Sub Aplicar(ventanaPrincipal As Window)
        If RestaurarTamanoPorDefecto() Then
            Return
        End If
        If ventanaPrincipal IsNot Nothing Then
            FijarSiEsPorDefecto(ventanaPrincipal)
        End If
        If Interlocked.Exchange(_registrada, 1) = 1 Then
            Return
        End If
        EventManager.RegisterClassHandler(GetType(Window), FrameworkElement.LoadedEvent, New RoutedEventHandler(AddressOf AlCargarseOAbrirse))
        EventManager.RegisterClassHandler(GetType(ToolTip), ToolTip.OpenedEvent, New RoutedEventHandler(AddressOf AlCargarseOAbrirse))
        EventManager.RegisterClassHandler(GetType(ContextMenu), ContextMenu.OpenedEvent, New RoutedEventHandler(AddressOf AlCargarseOAbrirse))
    End Sub

    ''' <summary>
    ''' Fuerza el constructor estático de WPF-UI (para que su cambio ya esté hecho y no llegue después) y devuelve el tamaño
    ''' por defecto de TextBlock al de Windows. Devuelve si el defecto queda como el de Windows.
    ''' </summary>
    Public Shared Function RestaurarTamanoPorDefecto() As Boolean
        Try
            RuntimeHelpers.RunClassConstructor(GetType(Wpf.Ui.Controls.TextBlock).TypeHandle)
            Dim tamano As Double = SystemFonts.MessageFontSize
            Dim metadata As PropertyMetadata = TextBlock.FontSizeProperty.GetMetadata(GetType(TextBlock))
            If Equals(metadata.DefaultValue, tamano) Then
                Return True
            End If
            ' La metadata está sellada (su DefaultValue ya no se deja cambiar): se cambia el campo de detrás. Se cambia el
            ' objeto (no se sustituye), así vale también para los tipos derivados que lo comparten.
            Dim campo As FieldInfo = GetType(PropertyMetadata).GetField("_defaultValue", BindingFlags.Instance Or BindingFlags.NonPublic)
            If campo Is Nothing OrElse campo.FieldType IsNot GetType(Object) Then
                Return False
            End If
            campo.SetValue(metadata, tamano)
            Return Equals(TextBlock.FontSizeProperty.GetMetadata(GetType(TextBlock)).DefaultValue, tamano)
        Catch
            Return False
        End Try
    End Function

    Private Shared Sub AlCargarseOAbrirse(sender As Object, e As RoutedEventArgs)
        Dim control = TryCast(sender, Control)
        If control IsNot Nothing Then
            FijarSiEsPorDefecto(control)
        End If
    End Sub

    ''' <summary>
    ''' Plan B (si no se puede restaurar el defecto): si el tamaño del control es el de por defecto (nadie lo ha puesto), se
    ''' le pone el de Windows. Se pone antes otro valor para que el cambio sí baje por el árbol ya construido (con el mismo
    ''' valor, WPF no lo baja más allá del primer Control); lo que se cree después por debajo de un Control puede seguir a 14.
    ''' </summary>
    Public Shared Sub FijarSiEsPorDefecto(control As Control)
        Try
            Dim origen = DependencyPropertyHelper.GetValueSource(control, Control.FontSizeProperty)
            If origen.BaseValueSource = BaseValueSource.Default Then
                Dim tamano As Double = SystemFonts.MessageFontSize
                control.FontSize = tamano + 1
                control.FontSize = tamano
            End If
        Catch
            ' Nunca rompe una ventana por esto
        End Try
    End Sub
End Class
