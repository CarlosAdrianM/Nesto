using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Composicion;
using Nesto.Infrastructure.Contracts;
using System;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 3): registro propio de los diálogos en vez del RegisterDialog de Prism. Con el
    /// mismo nombre que antes (por defecto, el de la clase de la vista) la ventana propia tiene que encontrar el
    /// diálogo y recibir la vista con su ViewModel ya puesto, los dos nuevos cada vez y sacados del contenedor
    /// (lo que antes hacía el AutoWire del ViewModelLocator de Prism).
    /// </summary>
    [TestClass]
    public class RegistroDialogosTests
    {
        public interface IServicioDePrueba
        {
        }

        public class ServicioDePrueba : IServicioDePrueba
        {
        }

        public class VistaDePrueba : Grid
        {
        }

        public class DialogoDePrueba : DialogoNestoBase
        {
            public DialogoDePrueba(IServicioDePrueba servicio)
            {
                Servicio = servicio;
                Title = "Diálogo de prueba";
            }

            public IServicioDePrueba Servicio { get; }

            public ParametrosDialogo Recibidos { get; private set; }

            public override void OnDialogOpened(ParametrosDialogo parameters) => Recibidos = parameters;

            public void Aceptar() => RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "respuesta", 42 } }));
        }

        // Nesto#490 (4C.5, tramo 4): se registra y se resuelve con el contenedor de Nesto (MS.DI), sin registrar la vista ni
        // el ViewModel: el resolutor los construye como hacía Unity
        private static IResolutorNesto ContenedorConElDialogo(string nombre = null)
        {
            var registro = new ContenedorMicrosoft();
            registro.Transitorio<IServicioDePrueba, ServicioDePrueba>();
            registro.RegistrarDialogo<VistaDePrueba, DialogoDePrueba>(nombre);
            return registro.Construir();
        }

        [TestMethod]
        public void SinNombre_SeRegistraConElDeLaClaseDeLaVista_YLaVistaLlevaSuViewModel()
        {
            EjecutarEnSTA(() =>
            {
                IResolutorNesto contenedor = ContenedorConElDialogo();

                FrameworkElement vista = RegistroDialogos.CrearDialogo(contenedor, "VistaDePrueba");

                Assert.IsInstanceOfType(vista, typeof(VistaDePrueba));
                Assert.IsInstanceOfType(vista.DataContext, typeof(DialogoDePrueba));
                Assert.IsInstanceOfType(((DialogoDePrueba)vista.DataContext).Servicio, typeof(ServicioDePrueba), "El ViewModel sale del contenedor, con sus dependencias");
            });
        }

        [TestMethod]
        public void ConNombre_SoloSeEncuentraConEseNombre()
        {
            EjecutarEnSTA(() =>
            {
                IResolutorNesto contenedor = ContenedorConElDialogo("OtroNombre");

                Assert.IsNotNull(RegistroDialogos.CrearDialogo(contenedor, "OtroNombre"));
                Assert.IsNull(RegistroDialogos.CrearDialogo(contenedor, "VistaDePrueba"));
            });
        }

        [TestMethod]
        public void CadaVez_UnaVistaYUnViewModelNuevos()
        {
            EjecutarEnSTA(() =>
            {
                IResolutorNesto contenedor = ContenedorConElDialogo();

                FrameworkElement primera = RegistroDialogos.CrearDialogo(contenedor, "VistaDePrueba");
                FrameworkElement segunda = RegistroDialogos.CrearDialogo(contenedor, "VistaDePrueba");

                Assert.AreNotSame(primera, segunda);
                Assert.AreNotSame(primera.DataContext, segunda.DataContext);
            });
        }

        [TestMethod]
        public void UnNombreNoRegistrado_DevuelveNull()
        {
            var contenedor = new ContenedorMicrosoft().Construir();

            Assert.IsNull(RegistroDialogos.CrearDialogo(contenedor, "NoExiste"));
            Assert.IsNull(RegistroDialogos.CrearDialogo(contenedor, null));
        }

        [TestMethod]
        public void LaVentanaPropia_AbreElDialogoRegistradoYDevuelveSuResultado()
        {
            EjecutarEnSTA(() =>
            {
                IResolutorNesto contenedor = ContenedorConElDialogo();
                var servicio = new ServicioDialogosNesto(nombre => RegistroDialogos.CrearDialogo(contenedor, nombre));
                ResultadoDialogo recibido = null;

                VentanaDialogo ventana = servicio.PrepararVentana("VistaDePrueba", new ParametrosDialogo { { "pedido", 901001 } }, r => recibido = r);
                ventana.Show();
                Bombear();

                var vm = (DialogoDePrueba)ventana.DataContext;
                Assert.AreEqual(901001, vm.Recibidos.GetValue<int>("pedido"));
                Assert.AreEqual("Diálogo de prueba", ventana.Title);

                vm.Aceptar();

                Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
                Assert.AreEqual(42, recibido.Parameters.GetValue<int>("respuesta"));
                Assert.IsFalse(ventana.IsVisible);
            });
        }

        [TestMethod]
        public void LaVentanaPropia_ConUnNombreNoRegistrado_LanzaConElNombre()
        {
            EjecutarEnSTA(() =>
            {
                IResolutorNesto contenedor = ContenedorConElDialogo();
                var servicio = new ServicioDialogosNesto(nombre => RegistroDialogos.CrearDialogo(contenedor, nombre));

                var ex = Assert.ThrowsException<InvalidOperationException>(() => servicio.PrepararVentana("PickingPopupView", null, null));
                StringAssert.Contains(ex.Message, "PickingPopupView");
            });
        }

        [TestMethod]
        public void DialogoRegistrado_ValidaElNombreLaVistaYElViewModel()
        {
            _ = Assert.ThrowsException<ArgumentException>(() => new DialogoRegistrado("", typeof(VistaDePrueba), typeof(DialogoDePrueba)));
            _ = Assert.ThrowsException<ArgumentException>(() => new DialogoRegistrado("X", typeof(string), typeof(DialogoDePrueba)));
            _ = Assert.ThrowsException<ArgumentException>(() => new DialogoRegistrado("X", typeof(VistaDePrueba), typeof(object)));
        }

        [TestMethod]
        public void LasVistasDeDialogo_YaNoPidenSuViewModel()
        {
            // Sin el AutoWire de Prism en el XAML: el ViewModel lo pone el registro, no la vista.
            EjecutarEnSTA(() =>
            {
                foreach (FrameworkElement vista in new FrameworkElement[]
                {
                    new ConfirmationDialog(), new NotificationDialog(), new InputAmountDialog(), new InputTextDialog(),
                    new InputDateDialog(), new SelectorProductoDuplicadoDialog(), new NovedadesDialog(),
                    new ParametrosUsuarioDialog(), new RevisionConceptoDialog()
                })
                {
                    Assert.IsNull(vista.DataContext, $"{vista.GetType().Name} trae ViewModel por su cuenta");
                }
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
