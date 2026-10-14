using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3): los ViewModels de diálogo (<see cref="IDialogoNesto"/>, por
    /// <see cref="DialogoNestoBase"/>) reciben los parámetros de quien los abre y devuelven su resultado con los
    /// tipos propios. Hasta el 14/10/26 estas pruebas los abrían también como IDialogAware (ventana de Prism,
    /// usuarios sin el piloto VentanaDialogosPropia); desde que la ventana propia es la de todos, solo así.
    /// </summary>
    [TestClass]
    public class DialogoNestoBaseTests
    {
        [TestMethod]
        public void LosParametrosLleganAlViewModel()
        {
            IDialogoNesto dialogo = new InputTextDialogViewModel();

            dialogo.OnDialogOpened(new ParametrosDialogo { { "title", "Nombre" }, { "message", "¿Cómo se llama?" }, { "defaultText", "Pepe" } });

            var vm = (InputTextDialogViewModel)dialogo;
            Assert.AreEqual("Nombre", vm.Title);
            Assert.AreEqual("Nombre", dialogo.Title);
            Assert.AreEqual("¿Cómo se llama?", vm.Message);
            Assert.AreEqual("Pepe", vm.Text);
        }

        [TestMethod]
        public void SinParametros_ConservaLosValoresPorDefecto()
        {
            IDialogoNesto dialogo = new NotificationDialogViewModel();

            dialogo.OnDialogOpened(new ParametrosDialogo());

            Assert.AreEqual("Información", dialogo.Title);
            Assert.IsNull(((NotificationDialogViewModel)dialogo).Message);
        }

        [TestMethod]
        public void ElResultadoLlevaElTextoEscrito()
        {
            var vm = new InputTextDialogViewModel { Text = "escrito" };
            ResultadoDialogo recibido = null;
            ((IDialogoNesto)vm).RequestClose += r => recibido = r;

            vm.AcceptCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
            Assert.AreEqual("escrito", recibido.Parameters.GetValue<string>("text"));
        }

        [TestMethod]
        public void ElImporteVuelveComoDecimal()
        {
            var vm = new InputAmountDialogViewModel { Amount = "12" };
            ResultadoDialogo recibido = null;
            ((IDialogoNesto)vm).RequestClose += r => recibido = r;

            vm.AcceptCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
            Assert.AreEqual(12m, recibido.Parameters.GetValue<decimal>("amount"));
        }

        [TestMethod]
        public void LaFechaPropuestaYLaElegida()
        {
            var vm = new InputDateDialogViewModel();
            ResultadoDialogo recibido = null;
            ((IDialogoNesto)vm).RequestClose += r => recibido = r;

            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "defaultDate", new DateTime(2026, 10, 2) } });
            vm.AcceptCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
            Assert.AreEqual(new DateTime(2026, 10, 2), recibido.Parameters.GetValue<DateTime>("date"));
        }

        [TestMethod]
        public void ConfirmarYCancelar()
        {
            var vm = new ConfirmationDialogViewModel();
            ResultadoDialogo recibido = null;
            ((IDialogoNesto)vm).RequestClose += r => recibido = r;

            vm.CloseDialogCommand.Execute("true");
            Assert.AreEqual(ResultadoBoton.OK, recibido.Result);

            vm.CloseDialogCommand.Execute("false");
            Assert.AreEqual(ResultadoBoton.Cancel, recibido.Result);

            vm.CloseDialogCommand.Execute(null);
            Assert.AreEqual(ResultadoBoton.None, recibido.Result);
            Assert.IsTrue(((IDialogoNesto)vm).CanCloseDialog());
        }

        [TestMethod]
        public void DesuscribirseDejaDeRecibir()
        {
            var vm = new ConfirmationDialogViewModel();
            int veces = 0;
            void contar(ResultadoDialogo r) => veces++;
            ((IDialogoNesto)vm).RequestClose += contar;
            vm.CloseDialogCommand.Execute("true");

            ((IDialogoNesto)vm).RequestClose -= contar;
            vm.CloseDialogCommand.Execute("true");

            Assert.AreEqual(1, veces);
        }

        [TestMethod]
        public void SelectorProductoDuplicado_DevuelveElProductoElegido()
        {
            var vm = new SelectorProductoDuplicadoDialogViewModel();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo recibido = null;
            dialogo.RequestClose += r => recibido = r;

            dialogo.OnDialogOpened(new ParametrosDialogo
            {
                { "candidatos", new System.Collections.Generic.List<global::ControlesUsuario.Models.ProductoCodigoBarrasDuplicado> { new() { Producto = "17404" }, new() { Producto = "17405" } } }
            });
            vm.Seleccionado = vm.Candidatos[1];
            vm.AceptarCommand.Execute(null);

            Assert.AreEqual("Código de barras duplicado", dialogo.Title);
            Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
            Assert.AreEqual("17405", recibido.Parameters.GetValue<string>("producto"));
        }

        [TestMethod]
        public void RevisionConcepto_DejarElMioCierraConCancel()
        {
            var vm = new RevisionConceptoDialogViewModel();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo recibido = null;
            dialogo.RequestClose += r => recibido = r;

            dialogo.OnDialogOpened(new ParametrosDialogo { { RevisionConceptoDialogViewModel.PARAMETRO_ORIGINAL, "Curso micronileng" } });
            vm.DejarElMioCommand.Execute(null);

            Assert.AreEqual("¿Quisiste decir…?", dialogo.Title);
            Assert.AreEqual("Curso micronileng", vm.Original);
            Assert.AreEqual(ResultadoBoton.Cancel, recibido.Result);
        }

        [TestMethod]
        public void SinTexto_DevuelveUnaCadenaVacia()
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
