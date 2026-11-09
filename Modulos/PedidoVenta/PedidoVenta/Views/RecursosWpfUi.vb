Imports System.Reflection
Imports System.Runtime.CompilerServices

''' <summary>
''' Nesto#631 (prueba): los recursos de WPF-UI (tema + estilos de sus controles) en UN diccionario compartido que solo se
''' fusiona en los controles que lo piden (hoy, <see cref="BotoneraDetallePedido"/> y su menú «Más»), nunca en
''' Application.Resources. Es el mismo patrón que el panel lateral del exe (PanelNavegacionLateral, Nesto#490/#560): el tema
''' se carga como diccionario normal, sin ApplicationThemeManager (que toca Application.Current), y la versión de WPF-UI es
''' la misma (4.3.0).
''' <list type="bullet">
''' <item>Una sola instancia para todo el proceso: cada pestaña de pedido no vuelve a leer los ~2.000 recursos de WPF-UI.</item>
''' <item>Lo único que WPF-UI cambia fuera de sus diccionarios: el constructor estático de Wpf.Ui.Controls.TextBlock (lo crean
''' SymbolIcon y las plantillas de sus botones) sube PARA TODO EL PROCESO el tamaño por defecto de TextBlock de 12 a 14. El
''' exe lo deshace con CompensacionTextoWpfUi solo cuando monta el panel lateral; aquí se hace lo mismo antes del primer
''' control de WPF-UI, para que abrir un pedido no agrande los textos del resto de Nesto. Si la prueba sale adelante, las
''' dos copias deberían ir a un proyecto común (p. ej. ControlesUsuario, con la referencia a WPF-UI).</item>
''' <item>Tema: claro, como el resto de Nesto (que usa los colores de sistema de Windows y no tiene modo oscuro).
''' <see cref="Oscuro"/> solo existe para las capturas de la prueba.</item>
''' </list>
''' </summary>
Public NotInheritable Class RecursosWpfUi

    Public Const URI_TEMA_CLARO As String = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/Light.xaml"
    Public Const URI_TEMA_OSCURO As String = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/Dark.xaml"
    ''' <summary>Los estilos de los controles de WPF-UI (lo que fusiona ControlsDictionary).</summary>
    Public Const URI_CONTROLES As String = "pack://application:,,,/Wpf.Ui;component/Resources/Wpf.Ui.xaml"

    ''' <summary>El tamaño de letra de los controles de WPF-UI (el de su diseño, como en el panel lateral).</summary>
    Public Const TAMANO_WPF_UI As Double = 14

    ''' <summary>Solo para las capturas: tema oscuro. Hay que ponerlo antes de la primera botonera.</summary>
    Public Shared Property Oscuro As Boolean

    Private Shared _diccionario As ResourceDictionary

    Private Sub New()
    End Sub

    ''' <summary>El diccionario compartido (se crea la primera vez, en el hilo de la interfaz).</summary>
    Public Shared ReadOnly Property Diccionario As ResourceDictionary
        Get
            If _diccionario Is Nothing Then
                RestaurarTamanoTextBlock()
                Dim nuevo As New ResourceDictionary()
                nuevo.MergedDictionaries.Add(New ResourceDictionary With {.Source = New Uri(If(Oscuro, URI_TEMA_OSCURO, URI_TEMA_CLARO), UriKind.Absolute)})
                nuevo.MergedDictionaries.Add(New ResourceDictionary With {.Source = New Uri(URI_CONTROLES, UriKind.Absolute)})
                _diccionario = nuevo
            End If
            Return _diccionario
        End Get
    End Property

    ''' <summary>
    ''' Lo mismo que CompensacionTextoWpfUi.RestaurarTamanoPorDefecto del exe: fuerza el constructor estático de WPF-UI y
    ''' devuelve el tamaño por defecto de TextBlock al de Windows. Si ya lo hizo el panel lateral, no cambia nada.
    ''' </summary>
    Public Shared Function RestaurarTamanoTextBlock() As Boolean
        Try
            RuntimeHelpers.RunClassConstructor(GetType(Wpf.Ui.Controls.TextBlock).TypeHandle)
            Dim tamano As Double = SystemFonts.MessageFontSize
            Dim metadata As PropertyMetadata = TextBlock.FontSizeProperty.GetMetadata(GetType(TextBlock))
            If Equals(metadata.DefaultValue, tamano) Then
                Return True
            End If
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

End Class
