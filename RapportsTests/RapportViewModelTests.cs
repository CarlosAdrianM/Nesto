using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Threading.Tasks;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#469 (NestoAPI#464): la combo de empleados del centro en la pantalla de rapport. La
    /// regla de a quién se le pregunta vive en la API y llega como <c>preguntarEmpleados</c>; el
    /// ViewModel solo la enseña, la rellena con lo que sabe la ficha y manda lo que elija el
    /// vendedor. Sin tocarla, viaja como null y la API no cambia nada.
    /// </summary>
    [TestClass]
    public class RapportViewModelTests
    {
        private static RapportViewModel CrearViewModel()
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult("NV"));
            return new RapportViewModel(configuracion, A.Fake<IRapportService>(), A.Fake<IRegionManager>(),
                A.Fake<IDialogService>(), A.Fake<IEventAggregator>());
        }

        [TestMethod]
        public void EmpleadosCombo_SoloSeEnsenaSiLaApiLoPide()
        {
            var vm = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO();

            vm.ClienteCompleto = new ClienteDTO { cliente = "15191", codigoPostal = "28100", preguntarEmpleados = true };
            Assert.IsTrue(vm.EstaVisibleEmpleados);

            vm.ClienteCompleto = new ClienteDTO { cliente = "1", codigoPostal = "08001", preguntarEmpleados = false };
            Assert.IsFalse(vm.EstaVisibleEmpleados);
        }

        [TestMethod]
        public void EmpleadosCombo_SinCliente_NoSeEnsena()
        {
            var vm = CrearViewModel();

            Assert.IsFalse(vm.EstaVisibleEmpleados);
        }

        [TestMethod]
        public void Empleados_SiLaFichaYaLoSabe_LaComboSaleRellena()
        {
            var vm = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO();

            vm.ClienteCompleto = new ClienteDTO { cliente = "15191", preguntarEmpleados = true, empleados = 3 };

            Assert.AreEqual((byte)3, vm.rapport.Empleados);
        }

        [TestMethod]
        public void Empleados_SiLaFichaNoLoSabe_ViajaComoNull()
        {
            // Es lo que hace que la API no toque la ficha cuando el vendedor no responde.
            var vm = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO();

            vm.ClienteCompleto = new ClienteDTO { cliente = "15191", preguntarEmpleados = true, empleados = null };

            Assert.IsNull(vm.rapport.Empleados);
        }

        [TestMethod]
        public void Empleados_ElRapportLlegaDespuesDelCliente_TambienSeRellena()
        {
            // En la navegación el orden no está garantizado: primero puede resolverse el selector.
            var vm = CrearViewModel();
            vm.ClienteCompleto = new ClienteDTO { cliente = "15191", preguntarEmpleados = true, empleados = 2 };

            vm.rapport = new SeguimientoClienteDTO();

            Assert.AreEqual((byte)2, vm.rapport.Empleados);
        }

        [TestMethod]
        public void Empleados_LoQueYaPusoElVendedor_NoSePisaConLaFicha()
        {
            var vm = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO { Empleados = 5 };

            vm.ClienteCompleto = new ClienteDTO { cliente = "15191", preguntarEmpleados = true, empleados = 1 };

            Assert.AreEqual((byte)5, vm.rapport.Empleados);
        }

        [TestMethod]
        public void ListaEmpleados_TieneLasSeisOpcionesConElValorQueGuardaLaApi()
        {
            var vm = CrearViewModel();

            Assert.AreEqual(6, vm.listaEmpleados.Count);
            Assert.AreEqual((byte)0, vm.listaEmpleados[0].id);
            Assert.AreEqual("Sin empleados", vm.listaEmpleados[0].descripcion);
            Assert.AreEqual((byte)5, vm.listaEmpleados[5].id);
            Assert.AreEqual("5 o más", vm.listaEmpleados[5].descripcion);
        }
    }
}
