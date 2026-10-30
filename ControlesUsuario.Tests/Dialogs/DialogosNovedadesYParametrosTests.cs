using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): «Parámetros de usuario» y «Novedades de Nesto» son IDialogoNesto
    /// (DialogoNestoBase). Novedades trae su propio estilo de ventana (760×700 y redimensionable), que tiene
    /// que seguir mandando en la ventana propia. (Hasta el 14/10/26 se probaban también con la de Prism.)
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
        public void ParametrosUsuario_RecibeLaInformacionYSeCierraConAceptar()
        {
            var vm = new ParametrosUsuarioDialogViewModel(ServicioConUnEditable());
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo { { "informacion", "Usuario: carlos" } });
            vm.CloseDialogCommand.Execute(null);

            Assert.AreEqual("Parámetros de usuario", dialogo.Title);
            Assert.AreEqual("Usuario: carlos", vm.Informacion);
            Assert.AreEqual(1, vm.Editables.Count);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
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

            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo());

            Assert.AreEqual("No se pudieron cargar los parámetros editables: sin conexión", vm.Mensaje);
        }

        // Nesto#490 (panel lateral): NavegacionLateral se lee al arrancar; al guardarlo hay que avisar de que se reinicie
        [TestMethod]
        public void ParametrosUsuario_AlGuardarUnoQueRequiereReiniciar_AvisaDeCerrarYAbrirNesto()
        {
            var servicio = A.Fake<IServicioParametrosEditables>();
            var opciones = new List<OpcionParametroModel> { new() { Valor = "0", Descripcion = "Cinta" }, new() { Valor = "1", Descripcion = "Menú lateral" } };
            A.CallTo(() => servicio.LeerEditables()).Returns(Task.FromResult(new List<ParametroEditableModel>
            {
                new() { Clave = "NavegacionLateral", Descripcion = "Diseño nuevo: menú lateral en lugar de la cinta (requiere reiniciar Nesto)",
                        ValorActual = "0", RequiereReiniciar = true, Opciones = opciones }
            }));
            A.CallTo(() => servicio.Cambiar("NavegacionLateral", "1")).Returns(Task.FromResult(
                new ParametroEditableModel { Clave = "NavegacionLateral", ValorActual = "1", RequiereReiniciar = true, Opciones = opciones }));
            var vm = new ParametrosUsuarioDialogViewModel(servicio);
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo());

            vm.Editables[0].ValorSeleccionado = "1";
            vm.GuardarCommand.Execute(null);

            StringAssert.Contains(vm.Mensaje, "cierre Nesto y vuelva a abrirlo");
            StringAssert.Contains(vm.Mensaje, "«Diseño nuevo: menú lateral en lugar de la cinta (requiere reiniciar Nesto)»");
        }

        [TestMethod]
        public void ParametrosUsuario_AlGuardarUnoNormal_ElMensajeDeSiempre()
        {
            var servicio = ServicioConUnEditable();
            A.CallTo(() => servicio.Cambiar("AlmacenPedidoVta", "AMZ")).Returns(Task.FromResult(
                new ParametroEditableModel { Clave = "AlmacenPedidoVta", ValorActual = "AMZ", Opciones = new List<OpcionParametroModel>() }));
            var vm = new ParametrosUsuarioDialogViewModel(servicio);
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo());

            vm.Editables[0].ValorSeleccionado = "AMZ";
            vm.GuardarCommand.Execute(null);

            Assert.AreEqual("Cambios guardados. Ya están activos para los próximos pedidos.", vm.Mensaje);
        }

        [TestMethod]
        public void Novedades_TituloPorDefectoYElDelLlamante()
        {
            var vm = new NovedadesDialogViewModel();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo { { "novedades", new List<NovedadUsuario> { new() { Version = "1.10.40.0", Titulo = "a", Categoria = "Nuevo" } } } });
            Assert.AreEqual("Novedades de Nesto", dialogo.Title);
            Assert.AreEqual(1, vm.Novedades.Count);

            dialogo.OnDialogOpened(new ParametrosDialogo { { "title", "Qué hay de nuevo" } });
            Assert.AreEqual("Qué hay de nuevo", dialogo.Title);

            vm.CloseDialogCommand.Execute(null);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
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
                // Nesto#575 (sugerencia 900): se puede minimizar y volver desde la barra de tareas sin bloquear Nesto
                Assert.IsTrue(DialogoNesto.GetSinBloquear((DependencyObject)ventana.Content));
                Assert.IsTrue(ventana.ShowInTaskbar);
                Assert.AreEqual(1, vm.Novedades.Count);

                vm.CloseDialogCommand.Execute(null);

                Assert.AreEqual(ResultadoBoton.OK, recibido.Result);
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
