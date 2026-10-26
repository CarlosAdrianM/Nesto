using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Sugerencia 648 (Paloma, NestoAPI#658): el Excel de lo facturado en la tienda. Por defecto, la semana anterior de lunes a
    /// domingo; las tiendas sacan la suya (la API decide con su AlmacénPedidoVta) y Dirección, Administración e Informática eligen.
    /// El Excel se manda por correo (en el escritorio remoto Office no tiene licencia): aquí no se abre.
    /// </summary>
    [TestClass]
    public class FacturacionTiendaViewModelTests
    {
        private static readonly DateTime MARTES = new DateTime(2026, 10, 27, 10, 0, 0);
        private IServicioFacturacionTienda _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioFacturacionTienda>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _servicio.LeerCorreoUsuario()).Returns(Task.FromResult<string>("paloma@nuevavision.es"));
            A.CallTo(() => _servicio.MandarExcelPorCorreo(A<string>._, A<DateTime>._, A<DateTime>._, A<string>._))
                .ReturnsLazily((string tienda, DateTime desde, DateTime hasta, string correo) => Task.FromResult(new ResultadoCorreoFacturacionTienda
                {
                    Correo = correo ?? "paloma@nuevavision.es",
                    Tienda = tienda ?? "ALC",
                    Desde = desde,
                    Hasta = hasta,
                    NumeroFacturas = 12,
                    Total = 1234.56m
                }));
        }

        private FacturacionTiendaViewModel Vm(params string[] grupos)
        {
            foreach (string grupo in grupos)
            {
                A.CallTo(() => _configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            }
            return new FacturacionTiendaViewModel(_servicio, _configuracion, _dialogos, () => MARTES);
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
        public void ElViewModel_YaNoAbreElExcelEnElServidor()
        {
            // En el escritorio remoto Office no tiene licencia: ni descarga ni «abrir aquí»
            Assert.IsNull(typeof(FacturacionTiendaViewModel).GetProperty("GuardarYAbrir",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic));
            Assert.IsNull(typeof(IServicioFacturacionTienda).GetMethod("DescargarExcel"));
        }

        // ---------------------------------------------------------------- el correo

        [TestMethod]
        public async Task AlLlegar_RellenaElCorreoConElDelUsuario()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.LeerCorreoDelUsuarioAsync();

            Assert.AreEqual("paloma@nuevavision.es", vm.Correo);
            Assert.IsNull(vm.Mensaje);
        }

        [TestMethod]
        public async Task AlLlegar_SinCorreoDelUsuario_LoDiceYSeQuedaVacio()
        {
            A.CallTo(() => _servicio.LeerCorreoUsuario()).Returns(Task.FromResult<string>(null!));
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.LeerCorreoDelUsuarioAsync();

            Assert.IsTrue(string.IsNullOrEmpty(vm.Correo));
            Assert.AreEqual(FacturacionTiendaViewModel.MENSAJE_SIN_CORREO, vm.Mensaje);
        }

        [TestMethod]
        public async Task AlLlegar_SiFallaLeerElCorreo_NoRompe()
        {
            A.CallTo(() => _servicio.LeerCorreoUsuario()).ThrowsAsync(new HttpRequestException("sin red"));
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.LeerCorreoDelUsuarioAsync();

            Assert.IsNull(vm.Correo);
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Mandar_Tiendas_PideSinTiendaConLaSemanaYSuCorreo_YDiceAQuienSeHaMandado()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            await vm.LeerCorreoDelUsuarioAsync();

            await vm.MandarAsync();

            A.CallTo(() => _servicio.MandarExcelPorCorreo(null, new DateTime(2026, 10, 19), new DateTime(2026, 10, 25), "paloma@nuevavision.es"))
                .MustHaveHappenedOnceExactly();
            StringAssert.StartsWith(vm.Mensaje, "Se lo hemos mandado a paloma@nuevavision.es");
            StringAssert.Contains(vm.Mensaje, "12 facturas");
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, vm.Mensaje)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Mandar_ConOtroCorreo_LoPasaSinEspacios()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Correo = "  tiendaalcobendas@nuevavision.es ";

            await vm.MandarAsync();

            A.CallTo(() => _servicio.MandarExcelPorCorreo(null, A<DateTime>._, A<DateTime>._, "tiendaalcobendas@nuevavision.es"))
                .MustHaveHappenedOnceExactly();
            StringAssert.StartsWith(vm.Mensaje, "Se lo hemos mandado a tiendaalcobendas@nuevavision.es");
        }

        [TestMethod]
        public async Task Mandar_CorreoVacio_DejaQueLaApiPongaElSuyoYLoEnsena()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Correo = "   ";

            await vm.MandarAsync();

            A.CallTo(() => _servicio.MandarExcelPorCorreo(null, A<DateTime>._, A<DateTime>._, null)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("paloma@nuevavision.es", vm.Correo);
            StringAssert.StartsWith(vm.Mensaje, "Se lo hemos mandado a paloma@nuevavision.es");
        }

        [TestMethod]
        public async Task Mandar_ConOtrasFechas_LasPasa()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Desde = new DateTime(2026, 10, 1, 15, 0, 0);
            vm.Hasta = new DateTime(2026, 10, 15);

            await vm.MandarAsync();

            A.CallTo(() => _servicio.MandarExcelPorCorreo(null, new DateTime(2026, 10, 1), new DateTime(2026, 10, 15), A<string>._))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Mandar_DesdePosteriorAHasta_NoSePuede()
        {
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);
            vm.Desde = new DateTime(2026, 10, 30);

            Assert.IsFalse(vm.MandarCommand.CanExecute(null));
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
            await vm.MandarAsync();

            Assert.IsTrue(vm.PuedeElegirTienda);
            CollectionAssert.AreEqual(new[] { "REI", "ALC" }, vm.Tiendas.Select(t => t.Almacen).ToArray());
            A.CallTo(() => _servicio.MandarExcelPorCorreo("ALC", A<DateTime>._, A<DateTime>._, A<string>._)).MustHaveHappenedOnceExactly();
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
        public async Task Mandar_LaApiDiceQueNo_EnsenaSuMensaje()
        {
            A.CallTo(() => _servicio.MandarExcelPorCorreo(A<string>._, A<DateTime>._, A<DateTime>._, A<string>._))
                .ThrowsAsync(new FacturacionTiendaException("Cada tienda solo puede ver lo facturado en su almacén.", 403));
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.MandarAsync();

            Assert.AreEqual("Cada tienda solo puede ver lo facturado en su almacén.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("Cada tienda solo puede ver lo facturado en su almacén.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Mandar_FallaLaRed_MensajeDeUsted()
        {
            A.CallTo(() => _servicio.MandarExcelPorCorreo(A<string>._, A<DateTime>._, A<DateTime>._, A<string>._))
                .ThrowsAsync(new HttpRequestException("sin red"));
            var vm = Vm(Constantes.GruposSeguridad.TIENDAS);

            await vm.MandarAsync();

            Assert.AreEqual("No se ha podido mandar el Excel: sin red", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("No se ha podido mandar el Excel: sin red")).MustHaveHappenedOnceExactly();
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
            public List<(string Metodo, string Url, string? Cuerpo)> Peticiones { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string? cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Peticiones.Add((request.Method.Method, request.RequestUri!.PathAndQuery, cuerpo));
                return _responder(request);
            }
        }

        private static ServicioFacturacionTienda Servicio(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioFacturacionTienda(factoria);
        }

        private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
            => new HttpResponseMessage(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        [TestMethod]
        public async Task Servicio_Mandar_HaceElPostConTiendaFechasYCorreo_YDevuelveElDestinatario()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK,
                "{\"Correo\":\"paloma@nuevavision.es\",\"Tienda\":\"ALC\",\"Desde\":\"2026-10-19T00:00:00\",\"Hasta\":\"2026-10-25T00:00:00\",\"NumeroFacturas\":3,\"TotalBaseImponible\":100.0,\"Total\":121.0}"));

            ResultadoCorreoFacturacionTienda resultado = await Servicio(handler)
                .MandarExcelPorCorreo("ALC", new DateTime(2026, 10, 19, 9, 0, 0), new DateTime(2026, 10, 25), " paloma@nuevavision.es ");

            var peticion = handler.Peticiones.Single();
            Assert.AreEqual("POST", peticion.Metodo);
            Assert.AreEqual("/api/Informes/FacturacionTienda/Correo", peticion.Url);
            JObject cuerpo = JObject.Parse(peticion.Cuerpo!);
            Assert.AreEqual("ALC", (string?)cuerpo["Tienda"]);
            Assert.AreEqual("2026-10-19", (string?)cuerpo["Desde"]);
            Assert.AreEqual("2026-10-25", (string?)cuerpo["Hasta"]);
            Assert.AreEqual("paloma@nuevavision.es", (string?)cuerpo["Correo"]);
            Assert.AreEqual("paloma@nuevavision.es", resultado.Correo);
            Assert.AreEqual(3, resultado.NumeroFacturas);
            Assert.AreEqual(121m, resultado.Total);
        }

        [TestMethod]
        public async Task Servicio_Mandar_SinTiendaNiCorreo_VanNull()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, "{\"Correo\":\"paloma@nuevavision.es\"}"));

            await Servicio(handler).MandarExcelPorCorreo(null, new DateTime(2026, 10, 19), new DateTime(2026, 10, 25), "  ");

            JObject cuerpo = JObject.Parse(handler.Peticiones.Single().Cuerpo!);
            Assert.AreEqual(JTokenType.Null, cuerpo["Tienda"]!.Type);
            Assert.AreEqual(JTokenType.Null, cuerpo["Correo"]!.Type);
        }

        [TestMethod]
        public async Task Servicio_Mandar_400ConMessage_LanzaConElMensajeDeLaApi()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.BadRequest,
                "{\"Message\":\"Por protección de datos, el informe solo se puede mandar a su correo o a uno de @nuevavision.es.\"}"));

            FacturacionTiendaException ex = await Assert.ThrowsExceptionAsync<FacturacionTiendaException>(
                () => Servicio(handler).MandarExcelPorCorreo(null, new DateTime(2026, 10, 19), new DateTime(2026, 10, 25), "x@gmail.com"));

            Assert.AreEqual(400, ex.Codigo);
            Assert.AreEqual("Por protección de datos, el informe solo se puede mandar a su correo o a uno de @nuevavision.es.", ex.Message);
        }

        [TestMethod]
        public async Task Servicio_Mandar_403EnTexto_LanzaConElMensajeDeLaApi()
        {
            var handler = new HandlerFalso(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("Cada tienda solo puede ver lo facturado en su almacén.", Encoding.UTF8, "text/plain")
            });

            FacturacionTiendaException ex = await Assert.ThrowsExceptionAsync<FacturacionTiendaException>(
                () => Servicio(handler).MandarExcelPorCorreo("REI", new DateTime(2026, 10, 19), new DateTime(2026, 10, 25), null));

            Assert.AreEqual(403, ex.Codigo);
            Assert.AreEqual("Cada tienda solo puede ver lo facturado en su almacén.", ex.Message);
        }

        [TestMethod]
        public async Task Servicio_LeerCorreo_GetYDevuelveElCorreo()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, "{\"Correo\":\" paloma@nuevavision.es \"}"));

            string correo = await Servicio(handler).LeerCorreoUsuario();

            Assert.AreEqual(("GET", "/api/Informes/FacturacionTienda/Correo"), (handler.Peticiones.Single().Metodo, handler.Peticiones.Single().Url));
            Assert.AreEqual("paloma@nuevavision.es", correo);
        }

        [TestMethod]
        public async Task Servicio_LeerCorreo_SinCorreo_Null()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK, "{\"Correo\":null}"));

            Assert.IsNull(await Servicio(handler).LeerCorreoUsuario());
        }
    }
}
