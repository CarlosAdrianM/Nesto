using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#484 / NestoAPI#518 (segunda parte): el combo «Servir» del detalle de pedido recibe el mismo tratamiento
    /// que el de la plantilla, con el código compartido en Nesto.Models (SelectorModosServicio,
    /// ProgramadorPeticionesConHuella, ModoServicioNoPermitidoException).
    /// </summary>
    [TestClass]
    public class ModosServicioPermitidosDetalleTests
    {
        private static ModoServicioSugeridoDTO Sugerencia(byte modo, params byte[] permitidos) => new ModoServicioSugeridoDTO
        {
            Modo = modo,
            ModosPermitidos = permitidos.ToList(),
            Modos = new List<ModoServicioPermitidoDTO>
            {
                new ModoServicioPermitidoDTO { Modo = ModosServicio.TODO_JUNTO, Permitido = permitidos.Contains(ModosServicio.TODO_JUNTO), Motivo = permitidos.Contains(ModosServicio.TODO_JUNTO) ? null : "Motivo 1." },
                new ModoServicioPermitidoDTO { Modo = ModosServicio.SEGUN_VAYA_ENTRANDO, Permitido = permitidos.Contains(ModosServicio.SEGUN_VAYA_ENTRANDO), Motivo = permitidos.Contains(ModosServicio.SEGUN_VAYA_ENTRANDO) ? null : "Motivo 2." },
                new ModoServicioPermitidoDTO { Modo = ModosServicio.TRAS_REPONER_DE_TIENDAS, Permitido = permitidos.Contains(ModosServicio.TRAS_REPONER_DE_TIENDAS), Motivo = permitidos.Contains(ModosServicio.TRAS_REPONER_DE_TIENDAS) ? null : "No hay nada que traer de las tiendas." },
                new ModoServicioPermitidoDTO { Modo = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ, Permitido = permitidos.Contains(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ), Motivo = permitidos.Contains(ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ) ? null : "Motivo 4." },
            }
        };

        // ---------- Lo compartido ----------

        [TestMethod]
        public void Selector_SinRespuesta_TodasHabilitadas_ConRespuesta_DeshabilitaConSuMotivo()
        {
            var selector = new SelectorModosServicio();
            selector.Aplicar(null);
            Assert.IsTrue(selector.Opciones.All(o => o.Habilitado));

            selector.Aplicar(Sugerencia(ModosServicio.TODO_JUNTO, ModosServicio.TODO_JUNTO));

            CollectionAssert.AreEqual(new[] { ModosServicio.TODO_JUNTO }, selector.Opciones.Where(o => o.Habilitado).Select(o => o.Codigo).ToArray());
            Assert.AreEqual("No hay nada que traer de las tiendas.", selector.Opciones.Single(o => o.Codigo == ModosServicio.TRAS_REPONER_DE_TIENDAS).Ayuda);
            Assert.IsFalse(selector.EsPermitido(ModosServicio.SEGUN_VAYA_ENTRANDO));
        }

        [TestMethod]
        public void Selector_TextoAviso_NombraLosDosModosYElMotivo()
        {
            string aviso = SelectorModosServicio.TextoAvisoCambio(ModosServicio.TRAS_REPONER_DE_TIENDAS, ModosServicio.TODO_JUNTO, "No hay nada que traer de las tiendas.");

            StringAssert.Contains(aviso, "Tras reponer");
            StringAssert.Contains(aviso, "Todo junto");
            StringAssert.Contains(aviso, "No hay nada que traer de las tiendas");
        }

        [TestMethod]
        public void Excepcion_DesdeRespuesta_SoloParaElCodigoDeModoYConElModoSugerido()
        {
            JObject rechazo = JObject.Parse("{\"error\":{\"code\":\"MODO_SERVICIO_NO_PERMITIDO\",\"message\":\"x\",\"details\":{\"modoSugerido\":1}}}");
            JObject otro = JObject.Parse("{\"error\":{\"code\":\"PEDIDO_VALIDACION_FALLO\"}}");

            ModoServicioNoPermitidoException ex = ModoServicioNoPermitidoException.DesdeRespuesta(rechazo, "Mensaje legible");

            Assert.IsNotNull(ex);
            Assert.AreEqual("Mensaje legible", ex.Message);
            Assert.AreEqual((byte?)ModosServicio.TODO_JUNTO, ex.ModoSugerido);
            Assert.IsNull(ModoServicioNoPermitidoException.DesdeRespuesta(otro, "y"));
        }

        [TestMethod]
        public void InterpretarRespuestaError_ReconoceElRechazoDeModo()
        {
            Exception ex = PedidoVentaService.InterpretarRespuestaError("{\"error\":{\"code\":\"MODO_SERVICIO_NO_PERMITIDO\",\"message\":\"Elige «Todo junto» y vuelve a guardar.\",\"details\":{\"modoSugerido\":1}}}");

            Assert.IsInstanceOfType(ex, typeof(ModoServicioNoPermitidoException));
            Assert.AreEqual((byte?)ModosServicio.TODO_JUNTO, ((ModoServicioNoPermitidoException)ex).ModoSugerido);
        }

        [TestMethod]
        public async Task Programador_MismaHuella_NoVuelveAPreguntar_DistintaSi()
        {
            var programador = new ProgramadorPeticionesConHuella(60000, null);
            string huella = "A";
            int llamadas = 0;
            Func<Task> ejecutar = () => programador.EjecutarAsync<string>(() => huella, h => h, null, h => { llamadas++; return Task.CompletedTask; });

            await ejecutar();
            await ejecutar();
            Assert.AreEqual(1, llamadas, "Con lo mismo no se vuelve a preguntar (NestoAPI#517)");

            huella = "B";
            await ejecutar();
            Assert.AreEqual(2, llamadas);
            Assert.AreEqual(2, programador.PeticionesEnviadas);
        }

        [TestMethod]
        public async Task Programador_ConUnaPeticionEnVuelo_NoLanzaOtra()
        {
            var programador = new ProgramadorPeticionesConHuella(60000, null);
            var enCurso = new TaskCompletionSource<bool>();
            int llamadas = 0;

            Task primera = programador.EjecutarAsync<string>(() => "A", h => h, null, h => { llamadas++; return enCurso.Task; });
            await programador.EjecutarAsync<string>(() => "B", h => h, null, h => { llamadas++; return Task.CompletedTask; });
            Assert.AreEqual(1, llamadas, "Mientras hay una en vuelo, la siguiente se aplaza");

            enCurso.SetResult(true);
            await primera;
        }

        [TestMethod]
        public async Task Programador_SinNadaQuePreguntar_LlamaAlNoHaberNada()
        {
            var programador = new ProgramadorPeticionesConHuella(60000, null);
            bool limpiado = false;

            await programador.EjecutarAsync<string>(() => null, h => h, () => limpiado = true, h => Task.CompletedTask);

            Assert.IsTrue(limpiado);
            Assert.AreEqual(0, programador.PeticionesEnviadas);
        }

        // ---------- El detalle de pedido ----------

        private static PedidoVentaDTO Pedido(int numero, byte modo) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "15191",
            contacto = "0",
            modoServicio = modo,
            servirJunto = ModosServicio.EsTodoJunto(modo),
            Lineas = new List<LineaPedidoVentaDTO> { new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1 } }
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IDialogService dialogService = null) => new DetallePedidoViewModel(
            A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
            dialogService ?? A.Fake<IDialogService>(), A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());

        [TestMethod]
        public async Task Detalle_AlAbrirUnPedidoGrabado_DeshabilitaLasOpcionesPeroNoCambiaElModo()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoServicioSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosServicio.TODO_JUNTO, ModosServicio.TODO_JUNTO));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.TRAS_REPONER_DE_TIENDAS));

            await vm.RefrescarModosPermitidos();

            CollectionAssert.AreEqual(new[] { ModosServicio.TODO_JUNTO }, vm.OpcionesModoServicio.Where(o => o.Habilitado).Select(o => o.Codigo).ToArray());
            Assert.AreEqual(ModosServicio.TRAS_REPONER_DE_TIENDAS, vm.pedido.ModoServicio, "En un pedido grabado el modo no se toca solo: el servidor solo lo comprueba si cambia");
            Assert.IsFalse(vm.HayAvisoModoServicio);
        }

        [TestMethod]
        public async Task Detalle_SiElModoQueEligioElUsuarioDejaDeValer_PasaAlSugeridoYAvisa()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoServicioSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosServicio.TODO_JUNTO, ModosServicio.TODO_JUNTO));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.TRAS_REPONER_DE_TIENDAS));

            vm.pedido.ModoServicio = ModosServicio.AHORA_LO_QUE_HAY_Y_EL_RESTO_DE_UNA_VEZ; // elección del usuario
            await vm.RefrescarModosPermitidos();

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.pedido.ModoServicio);
            Assert.IsTrue(vm.HayAvisoModoServicio);
            StringAssert.Contains(vm.AvisoModoServicio, "Motivo 4");
        }

        [TestMethod]
        public async Task Detalle_PedidoNuevoSinTocar_ElDefectoQueNoValeSeSustituyeSinAviso()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoServicioSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosServicio.SEGUN_VAYA_ENTRANDO, ModosServicio.SEGUN_VAYA_ENTRANDO));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(0, ModosServicio.TRAS_REPONER_DE_TIENDAS));

            await vm.RefrescarModosPermitidos();

            Assert.AreEqual(ModosServicio.SEGUN_VAYA_ENTRANDO, vm.pedido.ModoServicio);
            Assert.IsFalse(vm.HayAvisoModoServicio, "Es la preselección, como en la plantilla");
        }

        [TestMethod]
        public async Task Detalle_ConElPedidoQuieto_NoSeVuelveAPreguntar()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoServicioSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosServicio.TODO_JUNTO, ModosServicio.TODO_JUNTO, ModosServicio.SEGUN_VAYA_ENTRANDO));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.TODO_JUNTO));

            await vm.RefrescarModosPermitidos();
            await vm.RefrescarModosPermitidos();
            await vm.RefrescarModosPermitidos();

            Assert.AreEqual(1, vm.PeticionesModosEnviadas);
        }

        [TestMethod]
        public async Task Detalle_SiLaApiRechazaAlGuardar_EnsenaElMensajeYPreseleccionaElSugerido()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._))
                .ThrowsAsync(new ModoServicioNoPermitidoException("El modo «Tras reponer de tiendas» no tiene sentido. Elige «Todo junto» y vuelve a guardar.", ModosServicio.TODO_JUNTO));
            IDialogService dialogService = A.Fake<IDialogService>();
            DetallePedidoViewModel vm = Vm(servicio, dialogService);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosServicio.TRAS_REPONER_DE_TIENDAS));

            await vm.ModificarPedidoAsync();

            Assert.AreEqual(ModosServicio.TODO_JUNTO, vm.pedido.ModoServicio);
            A.CallTo(() => dialogService.ShowDialog("NotificationDialog",
                    A<IDialogParameters>.That.Matches(p => p.GetValue<string>("message").Contains("Elige «Todo junto»")), A<Action<IDialogResult>>._))
                .MustHaveHappened();
        }
    }
}
