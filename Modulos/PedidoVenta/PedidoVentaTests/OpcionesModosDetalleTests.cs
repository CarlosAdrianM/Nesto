using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#688 (corte 3): el detalle de pedido pregunta una sola vez (POST api/PedidosVenta/OpcionesModos) por el
    /// modo de servicio, el de facturación (y si se enseña) y la casilla «Servir lo pendiente en cuanto haya stock».
    /// Con «Todo junto» el combo «Facturación» se oculta; la casilla solo sale con «Todo ahora», con el valor de la API,
    /// y viaja al guardar solo si el usuario la tocó o cambió el modo. Con una API anterior (404) se pregunta como antes.
    /// </summary>
    [TestClass]
    public class OpcionesModosDetalleTests
    {
        // ---------- El servicio: una pregunta, y compatibilidad con la API anterior ----------

        /// <summary>Contesta por la ruta pedida, sin salir a la red, y apunta qué se ha pedido.</summary>
        private class RespuestasPorRuta : HttpMessageHandler
        {
            private readonly Dictionary<string, (HttpStatusCode codigo, string cuerpo)> _respuestas;
            public List<string> Pedidas { get; } = new List<string>();
            public List<string> Rutas { get; } = new List<string>();
            public List<string> Cuerpos { get; } = new List<string>();

            public RespuestasPorRuta(Dictionary<string, (HttpStatusCode, string)> respuestas) => _respuestas = respuestas;

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string ruta = request.RequestUri.AbsolutePath.Split('/').Last();
                Pedidas.Add(ruta);
                Rutas.Add(request.RequestUri.AbsolutePath);
                Cuerpos.Add(request.Content?.ReadAsStringAsync().Result);
                (HttpStatusCode codigo, string cuerpo) = _respuestas.TryGetValue(ruta, out var r) ? r : (HttpStatusCode.NotFound, "");
                return Task.FromResult(new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo) });
            }
        }

        private static (PedidoVentaService servicio, RespuestasPorRuta handler) Servicio(Dictionary<string, (HttpStatusCode, string)> respuestas)
        {
            var handler = new RespuestasPorRuta(respuestas);
            IClienteApiFactory factory = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factory.Crear()).ReturnsLazily(() => new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("https://api.nuevavision.es/api/")
            });
            IServicioAutenticacion autenticacion = A.Fake<IServicioAutenticacion>();
            A.CallTo(() => autenticacion.ConfigurarAutorizacion(A<HttpClient>._)).Returns(Task.FromResult(true));
            return (new PedidoVentaService(A.Fake<IConfiguracion>(), autenticacion, factory), handler);
        }

        private const string JSON_OPCIONES_TODO_JUNTO_TODO_AHORA =
            "{\"Servicio\":{\"Modo\":1,\"Nombre\":\"Todo junto\",\"Motivo\":\"Hay stock de todo\",\"ModosPermitidos\":[1,2,3,4],\"Modos\":[]}," +
            "\"Facturacion\":{\"Visible\":false,\"Modo\":3,\"Nombre\":\"Todo ahora\",\"Motivo\":null,\"ModosPermitidos\":[1,2,3],\"Modos\":[]}," +
            "\"ServirPendienteAlHaberStock\":{\"Visible\":true,\"Valor\":true,\"Motivo\":\"Sale de una vez cuando haya stock de todo.\"}}";

        [TestMethod]
        public async Task Servicio_ConLaApiNueva_UnaSolaPregunta_YLeeElContrato()
        {
            var (servicio, handler) = Servicio(new Dictionary<string, (HttpStatusCode, string)>
            {
                ["OpcionesModos"] = (HttpStatusCode.OK, JSON_OPCIONES_TODO_JUNTO_TODO_AHORA)
            });

            OpcionesModosDTO opciones = await servicio.OpcionesModos(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));

            CollectionAssert.AreEqual(new[] { "OpcionesModos" }, handler.Pedidas);
            Assert.IsFalse(opciones.EsApiAnterior);
            Assert.AreEqual(ModosServicio.TODO_JUNTO, opciones.Servicio.Modo);
            Assert.IsFalse(opciones.Facturacion.Visible);
            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, opciones.Facturacion.Modo);
            Assert.IsTrue(opciones.ServirPendienteAlHaberStock.Visible);
            Assert.AreEqual(true, opciones.ServirPendienteAlHaberStock.Valor);
        }

        [TestMethod]
        public async Task Servicio_ConLaApiAnterior404_PreguntaALosDosEndpointsViejos_SinCasillaYConLaFacturacionVisible()
        {
            var (servicio, handler) = Servicio(new Dictionary<string, (HttpStatusCode, string)>
            {
                ["ModoServicioSugerido"] = (HttpStatusCode.OK, "{\"Modo\":2,\"Nombre\":\"Según vaya entrando\",\"ModosPermitidos\":[2]}"),
                ["ModoFacturacionSugerido"] = (HttpStatusCode.OK, "{\"Modo\":1,\"Nombre\":\"Por entregas\",\"ModosPermitidos\":[1,3]}")
            });

            OpcionesModosDTO opciones = await servicio.OpcionesModos(Pedido(926700, ModosFacturacion.POR_ENTREGAS));

            CollectionAssert.AreEquivalent(new[] { "OpcionesModos", "ModoServicioSugerido", "ModoFacturacionSugerido" }, handler.Pedidas);
            Assert.IsTrue(opciones.EsApiAnterior);
            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, opciones.Servicio.Modo);
            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, opciones.Facturacion.Modo);
            CollectionAssert.AreEqual(new List<byte> { 1, 3 }, opciones.Facturacion.ModosPermitidos);
            Assert.IsTrue(opciones.Facturacion.Visible, "Como antes: el combo siempre se ve");
            Assert.IsFalse(opciones.ServirPendienteAlHaberStock.Visible, "La API anterior no sabe de la casilla");
        }

        [TestMethod]
        public async Task Servicio_ConLaApiAnteriorYSinLineas_NoPreguntaElModoDeServicio()
        {
            var (servicio, handler) = Servicio(new Dictionary<string, (HttpStatusCode, string)>
            {
                ["ModoFacturacionSugerido"] = (HttpStatusCode.OK, "{\"Modo\":1,\"ModosPermitidos\":[1,2,3]}")
            });
            PedidoVentaDTO pedido = Pedido(0, ModosFacturacion.POR_ENTREGAS);
            pedido.Lineas.Clear();

            OpcionesModosDTO opciones = await servicio.OpcionesModos(pedido);

            CollectionAssert.AreEqual(new[] { "OpcionesModos", "ModoFacturacionSugerido" }, handler.Pedidas);
            Assert.IsNull(opciones.Servicio);
        }

        [TestMethod]
        public async Task Servicio_SiLaApiFallaDeOtraManera_Nothing_YNoPreguntaALosViejos()
        {
            var (servicio, handler) = Servicio(new Dictionary<string, (HttpStatusCode, string)>
            {
                ["OpcionesModos"] = (HttpStatusCode.InternalServerError, "{\"Message\":\"boom\"}")
            });

            Assert.IsNull(await servicio.OpcionesModos(Pedido(926700, ModosFacturacion.POR_ENTREGAS)));
            CollectionAssert.AreEqual(new[] { "OpcionesModos" }, handler.Pedidas);
        }

        [TestMethod]
        public async Task Servicio_CasillaDeLaNota_VaASuEndpointConValorYUsuario()
        {
            var (servicio, handler) = Servicio(new Dictionary<string, (HttpStatusCode, string)>
            {
                ["ServirPendienteAlHaberStock"] = (HttpStatusCode.OK, "")
            });

            await servicio.CambiarServirPendienteNota("1  ", 926800, true, "NUEVAVISION\\Carlos");

            Assert.AreEqual("/api/PedidosVenta/1/926800/ServirPendienteAlHaberStock", handler.Rutas.Single());
            var cuerpo = Newtonsoft.Json.Linq.JObject.Parse(handler.Cuerpos.Single());
            Assert.IsTrue(cuerpo["Valor"].Value<bool>());
            Assert.AreEqual("NUEVAVISION\\Carlos", cuerpo["Usuario"].Value<string>());
        }

        [TestMethod]
        public async Task Servicio_CasillaDeLaNota_UnRechazoSeLanzaConElMensajeDeLaApi()
        {
            var (servicio, _) = Servicio(new Dictionary<string, (HttpStatusCode, string)>
            {
                ["ServirPendienteAlHaberStock"] = (HttpStatusCode.BadRequest, "{\"Message\":\"La nota ya tiene picking.\"}")
            });

            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => servicio.CambiarServirPendienteNota("1", 926800, false, "Carlos"));

            StringAssert.Contains(ex.Message, "La nota ya tiene picking.");
        }

        // ---------- El ViewModel ----------

        private static PedidoVentaDTO Pedido(int numero, byte modoFacturacion, bool? servirPendiente = null) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "15191",
            contacto = "0",
            plazosPago = "30",
            modoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO,
            modoFacturacion = modoFacturacion,
            mantenerJunto = ModosFacturacion.EsAlCompletar(modoFacturacion),
            servirPendienteAlHaberStock = servirPendiente,
            Lineas = new List<LineaPedidoVentaDTO> { new LineaPedidoVentaDTO { id = 1, Producto = "17404", Cantidad = 200, almacen = "ALG", tipoLinea = 1 } }
        };

        private static OpcionesModosDTO Opciones(byte modoFacturacion, bool facturacionVisible = true, bool casillaVisible = false, bool? valor = null) => new OpcionesModosDTO
        {
            Facturacion = new OpcionesFacturacionDTO
            {
                Modo = modoFacturacion,
                Visible = facturacionVisible,
                ModosPermitidos = new List<byte> { 1, 2, 3 }
            },
            ServirPendienteAlHaberStock = new OpcionServirPendienteDTO { Visible = casillaVisible, Valor = valor, Motivo = "Lo dice la API." }
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio) => new DetallePedidoViewModel(
            A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
            A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());

        private static (DetallePedidoViewModel vm, IPedidoVentaService servicio) VmCon(PedidoVentaDTO pedido, OpcionesModosDTO respuesta)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>._)).Returns(respuesta);
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(pedido);
            return (vm, servicio);
        }

        [TestMethod]
        public async Task Detalle_ConTodoJunto_LaFacturacionNoSeEnsena()
        {
            var (vm, _) = VmCon(Pedido(926700, ModosFacturacion.AL_COMPLETAR), Opciones(ModosFacturacion.AL_COMPLETAR, facturacionVisible: false));
            Assert.IsTrue(vm.FacturacionVisible, "Mientras la API no conteste, se ve");

            await vm.RefrescarOpcionesModos();

            Assert.IsFalse(vm.FacturacionVisible);
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible);
        }

        [TestMethod]
        public async Task Detalle_SinRespuesta_LaFacturacionSeVe_YLaCasillaNo()
        {
            var (vm, _) = VmCon(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES), null);

            await vm.RefrescarOpcionesModos();

            Assert.IsTrue(vm.FacturacionVisible);
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible);
        }

        [TestMethod]
        public async Task Detalle_ApiAnterior_ComoAntes_FacturacionVisibleYSinCasilla()
        {
            var respuesta = OpcionesModosDTO.DesdeApiAnterior(null, new ModoFacturacionSugeridoDTO { Modo = 3, ModosPermitidos = new List<byte> { 1, 2, 3 } });
            var (vm, _) = VmCon(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES), respuesta);

            await vm.RefrescarOpcionesModos();

            Assert.IsTrue(vm.FacturacionVisible);
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible);
            vm.PrepararServirPendienteParaGuardar();
            Assert.IsNull(vm.pedido.Model.servirPendienteAlHaberStock, "Sin casilla no se manda nada");
        }

        [TestMethod]
        public async Task Detalle_ConTodoAhora_LaCasillaSaleConElValorDeLaApi_YSinTocarNoViajaAlGuardar()
        {
            var (vm, _) = VmCon(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, servirPendiente: false),
                Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: false));

            await vm.RefrescarOpcionesModos();

            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Visible);
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Valor);
            Assert.AreEqual("Lo dice la API.", vm.ServirPendienteAlHaberStock.Ayuda);
            vm.PrepararServirPendienteParaGuardar();
            Assert.IsNull(vm.pedido.Model.servirPendienteAlHaberStock, "Ni la tocó ni cambió el modo: no se manda");
        }

        [TestMethod]
        public async Task Detalle_SiElUsuarioLaToca_ViajaAlGuardar_YLaRespuestaSiguienteNoLaPisa()
        {
            var (vm, servicio) = VmCon(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES),
                Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true));
            await vm.RefrescarOpcionesModos();

            vm.ServirPendienteAlHaberStock.Valor = false; // el cliente lo quiere poco a poco (200 rollos de la 17404)
            vm.pedido.plazosPago = "60"; // otra pregunta a la API, que vuelve a proponer marcada
            await vm.RefrescarOpcionesModos();

            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Valor, "Lo que eligió el usuario manda");
            vm.PrepararServirPendienteParaGuardar();
            Assert.AreEqual(false, vm.pedido.Model.servirPendienteAlHaberStock);
        }

        [TestMethod]
        public async Task Detalle_AlElegirTodoAhora_MarcadaPorDefecto_YSinTocarlaSeMandaNullParaQueLaApiPongaTrue()
        {
            // Un «false» heredado (columna NULL de un «Todo ahora» antiguo, o lo que trajo el GET) no puede viajar: ni en la
            // pregunta ni al guardar. Con null la API pone marcada por defecto (decisión 12).
            var (vm, servicio) = VmCon(Pedido(926700, ModosFacturacion.POR_ENTREGAS, servirPendiente: false), Opciones(ModosFacturacion.POR_ENTREGAS));
            await vm.RefrescarOpcionesModos();
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible, "Por entregas: sin casilla");

            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>._))
                .Returns(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true));
            vm.pedido.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;
            await vm.RefrescarOpcionesModos();

            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>.That.Matches(p =>
                p.modoFacturacion == ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES && p.servirPendienteAlHaberStock == null))).MustHaveHappened();
            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Visible);
            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Valor);
            vm.PrepararServirPendienteParaGuardar();
            Assert.IsNull(vm.pedido.Model.servirPendienteAlHaberStock, "Sin tocarla se manda null y la API pone marcada");
        }

        [TestMethod]
        public async Task Detalle_VolverAlModoCargado_LaPreguntaLlevaOtraVezLoGuardado()
        {
            var (vm, servicio) = VmCon(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, servirPendiente: false),
                Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: false));

            vm.pedido.ModoFacturacion = ModosFacturacion.POR_ENTREGAS;
            Assert.IsNull(vm.pedido.Model.servirPendienteAlHaberStock);
            vm.pedido.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            Assert.AreEqual(false, vm.pedido.Model.servirPendienteAlHaberStock, "Es su modo de siempre: manda lo guardado");
        }

        [TestMethod]
        public async Task Detalle_SiDejaTodoAhora_LaCasillaSeVa_YNoSeManda()
        {
            var (vm, servicio) = VmCon(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES),
                Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true));
            await vm.RefrescarOpcionesModos();
            vm.ServirPendienteAlHaberStock.Valor = false;

            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>._)).Returns(Opciones(ModosFacturacion.POR_ENTREGAS));
            vm.pedido.ModoFacturacion = ModosFacturacion.POR_ENTREGAS;
            await vm.RefrescarOpcionesModos();

            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Visible);
            vm.PrepararServirPendienteParaGuardar();
            Assert.IsNull(vm.pedido.Model.servirPendienteAlHaberStock);
        }

        [TestMethod]
        public async Task Detalle_LaPreguntaLlevaElModoDeServicio_YCambiarloVuelveAPreguntar()
        {
            var (vm, servicio) = VmCon(Pedido(926700, ModosFacturacion.AL_COMPLETAR), Opciones(ModosFacturacion.AL_COMPLETAR));
            await vm.RefrescarOpcionesModos();

            vm.pedido.ModoServicio = ModosServicio.TODO_JUNTO;
            await vm.RefrescarOpcionesModos();

            Assert.AreEqual(2, vm.PeticionesOpcionesModosEnviadas);
            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>.That.Matches(p => p.modoServicio == ModosServicio.TODO_JUNTO))).MustHaveHappened();
            A.CallTo(() => servicio.ModoServicioSugerido(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.ModoFacturacionSugerido(A<PedidoVentaDTO>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Detalle_PedidoNuevoConTodoAhora_SinTocarNull_YDesmarcadaFalse()
        {
            var (vm, _) = VmCon(Pedido(0, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES),
                Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true));

            await vm.RefrescarOpcionesModos();
            vm.PrepararServirPendienteParaGuardar();
            Assert.IsNull(vm.pedido.Model.servirPendienteAlHaberStock, "La API pone marcada por defecto");

            vm.ServirPendienteAlHaberStock.Valor = false;
            vm.PrepararServirPendienteParaGuardar();
            Assert.AreEqual(false, vm.pedido.Model.servirPendienteAlHaberStock);
        }

        private static PedidoVentaDTO Nota()
        {
            PedidoVentaDTO nota = Pedido(926800, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, servirPendiente: true);
            nota.notaEntrega = true;
            return nota;
        }

        [TestMethod]
        public async Task Detalle_NotaDeEntrega_CambiarLaCasilla_VaAlMomentoASuEndpoint_YNoPorElPut()
        {
            // Decisión 9: en la nota se puede cambiar mientras no tenga picking. La API no deja modificar una nota por el
            // PUT, así que va a POST api/PedidosVenta/{empresa}/{nota}/ServirPendienteAlHaberStock.
            var (vm, servicio) = VmCon(Nota(), Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true));
            await vm.RefrescarOpcionesModos();

            vm.ServirPendienteAlHaberStock.Valor = false;
            await vm.CambioServirPendienteNotaEnCurso;

            A.CallTo(() => servicio.CambiarServirPendienteNota("1", 926800, false, A<string>._)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Valor);
            vm.PrepararServirPendienteParaGuardar();
            Assert.IsNull(vm.pedido.Model.servirPendienteAlHaberStock, "En una nota la casilla nunca viaja en el PUT");
        }

        [TestMethod]
        public async Task Detalle_NotaDeEntrega_SiLaApiLoRechaza_EnsenaSuMensajeTalCual_YLaCasillaVuelveAComoEstaba()
        {
            const string MENSAJE = "La nota 926800 ya tiene picking: no se puede cambiar.";
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.OpcionesModos(A<PedidoVentaDTO>._))
                .Returns(Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true));
            A.CallTo(() => servicio.CambiarServirPendienteNota(A<string>._, A<int>._, A<bool>._, A<string>._)).ThrowsAsync(new Exception(MENSAJE));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Nota());
            await vm.RefrescarOpcionesModos();

            vm.ServirPendienteAlHaberStock.Valor = false;
            await vm.CambioServirPendienteNotaEnCurso;

            A.CallTo(() => dialogos.ShowError(MENSAJE)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.ServirPendienteAlHaberStock.Valor, "Vuelve a como estaba");
            Assert.IsFalse(vm.ServirPendienteAlHaberStock.Tocada);
        }

        [TestMethod]
        public async Task Detalle_PedidoQueNoEsNota_CambiarLaCasilla_NoLlamaAlEndpointDeLaNota()
        {
            var (vm, servicio) = VmCon(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES),
                Opciones(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, casillaVisible: true, valor: true));
            await vm.RefrescarOpcionesModos();

            vm.ServirPendienteAlHaberStock.Valor = false;

            A.CallTo(() => servicio.CambiarServirPendienteNota(A<string>._, A<int>._, A<bool>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
