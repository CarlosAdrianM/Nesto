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
        }

        private static Montaje Montar(string compactaGuardada = null)
        {
            var m = new Montaje { ComandoPlantilla = new Comando(), ComandoPedidos = new Comando() };
            m.Plantilla = new ElementoMenu(MapaMenuPorRol.Clave("Clientes", "Ventas", "Plantilla")) { Titulo = "Plantilla", Comando = m.ComandoPlantilla };
            m.Pedidos = new ElementoMenu(MapaMenuPorRol.Clave("Clientes", "Ventas", "Pedidos")) { Titulo = "Pedidos", Comando = m.ComandoPedidos };
            var parametros = new ElementoMenu(MapaMenuPorRol.Clave("Menú de aplicación", "", "Parámetros")) { Titulo = "Parámetros", Comando = new Comando() };
            CatalogoMenu catalogo = CatalogoMenu.Construir(new ElementoMenuBase[] { m.Plantilla, m.Pedidos }, new[] { parametros });
            var conmutador = new ConmutadorNavegacionLateral(() => "1", () => compactaGuardada, m.Guardados.Add);

            m.Panel = new PanelNavegacionLateral(catalogo, conmutador);
            m.BotonFuera = new Button { Content = "Fuera del panel" };
            m.TextoFuera = new TextBlock { Text = "Una vista de Nesto" };
            var fuera = new StackPanel();
            fuera.Children.Add(m.BotonFuera);
            fuera.Children.Add(m.TextoFuera);
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
                    // metadata del tipo, y un valor por defecto no se hereda
                    double tamanoTextBlockDespues = (double)TextBlock.FontSizeProperty.GetMetadata(typeof(TextBlock)).DefaultValue;
                    Console.WriteLine($"TextBlock.FontSize por defecto: antes {tamanoTextBlockAntes}, después {tamanoTextBlockDespues}");
                    Assert.AreEqual(14d, tamanoTextBlockDespues, "Si WPF-UI deja de hacerlo, se puede quitar CompensacionTextoWpfUi");
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
