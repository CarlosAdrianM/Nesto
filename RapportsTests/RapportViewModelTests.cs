using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
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
                A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
        }

        // Nesto#469 (Carlos, 16/09/26): si se le pregunta y la deja vacía, tiene que confirmar que no lo
        // sabe; si no confirma, no se guarda. Con valor o sin combo, no se pregunta.

        private static (RapportViewModel vm, IServicioDialogos dialogo) CrearViewModelConDialogo(bool respuestaUsuario)
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult("NV"));
            var dialogo = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogo.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<System.Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo parametros, System.Action<ResultadoDialogo> callback) =>
                {
                    var resultado = new ResultadoDialogo(respuestaUsuario ? ResultadoBoton.OK : ResultadoBoton.Cancel);
                    callback?.Invoke(resultado);
                });
            var vm = new RapportViewModel(configuracion, A.Fake<IRapportService>(), A.Fake<IRegionManager>(),
                dialogo, new WeakReferenceMessenger());
            return (vm, dialogo);
        }

        [TestMethod]
        public void EmpleadosSinRellenar_ElVendedorNoConfirma_NoSeGuarda()
        {
            var (vm, dialogo) = CrearViewModelConDialogo(respuestaUsuario: false);
            vm.rapport = new SeguimientoClienteDTO();
            vm.ClienteCompleto = new ClienteDTO { cliente = "15191", preguntarEmpleados = true, empleados = null };

            Assert.IsFalse(vm.ConfirmarEmpleadosSinRellenar());
            A.CallTo(() => dialogo.ShowDialog("ConfirmationDialog", A<ParametrosDialogo>._, A<System.Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void EmpleadosSinRellenar_ElVendedorConfirmaQueNoLoSabe_SeGuarda()
        {
            var (vm, _) = CrearViewModelConDialogo(respuestaUsuario: true);
            vm.rapport = new SeguimientoClienteDTO();
            vm.ClienteCompleto = new ClienteDTO { cliente = "15191", preguntarEmpleados = true, empleados = null };

            Assert.IsTrue(vm.ConfirmarEmpleadosSinRellenar());
            Assert.IsNull(vm.rapport.Empleados, "Viaja null: la API no toca la ficha");
        }

        [TestMethod]
        public void EmpleadosRellenados_OSinCombo_NoSePregunta()
        {
            var (conValor, dialogo1) = CrearViewModelConDialogo(respuestaUsuario: false);
            conValor.rapport = new SeguimientoClienteDTO { Empleados = 0 }; // «Sin empleados» también cuenta
            conValor.ClienteCompleto = new ClienteDTO { cliente = "15191", preguntarEmpleados = true };
            Assert.IsTrue(conValor.ConfirmarEmpleadosSinRellenar());
            A.CallTo(() => dialogo1.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<System.Action<ResultadoDialogo>>._)).MustNotHaveHappened();

            var (sinCombo, dialogo2) = CrearViewModelConDialogo(respuestaUsuario: false);
            sinCombo.rapport = new SeguimientoClienteDTO();
            sinCombo.ClienteCompleto = new ClienteDTO { cliente = "1", codigoPostal = "08001", preguntarEmpleados = false };
            Assert.IsTrue(sinCombo.ConfirmarEmpleadosSinRellenar());
            A.CallTo(() => dialogo2.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<System.Action<ResultadoDialogo>>._)).MustNotHaveHappened();
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
