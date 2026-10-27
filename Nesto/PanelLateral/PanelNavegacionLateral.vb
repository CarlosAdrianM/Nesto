Imports System.Windows.Controls.Primitives
Imports System.Windows.Markup
Imports System.Windows.Threading
Imports Nesto.Infrastructure.Contracts
Imports Nesto.Infrastructure.Menu
Imports Ui = Wpf.Ui.Controls

''' <summary>
''' Nesto#490 (panel lateral, tramo 1): el menú lateral de Nesto, un NavigationView de WPF-UI creado por código (quien no
''' tiene el piloto NavegacionLateral no carga nada de WPF-UI). Lo pinta a partir del <see cref="CatalogoMenu"/>: una
''' entrada por sección (por rol), con sus elementos dentro, y al pie Parámetros y Novedades.
''' <list type="bullet">
''' <item>El tema de WPF-UI (claro) y sus estilos se fusionan SOLO aquí, en los recursos de este contenedor (hermano del
''' TabControl MainRegion): nunca en Application.Resources, que cambiaría las 129 vistas de golpe.</item>
''' <item>Escritorio remoto (rds2016): sin FluentWindow, TitleBar ni Mica/Acrylic, sin transición de página.</item>
''' <item>Cada elemento lleva el MISMO comando que la cinta (lo ejecuta el propio NavigationViewItem, que es un
''' ButtonBase) y solo se ve si es visible y su comando se puede ejecutar (decisión 3).</item>
''' <item>Compacto por defecto (solo iconos; decisión 2). Pulsar una sección con el panel compacto lo abre con esa
''' sección desplegada y, al elegir, vuelve a compacto. Abrirlo o cerrarlo con su botón se recuerda por usuario.</item>
''' <item>Tramo 2: arriba, «Buscar pantalla…» (<see cref="BuscadorMenu"/>), con Ctrl+K desde toda la ventana. Con el panel
''' compacto, Ctrl+K (o la lupa) lo abre solo mientras se busca. La caja nunca coge el foco sola (ni al arrancar ni al
''' cambiar de pestaña: no es parada del tabulador) y, al terminar, el foco vuelve a la pestaña activa.</item>
''' </list>
''' </summary>
Public NotInheritable Class PanelNavegacionLateral
    Inherits Border

    ''' <summary>El tema claro de WPF-UI, cargado como diccionario normal (sin ApplicationThemeManager, que toca
    ''' Application.Current).</summary>
    Public Const URI_TEMA As String = "pack://application:,,,/Wpf.Ui;component/Resources/Theme/Light.xaml"
    ''' <summary>Los estilos de los controles de WPF-UI (lo que fusiona ControlsDictionary).</summary>
    Public Const URI_CONTROLES As String = "pack://application:,,,/Wpf.Ui;component/Resources/Wpf.Ui.xaml"

    Public Const TITULO_OPCIONES_INFORMES As String = "Opciones de los informes"

    Private ReadOnly _conmutador As ConmutadorNavegacionLateral
    Private ReadOnly _items As New Dictionary(Of ElementoMenuBase, Ui.NavigationViewItem)
    Private ReadOnly _secciones As New Dictionary(Of SeccionMenu, Ui.NavigationViewItem)
    Private ReadOnly _pie As New List(Of Ui.NavigationViewItem)
    Private _cambiandoPorCodigo As Boolean
    Private _abiertoParaUnaSeccion As Boolean

    ' Tramo 2: la búsqueda
    Public Const TEXTO_BUSCAR As String = "Buscar pantalla… (Ctrl+K)"
    Private ReadOnly _buscador As BuscadorMenu
    Private _cajaBusqueda As Ui.TextBox
    Private _botonBuscar As Ui.Button
    Private _listaResultados As ListBox
    Private _abiertoParaBuscar As Boolean
    Private _terminandoBusqueda As Boolean
    Private _focoAnterior As IInputElement

    Public Sub New(catalogo As CatalogoMenu, conmutador As ConmutadorNavegacionLateral)
        If catalogo Is Nothing Then
            Throw New ArgumentNullException(NameOf(catalogo))
        End If
        _conmutador = If(conmutador, New ConmutadorNavegacionLateral(Function() ConmutadorNavegacionLateral.VALOR_ACTIVADO))

        Resources.MergedDictionaries.Add(New ResourceDictionary With {.Source = New Uri(URI_TEMA, UriKind.Absolute)})
        Resources.MergedDictionaries.Add(New ResourceDictionary With {.Source = New Uri(URI_CONTROLES, UriKind.Absolute)})
        SetResourceReference(BackgroundProperty, "ApplicationBackgroundBrush")
        SetResourceReference(BorderBrushProperty, "ControlStrokeColorDefaultBrush")
        BorderThickness = New Thickness(0, 0, 1, 0)

        Navegacion = New Ui.NavigationView With {
            .PaneDisplayMode = Ui.NavigationViewPaneDisplayMode.Left,
            .IsBackButtonVisible = Ui.NavigationViewBackButtonVisible.Collapsed,
            .IsPaneToggleVisible = True,
            .OpenPaneLength = 300,
            .CompactPaneLength = 48,
            .Transition = Wpf.Ui.Animations.Transition.None,
            .TransitionDuration = 0,
            .HeaderVisibility = Visibility.Collapsed,
            .FrameMargin = New Thickness(0),
            .HorizontalAlignment = HorizontalAlignment.Left,
            .IsPaneOpen = Not _conmutador.CompactaAlArrancar
        }

        For Each seccion In catalogo.Secciones
            Dim itemSeccion = CrearSeccion(seccion)
            Navegacion.MenuItems.Add(itemSeccion)
        Next
        For Each elemento In catalogo.MenuAplicacion
            Dim itemPie = CrearItem(elemento, IconoDelPie(elemento.Titulo))
            _pie.Add(itemPie)
            Navegacion.FooterMenuItems.Add(itemPie)
        Next

        _buscador = New BuscadorMenu(catalogo, AddressOf _conmutador.Registrar)
        Navegacion.PaneHeader = CrearBusqueda()

        AddHandler Navegacion.Loaded, Sub(s, e) OcultarZonaDeContenido()
        AddHandler Navegacion.PaneOpened, Sub(s, e) AlCambiarPanel()
        AddHandler Navegacion.PaneClosed, Sub(s, e) AlCambiarPanel()

        Child = Navegacion
    End Sub

    ''' <summary>El NavigationView de WPF-UI.</summary>
    Public ReadOnly Property Navegacion As Ui.NavigationView

    ''' <summary>El item de cada elemento del catálogo (para las pruebas).</summary>
    Public ReadOnly Property ItemsDeElementos As IReadOnlyDictionary(Of ElementoMenuBase, Ui.NavigationViewItem)
        Get
            Return _items
        End Get
    End Property

    ''' <summary>El item de cada sección (para las pruebas).</summary>
    Public ReadOnly Property ItemsDeSecciones As IReadOnlyDictionary(Of SeccionMenu, Ui.NavigationViewItem)
        Get
            Return _secciones
        End Get
    End Property

    ''' <summary>Los items del pie (Parámetros y Novedades).</summary>
    Public ReadOnly Property ItemsDelPie As IReadOnlyList(Of Ui.NavigationViewItem)
        Get
            Return _pie
        End Get
    End Property

    ''' <summary>El ViewModel de «Buscar pantalla…» (su ActivarCommand es el Ctrl+K de la ventana principal).</summary>
    Public ReadOnly Property Buscador As BuscadorMenu
        Get
            Return _buscador
        End Get
    End Property

    ''' <summary>La caja de búsqueda (para las pruebas).</summary>
    Public ReadOnly Property CajaBusqueda As Ui.TextBox
        Get
            Return _cajaBusqueda
        End Get
    End Property

    ''' <summary>La lupa que se ve con el panel compacto (para las pruebas).</summary>
    Public ReadOnly Property BotonBuscar As Ui.Button
        Get
            Return _botonBuscar
        End Get
    End Property

    ''' <summary>La lista de resultados (para las pruebas).</summary>
    Public ReadOnly Property ListaResultados As ListBox
        Get
            Return _listaResultados
        End Get
    End Property

    ''' <summary>
    ''' Pone el foco en la pestaña activa (lo da la ventana principal). Al terminar una búsqueda, si el foco no puede
    ''' volver a donde estaba (o se ha abierto una pantalla y esta no lo ha cogido), va aquí.
    ''' </summary>
    Public Property DevolverFocoAPestanaActiva As Action

    ''' <summary>El popup de «Opciones de los informes» (fechas e «Incluir Global»), si el catálogo las trae.</summary>
    Public ReadOnly Property PopupOpcionesInformes As Popup

    Private Function CrearSeccion(seccion As SeccionMenu) As Ui.NavigationViewItem
        Dim item As New Ui.NavigationViewItem With {
            .Content = seccion.Titulo,
            .ToolTip = seccion.Titulo,
            .Icon = New Ui.SymbolIcon With {.Symbol = Simbolo(seccion.Icono)}
        }
        ' La sección no se pinta si no tiene nada que este usuario pueda usar
        Enlazar(item, VisibilityProperty, seccion, NameOf(SeccionMenu.HayAlgoQueMostrar), New BooleanToVisibilityConverter())
        _secciones(seccion) = item

        Dim fechas As ContenidoMenu = Nothing
        Dim incluirGlobal As ContenidoMenu = Nothing
        For Each elemento In seccion.Elementos
            Dim boton = TryCast(elemento, ElementoMenu)
            If boton IsNot Nothing Then
                item.MenuItems.Add(CrearItem(boton, Nothing))
            ElseIf elemento.Clave.EndsWith(MapaMenuPorRol.SEPARADOR & "Incluir Global", StringComparison.Ordinal) Then
                incluirGlobal = DirectCast(elemento, ContenidoMenu)
            ElseIf elemento.Clave.Contains(MapaMenuPorRol.SEPARADOR & "Fechas" & MapaMenuPorRol.SEPARADOR) Then
                fechas = DirectCast(elemento, ContenidoMenu)
            End If
        Next
        If fechas IsNot Nothing OrElse incluirGlobal IsNot Nothing Then
            item.MenuItems.Add(CrearOpcionesInformes(fechas, incluirGlobal))
        End If

        AddHandler item.Click, Sub(s, e) AlPulsarSeccion(item)
        Return item
    End Function

    Private Function CrearItem(elemento As ElementoMenu, simboloFijo As String) As Ui.NavigationViewItem
        Dim item As New Ui.NavigationViewItem()
        Enlazar(item, ContentControl.ContentProperty, elemento, NameOf(ElementoMenuBase.Titulo))
        Enlazar(item, ToolTipProperty, elemento, NameOf(ElementoMenuBase.Ayuda))
        Enlazar(item, ButtonBase.CommandProperty, elemento, NameOf(ElementoMenu.Comando))
        Enlazar(item, ButtonBase.CommandParameterProperty, elemento, NameOf(ElementoMenu.ParametroComando))
        Enlazar(item, VisibilityProperty, elemento, NameOf(ElementoMenuBase.Mostrar), New BooleanToVisibilityConverter())
        If simboloFijo IsNot Nothing Then
            item.Icon = New Ui.SymbolIcon With {.Symbol = Simbolo(simboloFijo)}
        ElseIf elemento.Icono IsNot Nothing Then
            Dim icono As New Ui.ImageIcon With {.Width = 20, .Height = 20}
            Enlazar(icono, Ui.ImageIcon.SourceProperty, elemento, NameOf(ElementoMenuBase.Icono))
            item.Icon = icono
        End If
        _items(elemento) = item
        AddHandler item.Click, Sub(s, e) AlPulsarElemento()
        Return item
    End Function

    ' Trampa 4 del plan: «Fechas» e «Incluir Global» guardan estado que usan los informes (ventas del grupo, balances,
    ' rapports). Se pintan con controles normales (sin estilos de WPF-UI: el popup no cuelga del panel) enlazados al
    ' mismo ViewModel que en la cinta (MenuBarViewModel). Tramo 3: lo declarará el propio MenuBar.
    Private Function CrearOpcionesInformes(fechas As ContenidoMenu, incluirGlobal As ContenidoMenu) As Ui.NavigationViewItem
        Dim item As New Ui.NavigationViewItem With {
            .Content = TITULO_OPCIONES_INFORMES,
            .ToolTip = "Fechas de los informes (Actual, Anterior o Personalizar) e «Incluir Global» en los balances",
            .Icon = New Ui.SymbolIcon With {.Symbol = Ui.SymbolRegular.CalendarLtr24}
        }
        Dim principal = If(fechas, incluirGlobal)
        Enlazar(item, VisibilityProperty, principal, NameOf(ElementoMenuBase.Mostrar), New BooleanToVisibilityConverter())
        _items(principal) = item

        Dim contenido As New StackPanel With {.Margin = New Thickness(12), .MinWidth = 220, .DataContext = principal.Fuente}
        If fechas IsNot Nothing Then
            contenido.DataContext = fechas.Fuente
            contenido.Children.Add(New TextBlock With {.Text = "Fechas de los informes", .FontWeight = FontWeights.SemiBold, .Margin = New Thickness(0, 0, 0, 6)})
            Dim opciones As New ComboBox With {.ItemsSource = New String() {"Actual", "Anterior", "Personalizar"}, .Margin = New Thickness(0, 0, 0, 6)}
            opciones.SetBinding(Selector.SelectedItemProperty, New Binding("OpcionesFechas") With {.Mode = BindingMode.TwoWay})
            contenido.Children.Add(opciones)
            Dim personalizadas As New StackPanel()
            personalizadas.SetBinding(VisibilityProperty, New Binding("MostrarFechas"))
            Dim desde As New DatePicker With {.Margin = New Thickness(0, 0, 0, 4)}
            desde.SetBinding(DatePicker.SelectedDateProperty, New Binding("FechaInformeInicial") With {.Mode = BindingMode.TwoWay})
            Dim hasta As New DatePicker()
            hasta.SetBinding(DatePicker.SelectedDateProperty, New Binding("FechaInformeFinal") With {.Mode = BindingMode.TwoWay})
            personalizadas.Children.Add(desde)
            personalizadas.Children.Add(hasta)
            contenido.Children.Add(personalizadas)
        End If
        If incluirGlobal IsNot Nothing Then
            Dim casilla As New CheckBox With {.DataContext = incluirGlobal.Fuente, .Margin = New Thickness(0, 8, 0, 0)}
            casilla.SetBinding(ContentControl.ContentProperty, New Binding(NameOf(ElementoMenuBase.Titulo)) With {.Source = incluirGlobal})
            casilla.SetBinding(ToggleButton.IsCheckedProperty, New Binding("IncluirGlobalEnBalances") With {.Mode = BindingMode.TwoWay})
            Enlazar(casilla, VisibilityProperty, incluirGlobal, NameOf(ElementoMenuBase.Mostrar), New BooleanToVisibilityConverter())
            contenido.Children.Add(casilla)
        End If

        _PopupOpcionesInformes = New Popup With {
            .StaysOpen = False,
            .PlacementTarget = item,
            .Placement = PlacementMode.Right,
            .Child = New Border With {
                .Background = SystemColors.WindowBrush,
                .BorderBrush = SystemColors.ActiveBorderBrush,
                .BorderThickness = New Thickness(1),
                .Child = contenido
            }
        }
        AddHandler _PopupOpcionesInformes.Closed, Sub(s, e) VolverACompactoSiHaceFalta()
        AddHandler item.Click, Sub(s, e) _PopupOpcionesInformes.IsOpen = True
        Return item
    End Function

    ' ── Tramo 2: «Buscar pantalla…» ──────────────────────────────────────────────────────────────────────────────────
    ' En la cabecera del panel (PaneHeader): con el panel abierto, la caja y, debajo, los resultados (título y ruta
    ' «Sección › Título»); con el panel compacto, solo la lupa. El foco se queda siempre en la caja: las flechas, Intro y
    ' Esc los atiende ella (la lista no coge el foco).
    Private Function CrearBusqueda() As FrameworkElement
        _cajaBusqueda = New Ui.TextBox With {
            .PlaceholderText = TEXTO_BUSCAR,
            .Icon = New Ui.SymbolIcon With {.Symbol = Ui.SymbolRegular.Search24},
            .ClearButtonEnabled = True,
            .IsTabStop = False,
            .Margin = New Thickness(4, 4, 4, 0),
            .ToolTip = "Escriba parte del nombre de la pantalla o de su sección. Intro abre la primera; las flechas eligen otra; Esc cancela."
        }
        _cajaBusqueda.SetBinding(TextBox.TextProperty, New Binding(NameOf(BuscadorMenu.Texto)) With {
            .Source = _buscador,
            .Mode = BindingMode.TwoWay,
            .UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged
        })
        _cajaBusqueda.SetBinding(VisibilityProperty, New Binding(NameOf(Ui.NavigationView.IsPaneOpen)) With {
            .Source = Navegacion,
            .Converter = New BooleanToVisibilityConverter()
        })
        AddHandler _cajaBusqueda.PreviewKeyDown, AddressOf AlPulsarTeclaEnLaBusqueda
        AddHandler _cajaBusqueda.PreviewGotKeyboardFocus, AddressOf AlEntrarEnLaBusqueda
        AddHandler _cajaBusqueda.LostKeyboardFocus, AddressOf AlSalirDeLaBusqueda

        _botonBuscar = New Ui.Button With {
            .Icon = New Ui.SymbolIcon With {.Symbol = Ui.SymbolRegular.Search24},
            .Appearance = Ui.ControlAppearance.Transparent,
            .BorderThickness = New Thickness(0),
            .Focusable = False,
            .Width = 40,
            .Height = 36,
            .Padding = New Thickness(0),
            .Margin = New Thickness(4, 4, 4, 0),
            .HorizontalAlignment = HorizontalAlignment.Left,
            .ToolTip = TEXTO_BUSCAR
        }
        _botonBuscar.SetBinding(VisibilityProperty, New Binding(NameOf(Ui.NavigationView.IsPaneOpen)) With {
            .Source = Navegacion,
            .Converter = New VisibleSiFalso()
        })
        AddHandler _botonBuscar.Click, Sub(s, e) ActivarBusqueda()

        _listaResultados = New ListBox With {
            .Focusable = False,
            .MaxHeight = 420,
            .Margin = New Thickness(4, 2, 4, 4),
            .ItemsSource = _buscador.Resultados,
            .ItemTemplate = PlantillaResultado()
        }
        Dim estiloItem As New Style(GetType(ListBoxItem), TryCast(TryFindResource(GetType(ListBoxItem)), Style))
        estiloItem.Setters.Add(New Setter(FocusableProperty, False))
        _listaResultados.ItemContainerStyle = estiloItem
        ScrollViewer.SetHorizontalScrollBarVisibility(_listaResultados, ScrollBarVisibility.Disabled)
        _listaResultados.SetBinding(Selector.SelectedItemProperty, New Binding(NameOf(BuscadorMenu.Seleccionado)) With {
            .Source = _buscador,
            .Mode = BindingMode.TwoWay
        })
        _listaResultados.SetBinding(VisibilityProperty, VisibleConPanelAbiertoY(NameOf(BuscadorMenu.HayBusqueda)))
        AddHandler _listaResultados.SelectionChanged, Sub(s, e)
                                                          If _listaResultados.SelectedItem IsNot Nothing Then
                                                              _listaResultados.ScrollIntoView(_listaResultados.SelectedItem)
                                                          End If
                                                      End Sub
        AddHandler _listaResultados.PreviewMouseLeftButtonUp, AddressOf AlPulsarResultado

        Dim sinResultados As New TextBlock With {
            .Margin = New Thickness(12, 6, 8, 6),
            .TextWrapping = TextWrapping.Wrap,
            .FontStyle = FontStyles.Italic,
            .Opacity = 0.75
        }
        sinResultados.SetBinding(TextBlock.TextProperty, New Binding(NameOf(BuscadorMenu.TextoSinResultados)) With {.Source = _buscador})
        sinResultados.SetBinding(VisibilityProperty, VisibleConPanelAbiertoY(NameOf(BuscadorMenu.SinResultados)))

        AddHandler _buscador.ActivacionPedida, Sub(s, e) ActivarBusqueda()
        AddHandler _buscador.Terminada, AddressOf AlTerminarBusqueda
        AddHandler _buscador.ErrorAlEjecutar, Sub(s, mensaje) AvisarError(mensaje)

        Dim cabecera As New StackPanel()
        cabecera.Children.Add(_botonBuscar)
        cabecera.Children.Add(_cajaBusqueda)
        cabecera.Children.Add(_listaResultados)
        cabecera.Children.Add(sinResultados)
        Return cabecera
    End Function

    Private Function VisibleConPanelAbiertoY(propiedadDelBuscador As String) As MultiBinding
        Dim enlace As New MultiBinding With {.Converter = New TodosVerdaderosAVisibilidad()}
        enlace.Bindings.Add(New Binding(propiedadDelBuscador) With {.Source = _buscador})
        enlace.Bindings.Add(New Binding(NameOf(Ui.NavigationView.IsPaneOpen)) With {.Source = Navegacion})
        Return enlace
    End Function

    Private Shared Function PlantillaResultado() As DataTemplate
        Const XAML As String =
            "<DataTemplate xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"">" &
            "<StackPanel Margin=""0,2,0,2"">" &
            "<TextBlock Text=""{Binding Titulo}"" FontSize=""13"" TextTrimming=""CharacterEllipsis""/>" &
            "<TextBlock Text=""{Binding Ruta}"" FontSize=""11"" Opacity=""0.7"" TextTrimming=""CharacterEllipsis""/>" &
            "</StackPanel></DataTemplate>"
        Return DirectCast(XamlReader.Parse(XAML), DataTemplate)
    End Function

    ''' <summary>
    ''' Ctrl+K (o la lupa con el panel compacto): el foco a la caja de búsqueda. Con el panel compacto lo abre solo mientras
    ''' se busca (no se recuerda) y, al abrir una pantalla o con Esc, vuelve a compacto.
    ''' </summary>
    Public Sub ActivarBusqueda()
        Try
            Dim actual = TryCast(Keyboard.FocusedElement, DependencyObject)
            If actual IsNot Nothing AndAlso Not EstaEnElPanel(actual) Then
                _focoAnterior = Keyboard.FocusedElement
            End If
            If Not Navegacion.IsPaneOpen Then
                _abiertoParaBuscar = True
                _abiertoParaUnaSeccion = False
                CambiarPorCodigo(True)
            End If
            If Not EnfocarCaja() Then
                ' Recién abierto, la caja puede no estar lista todavía: en cuanto se pueda
                Dispatcher.BeginInvoke(DispatcherPriority.Input, New Action(Sub() EnfocarCaja()))
            End If
        Catch ex As Exception
            _conmutador.Registrar(ex, BuscadorMenu.CONTEXTO_ERRORES & ": activar la búsqueda (Ctrl+K)")
        End Try
    End Sub

    ''' <summary>Si la caja tiene el foco (de teclado o, con la ventana sin activar, el lógico). Para las pruebas.</summary>
    Public ReadOnly Property BusquedaTieneElFoco As Boolean
        Get
            Return _cajaBusqueda.IsKeyboardFocusWithin OrElse
                FocusManager.GetFocusedElement(FocusManager.GetFocusScope(_cajaBusqueda)) Is _cajaBusqueda
        End Get
    End Property

    Private Function EnfocarCaja() As Boolean
        Try
            _cajaBusqueda.Focus()
            Keyboard.Focus(_cajaBusqueda)
            _cajaBusqueda.SelectAll()
            Return BusquedaTieneElFoco
        Catch ex As Exception
            _conmutador.Registrar(ex, BuscadorMenu.CONTEXTO_ERRORES & ": poner el foco en la búsqueda")
            Return True
        End Try
    End Function

    Private Sub AlPulsarTeclaEnLaBusqueda(sender As Object, e As KeyEventArgs)
        Try
            Select Case e.Key
                Case Key.Down
                    _buscador.Mover(1)
                    e.Handled = True
                Case Key.Up
                    _buscador.Mover(-1)
                    e.Handled = True
                Case Key.Enter
                    _buscador.Ejecutar()
                    e.Handled = True
                Case Key.Escape
                    _buscador.Cancelar()
                    e.Handled = True
            End Select
        Catch ex As Exception
            e.Handled = True
            _conmutador.Registrar(ex, BuscadorMenu.CONTEXTO_ERRORES & ": tecla " & e.Key.ToString() & " en la búsqueda")
        End Try
    End Sub

    ' Se recuerda de dónde venía el foco (también si se ha entrado con el ratón), para devolverlo con Esc
    Private Sub AlEntrarEnLaBusqueda(sender As Object, e As KeyboardFocusChangedEventArgs)
        Dim anterior = TryCast(e.OldFocus, DependencyObject)
        If anterior IsNot Nothing AndAlso Not EstaEnElPanel(anterior) Then
            _focoAnterior = e.OldFocus
        End If
    End Sub

    ' Si el usuario se va a otro sitio (clic en una pantalla), la búsqueda se cancela sin tocarle el foco
    Private Sub AlSalirDeLaBusqueda(sender As Object, e As KeyboardFocusChangedEventArgs)
        Try
            If _terminandoBusqueda Then
                Return
            End If
            Dim nuevo = TryCast(e.NewFocus, DependencyObject)
            If nuevo IsNot Nothing AndAlso EstaEnElPanel(nuevo) Then
                Return
            End If
            Dim ventana = Window.GetWindow(Me)
            If nuevo Is Nothing AndAlso ventana IsNot Nothing AndAlso Not ventana.IsActive Then
                Return ' Alt+Tab: al volver, sigue donde estaba
            End If
            If _buscador.HayBusqueda OrElse _abiertoParaBuscar Then
                _focoAnterior = Nothing
                _terminandoBusqueda = True
                Try
                    _buscador.Cancelar()
                Finally
                    _terminandoBusqueda = False
                End Try
            End If
        Catch ex As Exception
            _conmutador.Registrar(ex, BuscadorMenu.CONTEXTO_ERRORES & ": salir de la búsqueda")
        End Try
    End Sub

    Private Sub AlPulsarResultado(sender As Object, e As MouseButtonEventArgs)
        Try
            Dim contenedor = TryCast(ItemsControl.ContainerFromElement(_listaResultados, TryCast(e.OriginalSource, DependencyObject)), ListBoxItem)
            Dim resultado = TryCast(contenedor?.DataContext, ResultadoBusquedaMenu)
            If resultado IsNot Nothing Then
                e.Handled = True
                _buscador.Ejecutar(resultado)
            End If
        Catch ex As Exception
            _conmutador.Registrar(ex, BuscadorMenu.CONTEXTO_ERRORES & ": clic en un resultado de la búsqueda")
        End Try
    End Sub

    ' Fin de la búsqueda: el panel vuelve a compacto si se abrió para buscar y el foco sale de la caja. Con Esc, a donde
    ' estaba; tras abrir una pantalla, a la pestaña activa (si la pantalla no lo ha cogido ya). Si la búsqueda se ha
    ' cancelado porque el usuario se ha ido a otro sitio (_terminandoBusqueda), el foco no se toca.
    Private Sub AlTerminarBusqueda(sender As Object, e As BusquedaTerminadaEventArgs)
        Dim anterior = _focoAnterior
        _focoAnterior = Nothing
        Dim tocarElFoco = Not _terminandoBusqueda
        _terminandoBusqueda = True
        Try
            If _abiertoParaBuscar Then
                _abiertoParaBuscar = False
                CambiarPorCodigo(False)
            End If
            If tocarElFoco Then
                If e.Ejecutado IsNot Nothing Then
                    Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle, New Action(Sub() DevolverFocoSiSigueAqui()))
                Else
                    DevolverFoco(anterior)
                End If
            End If
        Catch ex As Exception
            _conmutador.Registrar(ex, BuscadorMenu.CONTEXTO_ERRORES & ": devolver el foco al terminar la búsqueda")
        Finally
            _terminandoBusqueda = Not tocarElFoco
        End Try
    End Sub

    Private Sub DevolverFocoSiSigueAqui()
        Try
            Dim actual = TryCast(Keyboard.FocusedElement, DependencyObject)
            If actual Is Nothing OrElse EstaEnElPanel(actual) OrElse TypeOf actual Is Window Then
                DevolverFoco(Nothing)
            End If
        Catch ex As Exception
            _conmutador.Registrar(ex, BuscadorMenu.CONTEXTO_ERRORES & ": devolver el foco tras abrir una pantalla")
        End Try
    End Sub

    Private Sub DevolverFoco(anterior As IInputElement)
        Dim elemento = TryCast(anterior, UIElement)
        If elemento IsNot Nothing AndAlso elemento.IsVisible AndAlso Not EstaEnElPanel(elemento) Then
            Keyboard.Focus(elemento)
            If elemento.IsKeyboardFocusWithin OrElse Not BusquedaTieneElFoco Then
                Return
            End If
        End If
        DevolverFocoAPestanaActiva?.Invoke()
        If BusquedaTieneElFoco Then
            ' Sin pestañas: al menos que salga de la caja (a la ventana, que sigue atendiendo Ctrl+K)
            Dim ventana = Window.GetWindow(Me)
            If ventana IsNot Nothing Then
                FocusManager.SetFocusedElement(ventana, ventana)
                ventana.Focus()
            End If
        End If
    End Sub

    Private Function EstaEnElPanel(elemento As DependencyObject) As Boolean
        Dim actual = elemento
        While actual IsNot Nothing
            If actual Is Me Then
                Return True
            End If
            actual = If(TypeOf actual Is Visual OrElse TypeOf actual Is Media3D.Visual3D,
                        VisualTreeHelper.GetParent(actual), LogicalTreeHelper.GetParent(actual))
        End While
        Return False
    End Function

    Private Sub AvisarError(mensaje As String)
        Try
            Dim ventana = Window.GetWindow(Me)
            If ventana IsNot Nothing Then
                MessageBox.Show(ventana, mensaje, "Nesto", MessageBoxButton.OK, MessageBoxImage.Warning)
            Else
                MessageBox.Show(mensaje, "Nesto", MessageBoxButton.OK, MessageBoxImage.Warning)
            End If
        Catch
            ' El aviso nunca rompe nada (el error ya está registrado)
        End Try
    End Sub

    ' El NavigationView trae, a la derecha del panel, su zona de páginas (un Frame con barra de desplazamiento). Aquí no
    ' se usa (las pantallas van en MainRegion, al lado): se oculta para que el panel mida solo lo que mide su columna.
    Private Sub OcultarZonaDeContenido()
        Try
            Navegacion.ApplyTemplate()
            Dim paginas = TryCast(Navegacion.Template?.FindName("PART_NavigationViewContentPresenter", Navegacion), FrameworkElement)
            Dim zona As DependencyObject = paginas
            While zona IsNot Nothing AndAlso TypeOf zona IsNot Border
                zona = VisualTreeHelper.GetParent(zona)
            End While
            If zona IsNot Nothing Then
                DirectCast(zona, Border).Visibility = Visibility.Collapsed
            End If
        Catch ex As Exception
            _conmutador.Registrar(ex, "NavegacionLateral: ocultar la zona de páginas del NavigationView")
        End Try
    End Sub

    ''' <summary>Si la zona de páginas del NavigationView está oculta (para las pruebas).</summary>
    Public ReadOnly Property ZonaDeContenidoOculta As Boolean
        Get
            Dim paginas = TryCast(Navegacion.Template?.FindName("PART_NavigationViewContentPresenter", Navegacion), FrameworkElement)
            Return paginas IsNot Nothing AndAlso Not paginas.IsVisible
        End Get
    End Property

    ' Con el panel compacto, una sección no se puede desplegar: se abre el panel con ella desplegada (sin recordarlo)
    Private Sub AlPulsarSeccion(item As Ui.NavigationViewItem)
        If Navegacion.IsPaneOpen Then
            Return
        End If
        _abiertoParaUnaSeccion = True
        CambiarPorCodigo(True)
        item.IsExpanded = True
    End Sub

    ' Al elegir algo con el panel abierto solo para esa sección, vuelve a compacto (el comando lo ejecuta el propio item)
    Private Sub AlPulsarElemento()
        If _abiertoParaUnaSeccion Then
            Dispatcher.BeginInvoke(New Action(AddressOf VolverACompactoSiHaceFalta))
        End If
    End Sub

    Private Sub VolverACompactoSiHaceFalta()
        If _abiertoParaUnaSeccion AndAlso (PopupOpcionesInformes Is Nothing OrElse Not PopupOpcionesInformes.IsOpen) Then
            _abiertoParaUnaSeccion = False
            CambiarPorCodigo(False)
        End If
    End Sub

    Private Sub CambiarPorCodigo(abierto As Boolean)
        _cambiandoPorCodigo = True
        Try
            Navegacion.IsPaneOpen = abierto
        Finally
            _cambiandoPorCodigo = False
        End Try
    End Sub

    ' El usuario lo abre o lo cierra con su botón: se recuerda (decisión 2)
    Private Sub AlCambiarPanel()
        If _cambiandoPorCodigo Then
            Return
        End If
        _abiertoParaUnaSeccion = False
        _abiertoParaBuscar = False
        _conmutador.RecordarCompacta(Not Navegacion.IsPaneOpen)
    End Sub

    Private Shared Function IconoDelPie(titulo As String) As String
        If titulo IsNot Nothing AndAlso titulo.StartsWith("Novedades", StringComparison.OrdinalIgnoreCase) Then
            Return "Sparkle24"
        End If
        Return "Settings24"
    End Function

    Friend Shared Function Simbolo(nombre As String) As Ui.SymbolRegular
        Dim simboloLeido As Ui.SymbolRegular
        If Not String.IsNullOrEmpty(nombre) AndAlso [Enum].TryParse(nombre, simboloLeido) Then
            Return simboloLeido
        End If
        Return Ui.SymbolRegular.Apps24
    End Function

    Private Shared Sub Enlazar(destino As DependencyObject, propiedad As DependencyProperty, origen As Object, ruta As String,
                               Optional conversor As IValueConverter = Nothing)
        BindingOperations.SetBinding(destino, propiedad, New Binding(ruta) With {
            .Source = origen,
            .Mode = BindingMode.OneWay,
            .Converter = conversor
        })
    End Sub
End Class

''' <summary>Visible con False (la lupa, con el panel compacto).</summary>
Friend NotInheritable Class VisibleSiFalso
    Implements IValueConverter

    Public Function Convert(value As Object, targetType As Type, parameter As Object, culture As Globalization.CultureInfo) As Object Implements IValueConverter.Convert
        Return If(TypeOf value Is Boolean AndAlso CBool(value), Visibility.Collapsed, Visibility.Visible)
    End Function

    Public Function ConvertBack(value As Object, targetType As Type, parameter As Object, culture As Globalization.CultureInfo) As Object Implements IValueConverter.ConvertBack
        Return Binding.DoNothing
    End Function
End Class

''' <summary>Visible si todos los valores son True (los resultados: hay búsqueda y el panel está abierto).</summary>
Friend NotInheritable Class TodosVerdaderosAVisibilidad
    Implements IMultiValueConverter

    Public Function Convert(values As Object(), targetType As Type, parameter As Object, culture As Globalization.CultureInfo) As Object Implements IMultiValueConverter.Convert
        Return If(values IsNot Nothing AndAlso values.All(Function(v) TypeOf v Is Boolean AndAlso CBool(v)), Visibility.Visible, Visibility.Collapsed)
    End Function

    Public Function ConvertBack(value As Object, targetTypes As Type(), parameter As Object, culture As Globalization.CultureInfo) As Object() Implements IMultiValueConverter.ConvertBack
        Return Nothing
    End Function
End Class
