using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;

using Nesto.Modulos.Cliente;
using Direccion2Cliente = Nesto.Modulos.Cliente.Models.Direccion2Cliente;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#643 (gemela de NestoAPI#874): el número propio cuando Google da la calle sin él y «¿Qué es?» (Dirección 2) en el alta.
    /// </summary>
    [TestClass]
    public class CrearClienteDireccion2Tests
    {
        private readonly IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IClienteService servicio = A.Fake<IClienteService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();

        private CrearClienteViewModel Vm() => new CrearClienteViewModel(navegacion, configuracion, servicio, new WeakReferenceMessenger(), dialogos);

        private async Task<CrearClienteViewModel> ConCalleSinNumero()
        {
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJ42143", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle Sor Livia Alcorta", CodigoPostal = "28100", Poblacion = "Alcobendas", PaisIso = "ES" });
            CrearClienteViewModel vm = Vm();
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ42143" });
            return vm;
        }

        // ---------------------------------------------------------------- regla (espejo de la API)

        [TestMethod]
        public void Regla_ComponeComoLaApi()
        {
            Assert.AreEqual("1º D", Direccion2Cliente.Componer("Piso", "1", "d", null, null, null, null, out _));
            Assert.AreEqual("LOCAL 6 · RÓTULO: EL EDÉN", Direccion2Cliente.Componer("Local", null, null, "6", "El Edén", null, null, out _));
            Assert.AreEqual("LOCAL · RÓTULO: EL EDÉN", Direccion2Cliente.Componer("Local", null, null, null, "el edén", null, null, out _));
            Assert.AreEqual("CASA", Direccion2Cliente.Componer("Casa", null, null, null, null, null, null, out _));
            Assert.AreEqual("NAVE 4", Direccion2Cliente.Componer("Nave", null, null, null, null, "4", null, out _));
            Assert.AreEqual("CENTRO COMERCIAL, PLANTA 2", Direccion2Cliente.Componer("Otro", null, null, null, null, null, "centro comercial, planta 2", out _));
        }

        [TestMethod]
        public void Regla_LoQueFalta()
        {
            Direccion2Cliente.Componer(null, null, null, null, null, null, null, out string sinTipo);
            Assert.AreEqual(Direccion2Cliente.MOTIVO_SIN_TIPO, sinTipo);
            Direccion2Cliente.Componer("Piso", "1", null, null, null, null, null, out string sinPuerta);
            Assert.AreEqual(Direccion2Cliente.MOTIVO_PISO, sinPuerta);
            Direccion2Cliente.Componer("Local", null, null, "6", " ", null, null, out string sinRotulo);
            Assert.AreEqual(Direccion2Cliente.MOTIVO_LOCAL, sinRotulo);
            Direccion2Cliente.Componer("Nave", null, null, null, null, null, null, out string sinNave);
            Assert.AreEqual(Direccion2Cliente.MOTIVO_NAVE, sinNave);
            Direccion2Cliente.Componer("Otro", null, null, null, null, null, null, out string sinTexto);
            Assert.AreEqual(Direccion2Cliente.MOTIVO_OTRO, sinTexto);
        }

        // ---------------------------------------------------------------- «¿Qué es?»

        [TestMethod]
        public async Task SinQueEs_NoSePuedeCrear()
        {
            CrearClienteViewModel vm = await ConCalleSinNumero();
            vm.ClienteNumeroCalle = "13";

            Assert.IsFalse(vm.SePuedeCrearCliente);
            Assert.AreEqual(Direccion2Cliente.MOTIVO_SIN_TIPO, vm.MotivoNoSePuedeCrearCliente);
        }

        [TestMethod]
        public async Task Local_ElRotuloEsObligatorioYNoSeProponeElNombre()
        {
            CrearClienteViewModel vm = await ConCalleSinNumero();
            vm.ClienteNumeroCalle = "13";
            vm.ClienteNombre = "CENTRO DE ESTETICA EL EDEN, SLU";

            vm.ClienteTipoLugar = Direccion2Cliente.LOCAL;
            vm.ClienteNumeroLocal = "6";

            Assert.IsTrue(vm.EsLocal);
            Assert.IsNull(vm.ClienteRotulo, "El rótulo no se rellena con el nombre fiscal");
            Assert.AreEqual(Direccion2Cliente.MOTIVO_LOCAL, vm.MotivoNoSePuedeCrearCliente);

            vm.ClienteRotulo = "El Edén";

            Assert.IsTrue(vm.SePuedeCrearCliente);
            Assert.AreEqual("LOCAL 6 · RÓTULO: EL EDÉN", vm.Direccion2Compuesta);
        }

        [TestMethod]
        public void EnUnaModificacion_NoSePideQueEs()
        {
            CrearClienteViewModel vm = Vm();
            vm.EsUnaModificacion = true;
            vm.ClienteDireccion = "CALLE DE TODA LA VIDA, 7";

            Assert.IsFalse(vm.EsAlta);
            Assert.IsTrue(vm.SePuedeCrearCliente);
        }

        [TestMethod]
        public void SinDireccion_NoSePideQueEs()
        {
            // Contactos de cobro: como la API, sin dirección no hay nada que pedir.
            Assert.IsTrue(Vm().SePuedeCrearCliente);
        }

        // ---------------------------------------------------------------- número propio

        [TestMethod]
        public async Task GoogleSinNumero_PideElNumero()
        {
            CrearClienteViewModel vm = await ConCalleSinNumero();
            vm.ClienteTipoLugar = Direccion2Cliente.CASA;

            Assert.IsTrue(vm.DireccionVerificadaPorGoogle);
            Assert.IsTrue(vm.NumeroSinDarPorGoogle);
            Assert.AreEqual(CrearClienteViewModel.MOTIVO_SIN_NUMERO, vm.MotivoNoSePuedeCrearCliente);

            vm.ClienteNumeroCalle = "13";

            Assert.IsNull(vm.MotivoNoSePuedeCrearCliente);
        }

        [TestMethod]
        public async Task GoogleConNumero_NoLoPide()
        {
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJ1", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle Mayor", Numero = "1", CodigoPostal = "28001" });
            CrearClienteViewModel vm = Vm();
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ1" });

            Assert.IsFalse(vm.NumeroSinDarPorGoogle);
        }

        [TestMethod]
        public async Task BorrarLaDireccion_OlvidaElNumero()
        {
            CrearClienteViewModel vm = await ConCalleSinNumero();
            vm.ClienteNumeroCalle = "13";

            vm.LimpiarDireccionCalleNumeroCommand.Execute(null);

            Assert.IsFalse(vm.NumeroSinDarPorGoogle);
            Assert.IsNull(vm.ClienteNumeroCalle);
        }

        [TestMethod]
        public async Task Validar_MandaElNumeroALaApiParaComprobarlo()
        {
            A.CallTo(() => servicio.ValidarDatosGenerales(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .Returns(new RespuestaDatosGeneralesClientes { DireccionFormateada = "C/ SOR LIVIA ALCORTA, 13", CodigoPostal = "28100", ClientesMismoTelefono = new List<ClienteTelefonoLookup>() });
            CrearClienteViewModel vm = await ConCalleSinNumero();
            vm.ClienteNumeroCalle = " 13 ";

            await vm.GoToDatosComisiones();

            A.CallTo(() => servicio.ValidarDatosGenerales("Calle Sor Livia Alcorta", "28100", A<string>.Ignored, true, "ES", "13")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("C/ SOR LIVIA ALCORTA, 13", vm.ClienteDireccion);
            Assert.IsTrue(vm.ClienteDireccionValidada);
        }

        [TestMethod]
        public async Task Validar_SinElNumero_NoSigueNiPreguntaALaApi()
        {
            CrearClienteViewModel vm = await ConCalleSinNumero();

            await vm.GoToDatosComisiones();

            A.CallTo(() => dialogos.ShowError(CrearClienteViewModel.MOTIVO_SIN_NUMERO)).MustHaveHappened();
            A.CallTo(() => servicio.ValidarDatosGenerales(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => servicio.ValidarDatosGenerales(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
            Assert.IsFalse(vm.ClienteDireccionValidada);
        }

        // ---------------------------------------------------------------- alta

        [TestMethod]
        public async Task Crear_MandaElNumeroYLoDeQueEs()
        {
            CrearClienteViewModel vm = await ConCalleSinNumero();
            vm.ClienteNumeroCalle = "13";
            vm.ClienteDireccion = "C/ SOR LIVIA ALCORTA, 13";
            vm.ClienteTipoLugar = Direccion2Cliente.LOCAL;
            vm.ClienteNumeroLocal = "6";
            vm.ClienteRotulo = "El Edén";
            vm.ClientePiso = "1"; // de un tipo elegido antes: no viaja

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c =>
                c.Numero == "13" && c.TipoLugar == "Local" && c.NumeroLocal == "6" && c.Rotulo == "El Edén" && c.Piso == null)))
                .MustHaveHappened();
        }

        [TestMethod]
        public async Task Crear_SinQueEs_NoLlamaAlServidor()
        {
            CrearClienteViewModel vm = await ConCalleSinNumero();
            vm.ClienteNumeroCalle = "13";
            vm.ClienteDireccion = "C/ SOR LIVIA ALCORTA, 13";

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowError(Direccion2Cliente.MOTIVO_SIN_TIPO)).MustHaveHappened();
        }
    }
}
