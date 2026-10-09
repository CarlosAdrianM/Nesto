using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using Prism.Unity;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): los ViewModels de diálogo que pasan a <see cref="IDialogoNesto"/>
    /// (por <see cref="DialogoNestoBase"/>) tienen que seguir abriéndose igual con la ventana de Prism, que es
    /// la de los usuarios sin el piloto <c>VentanaDialogosPropia</c>: Prism los ve como <see cref="IDialogAware"/>
    /// y les llegan los mismos parámetros y de ellos el mismo resultado que cuando eran IDialogAware.
    /// </summary>
    [TestClass]
    public class DialogoNestoBaseTests
    {
        [TestMethod]
        public void ComoIDialogAware_LosParametrosDePrismLleganAlViewModel()
        {
            IDialogAware prism = new InputTextDialogViewModel();

            prism.OnDialogOpened(new DialogParameters { { "title", "Nombre" }, { "message", "¿Cómo se llama?" }, { "defaultText", "Pepe" } });

            var vm = (InputTextDialogViewModel)prism;
            Assert.AreEqual("Nombre", vm.Title);
            Assert.AreEqual("Nombre", prism.Title);
            Assert.AreEqual("¿Cómo se llama?", vm.Message);
            Assert.AreEqual("Pepe", vm.Text);
        }

        [TestMethod]
        public void ComoIDialogAware_SinParametros_ConservaLosValoresPorDefecto()
        {
            IDialogAware prism = new NotificationDialogViewModel();

            prism.OnDialogOpened(null);

            Assert.AreEqual("Información", prism.Title);
            Assert.IsNull(((NotificationDialogViewModel)prism).Message);
        }

        [TestMethod]
        public void ComoIDialogAware_ElResultadoLlegaConLosTiposDePrism()
        {
            var vm = new InputTextDialogViewModel { Text = "escrito" };
            IDialogResult recibido = null;
            ((IDialogAware)vm).RequestClose += r => recibido = r;

            vm.AcceptCommand.Execute(null);

            Assert.AreEqual(ButtonResult.OK, recibido.Result);
            Assert.AreEqual("escrito", recibido.Parameters.GetValue<string>("text"));
        }

        [TestMethod]
        public void ComoIDialogAware_ElImporteVuelveComoDecimal()
        {
            var vm = new InputAmountDialogViewModel { Amount = "12" };
            IDialogResult recibido = null;
            ((IDialogAware)vm).RequestClose += r => recibido = r;

            vm.AcceptCommand.Execute(null);

            Assert.AreEqual(ButtonResult.OK, recibido.Result);
            Assert.AreEqual(12m, recibido.Parameters.GetValue<decimal>("amount"));
        }

        [TestMethod]
        public void ComoIDialogAware_LaFechaPropuestaYLaElegida()
        {
            var vm = new InputDateDialogViewModel();
            IDialogResult recibido = null;
            ((IDialogAware)vm).RequestClose += r => recibido = r;

            ((IDialogAware)vm).OnDialogOpened(new DialogParameters { { "defaultDate", new DateTime(2026, 10, 2) } });
            vm.AcceptCommand.Execute(null);

            Assert.AreEqual(ButtonResult.OK, recibido.Result);
            Assert.AreEqual(new DateTime(2026, 10, 2), recibido.Parameters.GetValue<DateTime>("date"));
        }

        [TestMethod]
        public void ComoIDialogAware_ConfirmarYCancelar()
        {
            var vm = new ConfirmationDialogViewModel();
            IDialogResult recibido = null;
            ((IDialogAware)vm).RequestClose += r => recibido = r;

            vm.CloseDialogCommand.Execute("true");
            Assert.AreEqual(ButtonResult.OK, recibido.Result);

            vm.CloseDialogCommand.Execute("false");
            Assert.AreEqual(ButtonResult.Cancel, recibido.Result);

            vm.CloseDialogCommand.Execute(null);
            Assert.AreEqual(ButtonResult.None, recibido.Result);
            Assert.IsTrue(((IDialogAware)vm).CanCloseDialog());
        }

        [TestMethod]
        public void ComoIDialogAware_DesuscribirseDejaDeRecibir()
        {
            var vm = new ConfirmationDialogViewModel();
            int veces = 0;
            void contar(IDialogResult r) => veces++;
            ((IDialogAware)vm).RequestClose += contar;
            vm.CloseDialogCommand.Execute("true");

            ((IDialogAware)vm).RequestClose -= contar;
            vm.CloseDialogCommand.Execute("true");

            Assert.AreEqual(1, veces);
        }

        [TestMethod]
        public void ComoIDialogAware_SelectorProductoDuplicado_DevuelveElProductoElegido()
        {
            var vm = new SelectorProductoDuplicadoDialogViewModel();
            IDialogAware prism = vm;
            IDialogResult recibido = null;
            prism.RequestClose += r => recibido = r;

            prism.OnDialogOpened(new DialogParameters
            {
                { "candidatos", new System.Collections.Generic.List<global::ControlesUsuario.Models.ProductoCodigoBarrasDuplicado> { new() { Producto = "17404" }, new() { Producto = "17405" } } }
            });
            vm.Seleccionado = vm.Candidatos[1];
            vm.AceptarCommand.Execute(null);

            Assert.AreEqual("Código de barras duplicado", prism.Title);
            Assert.AreEqual(ButtonResult.OK, recibido.Result);
            Assert.AreEqual("17405", recibido.Parameters.GetValue<string>("producto"));
        }

        [TestMethod]
        public void ComoIDialogAware_RevisionConcepto_DejarElMioCierraConCancel()
        {
            var vm = new RevisionConceptoDialogViewModel();
            IDialogAware prism = vm;
            IDialogResult recibido = null;
            prism.RequestClose += r => recibido = r;

            prism.OnDialogOpened(new DialogParameters { { RevisionConceptoDialogViewModel.PARAMETRO_ORIGINAL, "Curso micronileng" } });
            vm.DejarElMioCommand.Execute(null);

            Assert.AreEqual("¿Quisiste decir…?", prism.Title);
            Assert.AreEqual("Curso micronileng", vm.Original);
            Assert.AreEqual(ButtonResult.Cancel, recibido.Result);
        }

        [TestMethod]
        public void ComoIDialogoNesto_ElResultadoLlegaConLosTiposPropios()
        {
            var vm = new InputTextDialogViewModel { Text = null };
            ResultadoDialogo recibido = null;
            ((IDialogoNesto)vm).RequestClose += r => recibido = r;

            vm.AcceptCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
            Assert.AreEqual(string.Empty, recibido.Parameters.GetValue<string>("text"));
        }

        [TestMethod]
        public void VentanaPropia_AbreElViewModelMigradoYDevuelveSuResultado()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new ConfirmationDialogViewModel();
                var servicio = new ServicioDialogosNesto(nombre => new Grid { DataContext = vm });
                ResultadoDialogo recibido = null;

                VentanaDialogo ventana = servicio.PrepararVentana("ConfirmationDialog", new ParametrosDialogo { { "title", "¿Seguro?" }, { "message", "Se borra" } }, r => recibido = r);
                ventana.Show();
                Bombear();

                Assert.AreEqual("¿Seguro?", ventana.Title);
                Assert.AreEqual("Se borra", vm.Message);

                vm.CloseDialogCommand.Execute("true");

                Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
                Assert.IsFalse(ventana.IsVisible);
            });
        }

        // La vista que el contenedor de Prism crea para el diálogo, con el ViewModel que pone la prueba.
        private class VistaDePrueba : Grid
        {
            [ThreadStatic]
            public static object ViewModelSiguiente;

            [ThreadStatic]
            public static VistaDePrueba Ultima;

            public VistaDePrueba()
            {
                DataContext = ViewModelSiguiente;
                Ultima = this;
            }
        }

        [TestMethod]
        public void VentanaDePrism_AbreElViewModelMigradoYDevuelveSuResultado()
        {
            EjecutarEnSTA(() =>
            {
                var vm = new InputTextDialogViewModel();
                VistaDePrueba.ViewModelSiguiente = vm;
                var contenedor = new UnityContainerExtension();
                _ = contenedor.Register(typeof(IDialogWindow), typeof(DialogWindow));
                _ = contenedor.Register(typeof(object), typeof(VistaDePrueba), "MiDialogo");
                var prism = new DialogService(contenedor);
                IDialogResult recibido = null;

                prism.Show("MiDialogo", new DialogParameters { { "title", "Nombre" }, { "defaultText", "Pepe" } }, r => recibido = r);
                Bombear();

                Window ventana = Window.GetWindow(VistaDePrueba.Ultima);
                Assert.IsInstanceOfType(ventana, typeof(DialogWindow), "La abre la ventana de Prism");
                Assert.AreEqual("Nombre", vm.Title);
                Assert.AreEqual("Pepe", vm.Text);
                Assert.AreEqual("Nombre", ventana.Title);

                vm.Text = "Juan";
                vm.AcceptCommand.Execute(null);
                Bombear();

                Assert.IsNotNull(recibido, "La ventana de Prism tiene que cerrarse con el resultado del ViewModel");
                Assert.AreEqual(ButtonResult.OK, recibido.Result);
                Assert.AreEqual("Juan", recibido.Parameters.GetValue<string>("text"));
                Assert.IsFalse(ventana.IsVisible);
            });
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
