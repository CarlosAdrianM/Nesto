using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3): la ventana de diálogos propia tiene que comportarse como la de
    /// Prism 8.1.97 (DialogService/DialogWindow), que es lo que los usuarios conocen: mismos
    /// eventos, mismo orden, misma ventana. Y durante la migración tiene que abrir también los
    /// ViewModels que siguen siendo IDialogAware de Prism.
    /// </summary>
    [TestClass]
    public class ServicioDialogosNestoTests
    {
        private class DialogoDePrueba : IDialogoNesto
        {
            public string Title { get; set; } = "Título del diálogo";
            public event Action<ResultadoDialogo> RequestClose;
            public bool PermitirCerrar { get; set; } = true;
            public ParametrosDialogo Recibidos { get; private set; }
            public int VecesCerrado { get; private set; }

            public bool CanCloseDialog() => PermitirCerrar;
            public void OnDialogClosed() => VecesCerrado++;
            public void OnDialogOpened(ParametrosDialogo parameters) => Recibidos = parameters;
            public void Cerrar(ResultadoDialogo resultado) => RequestClose?.Invoke(resultado);
        }

        private class DialogoPrismDePrueba : IDialogAware
        {
            public string Title => "Diálogo de Prism";
            public event Action<IDialogResult> RequestClose;
            public IDialogParameters Recibidos { get; private set; }
            public bool Cerrado { get; private set; }

            public bool CanCloseDialog() => true;
            public void OnDialogClosed() => Cerrado = true;
            public void OnDialogOpened(IDialogParameters parameters) => Recibidos = parameters;
            public void Cerrar(IDialogResult resultado) => RequestClose?.Invoke(resultado);
        }

        private static ServicioDialogosNesto ServicioQueDevuelve(Func<FrameworkElement> vista)
            => new(nombre => nombre == "MiDialogo" ? vista() : null);

        [TestMethod]
        public void Abrir_PasaLosParametrosAlViewModelYPoneSuTituloEnLaVentana()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new DialogoDePrueba();
                var servicio = ServicioQueDevuelve(() => new Grid { DataContext = vm });

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", new ParametrosDialogo { { "message", "Hola" } }, null);
                Assert.AreEqual(SizeToContent.WidthAndHeight, ventana.SizeToContent, "Como el DialogWindow de Prism: nace ajustada al contenido");
                ventana.Show();
                Bombear();

                Assert.AreEqual("Hola", vm.Recibidos.GetValue<string>("message"));
                Assert.AreEqual("Título del diálogo", ventana.Title);
                Assert.AreSame(vm, ventana.DataContext);
                Assert.AreEqual(WindowStartupLocation.CenterOwner, ventana.WindowStartupLocation);
                ventana.Close();
            });
        }

        [TestMethod]
        public void Abrir_SinParametros_ElViewModelRecibeUnaColeccionVacia()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new DialogoDePrueba();
                var servicio = ServicioQueDevuelve(() => new Grid { DataContext = vm });

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, null);

                Assert.IsNotNull(vm.Recibidos, "Como Prism: nunca null");
                Assert.AreEqual(0, vm.Recibidos.Count);
                ventana.Close();
            });
        }

        [TestMethod]
        public void RequestClose_CierraLaVentanaYDevuelveElResultadoAQuienLaAbrio()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new DialogoDePrueba();
                var servicio = ServicioQueDevuelve(() => new Grid { DataContext = vm });
                ResultadoDialogo recibido = null;

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, r => recibido = r);
                ventana.Show();
                Bombear();
                vm.Cerrar(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "amount", 12.5m } }));

                Assert.IsNotNull(recibido);
                Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
                Assert.AreEqual(12.5m, recibido.Parameters.GetValue<decimal>("amount"));
                Assert.AreEqual(1, vm.VecesCerrado);
                Assert.IsNull(ventana.DataContext, "Al cerrar suelta el ViewModel, como Prism");
                Assert.IsNull(ventana.Content);
            });
        }

        [TestMethod]
        public void CerrarConLaX_DevuelveNone()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new DialogoDePrueba();
                var servicio = ServicioQueDevuelve(() => new Grid { DataContext = vm });
                ResultadoDialogo recibido = null;

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, r => recibido = r);
                ventana.Show();
                Bombear();
                ventana.Close();

                Assert.AreEqual(ResultadoBoton.None, recibido.Result);
                Assert.AreEqual(1, vm.VecesCerrado);
            });
        }

        [TestMethod]
        public void SiElViewModelNoDejaCerrar_LaVentanaSigueAbierta()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new DialogoDePrueba { PermitirCerrar = false };
                var servicio = ServicioQueDevuelve(() => new Grid { DataContext = vm });
                bool llamado = false;

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, _ => llamado = true);
                ventana.Show();
                Bombear();
                ventana.Close();

                Assert.IsFalse(llamado);
                Assert.IsTrue(ventana.IsVisible);
                Assert.AreEqual(0, vm.VecesCerrado);

                vm.PermitirCerrar = true;
                ventana.Close();
                Assert.IsTrue(llamado);
            });
        }

        [TestMethod]
        public void LaVistaConSuPropioEstiloDeVentana_LoSustituye()
        {
            EjecutarEnSTA(() =>
            {
                var estilo = new Style(typeof(Window));
                estilo.Setters.Add(new Setter(FrameworkElement.WidthProperty, 640d));
                var servicio = ServicioQueDevuelve(() =>
                {
                    var vista = new Grid { DataContext = new DialogoDePrueba() };
                    Dialog.SetWindowStyle(vista, estilo);
                    return vista;
                });

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, null);

                Assert.AreSame(estilo, ventana.Style);
                Assert.AreEqual(640d, ventana.Width);
                Assert.AreEqual(SizeToContent.Manual, ventana.SizeToContent, "Como Prism: el estilo de la vista sustituye al de la ventana entero");
                ventana.Close();
            });
        }

        // Carlos 05/10/26: con Prism un texto largo no salía entero y, al hacer la ventana más grande,
        // el texto seguía del mismo tamaño. La ventana propia nace con un ancho razonable y, después,
        // el contenido se ajusta a lo que el usuario la redimensione.
        private static TextBlock TextoLargo() => new()
        {
            Text = string.Join(" ", System.Linq.Enumerable.Repeat("Un aviso con un texto muy largo que no cabe en una línea.", 40)),
            TextWrapping = TextWrapping.Wrap,
            DataContext = new DialogoDePrueba()
        };

        [TestMethod]
        public void UnTextoLargo_LaVentanaNaceConUnAnchoRazonable_YEnvuelveElTexto()
        {
            EjecutarEnSTA(() =>
            {
                var texto = TextoLargo();
                var servicio = ServicioQueDevuelve(() => texto);

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, null);
                ventana.Show();
                Bombear();

                Assert.IsTrue(ventana.ActualWidth <= VentanaDialogo.ANCHO_MAXIMO_INICIAL + 1, $"Ancho {ventana.ActualWidth}");
                Assert.IsTrue(texto.ActualHeight > 40, "El texto se envuelve en varias líneas");
                Assert.AreEqual(ResizeMode.CanResize, ventana.ResizeMode);
                ventana.Close();
            });
        }

        [TestMethod]
        public void AlAgrandarLaVentana_ElContenidoSeAjustaAlNuevoAncho()
        {
            EjecutarEnSTA(() =>
            {
                var texto = TextoLargo();
                var servicio = ServicioQueDevuelve(() => texto);
                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", null, null);
                ventana.Show();
                Bombear();

                ventana.Width = 900;
                Bombear();

                Assert.IsTrue(texto.ActualWidth > VentanaDialogo.ANCHO_MAXIMO_INICIAL, $"El texto ocupa {texto.ActualWidth}");
                ventana.Close();
            });
        }

        [TestMethod]
        public void LosDialogosDeMensaje_NoTienenUnTamanoFijo()
        {
            EjecutarEnSTA(() =>
            {
                foreach (FrameworkElement vista in new FrameworkElement[] { new NotificationDialog(), new ConfirmationDialog() })
                {
                    Assert.IsTrue(double.IsNaN(vista.Width), $"{vista.GetType().Name} con ancho fijo {vista.Width}");
                    Assert.IsTrue(double.IsNaN(vista.Height), $"{vista.GetType().Name} con alto fijo {vista.Height}");
                }
            });
        }

        [TestMethod]
        public void UnViewModelDePrism_SeAbreYCierraIgual()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new DialogoPrismDePrueba();
                var servicio = ServicioQueDevuelve(() => new Grid { DataContext = vm });
                ResultadoDialogo recibido = null;

                VentanaDialogo ventana = servicio.PrepararVentana("MiDialogo", new ParametrosDialogo { { "title", "Ojo" } }, r => recibido = r);
                ventana.Show();
                Bombear();

                Assert.AreEqual("Ojo", vm.Recibidos.GetValue<string>("title"));
                Assert.AreEqual("Diálogo de Prism", ventana.Title);

                vm.Cerrar(new DialogResult(ButtonResult.Yes, new DialogParameters { { "text", "escrito" } }));

                Assert.AreEqual(ResultadoBoton.Yes, recibido.Result);
                Assert.AreEqual("escrito", recibido.Parameters.GetValue<string>("text"));
                Assert.IsTrue(vm.Cerrado);
            });
        }

        [TestMethod]
        public void UnDialogoNoRegistrado_LanzaConSuNombre()
        {
            EjecutarEnSTA(() =>
            {
                var servicio = ServicioQueDevuelve(() => new Grid { DataContext = new DialogoDePrueba() });

                var ex = Assert.ThrowsException<InvalidOperationException>(() => servicio.PrepararVentana("OtroDialogo", null, null));
                StringAssert.Contains(ex.Message, "OtroDialogo");
            });
        }

        [TestMethod]
        public void UnViewModelQueNoEsDialogo_Lanza()
        {
            EjecutarEnSTA(() =>
            {
                var servicio = ServicioQueDevuelve(() => new Grid { DataContext = new object() });

                _ = Assert.ThrowsException<InvalidOperationException>(() => servicio.PrepararVentana("MiDialogo", null, null));
            });
        }

        [TestMethod]
        public void Conmutable_ConElParametroAUno_AbreConLaVentanaPropia()
        {
            IServicioDialogos prism = A.Fake<IServicioDialogos>();
            IServicioDialogos propio = A.Fake<IServicioDialogos>();
            int lecturas = 0;
            var servicio = new ServicioDialogosConmutable(prism, propio, () => { lecturas++; return true; });

            servicio.ShowError("uno");
            servicio.ShowNotification("dos");

            A.CallTo(() => propio.ShowDialog("NotificationDialog", A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => prism.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustNotHaveHappened();
            Assert.AreEqual(1, lecturas, "El parámetro se lee una sola vez");
        }

        [TestMethod]
        public void Conmutable_SinElParametro_AbreConPrism()
        {
            IServicioDialogos prism = A.Fake<IServicioDialogos>();
            IServicioDialogos propio = A.Fake<IServicioDialogos>();
            var servicio = new ServicioDialogosConmutable(prism, propio, () => false);

            servicio.Show("AvisoNoModal", null, null);

            A.CallTo(() => prism.Show("AvisoNoModal", null, null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => propio.Show(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Conmutable_SiNoSePuedeLeerElParametro_AbreConPrism()
        {
            IServicioDialogos prism = A.Fake<IServicioDialogos>();
            IServicioDialogos propio = A.Fake<IServicioDialogos>();
            var servicio = new ServicioDialogosConmutable(prism, propio, () => throw new Exception("Sin conexión"));

            servicio.ShowNotification("hola");

            A.CallTo(() => prism.ShowDialog("NotificationDialog", A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
        }

        // Deja que el Dispatcher procese lo pendiente (el Loaded de la ventana, los bindings).
        private static void Bombear()
        {
            var marco = new DispatcherFrame();
            _ = Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => marco.Continue = false));
            Dispatcher.PushFrame(marco);
        }

        private static void EjecutarEnSTA(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    accion();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
                finally
                {
                    Dispatcher.CurrentDispatcher.InvokeShutdown();
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.Message, error);
            }
        }
    }
}
