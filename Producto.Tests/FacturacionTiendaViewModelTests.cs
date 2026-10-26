using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Sugerencia 648 (Paloma, NestoAPI#658): el Excel de lo facturado en la tienda. Por defecto, la semana anterior de lunes a
    /// domingo; las tiendas sacan la suya (la API decide con su AlmacénPedidoVta) y Dirección, Administración e Informática eligen.
    /// </summary>
    [TestClass]
    public class FacturacionTiendaViewModelTests
    {
        private static readonly DateTime MARTES = new DateTime(2026, 10, 27, 10, 0, 0);
        private IServicioFacturacionTienda _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;
        private readonly List<(byte[] Excel, string Nombre)> _abiertos = new();

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioFacturacionTienda>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _servicio.DescargarExcel(A<string>._, A<DateTime>._, A<DateTime>._)).Returns(Task.FromResult(new byte[] { 0x50, 0x4B }));
        }

        private FacturacionTiendaViewModel Vm(params string[] grupos)
        {
            foreach (string grupo in grupos)
            {
                A.CallTo(() => _configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            }
            var vm = new FacturacionTiendaViewModel(_servicio, _configuracion, _dialogos, () => MARTES);
            vm.GuardarYAbrir = (excel, nombre) =>
            {
                _abiertos.Add((excel, nombre));
                return null;
            };
            return vm;
        }

        [TestMethod]
        public void SemanaAnterior_UnLunes_DaLaSemanaPasadaDeLunesADomingo()
        {
            (DateTime desde, DateTime hasta) = FacturacionTiendaViewModel.SemanaAnterior(new DateTime(2026, 10, 26, 8, 0, 0));

            Assert.AreEqual(new DateTime(2026, 10, 19), desde);
            Assert.AreEqual(new DateTime(2026, 10, 25), hasta);
        }

        [TestMethod]
        public void SemanaAnterior_UnDomingo_NoCuentaLaSemanaEnCurso()
        {
            (DateTime desde, DateTime hasta) = FacturacionTiendaViewModel.SemanaAnterior(new DateTime(2026, 10, 25, 20, 0, 0));

            Assert.AreEqual(new DateTime(2026, 10, 12), desde);
            Assert.AreEqual(new DateTime(2026, 10, 18), hasta);
        }

        [TestMethod]
        public void AlAbrir_FechasDeLaSemanaAnterior()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            Assert.AreEqual(new DateTime(2026, 10, 19), vm.Desde);
            Assert.AreEqual(new DateTime(2026, 10, 25), vm.Hasta);
        }

        [TestMethod]
        public void Tiendas_NoEligenTienda_SacanLaSuya()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            Assert.IsFalse(vm.PuedeElegirTienda);
            Assert.AreEqual(FacturacionTiendaViewModel.OPCION_SU_TIENDA, vm.Tienda.Texto);
            Assert.IsNull(vm.Tienda.Almacen, "La tienda la pone la API con su AlmacénPedidoVta");
        }

        [TestMethod]
        public async Task Generar_Tiendas_PideSinTiendaConLaSemanaYAbreElExcel()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.GenerarAsync();

            A.CallTo(() => _servicio.DescargarExcel(null, new DateTime(2026, 10, 19), new DateTime(2026, 10, 25))).MustHaveHappenedOnceExactly();
            Assert.AreEqual("FacturacionTienda_Tienda_20261019_20261025", _abiertos.Single().Nombre);
            CollectionAssert.AreEqual(new byte[] { 0x50, 0x4B }, _abiertos.Single().Excel);
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Generar_ConOtrasFechas_LasPasa()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Desde = new DateTime(2026, 10, 1, 15, 0, 0);
            vm.Hasta = new DateTime(2026, 10, 15);

            await vm.GenerarAsync();

            A.CallTo(() => _servicio.DescargarExcel(null, new DateTime(2026, 10, 1), new DateTime(2026, 10, 15))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Generar_DesdePosteriorAHasta_NoSePuede()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Desde = new DateTime(2026, 10, 30);

            Assert.IsFalse(vm.GenerarCommand.CanExecute(null));
        }

        [TestMethod]
        public void SemanaAnterior_VuelveALasFechasPorDefecto()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Desde = new DateTime(2026, 1, 1);
            vm.Hasta = new DateTime(2026, 1, 31);

            vm.SemanaAnteriorCommand.Execute(null);

            Assert.AreEqual(new DateTime(2026, 10, 19), vm.Desde);
            Assert.AreEqual(new DateTime(2026, 10, 25), vm.Hasta);
        }

        [TestMethod]
        public async Task Direccion_EligeTiendaYEmpiezaConLaSuya()
        {
            A.CallTo(() => _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenPedidoVta))
                .Returns(Task.FromResult("ALC "));
            var vm = Vm(Constantes.GruposSeguridad.DIRECCION);

            await vm.LeerTiendaDelUsuarioAsync();
            await vm.GenerarAsync();

            Assert.IsTrue(vm.PuedeElegirTienda);
            CollectionAssert.AreEqual(new[] { "REI", "ALC" }, vm.Tiendas.Select(t => t.Almacen).ToArray());
            A.CallTo(() => _servicio.DescargarExcel("ALC", A<DateTime>._, A<DateTime>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Administracion_SinAlmacenDeTienda_EmpiezaConReina()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult("ALG"));
            var vm = Vm(Constantes.GruposSeguridad.ADMINISTRACION);

            await vm.LeerTiendaDelUsuarioAsync();

            Assert.AreEqual("REI", vm.Tienda.Almacen);
        }

        [TestMethod]
        public async Task Generar_LaApiDiceQueNo_EnsenaSuMensaje()
        {
            A.CallTo(() => _servicio.DescargarExcel(A<string>._, A<DateTime>._, A<DateTime>._))
                .ThrowsAsync(new FacturacionTiendaException("Cada tienda solo puede ver lo facturado en su almacén.", 403));
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.GenerarAsync();

            Assert.AreEqual("Cada tienda solo puede ver lo facturado en su almacén.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("Cada tienda solo puede ver lo facturado en su almacén.")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, _abiertos.Count);
            Assert.IsFalse(vm.EstaOcupado);
        }

        // ---------------------------------------------------------------- el botón de la cinta

        [TestMethod]
        public void MenuBar_Tiendas_AbreLaVentana()
        {
            var navegacion = A.Fake<IServicioNavegacion>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            var menu = new ProductoMenuBarViewModel(navegacion, _configuracion, A.Fake<IServicioDiferenciasReposicion>());

            Assert.IsTrue(menu.AbrirFacturacionTiendaCommand.CanExecute(null));
            menu.AbrirFacturacionTiendaCommand.Execute(null);

            A.CallTo(() => navegacion.RequestNavigate("MainRegion", ProductoMenuBarViewModel.VISTA_FACTURACION_TIENDA)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void MenuBar_UnComercial_NoPuede()
        {
            var menu = new ProductoMenuBarViewModel(A.Fake<IServicioNavegacion>(), _configuracion, A.Fake<IServicioDiferenciasReposicion>());

            Assert.IsFalse(menu.AbrirFacturacionTiendaCommand.CanExecute(null));
        }

        // ---------------------------------------------------------------- el servicio

        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<string> Urls { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Urls.Add(request.RequestUri!.PathAndQuery);
                return Task.FromResult(_responder(request));
            }
        }

        private static ServicioFacturacionTienda Servicio(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioFacturacionTienda(factoria);
        }

        [TestMethod]
        public async Task Servicio_SinTienda_PideElExcelSoloConLasFechas()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[] { 1, 2, 3 }) });

            byte[] excel = await Servicio(handler).DescargarExcel(null, new DateTime(2026, 10, 19), new DateTime(2026, 10, 25));

            Assert.AreEqual("/api/Informes/FacturacionTienda/Excel?desde=2026-10-19&hasta=2026-10-25", handler.Urls.Single());
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, excel);
        }

        [TestMethod]
        public async Task Servicio_ConTienda_LaPasa()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Array.Empty<byte>()) });

            await Servicio(handler).DescargarExcel("ALC", new DateTime(2026, 10, 19), new DateTime(2026, 10, 25));

            Assert.AreEqual("/api/Informes/FacturacionTienda/Excel?tienda=ALC&desde=2026-10-19&hasta=2026-10-25", handler.Urls.Single());
        }

        [TestMethod]
        public async Task Servicio_403EnTexto_LanzaConElMensajeDeLaApi()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("Cada tienda solo puede ver lo facturado en su almacén.", Encoding.UTF8, "text/plain")
            });

            FacturacionTiendaException ex = await Assert.ThrowsExceptionAsync<FacturacionTiendaException>(
                () => Servicio(handler).DescargarExcel("REI", new DateTime(2026, 10, 19), new DateTime(2026, 10, 25)));

            Assert.AreEqual(403, ex.Codigo);
            Assert.AreEqual("Cada tienda solo puede ver lo facturado en su almacén.", ex.Message);
        }
    }
}
