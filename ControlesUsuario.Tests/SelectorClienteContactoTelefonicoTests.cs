using ControlesUsuario.Models;
using ControlesUsuario.Services;
using ControlesUsuario.ViewModels;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Sugerencia 582 (María José): en Rapports, a la vendedora telefónica le sale el contacto en
    /// estado 9 del cliente y no el principal. El selector solo lo pide con PriorizarContactoTelefonico
    /// (Rapports); quién es telefónico lo decide la API con el vendedor del usuario.
    /// </summary>
    [TestClass]
    public class SelectorClienteContactoTelefonicoTests
    {
        private const string EMPRESA = "1";

        private static void EnSta(Action accion)
        {
            Exception error = null;
            Thread hilo = new Thread(() =>
            {
                try { accion(); }
                catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            Assert.IsNull(error, error?.ToString());
        }

        private static (SelectorClienteViewModel vm, ISelectorClienteService servicio) Preparar(bool priorizar)
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(EMPRESA, "Vendedor")).Returns(Task.FromResult("MPP"));
            var servicio = A.Fake<ISelectorClienteService>();
            A.CallTo(() => servicio.CargarCliente(EMPRESA, "10932", A<string>._))
                .Returns(new ClienteDTO { empresa = EMPRESA, cliente = "10932", contacto = "0", estado = 7 });
            A.CallTo(() => servicio.CargarClienteContactoTelefonico(EMPRESA, "10932", "MPP"))
                .Returns(new ClienteDTO { empresa = EMPRESA, cliente = "10932", contacto = "1", estado = 9 });
            A.CallTo(() => servicio.BuscarClientes(EMPRESA, A<string>._, A<string>._))
                .Returns(new ObservableCollection<ClienteDTO>());
            A.CallTo(() => servicio.BuscarClientesContactoTelefonicoPrimero(EMPRESA, A<string>._, A<string>._))
                .Returns(new ObservableCollection<ClienteDTO>());

            var vm = new SelectorClienteViewModel(configuracion, servicio)
            {
                PriorizarContactoTelefonico = priorizar
            };
            vm.CargarVendedor(EMPRESA).GetAwaiter().GetResult();
            return (vm, servicio);
        }

        [TestMethod]
        public void EnRapports_TeclearElClienteSinContacto_PideElContactoTelefonicoConElVendedor()
        {
            EnSta(() =>
            {
                var (vm, servicio) = Preparar(priorizar: true);

                vm.cargarCliente(EMPRESA, "10932", null);

                Assert.AreEqual("1", (vm.listaClientes.ElementoSeleccionado as ClienteDTO)?.contacto);
                A.CallTo(() => servicio.CargarClienteContactoTelefonico(EMPRESA, "10932", "MPP")).MustHaveHappenedOnceExactly();
                A.CallTo(() => servicio.CargarCliente(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
            });
        }

        [TestMethod]
        public void EnRapports_ConContactoYaElegido_RespetaEseContacto()
        {
            EnSta(() =>
            {
                var (vm, servicio) = Preparar(priorizar: true);

                vm.cargarCliente(EMPRESA, "10932", "0");

                A.CallTo(() => servicio.CargarCliente(EMPRESA, "10932", "0")).MustHaveHappenedOnceExactly();
                A.CallTo(() => servicio.CargarClienteContactoTelefonico(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
            });
        }

        [TestMethod]
        public void FueraDeRapports_TeclearElCliente_SigueSaliendoElPrincipal()
        {
            EnSta(() =>
            {
                var (vm, servicio) = Preparar(priorizar: false);

                vm.cargarCliente(EMPRESA, "10932", null);

                Assert.AreEqual("0", (vm.listaClientes.ElementoSeleccionado as ClienteDTO)?.contacto);
                A.CallTo(() => servicio.CargarClienteContactoTelefonico(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
            });
        }

        [TestMethod]
        public void EnRapports_BuscarPorTexto_PideLaListaConElContactoTelefonicoPrimero()
        {
            EnSta(() =>
            {
                var (vm, servicio) = Preparar(priorizar: true);
                A.CallTo(() => servicio.CargarClienteContactoTelefonico(EMPRESA, "peluquería", A<string>._)).Returns((ClienteDTO)null);

                vm.cargarCliente(EMPRESA, "peluquería", null);

                A.CallTo(() => servicio.BuscarClientesContactoTelefonicoPrimero(EMPRESA, "MPP", "peluquería")).MustHaveHappenedOnceExactly();
                A.CallTo(() => servicio.BuscarClientes(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
            });
        }
    }
}
