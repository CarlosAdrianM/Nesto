using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json;

namespace Producto.Tests
{
    /// <summary>
    /// NestoAPI#857 (27/11/26, caso SEUR 38 de Iberocel): en «Envíos por asignar», «Este remitente es del proveedor…» (POST
    /// …/RemitentesPaquete), el pedido que ya estaba recibido al asignar (YaRecibido: aviso, no error, y el envío sale de la lista), el
    /// proveedor propuesto con su nombre y la explicación de un envío sin candidatos, y el nombre en los remitentes recordados.
    /// </summary>
    [TestClass]
    public class EnviosProveedorRemitenteYaRecibidoTests
    {
        private IServicioEnviosProveedorPorAsignar _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private readonly List<string> _notificaciones = new();
        private readonly List<string> _errores = new();
        private EnviosProveedorPorAsignar _leido = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioEnviosProveedorPorAsignar>();
            _dialogos = A.Fake<IServicioDialogos>();
            _leido = Leido();
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).ReturnsLazily(() => Task.FromResult(_leido));
            A.CallTo(() => _servicio.LeerRemitentesPaquete()).Returns(Task.FromResult(new List<RemitentePaqueteRecordado>()));
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>._)).Invokes((string t, string m) => _notificaciones.Add(m));
            A.CallTo(() => _dialogos.ShowError(A<string>._)).Invokes((string m) => _errores.Add(m));
        }

        private static EnviosProveedorPorAsignar Leido() => new()
        {
            Envios = new List<EnvioProveedorPorAsignar>
            {
                new EnvioProveedorPorAsignar
                {
                    Clave = 38,
                    PrimerCorreo = new DateTime(2026, 11, 26, 12, 55, 0),
                    Agencia = "SEUR",
                    Codigos = new List<string> { "A-2609942" },
                    RemitentePaquete = "iberocel - paraiso",
                    Candidatos = new List<PedidoCompraPorAsignar>(),
                    Correos = new List<CorreoEnvioProveedor> { new CorreoEnvioProveedor { Id = 38, Asunto = "Tu envío será entregado hoy" } }
                },
                new EnvioProveedorPorAsignar
                {
                    Clave = 40,
                    PrimerCorreo = new DateTime(2026, 11, 25, 9, 0, 0),
                    Agencia = "GLS",
                    ProveedorPropuesto = "451",
                    NombreProveedorPropuesto = "POSTQUAM COSMETIC, S.L.",
                    Explicacion = "Postquam es el proveedor 451 (POSTQUAM COSMETIC, S.L.), pero no tiene ningún pedido de compra enviado y sin recibir.",
                    Candidatos = new List<PedidoCompraPorAsignar>(),
                    Correos = new List<CorreoEnvioProveedor> { new CorreoEnvioProveedor { Id = 40 } }
                }
            },
            PedidosEnAire = new List<PedidoCompraPorAsignar>
            {
                new PedidoCompraPorAsignar { Empresa = "1", Pedido = 220985, Proveedor = "846", NombreProveedor = "IBEROCELULOSA MADRILEÑA" }
            }
        };

        private async Task<EnviosProveedorPorAsignarViewModel> Llegar()
        {
            var vm = new EnviosProveedorPorAsignarViewModel(_servicio, _dialogos);
            vm.AlLlegar(new ParametrosNavegacion());
            await vm.LlegadaEnCurso;
            return vm;
        }

        [TestMethod]
        public void LaApiNueva_TraeNombreYExplicacion_YSeEnsenan()
        {
            string json = "{\"Envios\":[{\"Clave\":40,\"ProveedorPropuesto\":\"451\",\"NombreProveedorPropuesto\":\"POSTQUAM COSMETIC, S.L.\"," +
                "\"Explicacion\":\"No tiene pedidos.\",\"Candidatos\":[]}],\"PedidosEnAire\":[]}";
            EnvioProveedorPorAsignar envio = JsonConvert.DeserializeObject<EnviosProveedorPorAsignar>(json)!.Envios.Single();

            Assert.AreEqual("POSTQUAM COSMETIC, S.L. (451)", envio.ProveedorPropuestoTexto);
            Assert.IsTrue(envio.HayExplicacion);
            Assert.AreEqual("No tiene pedidos.", envio.Explicacion);
            Assert.AreEqual(string.Empty, new EnvioProveedorPorAsignar().ProveedorPropuestoTexto);
            Assert.AreEqual("451", new EnvioProveedorPorAsignar { ProveedorPropuesto = "451" }.ProveedorPropuestoTexto);
            Assert.IsFalse(new EnvioProveedorPorAsignar().HayExplicacion);
        }

        [TestMethod]
        public void RemitenteRecordado_ConNombreDelProveedor()
        {
            Assert.AreEqual("IBEROCELULOSA MADRILEÑA (846)",
                new RemitentePaqueteRecordado { Proveedor = "846", NombreProveedor = "IBEROCELULOSA MADRILEÑA" }.ProveedorTexto);
            Assert.AreEqual("846", new RemitentePaqueteRecordado { Proveedor = "846 " }.ProveedorTexto);
        }

        [TestMethod]
        public async Task EsteRemitenteEsDelProveedor_LlamaALaApi_EnsenaSuMensaje_YRecarga()
        {
            A.CallTo(() => _servicio.EnsenarRemitentePaquete(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new RespuestaRemitentePaquete
                {
                    RemitentePaquete = "iberocel - paraiso", Proveedor = "846", ProveedorAnterior = "123",
                    Mensaje = "«iberocel - paraiso» era del proveedor 123; ahora es del 846."
                }));
            EnviosProveedorPorAsignarViewModel vm = await Llegar();
            Assert.AreEqual(38, vm.EnvioSeleccionado!.Clave);
            Assert.IsTrue(vm.EmpezarEnsenarRemitenteCommand.CanExecute(null));

            vm.EmpezarEnsenarRemitenteCommand.Execute(null);
            Assert.IsTrue(vm.EnsenandoRemitente);
            Assert.IsFalse(vm.EnsenarRemitenteCommand.CanExecute(null), "Sin proveedor elegido no se guarda");
            vm.ProveedorDelRemitente = "846";
            await vm.EnsenarRemitenteAsync();

            A.CallTo(() => _servicio.EnsenarRemitentePaquete("iberocel - paraiso", null, "846")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("«iberocel - paraiso» era del proveedor 123; ahora es del 846.", vm.Mensaje);
            Assert.AreEqual(vm.Mensaje, _notificaciones.Single());
            Assert.IsFalse(vm.EnsenandoRemitente);
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task EsteRemitenteEsDelProveedor_ProveedorQueNoExiste_EnsenaElErrorDeLaApi()
        {
            A.CallTo(() => _servicio.EnsenarRemitentePaquete(A<string>._, A<string>._, A<string>._))
                .Throws(new EnvioProveedorPorAsignarException("No existe el proveedor 99999 en la empresa 1.", 400));
            EnviosProveedorPorAsignarViewModel vm = await Llegar();
            vm.ProveedorDelRemitente = "99999";

            await vm.EnsenarRemitenteAsync();

            CollectionAssert.AreEqual(new[] { "No existe el proveedor 99999 en la empresa 1." }, _errores);
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).MustHaveHappenedOnceExactly(); // un 400 no recarga
        }

        [TestMethod]
        public async Task EsteRemitenteEsDelProveedor_SinRemitenteDelPaquete_NoSePuede()
        {
            EnviosProveedorPorAsignarViewModel vm = await Llegar();
            vm.EnvioSeleccionado = vm.Envios.Single(e => e.Clave == 40);

            Assert.IsFalse(vm.EmpezarEnsenarRemitenteCommand.CanExecute(null));
        }

        [TestMethod]
        public void TextoRemitenteEnsenado_SinMensajeDeLaApi()
        {
            Assert.AreEqual("«x» ya era del proveedor IBERO (846): no se ha cambiado nada.", EnviosProveedorPorAsignarViewModel.TextoRemitenteEnsenado(
                new RespuestaRemitentePaquete { SinCambios = true, Proveedor = "846", NombreProveedor = "IBERO" }, "x", "846"));
            Assert.AreEqual("Recordado: «x» es del proveedor 846.", EnviosProveedorPorAsignarViewModel.TextoRemitenteEnsenado(
                new RespuestaRemitentePaquete(), "x", "846"));
        }

        [TestMethod]
        public async Task Asignar_PedidoYaRecibido_AvisoInformativo_YElEnvioSaleDeLaLista()
        {
            A.CallTo(() => _servicio.Asignar(38, "1", 220985, A<bool>._, false)).ReturnsLazily(() =>
            {
                // La API ya no lo devuelve como pendiente
                _leido = Leido();
                _leido.Envios.RemoveAll(e => e.Clave == 38);
                return Task.FromResult(new RespuestaAsignacionEnvioProveedor
                {
                    YaRecibido = true,
                    FechaAlbaran = new DateTime(2026, 11, 26),
                    Mensaje = "El pedido 220985 ya estaba recibido: no se ha cambiado ninguna fecha; el envío queda asignado a él."
                });
            });
            EnviosProveedorPorAsignarViewModel vm = await Llegar();
            vm.OtroPedidoSeleccionado = vm.OtrosPedidos.Single(p => p.Pedido == 220985);

            await vm.AsignarAsync();

            Assert.AreEqual("El pedido 220985 ya estaba recibido: no se ha cambiado ninguna fecha; el envío queda asignado a él.", vm.Mensaje);
            Assert.AreEqual(vm.Mensaje, _notificaciones.Single());
            Assert.IsFalse(_errores.Any(), "Ya recibido no es un error");
            Assert.IsFalse(vm.Envios.Any(e => e.Clave == 38));
        }

        [TestMethod]
        public void QuitarEnvio_EligeElSiguiente()
        {
            var vm = new EnviosProveedorPorAsignarViewModel(_servicio, _dialogos);
            var a = new EnvioProveedorPorAsignar { Clave = 1 };
            var b = new EnvioProveedorPorAsignar { Clave = 2 };
            vm.Envios.Add(a);
            vm.Envios.Add(b);
            vm.EnvioSeleccionado = a;

            vm.QuitarEnvio(a);

            Assert.AreSame(b, vm.EnvioSeleccionado);
            Assert.AreEqual("1 envío por asignar.", vm.TextoEnvios);
        }
    }
}
