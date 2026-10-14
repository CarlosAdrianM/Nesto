using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using Prism.Unity;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): «Parámetros de usuario» y «Novedades de Nesto» ya son IDialogoNesto
    /// (DialogoNestoBase) y tienen que abrirse igual con las dos ventanas: la de Prism (usuarios sin el piloto
    /// VentanaDialogosPropia), que los ve como IDialogAware, y la propia. Novedades trae su propio estilo de
    /// ventana (prism:Dialog.WindowStyle, 760×700 y redimensionable), que tiene que seguir mandando en las dos.
    /// </summary>
    [TestClass]
    public class DialogosNovedadesYParametrosTests
    {
        private static IServicioParametrosEditables ServicioConUnEditable()
        {
            var servicio = A.Fake<IServicioParametrosEditables>();
            A.CallTo(() => servicio.LeerEditables()).Returns(Task.FromResult(new List<ParametroEditableModel>
            {
                new() { Clave = "AlmacenPedidoVta", Descripcion = "Almacén", ValorActual = "ALG", Opciones = new List<OpcionParametroModel>() }
            }));
            return servicio;
        }

        [TestMethod]
        public void ParametrosUsuario_ConLaVentanaDePrism_RecibeLaInformacionYSeCierraConAceptar()
        {
            var vm = new ParametrosUsuarioDialogViewModel(ServicioConUnEditable());
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters { { "informacion", "Usuario: carlos" } });
            vm.CloseDialogCommand.Execute(null);

            Assert.AreEqual("Parámetros de usuario", prism.Title);
            Assert.AreEqual("Usuario: carlos", vm.Informacion);
            Assert.AreEqual(1, vm.Editables.Count);
            Assert.AreEqual(ButtonResult.OK, resultado.Result);
        }

        [TestMethod]
        public void ParametrosUsuario_ConLaVentanaPropia_CargaLosEditablesYSeCierraConAceptar()
        {
            var vm = new ParametrosUsuarioDialogViewModel(ServicioConUnEditable());
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;

            vm.OnDialogOpened(new ParametrosDialogo());
            vm.CloseDialogCommand.Execute(null);

            Assert.AreEqual("Parámetros de usuario", vm.Title);
            Assert.IsNull(vm.Informacion);
            Assert.AreEqual("AlmacenPedidoVta", vm.Editables[0].Clave);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }

        [TestMethod]
        public void ParametrosUsuario_SiNoSePuedenLeerLosEditables_LoDice()
        {
            var servicio = A.Fake<IServicioParametrosEditables>();
            A.CallTo(() => servicio.LeerEditables()).ThrowsAsync(new Exception("sin conexión"));
            var vm = new ParametrosUsuarioDialogViewModel(servicio);

            ((IDialogAware)vm).OnDialogOpened(null);

            Assert.AreEqual("No se pudieron cargar los parámetros editables: sin conexión", vm.Mensaje);
        }

        [TestMethod]
        public void Novedades_ConLaVentanaDePrism_TituloPorDefectoYElDelLlamante()
        {
            var vm = new NovedadesDialogViewModel();
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters { { "novedades", new List<NovedadUsuario> { new() { Version = "1.10.40.0", Titulo = "a", Categoria = "Nuevo" } } } });
            Assert.AreEqual("Novedades de Nesto", prism.Title);
            Assert.AreEqual(1, vm.Novedades.Count);

            prism.OnDialogOpened(new DialogParameters { { "title", "Qué hay de nuevo" } });
            Assert.AreEqual("Qué hay de nuevo", prism.Title);

            vm.CloseDialogCommand.Execute(null);
            Assert.AreEqual(ButtonResult.OK, resultado.Result);
        }

        [TestMethod]
        public void Novedades_VentanaPropia_AbreLaVistaRealConSuEstiloDeVentana()
        {
            EjecutarEnSTA(() =>
            {
                // Como en la aplicación: la vista real con su ViewModel, del registro propio (ya sin AutoWire de Prism).
                var registrado = new DialogoRegistrado("NovedadesDialog", typeof(NovedadesDialog), typeof(NovedadesDialogViewModel));
                var servicio = new ServicioDialogosNesto(nombre => registrado.Crear(Activator.CreateInstance));
                ResultadoDialogo recibido = null;

                var ventana = servicio.PrepararVentana("NovedadesDialog", new ParametrosDialogo
                {
                    { "novedades", new List<NovedadUsuario> { new() { Version = "1.10.40.0", Titulo = "a", Categoria = "Nuevo" } } }
                }, r => recibido = r);
                ventana.Show();
                Bombear();

                var vm = (NovedadesDialogViewModel)ventana.DataContext;
                Assert.AreEqual("Novedades de Nesto", ventana.Title);
                Assert.AreEqual(760d, ventana.Width, "Manda el estilo de ventana de la vista");
                Assert.AreEqual(ResizeMode.CanResize, ventana.ResizeMode);
                Assert.AreEqual(1, vm.Novedades.Count);

                vm.CloseDialogCommand.Execute(null);

                Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
                Assert.IsFalse(ventana.IsVisible);
            });
        }

        [TestMethod]
        public void Novedades_VentanaDePrism_AbreLaVistaRealConSuEstiloDeVentana()
        {
            EjecutarEnSTA(() =>
            {
                // La vista real (con su AutoWire); se da hecha para poder encontrar luego su ventana.
                var vista = new NovedadesDialog();
                var contenedor = new UnityContainerExtension();
                _ = contenedor.Register(typeof(IDialogWindow), typeof(DialogWindow));
                _ = contenedor.RegisterInstance(typeof(object), vista, "NovedadesDialog");
                var prism = new DialogService(contenedor);
                IDialogResult recibido = null;

                prism.Show("NovedadesDialog", new DialogParameters
                {
                    { "title", "Qué hay de nuevo" },
                    { "novedades", new List<NovedadUsuario> { new() { Version = "1.10.40.0", Titulo = "a", Categoria = "Nuevo" } } }
                }, r => recibido = r);
                Bombear();

                Window ventana = Window.GetWindow(vista);
                Assert.IsInstanceOfType(ventana, typeof(DialogWindow), "La abre la ventana de Prism");
                var vm = (NovedadesDialogViewModel)((FrameworkElement)ventana.Content).DataContext;
                Assert.AreEqual("Qué hay de nuevo", ventana.Title);
                Assert.AreEqual(760d, ventana.Width, "Manda el estilo de ventana de la vista");
                Assert.AreEqual(1, vm.Novedades.Count);

                vm.CloseDialogCommand.Execute(null);
                Bombear();

                Assert.IsNotNull(recibido, "La ventana de Prism tiene que cerrarse con el resultado del ViewModel");
                Assert.AreEqual(ButtonResult.OK, recibido.Result);
                Assert.IsFalse(ventana.IsVisible);
            });
        }

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
