using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using System;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>Nesto#652 (NestoAPI#903): los DIR3 del cliente en la pestaña «Direcciones» de la ficha. Con el servicio falso.</summary>
    [TestClass]
    public class FacturaElectronicaClienteViewModelTests
    {
        private IServicioFacturae _servicio;
        private IConfiguracion _configuracion;
        private Dir3ClienteModel _enviado;

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IServicioFacturae>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            A.CallTo(() => _servicio.LeerDir3("1", "33895", "0")).Returns(Task.FromResult(new Dir3ClienteModel
            {
                Empresa = "1", Cliente = "33895", Contacto = "0", OficinaContable = "L01280796", OrganoGestor = "L01280796",
                UnidadTramitadora = "L01280796", NombreOficinaContable = "Intervención", Guardado = true, TablaCreada = true,
                Usuario = "Carlos", FechaModificacion = new DateTime(2026, 11, 24)
            }));
            A.CallTo(() => _servicio.GuardarDir3(A<Dir3ClienteModel>._))
                .ReturnsLazily((Dir3ClienteModel d) => { _enviado = d; return Task.FromResult(d); });
        }

        private FacturaElectronicaClienteViewModel Seccion() => new(_servicio, _configuracion);

        [TestMethod]
        public async Task Cargar_EnseñaLoGuardado()
        {
            var vm = Seccion();
            await vm.CargarAsync("1", "33895 ", "0");

            Assert.AreEqual("L01280796", vm.OficinaContable);
            Assert.AreEqual("Intervención", vm.NombreOficinaContable);
            Assert.IsTrue(vm.Guardado);
            StringAssert.Contains(vm.Mensaje, "Guardados por Carlos el 24/11/2026");
        }

        [TestMethod]
        public async Task SinPermiso_NiSeVeNiSePregunta()
        {
            var vm = new FacturaElectronicaClienteViewModel(_servicio, A.Fake<IConfiguracion>());
            await vm.CargarAsync("1", "33895", "0");

            Assert.IsFalse(vm.PuedeVer);
            A.CallTo(() => _servicio.LeerDir3(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_FaltaUnObligatorio_NoLlamaALaApi()
        {
            var vm = Seccion();
            await vm.CargarAsync("1", "33895", "0");
            vm.OrganoGestor = "";

            await vm.GuardarAsync();

            StringAssert.Contains(vm.Error, "Falta el código DIR3 del órgano gestor");
            A.CallTo(() => _servicio.GuardarDir3(A<Dir3ClienteModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_MandaLosCodigosLimpiosConClienteYContacto()
        {
            var vm = Seccion();
            await vm.CargarAsync("1", "33895", "0");
            vm.UnidadTramitadora = "ge 000-1234";
            vm.OrganoProponente = "";

            await vm.GuardarAsync();

            Assert.AreEqual("33895", _enviado.Cliente);
            Assert.AreEqual("0", _enviado.Contacto);
            Assert.AreEqual("GE0001234", _enviado.UnidadTramitadora);
            Assert.IsNull(_enviado.OrganoProponente);
            StringAssert.Contains(vm.Mensaje, "guardados para el cliente 33895/0");
        }

        [TestMethod]
        public async Task Quitar_MandaLosCuatroVacios()
        {
            var vm = Seccion();
            await vm.CargarAsync("1", "33895", "0");

            await vm.QuitarAsync();

            Assert.IsNull(_enviado.OficinaContable);
            Assert.IsNull(_enviado.OrganoGestor);
            Assert.IsNull(_enviado.UnidadTramitadora);
            StringAssert.Contains(vm.Mensaje, "Se han quitado");
            Assert.IsNull(vm.OficinaContable);
        }

        [TestMethod]
        public async Task Guardar_ErrorDeLaApi_SeEnseña()
        {
            A.CallTo(() => _servicio.GuardarDir3(A<Dir3ClienteModel>._)).ThrowsAsync(new InvalidOperationException("Todavía no existe la tabla de los DIR3."));
            var vm = Seccion();
            await vm.CargarAsync("1", "33895", "0");

            await vm.GuardarAsync();

            StringAssert.Contains(vm.Error, "Todavía no existe la tabla");
            Assert.IsFalse(vm.EstaOcupado);
        }
    }
}
