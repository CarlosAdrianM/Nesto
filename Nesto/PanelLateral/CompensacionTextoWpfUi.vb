Imports System.Threading

''' <summary>
''' Nesto#490 (panel lateral, tramo 1). Hallazgo de la prueba previa (spike): el primer icono de WPF-UI (SymbolIcon, que
''' usa el propio NavigationView en su plantilla) crea un Wpf.Ui.Controls.TextBlock, y su constructor estático cambia PARA
''' TODO EL PROCESO el tamaño por defecto de System.Windows.Controls.TextBlock: de 12 (el de Windows) a 14. No es un estilo
''' (aislarlo en el panel no sirve): es metadata del tipo. Como en WPF un valor por defecto no se hereda, todos los
''' TextBlock cuyo contenedor no fije el tamaño pasarían a 14 en las 129 vistas, los diálogos y los ToolTip.
''' <para>
''' Se compensa dando a las raíces un tamaño LOCAL igual al de siempre (SystemFonts.MessageFontSize), que sí se hereda:
''' la ventana principal al montar el panel y, después, cada ventana al cargarse y cada ToolTip y menú contextual al
''' abrirse, siempre que no lo tengan ya puesto (por estilo o a mano). Solo con el piloto encendido: sin él no se carga
''' WPF-UI y esto no se llama.
''' </para>
''' </summary>
Public NotInheritable Class CompensacionTextoWpfUi

    Private Shared _registrada As Integer

    Private Sub New()
    End Sub

    ''' <summary>Fija el tamaño en la ventana principal y registra (una sola vez) el resto.</summary>
    Public Shared Sub Aplicar(ventanaPrincipal As Window)
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

    Private Shared Sub AlCargarseOAbrirse(sender As Object, e As RoutedEventArgs)
        Dim control = TryCast(sender, Control)
        If control IsNot Nothing Then
            FijarSiEsPorDefecto(control)
        End If
    End Sub

    ''' <summary>Si el tamaño del control es el de por defecto (nadie lo ha puesto), se le pone el de Windows.</summary>
    Public Shared Sub FijarSiEsPorDefecto(control As Control)
        Try
            Dim origen = DependencyPropertyHelper.GetValueSource(control, Control.FontSizeProperty)
            If origen.BaseValueSource = BaseValueSource.Default Then
                control.FontSize = SystemFonts.MessageFontSize
            End If
        Catch
            ' Nunca rompe una ventana por esto
        End Try
    End Sub
End Class
