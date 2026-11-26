using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClienteCrear = Nesto.Modulos.Cliente.ClienteCrear;

namespace ClienteTests
{
    /// <summary>
    /// NestoAPI#876: alta de un cliente que cambia de forma fiscal (origen CAMBIO-FISCAL): se pide el número de cliente anterior,
    /// se manda en el POST y se enseña lo que la API devuelve (ClienteAnterior y AvisoClienteAnterior). La ficha lo pinta.
    /// </summary>
    [TestClass]
    public class ClienteAnteriorAltaTests
    {
        private const string CAMBIO = "CAMBIO-FISCAL";

        private readonly IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IClienteService servicio = A.Fake<IClienteService>();
        private readonly IOrigenesClienteService origenes = A.Fake<IOrigenesClienteService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<string> avisos = new();

        public ClienteAnteriorAltaTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => dialogos.ShowNotification(A<string>.Ignored, A<string>.Ignored)).Invokes((string t, string m) => avisos.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            A.CallTo(() => configuracion.usuario).Returns("NUEVAVISION\\Paloma");
            A.CallTo(() => origenes.LeerOrigenes()).Returns(new List<OrigenClienteModel>
            {
                new OrigenClienteModel { Codigo = CAMBIO, Descripcion = "Cambio de forma fiscal", Activo = true },
                new OrigenClienteModel { Codigo = "WEB", Descripcion = "Web", Activo = true }
            });
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJ876", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle Mayor", Numero = "1", CodigoPostal = "28001" });
        }

        private async Task<CrearClienteViewModel> AltaConDireccion()
        {
            var vm = new CrearClienteViewModel(navegacion, configuracion, servicio, new WeakReferenceMessenger(), dialogos, origenes);
            await vm.CargaOrigenesEnCurso;
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ876" });
            vm.ClienteDireccion = "CALLE MAYOR, 1";
            vm.ClienteTipoLugar = "Casa";
            return vm;
        }

        [TestMethod]
        public async Task ConOtroOrigen_NoSePide()
        {
            CrearClienteViewModel vm = await AltaConDireccion();
            vm.ClienteOrigen = "WEB";

            Assert.IsFalse(vm.PideClienteAnterior);
            Assert.IsTrue(vm.SePuedeCrearCliente);
        }

        [TestMethod]
        public async Task CambioFiscalSinNumero_NoDejaSeguirNiCrear()
        {
            CrearClienteViewModel vm = await AltaConDireccion();
            vm.ClienteNif = "B12345678";
            vm.ClienteOrigen = "cambio-fiscal";

            Assert.IsTrue(vm.PideClienteAnterior);
            Assert.IsFalse(vm.SePuedeAvanzarADatosGenerales);
            Assert.AreEqual(CrearClienteViewModel.MOTIVO_FALTA_CLIENTE_ANTERIOR, vm.MotivoNoSePuedeCrearCliente);

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(200);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>._)).MustNotHaveHappened();
            Assert.AreEqual(CrearClienteViewModel.MOTIVO_FALTA_CLIENTE_ANTERIOR, errores.Single());
        }

        [TestMethod]
        public async Task CambioFiscalConNumero_LoMandaYEnseniaElAviso()
        {
            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>._)).Returns(new Clientes
            {
                Empresa = "1", Nº_Cliente = "50001", Contacto = "0", ClienteAnterior = "15191",
                AvisoClienteAnterior = "El cliente anterior 15191 tiene el mismo NIF."
            });
            CrearClienteViewModel vm = await AltaConDireccion();
            vm.ClienteOrigen = CAMBIO;
            vm.ClienteAnterior = " 15191 ";

            Assert.IsTrue(vm.SePuedeCrearCliente);
            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c => c.Origen == CAMBIO && c.ClienteAnterior == "15191"))).MustHaveHappened();
            string aviso = avisos.Single();
            StringAssert.Contains(aviso, "Cliente anterior: 15191");
            StringAssert.Contains(aviso, "tiene el mismo NIF");
        }

        [TestMethod]
        public async Task SiSeCambiaElOrigen_ElNumeroNoViaja()
        {
            CrearClienteViewModel vm = await AltaConDireccion();
            vm.ClienteOrigen = CAMBIO;
            vm.ClienteAnterior = "15191";
            vm.ClienteOrigen = "WEB";

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c => c.ClienteAnterior == null))).MustHaveHappened();
        }

        [TestMethod]
        public async Task ElCuatrocientosDeLaApi_SeEnseniaConSuMensaje()
        {
            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>._))
                .Throws(new Exception("No se ha podido crear el cliente X\nNo existe el cliente 99999 en la empresa 1."));
            CrearClienteViewModel vm = await AltaConDireccion();
            vm.ClienteOrigen = CAMBIO;
            vm.ClienteAnterior = "99999";

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            StringAssert.Contains(errores.Single(), "No existe el cliente 99999");
        }

        [TestMethod]
        public void TextoClienteAnteriorCreado_SinNadaVacio()
        {
            Assert.AreEqual(string.Empty, CrearClienteViewModel.TextoClienteAnteriorCreado(new Clientes()));
            Assert.AreEqual(". Cliente anterior: 15191", CrearClienteViewModel.TextoClienteAnteriorCreado(new Clientes { ClienteAnterior = "15191 " }));
        }
    }
}
