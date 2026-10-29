using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Menu;
using Nesto.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Ui = Wpf.Ui.Controls;

namespace Nesto.Tests
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1): la prueba previa (spike) del plan, con el panel de verdad
    /// (<see cref="PanelNavegacionLateral"/>, un NavigationView de WPF-UI 4.3.0 creado por código):
    /// <list type="number">
    /// <item>Los estilos de WPF-UI quedan aislados en el panel: fuera de él (lo que serían las 129 vistas) no hay estilos
    /// implícitos de WPF-UI, y no se crea ni se toca ninguna Application.</item>
    /// <item>El NavigationViewItem ejecuta el Command (es un ButtonBase: su OnClick acaba en base.OnClick), sin ItemInvoked.</item>
    /// <item>Lo que no se puede usar no se ve (decisión 3) y el panel compacto se abre para una sección y vuelve.</item>
    /// </list>
    /// Sin Application (como las demás pruebas del proceso): la ventana se muestra fuera de la pantalla.
    /// </summary>
    [TestClass]
    public class PanelNavegacionLateralTests
    {
        private sealed class Comando : ICommand
        {
            public bool Puede { get; set; } = true;
            public int Ejecuciones { get; private set; }
            public event EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter) => Puede;
            public void Execute(object parameter) => Ejecuciones++;
            public void Cambiar(bool puede)
            {
                Puede = puede;
                CanExecuteChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private static void EnSta(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    // El esquema pack:// sin Application
                    _ = System.IO.Packaging.PackUriHelper.UriSchemePack;
                    accion();
                }
                catch (Exception ex) { error = ex; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromMinutes(1)))
            {
                throw new AssertFailedException("El panel no terminó en un minuto");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }

        private static void Bombear()
        {
            var marco = new DispatcherFrame();
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => marco.Continue = false));
            Dispatcher.PushFrame(marco);
        }

        private static void Esperar(int milisegundos)
        {
            var marco = new DispatcherFrame();
            var reloj = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milisegundos) };
            reloj.Tick += (_, _) => { reloj.Stop(); marco.Continue = false; };
            reloj.Start();
            Dispatcher.PushFrame(marco);
        }

        private sealed class Montaje
        {
            public Window Ventana;
            public PanelNavegacionLateral Panel;
            public Button BotonFuera;
            public TextBlock TextoFuera;
            public ElementoMenu Plantilla;
            public ElementoMenu Pedidos;
            public Comando ComandoPlantilla;
            public Comando ComandoPedidos;
            public List<string> Guardados = new List<string>();
            public List<string> Errores = new List<string>();
            public TabControl Pestanas;
            public TextBox CajaPestana1;
            public TextBox CajaPestana2;
        }

        private static Montaje Montar(string compactaGuardada = null)
        {
            var m = new Montaje { ComandoPlantilla = new Comando(), ComandoPedidos = new Comando() };
            m.Plantilla = new ElementoMenu(MapaMenuPorRol.Clave("Clientes", "Ventas", "Plantilla")) { Titulo = "Plantilla", Comando = m.ComandoPlantilla };
            m.Pedidos = new ElementoMenu(MapaMenuPorRol.Clave("Clientes", "Ventas", "Pedidos")) { Titulo = "Pedidos", Comando = m.ComandoPedidos };
            var parametros = new ElementoMenu(MapaMenuPorRol.Clave("Menú de aplicación", "", "Parámetros")) { Titulo = "Parámetros", Comando = new Comando() };
            CatalogoMenu catalogo = CatalogoMenu.Construir(new ElementoMenuBase[] { m.Plantilla, m.Pedidos }, new[] { parametros });
            var conmutador = new ConmutadorNavegacionLateral(() => "1", () => compactaGuardada, m.Guardados.Add,
                (ex, contexto) => m.Errores.Add(contexto + " | " + ex));

            m.Panel = new PanelNavegacionLateral(catalogo, conmutador);
            m.BotonFuera = new Button { Content = "Fuera del panel" };
            m.TextoFuera = new TextBlock { Text = "Una vista de Nesto" };
            var fuera = new StackPanel();
            fuera.Children.Add(m.BotonFuera);
            fuera.Children.Add(m.TextoFuera);
            // Tramo 2: como MainRegion, unas pestañas con pantallas que tienen dónde escribir
            m.CajaPestana1 = new TextBox { Text = "Pantalla 1" };
            m.CajaPestana2 = new TextBox { Text = "Pantalla 2" };
            m.Pestanas = new TabControl();
            _ = m.Pestanas.Items.Add(new TabItem { Header = "Uno", Content = m.CajaPestana1 });
            _ = m.Pestanas.Items.Add(new TabItem { Header = "Dos", Content = m.CajaPestana2 });
            m.Pestanas.SelectedIndex = 0;
            fuera.Children.Add(m.Pestanas);
            var raiz = new Grid();
            raiz.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            raiz.ColumnDefinitions.Add(new ColumnDefinition());
            Grid.SetColumn(fuera, 1);
            raiz.Children.Add(m.Panel);
            raiz.Children.Add(fuera);
            m.Ventana = new Window
            {
                Content = raiz,
                Width = 700,
                Height = 500,
                Left = -20000,
                Top = -20000,
                ShowInTaskbar = false,
                ShowActivated = false,
                WindowStyle = WindowStyle.None
            };
            // Como MainWindow.MontarNavegacionLateral
            CompensacionTextoWpfUi.Aplicar(m.Ventana);
            m.Ventana.Show();
            Bombear();
            return m;
        }

        [TestMethod]
        public void Spike_LosEstilosDeWpfUi_QuedanAisladosEnElPanel()
        {
            EnSta(() =>
            {
                double tamanoTextBlockAntes = (double)TextBlock.FontSizeProperty.GetMetadata(typeof(TextBlock)).DefaultValue;
                Montaje m = Montar();
                try
                {
                    Assert.IsNull(System.Windows.Application.Current, "WPF-UI no debe crear una Application");
                    object estiloDentro = m.Panel.TryFindResource(typeof(Button));
                    Assert.IsNotNull(estiloDentro, "Dentro del panel están los estilos de WPF-UI");
                    // Fuera, el del tema de Windows de siempre (Aero2), no el de WPF-UI
                    Assert.AreNotSame(estiloDentro, m.BotonFuera.TryFindResource(typeof(Button)), "Fuera del panel no llega el estilo de botón de WPF-UI");
                    Assert.AreNotSame(estiloDentro, m.BotonFuera.Style ?? m.BotonFuera.FindResource(typeof(Button)));
                    object estiloTextoDentro = m.Panel.TryFindResource(typeof(TextBlock));
                    Assert.IsNotNull(estiloTextoDentro);
                    Assert.AreNotSame(estiloTextoDentro, m.TextoFuera.TryFindResource(typeof(TextBlock)), "Ni el de TextBlock");
                    Assert.IsNull(m.BotonFuera.TryFindResource("ApplicationBackgroundBrush"), "Ni los colores del tema");
                    // El hallazgo del spike: el primer SymbolIcon crea un Wpf.Ui.Controls.TextBlock y su constructor estático
                    // cambia, para todo el proceso, el tamaño POR DEFECTO de los TextBlock (12 → 14). No es un estilo: es
                    // metadata del tipo. Nesto#560: la compensación (como en MainWindow.MontarNavegacionLateral) lo devuelve al
                    // de Windows
                    double tamanoTextBlockDespues = (double)TextBlock.FontSizeProperty.GetMetadata(typeof(TextBlock)).DefaultValue;
                    Console.WriteLine($"TextBlock.FontSize por defecto: antes {tamanoTextBlockAntes}, después {tamanoTextBlockDespues}");
                    Assert.AreEqual(SystemFonts.MessageFontSize, tamanoTextBlockDespues, "El defecto de TextBlock, el de Windows otra vez");
                    Assert.AreEqual(CompensacionTextoWpfUi.TAMANO_WPF_UI, TextElement.GetFontSize(m.Panel), "El panel, con el 14 de WPF-UI");
                    // Con la compensación, el texto de fuera sigue con el tamaño de Windows (lo hereda de la ventana)
                    Assert.AreEqual(SystemFonts.MessageFontSize, m.TextoFuera.FontSize, "El texto de fuera del panel, con el tamaño de siempre");
                    // Y también en una ventana que se abra después (un diálogo)
                    var texto = new TextBlock { Text = "Un diálogo" };
                    var dialogo = new Window { Content = texto, Left = -20000, Top = -20000, Width = 200, Height = 100, ShowInTaskbar = false, ShowActivated = false };
                    dialogo.Show();
                    Bombear();
                    Assert.AreEqual(SystemFonts.MessageFontSize, texto.FontSize, "El texto de un diálogo abierto después, con el tamaño de siempre");
                    dialogo.Close();
                    Assert.IsTrue(m.Panel.ZonaDeContenidoOculta, "La zona de páginas del NavigationView no se usa: oculta");
                    Esperar(400);
                    GuardarCaptura(m.Ventana, "panel-lateral-compacto.png");
                    Assert.IsTrue(m.Panel.ActualWidth <= 50, $"Compacto: unos 48 px (mide {m.Panel.ActualWidth})");
                    m.Panel.Navegacion.IsPaneOpen = true;
                    m.Panel.ItemsDeSecciones.Values.First().IsExpanded = true;
                    Esperar(400); // la animación de la plantilla de WPF-UI (0,16 s) al abrir
                    GuardarCaptura(m.Ventana, "panel-lateral-abierto.png");
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        public sealed class FilaBanco
        {
            public string Concepto { get; set; }
            public decimal Importe { get; set; }
        }

        private static DataGrid RejillaBanco() => new DataGrid
        {
            AutoGenerateColumns = true,
            ItemsSource = new List<FilaBanco>
            {
                new FilaBanco { Concepto = "ABONARÉS - ENTREGAS - INGRESOS", Importe = 304m },
                new FilaBanco { Concepto = "TARJETAS DE CRÉDITO - TARJETAS DÉBITO", Importe = -7.01m },
            }
        };

        private static List<TextBlock> TextosDe(DependencyObject raiz)
        {
            var textos = new List<TextBlock>();
            void Recorrer(DependencyObject nodo)
            {
                if (nodo is TextBlock texto)
                {
                    textos.Add(texto);
                }
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(nodo); i++)
                {
                    Recorrer(VisualTreeHelper.GetChild(nodo, i));
                }
            }
            Recorrer(raiz);
            return textos;
        }

        private static void ComprobarTamanoDeSiempre(DependencyObject raiz, string donde)
        {
            List<TextBlock> textos = TextosDe(raiz);
            Assert.IsTrue(textos.Count > 0, "No hay textos que mirar en " + donde);
            var distintos = textos.Where(t => t.FontSize != SystemFonts.MessageFontSize)
                .Select(t => $"«{t.Text}» {t.FontSize} ({DependencyPropertyHelper.GetValueSource(t, TextBlock.FontSizeProperty).BaseValueSource})")
                .ToList();
            Assert.AreEqual(0, distintos.Count, $"{donde}: textos que no salen con el tamaño de la cinta ({SystemFonts.MessageFontSize}):\n" + string.Join("\n", distintos));
        }

        /// <summary>
        /// Nesto#560: con el panel lateral las rejillas, las pestañas y las etiquetas salían a 14 (filas de ~22 px en vez de
        /// ~19) y Carlos no podía cuadrar el banco. La compensación ponía en la ventana el MISMO valor que ya tenía (12), WPF
        /// no bajaba ese «cambio» más allá del primer Control (el TabControl MainRegion, que ya valía 12 por su defecto) y los
        /// TextBlock de debajo usaban su defecto de WPF-UI (14). Aquí, montado como MainWindow (la ventana ya construida
        /// cuando se monta el panel, una pantalla abierta antes y otra después, y un diálogo), todos los textos deben salir
        /// con el tamaño de siempre, el de la cinta.
        /// </summary>
        [TestMethod]
        public void Nesto560_ConElPanelLateral_LasPantallasYLosDialogos_TienenLaLetraDeLaCinta()
        {
            EnSta(() =>
            {
                // WPF-UI cargado (como al crear el panel): sin compensar, TextBlock pasaría a 14 por defecto para todo el proceso
                _ = new Ui.TextBlock();

                // Como MainWindow.xaml: Grid > Border > TabControl (MainRegion), ya construido antes de montar el panel
                var region = new TabControl();
                _ = region.Items.Add(new TabItem { Header = "Bancos", Content = new UserControl { Content = RejillaBanco() } });
                region.SelectedIndex = 0;
                var raiz = new Grid();
                raiz.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                raiz.RowDefinitions.Add(new RowDefinition());
                var borde = new Border { Child = region };
                Grid.SetRow(borde, 1);
                raiz.Children.Add(borde);
                var ventana = new Window
                {
                    Content = raiz, Width = 900, Height = 600, Left = -20000, Top = -20000,
                    ShowInTaskbar = false, ShowActivated = false, WindowStyle = WindowStyle.None
                };
                Window dialogo = null;
                try
                {
                    // MainWindow.MontarNavegacionLateral
                    CompensacionTextoWpfUi.Aplicar(ventana);
                    Assert.AreEqual(SystemFonts.MessageFontSize, (double)TextBlock.FontSizeProperty.GetMetadata(typeof(TextBlock)).DefaultValue,
                        "El defecto de TextBlock, como sin WPF-UI");
                    Assert.AreEqual(SystemFonts.MessageFontSize, (double)TextBlock.FontSizeProperty.GetMetadata(typeof(Ui.TextBlock)).DefaultValue,
                        "También para los TextBlock de WPF-UI, que comparten la metadata");
                    ventana.Show();
                    Bombear();
                    ComprobarTamanoDeSiempre(ventana, "La pantalla abierta al montar el panel");

                    // Una pantalla que se abre después (como Bancos desde el menú)
                    var botonera = new StackPanel();
                    botonera.Children.Add(new Button { Content = "Puntear automáticamente" });
                    botonera.Children.Add(new Label { Content = "Banco:" });
                    botonera.Children.Add(RejillaBanco());
                    var otra = new TabItem { Header = "Bancos (2)", Content = new UserControl { Content = botonera } };
                    _ = region.Items.Add(otra);
                    region.SelectedItem = otra;
                    Bombear();
                    ComprobarTamanoDeSiempre(ventana, "Una pantalla abierta después");

                    // Y un diálogo con botones
                    var contenido = new StackPanel();
                    contenido.Children.Add(new Label { Content = "¿Desea continuar?" });
                    contenido.Children.Add(new Button { Content = "Aceptar" });
                    dialogo = new Window
                    {
                        Content = contenido, Left = -20000, Top = -20000, Width = 300, Height = 150,
                        ShowInTaskbar = false, ShowActivated = false
                    };
                    dialogo.Show();
                    Bombear();
                    ComprobarTamanoDeSiempre(dialogo, "Un diálogo");
                }
                finally
                {
                    dialogo?.Close();
                    ventana.Close();
                }
            });
        }

        [TestMethod]
        public void Spike_ElNavigationViewItem_EjecutaElComandoDeLaCinta()
        {
            EnSta(() =>
            {
                Montaje m = Montar("0"); // abierto
                try
                {
                    Ui.NavigationViewItem seccion = m.Panel.ItemsDeSecciones.Values.Single();
                    seccion.IsExpanded = true;
                    Bombear();
                    Ui.NavigationViewItem pedidos = m.Panel.ItemsDeElementos[m.Pedidos];
                    Assert.AreSame(m.ComandoPedidos, pedidos.Command, "El item lleva el mismo ICommand");

                    Pulsar(pedidos);
                    Bombear();

                    Assert.AreEqual(1, m.ComandoPedidos.Ejecuciones, "El clic en el item ejecuta el comando (sin ItemInvoked)");
                    Assert.AreEqual(0, m.ComandoPlantilla.Ejecuciones);
                    Assert.AreEqual("Pedidos de venta", pedidos.Content, "Con el título del mapa");
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        [TestMethod]
        public void LoQueNoSePuedeUsar_NoSeVe_YLaSeccionSinNadaTampoco()
        {
            EnSta(() =>
            {
                Montaje m = Montar("0");
                try
                {
                    Ui.NavigationViewItem seccion = m.Panel.ItemsDeSecciones.Values.Single();
                    Assert.AreEqual(Visibility.Visible, m.Panel.ItemsDeElementos[m.Pedidos].Visibility);

                    m.ComandoPedidos.Cambiar(false);
                    Assert.AreEqual(Visibility.Collapsed, m.Panel.ItemsDeElementos[m.Pedidos].Visibility, "Sin CanExecute, no se ve (no sale deshabilitado)");
                    Assert.AreEqual(Visibility.Visible, seccion.Visibility);

                    m.Plantilla.Visibilidad = Visibility.Collapsed;
                    Assert.AreEqual(Visibility.Collapsed, seccion.Visibility, "Sin nada que usar, la sección no se pinta");

                    m.ComandoPedidos.Cambiar(true);
                    Assert.AreEqual(Visibility.Visible, seccion.Visibility);
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        [TestMethod]
        public void Compacto_PorDefecto_SeAbrePorUnaSeccionSinRecordarlo_YLoQueHaceElUsuarioSeRecuerda()
        {
            EnSta(() =>
            {
                Montaje m = Montar(null);
                try
                {
                    Ui.NavigationView navegacion = m.Panel.Navegacion;
                    Assert.IsFalse(navegacion.IsPaneOpen, "Decisión 2: compacto por defecto");
                    Ui.NavigationViewItem seccion = m.Panel.ItemsDeSecciones.Values.Single();

                    Pulsar(seccion);
                    Bombear();
                    Assert.IsTrue(navegacion.IsPaneOpen, "Pulsar una sección con el panel compacto lo abre");
                    Assert.IsTrue(seccion.IsExpanded, "con la sección desplegada");

                    Pulsar(m.Panel.ItemsDeElementos[m.Plantilla]);
                    Bombear();
                    Assert.AreEqual(1, m.ComandoPlantilla.Ejecuciones);
                    Assert.IsFalse(navegacion.IsPaneOpen, "Al elegir, vuelve a compacto");
                    Assert.AreEqual(0, m.Guardados.Count, "Abrirlo para una sección no se recuerda");

                    navegacion.IsPaneOpen = true; // el botón del panel
                    Bombear();
                    CollectionAssert.AreEqual(new[] { "0" }, m.Guardados, "Lo deja abierto: se recuerda");
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        // ── Tramo 2: «Buscar pantalla…» y Ctrl+K ──────────────────────────────────────────────────────────────────────

        private static bool TieneElFoco(Window ventana, IInputElement elemento)
            => (elemento as UIElement)?.IsKeyboardFocusWithin == true || FocusManager.GetFocusedElement(ventana) == elemento;

        // Una tecla como la manda WPF: PreviewKeyDown sobre la caja (que es quien la atiende)
        private static void Tecla(UIElement destino, Key tecla)
        {
            var args = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual((Visual)destino), 0, tecla)
            {
                RoutedEvent = Keyboard.PreviewKeyDownEvent
            };
            destino.RaiseEvent(args);
            Bombear();
        }

        [TestMethod]
        public void Busqueda_NoCogeElFoco_NiAlArrancar_NiAlCambiarDePestana()
        {
            EnSta(() =>
            {
                Montaje m = Montar("0"); // abierto: la caja se ve
                try
                {
                    m.Ventana.Activate();
                    Bombear();
                    Assert.AreEqual(Visibility.Visible, m.Panel.CajaBusqueda.Visibility);
                    Assert.IsFalse(m.Panel.BusquedaTieneElFoco, "Al arrancar, la búsqueda no tiene el foco");
                    Assert.IsFalse(m.Panel.CajaBusqueda.IsTabStop, "El tabulador no entra en la búsqueda");

                    _ = Keyboard.Focus(m.CajaPestana1);
                    m.Pestanas.SelectedIndex = 1;
                    Bombear();
                    m.Pestanas.SelectedIndex = 0;
                    Bombear();
                    Assert.IsFalse(m.Panel.BusquedaTieneElFoco, "Al cambiar de pestaña, tampoco");
                    Assert.AreEqual(0, m.Errores.Count, string.Join("\n", m.Errores));
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        [TestMethod]
        public void CtrlK_ConElPanelCompacto_LoAbreConElFocoEnLaBusqueda_YEscLoDejaComoEstaba()
        {
            EnSta(() =>
            {
                Montaje m = Montar(null); // compacto
                try
                {
                    m.Ventana.Activate();
                    _ = Keyboard.Focus(m.CajaPestana1);
                    Bombear();
                    Assert.AreEqual(Visibility.Collapsed, m.Panel.CajaBusqueda.Visibility, "Compacto: solo la lupa");

                    m.Panel.Buscador.ActivarCommand.Execute(null); // Ctrl+K
                    Bombear();

                    Assert.IsTrue(m.Panel.Navegacion.IsPaneOpen, "Ctrl+K abre el panel compacto");
                    Assert.IsTrue(m.Panel.BusquedaTieneElFoco, "con el foco en la búsqueda");
                    m.Panel.CajaBusqueda.Text = "PLÁN";
                    Bombear();
                    Assert.AreEqual(Visibility.Visible, m.Panel.ListaResultados.Visibility);
                    Assert.AreEqual("Ventas › Plantilla", ((ResultadoBusquedaMenu)m.Panel.ListaResultados.Items[0]).Ruta);

                    Tecla(m.Panel.CajaBusqueda, Key.Escape);

                    Assert.IsFalse(m.Panel.Navegacion.IsPaneOpen, "Esc: vuelve a compacto");
                    Assert.AreEqual(string.Empty, m.Panel.CajaBusqueda.Text, "y borra lo escrito");
                    Assert.IsTrue(TieneElFoco(m.Ventana, m.CajaPestana1), "El foco vuelve a donde estaba");
                    Assert.AreEqual(0, m.ComandoPlantilla.Ejecuciones);
                    Assert.AreEqual(0, m.Guardados.Count, "Abrirlo para buscar no se recuerda");
                    Assert.AreEqual(0, m.Errores.Count, string.Join("\n", m.Errores));
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        [TestMethod]
        public void CtrlK_FlechasEIntro_AbrenLaPantallaElegida_YElPanelVuelveACompacto()
        {
            EnSta(() =>
            {
                Montaje m = Montar(null);
                try
                {
                    m.Ventana.Activate();
                    _ = Keyboard.Focus(m.CajaPestana1);
                    m.Panel.Buscador.ActivarCommand.Execute(null);
                    Bombear();
                    m.Panel.CajaBusqueda.Text = "venta"; // «Pedidos de venta» (título) y «Plantilla» (por la sección)
                    Bombear();
                    CollectionAssert.AreEqual(new[] { "Ventas › Pedidos de venta", "Ventas › Plantilla" },
                        m.Panel.ListaResultados.Items.Cast<ResultadoBusquedaMenu>().Select(r => r.Ruta).ToArray());
                    Assert.AreEqual(0, m.Panel.ListaResultados.SelectedIndex, "El primero, elegido");

                    Tecla(m.Panel.CajaBusqueda, Key.Down);
                    Assert.AreEqual(1, m.Panel.ListaResultados.SelectedIndex);
                    Assert.IsTrue(m.Panel.BusquedaTieneElFoco, "Las flechas no sacan el foco de la caja");

                    Tecla(m.Panel.CajaBusqueda, Key.Enter);
                    Esperar(100);

                    Assert.AreEqual(1, m.ComandoPlantilla.Ejecuciones, "Intro abre el elegido");
                    Assert.AreEqual(0, m.ComandoPedidos.Ejecuciones);
                    Assert.IsFalse(m.Panel.Navegacion.IsPaneOpen, "y vuelve a compacto");
                    Assert.IsFalse(m.Panel.BusquedaTieneElFoco, "El foco sale de la búsqueda");
                    Assert.AreEqual(0, m.Guardados.Count);
                    Assert.AreEqual(0, m.Errores.Count, string.Join("\n", m.Errores));
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        [TestMethod]
        public void CtrlK_ConElPanelAbierto_LoDejaAbierto()
        {
            EnSta(() =>
            {
                Montaje m = Montar("0");
                try
                {
                    m.Ventana.Activate();
                    m.Panel.Buscador.ActivarCommand.Execute(null);
                    Bombear();
                    Assert.IsTrue(m.Panel.BusquedaTieneElFoco);
                    m.Panel.CajaBusqueda.Text = "pedidos";
                    Tecla(m.Panel.CajaBusqueda, Key.Enter);
                    Esperar(100);

                    Assert.AreEqual(1, m.ComandoPedidos.Ejecuciones);
                    Assert.IsTrue(m.Panel.Navegacion.IsPaneOpen, "Lo había dejado abierto el usuario: sigue abierto");
                    Assert.AreEqual(0, m.Guardados.Count);
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        [TestMethod]
        public void SiElUsuarioSeVaAOtroSitio_LaBusquedaSeCancelaSinQuitarleElFoco()
        {
            EnSta(() =>
            {
                Montaje m = Montar(null);
                try
                {
                    m.Ventana.Activate();
                    m.Panel.Buscador.ActivarCommand.Execute(null);
                    Bombear();
                    m.Panel.CajaBusqueda.Text = "pla";
                    Bombear();

                    _ = Keyboard.Focus(m.CajaPestana1); // un clic en la pantalla
                    Bombear();

                    Assert.IsTrue(TieneElFoco(m.Ventana, m.CajaPestana1), "El foco se queda donde ha ido el usuario");
                    Assert.IsFalse(m.Panel.Navegacion.IsPaneOpen, "El panel vuelve a compacto");
                    Assert.AreEqual(string.Empty, m.Panel.Buscador.Texto);
                    Assert.AreEqual(0, m.ComandoPlantilla.Ejecuciones);
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        [TestMethod]
        public void LaLupa_SoloConElPanelCompacto_YHaceLoMismoQueCtrlK_YCaptura()
        {
            EnSta(() =>
            {
                Montaje m = Montar(null);
                try
                {
                    m.Ventana.Activate();
                    Bombear();
                    Assert.AreEqual(Visibility.Collapsed, m.Panel.ListaResultados.Visibility, "Sin escribir, sin lista");
                    Assert.AreEqual(Visibility.Visible, m.Panel.BotonBuscar.Visibility, "Compacto: la lupa");
                    Assert.IsFalse(m.Panel.BotonBuscar.Focusable, "La lupa no se queda con el foco");

                    typeof(System.Windows.Controls.Primitives.ButtonBase).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(m.Panel.BotonBuscar, null);
                    Bombear();

                    Assert.IsTrue(m.Panel.Navegacion.IsPaneOpen, "La lupa abre el panel");
                    Assert.AreEqual(Visibility.Collapsed, m.Panel.BotonBuscar.Visibility, "Abierto: la caja en lugar de la lupa");
                    Assert.IsTrue(m.Panel.BusquedaTieneElFoco, "con el foco en la búsqueda");
                    m.Panel.CajaBusqueda.Text = "p";
                    Esperar(400);
                    GuardarCaptura(m.Ventana, "panel-lateral-busqueda-prueba.png");
                    Tecla(m.Panel.CajaBusqueda, Key.Escape);
                    m.Panel.CajaBusqueda.Text = "zzz"; // con el panel compacto no se pinta nada
                    Bombear();
                    Assert.AreEqual(Visibility.Collapsed, m.Panel.ListaResultados.Visibility);
                    Assert.AreEqual(0, m.Errores.Count, string.Join("\n", m.Errores));
                }
                finally
                {
                    m.Ventana.Close();
                }
            });
        }

        // El clic de verdad: el OnClick protegido del NavigationViewItem (lo que hace el ratón al soltar)
        private static void Pulsar(Ui.NavigationViewItem item)
        {
            MethodInfo onClick = typeof(Ui.NavigationViewItem).GetMethod("OnClick", BindingFlags.Instance | BindingFlags.NonPublic);
            _ = onClick.Invoke(item, null);
        }

        private static void GuardarCaptura(Window ventana, string nombre)
        {
            try
            {
                var contenido = (FrameworkElement)ventana.Content;
                var mapa = new RenderTargetBitmap((int)Math.Max(1, contenido.ActualWidth), (int)Math.Max(1, contenido.ActualHeight), 96, 96, PixelFormats.Pbgra32);
                mapa.Render(contenido);
                var codificador = new PngBitmapEncoder();
                codificador.Frames.Add(BitmapFrame.Create(mapa));
                string ruta = Path.Combine(Path.GetTempPath(), nombre);
                using (FileStream fichero = File.Create(ruta))
                {
                    codificador.Save(fichero);
                }
                Console.WriteLine("Captura: " + ruta);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Sin captura: " + ex.Message);
            }
        }
    }
}
