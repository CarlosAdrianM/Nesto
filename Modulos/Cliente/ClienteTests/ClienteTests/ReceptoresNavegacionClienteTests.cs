using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#490 (4C.4): Crear cliente recibe la navegación por <see cref="IReceptorNavegacion"/> en vez de
    /// INavigationAware. Reutiliza la pestaña abierta (lo que hacía IsNavigationTarget = true, y lo que hace Prism
    /// sin INavigationAware) y <c>AlLlegar</c> hace lo de <c>OnNavigatedTo</c>, con las mismas claves.
    /// </summary>
    [TestClass]
    public class ReceptoresNavegacionClienteTests
    {
        private IClienteService _servicio;
        private CrearClienteViewModel _vm;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IClienteService>();
            _vm = new CrearClienteViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), _servicio, new WeakReferenceMessenger(), A.Fake<IServicioDialogos>());
        }

        [TestMethod]
        public void CrearCliente_NoDependeDeLaNavegacionDePrismYReutilizaLaPestana()
        {
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(CrearClienteViewModel)));
            Assert.IsTrue(typeof(IReceptorNavegacion).IsAssignableFrom(typeof(CrearClienteViewModel)));
            Assert.IsFalse(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(CrearClienteViewModel)));
        }

        [TestMethod]
        public void CrearCliente_AlLlegarConEmpresaClienteYContacto_CargaElClienteParaModificarlo()
        {
            A.CallTo(() => _servicio.LeerClienteCrear("1", "15191", "0")).Returns(Task.FromResult(new ClienteCrear
            {
                Empresa = "1",
                Cliente = "15191",
                Contacto = "0",
                Nombre = "Peluquería Prueba",
                PersonasContacto = new List<PersonaContactoDTO>()
            }));

            _vm.AlLlegar(new ParametrosNavegacion { { "empresaParameter", "1" }, { "clienteParameter", "15191" }, { "contactoParameter", "0" } });

            A.CallTo(() => _servicio.LeerClienteCrear("1", "15191", "0")).MustHaveHappenedOnceExactly();
            Assert.IsTrue(_vm.EsUnaModificacion);
            Assert.AreEqual("15191", _vm.ClienteNumero);
            Assert.AreEqual("PELUQUERÍA PRUEBA", _vm.ClienteNombre); // el nombre se guarda en mayúsculas
        }

        [TestMethod]
        public void CrearCliente_AlLlegarConNifYNombre_LosValidaParaDarDeAlta()
        {
            _vm.AlLlegar(new ParametrosNavegacion { { "nifParameter", "B12345678" }, { "nombreParameter", "Centro Prueba" } });

            // Rellena NIF y nombre y pasa a datos generales, que valida el NIF con la API
            A.CallTo(() => _servicio.ValidarNif("B12345678", A<string>._)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(_vm.EsUnaModificacion);
            A.CallTo(() => _servicio.LeerClienteCrear(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void CrearCliente_AlLlegarSinParametros_NoTocaNada()
        {
            _vm.AlLlegar(new ParametrosNavegacion());
            _vm.AlLlegar(null);

            Assert.IsNull(_vm.ClienteNif);
            Assert.IsFalse(_vm.EsUnaModificacion);
            A.CallTo(() => _servicio.LeerClienteCrear(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
