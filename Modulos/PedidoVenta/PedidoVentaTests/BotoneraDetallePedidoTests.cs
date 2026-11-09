using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using Ui = Wpf.Ui.Controls;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#631 (prueba): la botonera de DetallePedidoView con WPF-UI conserva los mismos comandos y visibilidades que la
    /// rejilla de botones de antes, y sus recursos no salen de la botonera (ni a Application.Resources ni al tamaño de letra
    /// del resto de Nesto).
    /// </summary>
    [TestClass]
    public class BotoneraDetallePedidoTests
    {
        private static readonly string[] COMANDOS_DE_ANTES =
        {
            "CmdRetenerPedido", "CmdLiberarPedido", "AbrirFacturarRutasCommand", "CopiarFacturaCommand",
            "cmdAbrirPicking", "DescargarPresupuestoCommand", "AceptarPresupuestoCommand", "PasarAPresupuestoCommand",
            "EnviarPedidoPorCorreoCommand", "EnviarPedidoPorWhatsAppCommand", "AbrirFacturarFinDeMesCommand",
            "NuevaNotaRecogidaCommand", "NuevaNotaCobroCommand", "LeerEtiquetaRecogidaCommand"
        };

        private static readonly Dictionary<string, string> VISIBILIDAD_DE_ANTES = new Dictionary<string, string>
        {
            ["CmdLiberarPedido"] = "EstaRetenido",
            ["AbrirFacturarRutasCommand"] = "EsGrupoQuePuedeFacturar",
            ["CopiarFacturaCommand"] = "MostrarCopiarFactura",
            ["AceptarPresupuestoCommand"] = "mostrarAceptarPresupuesto",
            ["AbrirFacturarFinDeMesCommand"] = "PuedeFacturarFinDeMes",
            ["NuevaNotaRecogidaCommand"] = "EsGrupoQuePuedeCrearNotas",
            ["NuevaNotaCobroCommand"] = "EsGrupoQuePuedeCrearNotas",
            ["LeerEtiquetaRecogidaCommand"] = "PuedeLeerEtiquetaRecogida"
        };

        [TestMethod]
        public void Botonera_MismosComandosVisibilidadesYAgrupacion_YRecursosAcotados()
        {
            EjecutarEnSTA(() =>
            {
                // Como en Nesto, con Application (los diccionarios de WPF-UI se cargan con pack://application)
                var app = System.Windows.Application.Current ?? new System.Windows.Application();
                var clavesAntes = TodasLasClaves(app.Resources).ToList();
                int diccionariosAntes = app.Resources.MergedDictionaries.Count;

                var botonera = new BotoneraDetallePedido();
                var barra = (WrapPanel)botonera.FindName("BarraComandos");
                var grupoRetener = (StackPanel)botonera.FindName("GrupoRetener");
                var mas = (Ui.DropDownButton)botonera.FindName("BotonMas");
                var menu = (ContextMenu)mas.Flyout;
                var retener = (Ui.Button)botonera.FindName("BotonRetener");

                var aLaVista = barra.Children.OfType<Ui.Button>().Where(b => !(b is Ui.DropDownButton)).ToList();
                var enMas = menu.Items.OfType<MenuItem>().ToList();
                var todos = new List<FrameworkElement>();
                todos.AddRange(grupoRetener.Children.OfType<Ui.Button>());
                todos.AddRange(aLaVista);
                todos.AddRange(enMas);

                // 1) Los 14 comandos de antes, ni uno más ni uno menos, y todos existen en el ViewModel
                var comandos = todos.Select(b => RutaBinding(b, CommandDe(b))).ToList();
                CollectionAssert.AreEquivalent(COMANDOS_DE_ANTES, comandos);
                foreach (var ruta in comandos.Concat(VISIBILIDAD_DE_ANTES.Values))
                {
                    Assert.IsTrue(ExisteEnViewModel(ruta), $"DetallePedidoViewModel no tiene «{ruta}»");
                }

                // 2) Las mismas visibilidades; los que no tenían, siguen sin tenerla
                foreach (var boton in todos)
                {
                    string comando = RutaBinding(boton, CommandDe(boton));
                    string visibilidad = RutaBinding(boton, UIElement.VisibilityProperty);
                    VISIBILIDAD_DE_ANTES.TryGetValue(comando, out string esperada);
                    Assert.AreEqual(esperada, visibilidad, $"Visibilidad de {comando}");
                }

                // 3) A la vista lo de cada día (con icono); en «Más» Documentos · Facturación · Notas
                CollectionAssert.AreEqual(
                    new[] { "Sacar picking", "Descargar PDF", "Enviar por correo…", "Facturar rutas", "Aceptar presupuesto" },
                    aLaVista.Select(b => (string)b.Content).ToArray());
                Assert.IsTrue(aLaVista.All(b => b.Icon is Ui.SymbolIcon), "Todos los botones a la vista llevan icono");
                Assert.AreSame(mas, barra.Children.Cast<UIElement>().Last(), "«Más» va al final de la barra");
                var secundarios = menu.Items.Cast<object>()
                    .Select(c => c is Separator ? "|" : (string)((MenuItem)c).Header).ToArray();
                CollectionAssert.AreEqual(new[]
                {
                    "Enviar por WhatsApp…", "Copiar factura", "|",
                    "Facturar fin de mes", "Pasar a presupuesto", "|",
                    "Nueva nota de cobro…", "Nueva nota de recogida", "Leer etiqueta de recogida…"
                }, secundarios);
                Assert.IsTrue(enMas.All(m => m.Icon is Ui.SymbolIcon), "Todas las opciones de «Más» llevan icono");

                // 4) «Retener…» fuera de la barra, con apariencia de precaución
                Assert.IsFalse(barra.Children.Contains(retener));
                Assert.AreEqual(Ui.ControlAppearance.Caution, retener.Appearance);

                // 5) Recursos: el mismo diccionario compartido en cada botonera y en su menú, y nada en Application.Resources
                var otra = new BotoneraDetallePedido();
                Assert.IsTrue(botonera.Resources.MergedDictionaries.Contains(RecursosWpfUi.Diccionario));
                Assert.IsTrue(otra.Resources.MergedDictionaries.Contains(RecursosWpfUi.Diccionario));
                Assert.IsTrue(menu.Resources.MergedDictionaries.Contains(RecursosWpfUi.Diccionario));
                Assert.AreEqual(diccionariosAntes, app.Resources.MergedDictionaries.Count);
                var nuevas = TodasLasClaves(app.Resources).Except(clavesAntes).ToList();
                Assert.AreEqual(0, nuevas.Count, "Claves nuevas en Application.Resources: " + string.Join(", ", nuevas));

                // 6) El tamaño por defecto de TextBlock del resto de Nesto sigue siendo el de Windows (WPF-UI lo sube a 14)
                Assert.AreEqual(SystemFonts.MessageFontSize,
                    (double)TextBlock.FontSizeProperty.GetMetadata(typeof(TextBlock)).DefaultValue);
                Assert.AreEqual(SystemFonts.MessageFontSize, new TextBlock().FontSize);

                // 7) Los pinceles del tema llegan a la botonera y al menú (si no, saldrían sin colores)
                Assert.IsInstanceOfType(botonera.TryFindResource("ApplicationBackgroundBrush"), typeof(Brush));
                Assert.IsInstanceOfType(menu.TryFindResource("TextFillColorPrimaryBrush"), typeof(Brush));

                // 8) «Más» se abre también con el teclado (Click), no solo al soltar el ratón como el DropDownButton de WPF-UI,
                //    y el menú recibe el DataContext de la botonera (es un popup, fuera de su árbol)
                var contexto = new object();
                var ventana = new Window
                {
                    Content = botonera, DataContext = contexto, Width = 900, Height = 200, WindowStyle = WindowStyle.None,
                    ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = -20000
                };
                try
                {
                    ventana.Show();
                    Assert.IsFalse(menu.IsOpen);
                    mas.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                    Assert.IsTrue(menu.IsOpen, "Con el teclado (Click) se abre el menú «Más»");
                    Assert.AreSame(contexto, menu.DataContext);
                    Assert.AreSame(contexto, enMas[0].DataContext);
                    menu.IsOpen = false;
                }
                finally
                {
                    ventana.Close();
                }
            });
        }

        private static DependencyProperty CommandDe(FrameworkElement elemento)
        {
            return elemento is MenuItem ? MenuItem.CommandProperty : System.Windows.Controls.Primitives.ButtonBase.CommandProperty;
        }

        private static string RutaBinding(DependencyObject objeto, DependencyProperty propiedad)
        {
            return BindingOperations.GetBinding(objeto, propiedad)?.Path?.Path;
        }

        private static bool ExisteEnViewModel(string propiedad)
        {
            const BindingFlags flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase;
            return typeof(DetallePedidoViewModel).GetProperty(propiedad, flags) != null;
        }

        private static IEnumerable<object> TodasLasClaves(ResourceDictionary diccionario)
        {
            return diccionario.Keys.Cast<object>().Concat(diccionario.MergedDictionaries.SelectMany(TodasLasClaves));
        }

        private static void EjecutarEnSTA(Action action)
        {
            Exception excepcion = null;
            var hilo = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { excepcion = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (excepcion != null)
            {
                throw new AssertFailedException(excepcion.ToString(), excepcion);
            }
        }
    }
}
