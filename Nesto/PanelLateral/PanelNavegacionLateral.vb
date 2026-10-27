Imports System.Windows.Controls.Primitives
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
