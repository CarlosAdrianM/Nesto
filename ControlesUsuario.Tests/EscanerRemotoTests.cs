using ControlesUsuario.Behaviors;
using ControlesUsuario.Dialogs;
using ControlesUsuario.EscanerRemoto;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.Xaml.Behaviors;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#589 (NestoAPI#768): escáner remoto. Lo leído con Ariadna va al campo con el foco SOLO si acepta códigos de barras
    /// (y se pulsa Intro, como un lector USB); si no, el aviso «Último código leído».
    /// </summary>
    [TestClass]
    public class EscanerRemotoTests
    {
        private sealed class EscanerFalso : IEscanerRemoto
        {
            public event EventHandler<LecturaEscanerRemoto> LecturaRecibida;
            public void NotificarActividad(bool conFoco) { }
            public void Llega(string codigo, string clave = null)
                => LecturaRecibida?.Invoke(this, new LecturaEscanerRemoto { Codigo = codigo, ClaveIdempotencia = clave });
            public bool TieneSuscriptores => LecturaRecibida != null;
        }

        private sealed class OrdenFalsa : System.Windows.Input.ICommand
        {
            public int Veces;
            public string TextoAlEjecutar;
            public Func<string> Leer;
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) { Veces++; TextoAlEjecutar = Leer?.Invoke(); }
        }

        [TestMethod]
        public void DestinoDe_SoloElCuadroMarcadoHabilitadoYEditable()
        {
            EnVentana((ventana, marcado, normal) =>
            {
                Assert.AreSame(marcado, LectorCodigoBarras.DestinoDe(marcado));
                Assert.IsNull(LectorCodigoBarras.DestinoDe(normal), "Un cuadro sin marcar no recibe lecturas");
                Assert.IsNull(LectorCodigoBarras.DestinoDe(null));
                Assert.IsNull(LectorCodigoBarras.DestinoDe(new Button()));

                marcado.IsReadOnly = true;
                Assert.IsNull(LectorCodigoBarras.DestinoDe(marcado), "De solo lectura, no");
                marcado.IsReadOnly = false;
                marcado.IsEnabled = false;
                Assert.IsNull(LectorCodigoBarras.DestinoDe(marcado), "Deshabilitado, no");
                marcado.IsEnabled = true;
                marcado.Visibility = Visibility.Collapsed;
                Assert.IsNull(LectorCodigoBarras.DestinoDe(marcado), "Oculto, no");
            });
        }

        [TestMethod]
        public void Teclear_EscribeDondeEstaElCursorYPulsaIntroComoElLector()
        {
            EnVentana((ventana, marcado, normal) =>
            {
                var orden = new OrdenFalsa { Leer = () => marcado.Text };
                marcado.InputBindings.Add(new KeyBinding(orden, Key.Return, ModifierKeys.None));
                var teclasArriba = new List<Key>();
                marcado.KeyUp += (s, e) => teclasArriba.Add(e.Key);
                marcado.Text = "17404";
                marcado.SelectAll();

                LectorCodigoBarras.Teclear(marcado, "8412345678901");

                Assert.AreEqual("8412345678901", marcado.Text, "Sustituye lo seleccionado, como al teclear");
                Assert.AreEqual(1, orden.Veces, "El Intro dispara el KeyBinding del campo");
                Assert.AreEqual("8412345678901", orden.TextoAlEjecutar, "Con el código ya escrito");
                CollectionAssert.AreEqual(new[] { Key.Enter }, teclasArriba, "También el KeyUp de Intro (Inventario y la ficha lo usan)");
            });
        }

        [TestMethod]
        public void Teclear_SiElPreviewKeyDownAtiendeElIntro_NoSigueElKeyDown()
        {
            EnVentana((ventana, marcado, normal) =>
            {
                int keyDown = 0;
                marcado.PreviewKeyDown += (s, e) => { if (e.Key == Key.Enter) { e.Handled = true; } };
                marcado.KeyDown += (s, e) => keyDown++;

                LectorCodigoBarras.Teclear(marcado, "123");

                Assert.AreEqual("123", marcado.Text);
                Assert.AreEqual(0, keyDown);
            });
        }

        [TestMethod]
        public void Receptor_FocoEnCampoQueAcepta_LoTecleaYNoAvisa()
        {
            EnVentana((ventana, marcado, normal) =>
            {
                var escaner = new EscanerFalso();
                var tecleados = new List<(TextBox, string)>();
                var avisos = new List<string>();
                using (new ReceptorEscanerRemoto(escaner, Dispatcher.CurrentDispatcher, () => marcado, _ => true,
                    (c, codigo) => tecleados.Add((c, codigo)), avisos.Add))
                {
                    escaner.Llega(" 8412345678901 ", "k1");
                    Bombear();
                }

                Assert.AreEqual(1, tecleados.Count);
                Assert.AreSame(marcado, tecleados[0].Item1);
                Assert.AreEqual("8412345678901", tecleados[0].Item2);
                Assert.AreEqual(0, avisos.Count);
            });
        }

        [TestMethod]
        public void Receptor_FocoEnCampoQueNoAcepta_AvisaYNoTeclea()
        {
            EnVentana((ventana, marcado, normal) =>
            {
                var escaner = new EscanerFalso();
                var tecleados = new List<string>();
                var avisos = new List<string>();
                using (new ReceptorEscanerRemoto(escaner, Dispatcher.CurrentDispatcher, () => normal, _ => true,
                    (c, codigo) => tecleados.Add(codigo), avisos.Add))
                {
                    escaner.Llega("8412345678901");
                    Bombear();
                }

                CollectionAssert.AreEqual(new[] { "8412345678901" }, avisos);
                Assert.AreEqual(0, tecleados.Count);
                Assert.AreEqual("", normal.Text, "No se escribe nada en un campo que no acepta códigos");
            });
        }

        [TestMethod]
        public void Receptor_SinFocoOConNestoDetras_Avisa()
        {
            EnVentana((ventana, marcado, normal) =>
            {
                var escaner = new EscanerFalso();
                var tecleados = new List<string>();
                var avisos = new List<string>();
                IInputElement foco = null;
                using (new ReceptorEscanerRemoto(escaner, Dispatcher.CurrentDispatcher, () => foco, _ => false,
                    (c, codigo) => tecleados.Add(codigo), avisos.Add))
                {
                    escaner.Llega("A1");
                    Bombear();
                    foco = marcado; // el campo acepta, pero la ventana no está delante
                    escaner.Llega("A2");
                    Bombear();
                }

                CollectionAssert.AreEqual(new[] { "A1", "A2" }, avisos);
                Assert.AreEqual(0, tecleados.Count);
            });
        }

        [TestMethod]
        public void Receptor_LaMismaClaveSoloUnaVezYLaVaciaNunca()
        {
            EnVentana((ventana, marcado, normal) =>
            {
                var escaner = new EscanerFalso();
                var tecleados = new List<string>();
                using (new ReceptorEscanerRemoto(escaner, Dispatcher.CurrentDispatcher, () => marcado, _ => true,
                    (c, codigo) => tecleados.Add(codigo), _ => { }))
                {
                    escaner.Llega("123", "k1");
                    escaner.Llega("123", "k1");
                    escaner.Llega("   ", "k2");
                    escaner.Llega("123", "k3");
                    Bombear();
                }

                CollectionAssert.AreEqual(new[] { "123", "123" }, tecleados);
            });
        }

        [TestMethod]
        public void Receptor_SiFallaAlTeclear_AvisaYNoLanza()
        {
            EnVentana((ventana, marcado, normal) =>
            {
                var escaner = new EscanerFalso();
                var avisos = new List<string>();
                using (new ReceptorEscanerRemoto(escaner, Dispatcher.CurrentDispatcher, () => marcado, _ => true,
                    (c, codigo) => throw new InvalidOperationException("roto"), avisos.Add))
                {
                    escaner.Llega("123");
                    Bombear();
                }

                CollectionAssert.AreEqual(new[] { "123" }, avisos);
            });
        }

        [TestMethod]
        public void Receptor_AlDesecharloDejaDeEscuchar()
        {
            EjecutarEnSTA(() =>
            {
                var escaner = new EscanerFalso();
                var receptor = new ReceptorEscanerRemoto(escaner, Dispatcher.CurrentDispatcher, () => null, _ => true, (c, s) => { }, _ => { });
                Assert.IsTrue(escaner.TieneSuscriptores);
                receptor.Dispose();
                Assert.IsFalse(escaner.TieneSuscriptores);
            });
        }

        [TestMethod]
        public void ProductoBehavior_MarcaElCampoDeProductoComoQueAceptaCodigos()
        {
            EjecutarEnSTA(() =>
            {
                var producto = new TextBox();
                Assert.IsFalse(LectorCodigoBarras.GetAceptaCodigoBarras(producto));

                Interaction.GetBehaviors(producto).Add(new ProductoBehavior());

                Assert.IsTrue(LectorCodigoBarras.GetAceptaCodigoBarras(producto));
            });
        }

        [TestMethod]
        public void DialogoDeTexto_GetCodigoBarrasMarcaElCuadro()
        {
            var vm = new InputTextDialogViewModel();
            vm.OnDialogOpened(new ParametrosDialogo { { "title", "Leer etiqueta de recogida" } });
            Assert.IsFalse(vm.AceptaCodigoBarras, "GetText de siempre: no acepta lecturas");

            var conCodigo = new InputTextDialogViewModel();
            conCodigo.OnDialogOpened(new ParametrosDialogo { { "title", "Leer etiqueta de recogida" }, { InputTextDialogViewModel.PARAMETRO_ACEPTA_CODIGO_BARRAS, true } });
            Assert.IsTrue(conCodigo.AceptaCodigoBarras);
        }

        [TestMethod]
        public void Aviso_EnseñaElUltimoCodigo()
        {
            EjecutarEnSTA(() =>
            {
                var aviso = new AvisoUltimoCodigoLeido();
                aviso.Poner("8412345678901");
                Assert.AreEqual("8412345678901", aviso.Codigo);
                aviso.Poner("R81010-2");
                Assert.AreEqual("R81010-2", aviso.Codigo);
                Assert.IsFalse(aviso.ShowActivated, "No quita el foco a lo que esté haciendo");
                Assert.IsTrue(aviso.Topmost);
                aviso.Close();
            });
        }

        /// <summary>Una ventana de verdad (fuera de la pantalla y sin activarla) con un cuadro marcado (con el foco) y otro sin marcar.</summary>
        private static void EnVentana(Action<Window, TextBox, TextBox> prueba)
        {
            EjecutarEnSTA(() =>
            {
                var marcado = new TextBox();
                LectorCodigoBarras.SetAceptaCodigoBarras(marcado, true);
                var normal = new TextBox();
                var panel = new StackPanel();
                panel.Children.Add(marcado);
                panel.Children.Add(normal);
                var ventana = new Window
                {
                    Content = panel,
                    Left = -10000,
                    Top = -10000,
                    Width = 200,
                    Height = 100,
                    ShowInTaskbar = false,
                    ShowActivated = false,
                    WindowStartupLocation = WindowStartupLocation.Manual
                };
                ventana.Show();
                try
                {
                    _ = marcado.Focus();
                    Keyboard.Focus(marcado);
                    prueba(ventana, marcado, normal);
                }
                finally
                {
                    ventana.Close();
                }
            });
        }

        private static void Bombear()
        {
            var marco = new DispatcherFrame();
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => marco.Continue = false));
            Dispatcher.PushFrame(marco);
        }

        private static void EjecutarEnSTA(Action action)
        {
            Exception capturada = null;
            var hilo = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { capturada = ex; }
                finally { Dispatcher.CurrentDispatcher.InvokeShutdown(); }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.Message, capturada);
            }
        }
    }
}
