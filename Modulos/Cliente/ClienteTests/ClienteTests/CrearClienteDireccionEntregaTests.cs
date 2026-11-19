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
    /// Alta con dirección de entrega distinta (19/11/26, gemela de NestoAPI ClienteCrear.DireccionEntrega): «La entrega es en otra dirección»
    /// abre un segundo bloque de dirección igual que el fiscal (BloqueDireccionAlta) y la API crea el contacto de entrega.
    /// </summary>
    [TestClass]
    public class CrearClienteDireccionEntregaTests
    {
        private readonly IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IClienteService servicio = A.Fake<IClienteService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();

        public CrearClienteDireccionEntregaTests()
        {
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJFiscal", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle Mayor", Numero = "1", CodigoPostal = "28001", Poblacion = "Madrid", Provincia = "Madrid", PaisIso = "ES" });
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJEntrega", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle del Local", Numero = "6", CodigoPostal = "28100", Poblacion = "Alcobendas", Provincia = "Madrid", PaisIso = "ES" });
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJEntregaSinCp", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Rua da Praia", Numero = "3", Poblacion = "Vila do Conde", PaisIso = "PT" });
            A.CallTo(() => servicio.ValidarDatosGenerales("Calle Mayor, 1", A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored))
                .Returns(new RespuestaDatosGeneralesClientes { DireccionFormateada = "C/ MAYOR, 1", CodigoPostal = "28001", Ruta = "16", TelefonoFormateado = "916000000", VendedorEstetica = "JE", ClientesMismoTelefono = new List<ClienteTelefonoLookup> { new ClienteTelefonoLookup { Cliente = "1" } } });
            A.CallTo(() => servicio.ValidarDatosGenerales("Calle del Local, 6", A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored))
                .Returns(new RespuestaDatosGeneralesClientes { DireccionFormateada = "C/ DEL LOCAL, 6", CodigoPostal = "28100", Ruta = "AT", TelefonoFormateado = "916000000", VendedorEstetica = "XX", ClientesMismoTelefono = new List<ClienteTelefonoLookup> { new ClienteTelefonoLookup { Cliente = "1" } } });
        }

        private CrearClienteViewModel Vm() => new CrearClienteViewModel(navegacion, configuracion, servicio, new WeakReferenceMessenger(), dialogos);

        /// <summary>Las dos direcciones elegidas de Google y con «¿Qué es?» contestado.</summary>
        private async Task<CrearClienteViewModel> ConLasDosDirecciones()
        {
            CrearClienteViewModel vm = Vm();
            vm.ClienteTipoLugar = Direccion2Cliente.CASA;
            vm.ClienteTelefono = "916000000";
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJFiscal" });
            vm.EntregaEnOtraDireccion = true;
            await vm.DireccionEntrega.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJEntrega" });
            vm.DireccionEntrega.TipoLugar = Direccion2Cliente.LOCAL;
            vm.DireccionEntrega.NumeroLocal = "6";
            vm.DireccionEntrega.Rotulo = "El Edén";
            vm.DireccionEntrega.ComentariosRuta = "Timbre 2";
            vm.DireccionEntrega.ComentariosPicking = "Frágil";
            return vm;
        }

        // ---------------------------------------------------------------- la casilla

        [TestMethod]
        public void Casilla_SoloAlCrearUnClienteNuevo()
        {
            CrearClienteViewModel vm = Vm();
            Assert.IsTrue(vm.PuedeIndicarEntregaEnOtraDireccion);
            Assert.IsFalse(vm.HayDireccionEntrega, "Sin marcar no cuenta");

            vm.EntregaEnOtraDireccion = true;
            Assert.IsTrue(vm.HayDireccionEntrega);

            vm.ClienteEsContacto = true;
            Assert.IsFalse(vm.PuedeIndicarEntregaEnOtraDireccion, "Al crear un contacto de un cliente que ya existe, no");
            Assert.IsFalse(vm.HayDireccionEntrega);

            CrearClienteViewModel modificando = Vm();
            modificando.EsUnaModificacion = true;
            Assert.IsFalse(modificando.PuedeIndicarEntregaEnOtraDireccion, "Al modificar, no");
        }

        [TestMethod]
        public void MarcarLaCasilla_ProponeElTelefonoDeLaFiscal()
        {
            CrearClienteViewModel vm = Vm();
            vm.ClienteTelefono = "916000000";

            vm.EntregaEnOtraDireccion = true;

            Assert.AreEqual("916000000", vm.DireccionEntrega.Telefono);
            Assert.IsTrue(vm.DireccionEntrega.MostrarComentarios, "La de entrega pide sus comentarios de ruta y de picking");
            Assert.IsFalse(vm.DireccionFiscal.MostrarComentarios);
        }

        // ---------------------------------------------------------------- lo que falta

        [TestMethod]
        public async Task Marcada_SinDireccionDeEntrega_NoSePuedeCrear()
        {
            CrearClienteViewModel vm = Vm();
            vm.ClienteTipoLugar = Direccion2Cliente.CASA;
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJFiscal" });
            Assert.IsTrue(vm.SePuedeCrearCliente);

            vm.EntregaEnOtraDireccion = true;

            Assert.IsFalse(vm.SePuedeCrearCliente);
            StringAssert.StartsWith(vm.MotivoNoSePuedeCrearCliente, CrearClienteViewModel.PREFIJO_ENTREGA);
        }

        [TestMethod]
        public void EntregaTecleadaAMano_ElMotivoDiceQueEsLaDeEntrega()
        {
            CrearClienteViewModel vm = Vm();
            vm.EntregaEnOtraDireccion = true;

            vm.DireccionEntrega.CalleNumero = "Calle inventada, 3";

            Assert.AreEqual(CrearClienteViewModel.PREFIJO_ENTREGA + BloqueDireccionAlta.MOTIVO_DIRECCION_A_MANO, vm.MotivoNoSePuedeCrearCliente);
        }

        [TestMethod]
        public async Task EntregaSinQueEs_NoSePuedeCrear()
        {
            CrearClienteViewModel vm = await ConLasDosDirecciones();
            vm.DireccionEntrega.TipoLugar = null;

            Assert.AreEqual(CrearClienteViewModel.PREFIJO_ENTREGA + Direccion2Cliente.MOTIVO_SIN_TIPO, vm.MotivoNoSePuedeCrearCliente);
        }

        [TestMethod]
        public async Task Entrega_ElCodigoPostalSaleDeGoogleYSoloSeTecleaSiNoLoDa()
        {
            CrearClienteViewModel vm = Vm();
            vm.EntregaEnOtraDireccion = true;
            Assert.IsFalse(vm.DireccionEntrega.CodigoPostalIsEnabled, "Corte 1: sin dirección de Google, el CP no se teclea");

            await vm.DireccionEntrega.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJEntrega" });
            Assert.AreEqual("28100", vm.DireccionEntrega.CodigoPostal);
            Assert.IsFalse(vm.DireccionEntrega.CodigoPostalIsEnabled);

            await vm.DireccionEntrega.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJEntregaSinCp" });
            Assert.IsTrue(vm.DireccionEntrega.CodigoPostalIsEnabled, "Novedad 482: Google no da el CP, se teclea");
            Assert.AreEqual("PT", vm.DireccionEntrega.PaisDireccion);
            Assert.AreEqual("ES", vm.ClientePaisDireccion, "La fiscal no se toca");
        }

        // ---------------------------------------------------------------- validar al pasar de «Datos generales»

        [TestMethod]
        public async Task Validar_LasDosDirecciones_YLosVendedoresSalenDeLaFiscal()
        {
            CrearClienteViewModel vm = await ConLasDosDirecciones();

            await vm.GoToDatosComisiones();

            Assert.IsTrue(vm.DatosGeneralesValidados);
            Assert.AreEqual("C/ MAYOR, 1", vm.ClienteDireccion);
            Assert.AreEqual("C/ DEL LOCAL, 6", vm.DireccionEntrega.Direccion);
            Assert.AreEqual("AT", vm.DireccionEntrega.Ruta);
            Assert.AreEqual("ALCOBENDAS", vm.DireccionEntrega.Poblacion);
            Assert.AreEqual("JE", vm.ClienteVendedorEstetica, "Los vendedores, de la fiscal");
            A.CallTo(() => dialogos.ShowDialog("NotificacionTelefonoView", A<ParametrosDialogo>._, A<System.Action<ResultadoDialogo>>._))
                .MustHaveHappenedOnceExactly(); // el mismo teléfono en las dos: el aviso, una vez
        }

        [TestMethod]
        public async Task Validar_SinDireccionDeEntrega_NoSePasa()
        {
            CrearClienteViewModel vm = Vm();
            vm.ClienteTipoLugar = Direccion2Cliente.CASA;
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJFiscal" });
            vm.EntregaEnOtraDireccion = true;

            await vm.GoToDatosComisiones();

            Assert.IsTrue(vm.ClienteDireccionValidada);
            Assert.IsFalse(vm.DatosGeneralesValidados);
            A.CallTo(() => dialogos.ShowError(A<string>.That.StartsWith(CrearClienteViewModel.PREFIJO_ENTREGA))).MustHaveHappened();
        }

        [TestMethod]
        public async Task Validar_SinMarcar_SoloLaFiscal()
        {
            CrearClienteViewModel vm = Vm();
            vm.ClienteTipoLugar = Direccion2Cliente.CASA;
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJFiscal" });

            await vm.GoToDatosComisiones();

            Assert.IsTrue(vm.DatosGeneralesValidados);
            A.CallTo(() => servicio.ValidarDatosGenerales(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<bool>.Ignored, A<string>.Ignored))
                .MustHaveHappenedOnceExactly();
        }

        // ---------------------------------------------------------------- lo que viaja a la API

        [TestMethod]
        public async Task Crear_MandaLaDireccionDeEntrega()
        {
            CrearClienteViewModel vm = await ConLasDosDirecciones();
            await vm.GoToDatosComisiones();

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c =>
                c.Direccion == "C/ MAYOR, 1" && c.TipoLugar == "Casa" && c.CodigoPostal == "28001"
                && c.DireccionEntrega != null
                && c.DireccionEntrega.Direccion == "C/ DEL LOCAL, 6"
                && c.DireccionEntrega.DireccionVerificada
                && c.DireccionEntrega.CodigoPostal == "28100"
                && c.DireccionEntrega.Poblacion == "ALCOBENDAS"
                && c.DireccionEntrega.Ruta == "AT"
                && c.DireccionEntrega.Telefono == "916000000"
                && c.DireccionEntrega.TipoLugar == "Local"
                && c.DireccionEntrega.NumeroLocal == "6"
                && c.DireccionEntrega.Rotulo == "El Edén"
                && c.DireccionEntrega.Piso == null
                && c.DireccionEntrega.Numero == null
                && c.DireccionEntrega.ComentariosRuta == "Timbre 2"
                && c.DireccionEntrega.ComentariosPicking == "Frágil")))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Crear_SinMarcar_NoMandaDireccionDeEntrega()
        {
            CrearClienteViewModel vm = await ConLasDosDirecciones();
            vm.EntregaEnOtraDireccion = false;

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c => c.DireccionEntrega == null))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ClienteCrear_SinDireccionDeEntrega_NoLaSerializa()
        {
            string json = Newtonsoft.Json.JsonConvert.SerializeObject(new ClienteCrear { Nombre = "X" });

            Assert.IsFalse(json.Contains("DireccionEntrega"), "Con la API de antes, el alta viaja como siempre");
        }

        [TestMethod]
        public void MensajeClienteCreado_ConEntrega_DiceElContactoDeEntrega()
        {
            var creado = new Nesto.Models.Nesto.Models.Clientes { Nº_Cliente = "42300     ", Contacto = "0  ", ContactoDefecto = "1  " };

            Assert.AreEqual("Se ha creado correctamente el cliente 42300/0 (dirección fiscal) y su dirección de entrega, el contacto 42300/1, que queda como entrega por defecto",
                CrearClienteViewModel.MensajeClienteCreado(creado, true));
            Assert.AreEqual("Se ha creado correctamente el cliente 42300/0", CrearClienteViewModel.MensajeClienteCreado(creado, false));
        }
    }
}
