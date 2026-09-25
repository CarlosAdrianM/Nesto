using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#493 / NestoAPI#542: el combo «Facturación» del detalle de pedido sustituye a la casilla «Mantener junto»,
    /// con el mismo patrón que «Servir» (Nesto#484) y el núcleo compartido en Nesto.Models (SelectorModos,
    /// ProgramadorPeticionesConHuella). Aquí lo que es del cliente: coherencia modo ↔ mantenerJunto en el wrapper,
    /// el selector que refleja los permitidos, el DTO que serializa el modo, y el ViewModel que solo pregunta
    /// cuando cambia lo que la API mira (cliente, plazos, periodo), nunca al montar líneas.
    /// </summary>
    [TestClass]
    public class ModosFacturacionDetalleTests
    {
        private static ModoFacturacionSugeridoDTO Sugerencia(byte modo, params byte[] permitidos) => new ModoFacturacionSugeridoDTO
        {
            Modo = modo,
            Nombre = ModosFacturacion.Nombre(modo),
            ModosPermitidos = permitidos.ToList(),
            Modos = ModosFacturacion.Lista.Select(m => new ModoFacturacionPermitidoDTO
            {
                Modo = m.Codigo,
                Nombre = m.Nombre,
                Permitido = permitidos.Contains(m.Codigo),
                Motivo = permitidos.Contains(m.Codigo) ? null : "Los plazos de pago no son los de la ficha del cliente."
            }).ToList()
        };

        // ---------- El modelo ----------

        [TestMethod]
        public void ModosFacturacion_Efectivo_MantenerJuntoMarcadoEsAlCompletar_DesmarcadoRespetaElTres()
        {
            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, ModosFacturacion.Efectivo(null, true));
            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, ModosFacturacion.Efectivo(null, false));
            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, ModosFacturacion.Efectivo(3, true), "Marcado manda aunque el guardado sea 3 (misma regla que la API)");
            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, ModosFacturacion.Efectivo(3, false));
            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, ModosFacturacion.Efectivo(2, false), "Un 2 desmarcado (el Nesto viejo lo desmarcó) es por entregas");
        }

        [TestMethod]
        public void ModosFacturacion_Lista_OfreceLosTresModosConNombreYDescripcion()
        {
            CollectionAssert.AreEqual(new List<byte> { 1, 2, 3 }, ModosFacturacion.Lista.Select(m => m.Codigo).ToList());
            Assert.IsTrue(ModosFacturacion.Lista.All(m => !string.IsNullOrWhiteSpace(m.Nombre) && !string.IsNullOrWhiteSpace(m.Descripcion)));
            Assert.AreEqual("Todo ahora, lo pendiente se entrega después", ModosFacturacion.Nombre(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));
        }

        [TestMethod]
        public void PedidoVentaDTO_SerializaModoFacturacion_YNullCuandoNoSeManda()
        {
            var conModo = new PedidoVentaDTO { empresa = "1", modoFacturacion = 3, mantenerJunto = false };
            var sinModo = new PedidoVentaDTO { empresa = "1", mantenerJunto = true };

            JObject jsonConModo = JObject.Parse(JsonConvert.SerializeObject(conModo));
            JObject jsonSinModo = JObject.Parse(JsonConvert.SerializeObject(sinModo));

            Assert.AreEqual(3, jsonConModo["modoFacturacion"].Value<int>());
            Assert.AreEqual(JTokenType.Null, jsonSinModo["modoFacturacion"].Type, "Sin modo se manda null y la API lo deriva de mantenerJunto");
            Assert.IsTrue(jsonSinModo["mantenerJunto"].Value<bool>());
        }

        [TestMethod]
        public void PedidoVentaDTO_ModoFacturacionCuentaComoCambio_YElSnapshotLoConserva()
        {
            var a = new PedidoVentaDTO { empresa = "1", numero = 1, modoFacturacion = 1 };
            var b = new PedidoVentaDTO { empresa = "1", numero = 1, modoFacturacion = 3 };

            Assert.IsFalse(a.Equals(b));
            Assert.IsTrue(a.ObtenerCamposDiferentes(b).Any(d => d.StartsWith("modoFacturacion")));
            Assert.AreEqual((byte?)3, b.CrearSnapshot().modoFacturacion);
            Assert.IsTrue(b.Equals(b.CrearSnapshot()));
        }

        [TestMethod]
        public void LineaPedidoVentaDTO_DeserializaRecogerYYaFacturado()
        {
            var linea = JsonConvert.DeserializeObject<LineaPedidoVentaDTO>("{\"id\":1,\"Producto\":\"38093\",\"Cantidad\":5,\"recoger\":3,\"yaFacturado\":true}");

            Assert.AreEqual(3, linea.recoger);
            Assert.IsTrue(linea.yaFacturado);
        }

        // ---------- El wrapper: modo ↔ mantenerJunto ----------

        [TestMethod]
        public void Wrapper_PedidoAnteriorAlModo_MuestraElQueDerivaDeMantenerJunto()
        {
            var marcado = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", mantenerJunto = true });
            var desmarcado = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", mantenerJunto = false });

            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, marcado.ModoFacturacion);
            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, desmarcado.ModoFacturacion);
        }

        [TestMethod]
        public void Wrapper_ElegirTodoAhora_DesmarcaMantenerJuntoYLoGuardaEnElModelo()
        {
            var wrapper = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", mantenerJunto = true, modoFacturacion = 2 });

            wrapper.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            Assert.AreEqual((byte?)3, wrapper.Model.modoFacturacion);
            Assert.IsFalse(wrapper.mantenerJunto);
        }

        [TestMethod]
        public void Wrapper_ElegirAlCompletar_MarcaMantenerJunto()
        {
            var wrapper = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", mantenerJunto = false, modoFacturacion = 1 });

            wrapper.ModoFacturacion = ModosFacturacion.AL_COMPLETAR;

            Assert.IsTrue(wrapper.mantenerJunto);
            Assert.AreEqual((byte?)2, wrapper.Model.modoFacturacion);
        }

        [TestMethod]
        public void Wrapper_MarcarMantenerJuntoPorCodigo_PoneElModo2_YDesmarcarloRespetaElTres()
        {
            var wrapper = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", mantenerJunto = false, modoFacturacion = 3 });

            wrapper.mantenerJunto = true;
            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, wrapper.ModoFacturacion);

            wrapper.ModoFacturacion = 3;
            wrapper.mantenerJunto = false;
            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, wrapper.ModoFacturacion, "Desmarcar con un 3 puesto no lo pisa");

            wrapper.mantenerJunto = true;
            wrapper.mantenerJunto = false;
            Assert.AreEqual(ModosFacturacion.POR_ENTREGAS, wrapper.ModoFacturacion, "Desmarcar desde el 2 cae al 1");
        }

        // ---------- El selector compartido ----------

        [TestMethod]
        public void Selector_SinRespuesta_TodasHabilitadas_ConRespuesta_DeshabilitaConSuMotivo()
        {
            var selector = new SelectorModosFacturacion();
            selector.Aplicar(null);
            Assert.IsTrue(selector.Opciones.All(o => o.Habilitado));

            selector.Aplicar(Sugerencia(ModosFacturacion.AL_COMPLETAR, ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));

            CollectionAssert.AreEqual(new[] { ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES },
                selector.Opciones.Where(o => o.Habilitado).Select(o => o.Codigo).ToArray());
            Assert.AreEqual("Los plazos de pago no son los de la ficha del cliente.", selector.Opciones.Single(o => o.Codigo == ModosFacturacion.POR_ENTREGAS).Ayuda);
            Assert.IsFalse(selector.EsPermitido(ModosFacturacion.POR_ENTREGAS));
        }

        [TestMethod]
        public void Selector_TextoAviso_NombraLosDosModosYElMotivo()
        {
            string aviso = SelectorModosFacturacion.TextoAvisoCambio(ModosFacturacion.POR_ENTREGAS, ModosFacturacion.AL_COMPLETAR, "Los plazos no son los de la ficha.");

            StringAssert.Contains(aviso, "Por entregas");
            StringAssert.Contains(aviso, "Al completar el pedido");
            StringAssert.Contains(aviso, "Los plazos no son los de la ficha");
        }

        [TestMethod]
        public void SelectorModosServicio_SigueFuncionandoSobreElNucleoComun()
        {
            var selector = new SelectorModosServicio();
            var sugerencia = new ModoServicioSugeridoDTO
            {
                Modo = ModosServicio.TODO_JUNTO,
                ModosPermitidos = new List<byte> { ModosServicio.TODO_JUNTO },
                Modos = new List<ModoServicioPermitidoDTO> { new ModoServicioPermitidoDTO { Modo = ModosServicio.SEGUN_VAYA_ENTRANDO, Permitido = false, Motivo = "Motivo 2." } }
            };

            selector.Aplicar(sugerencia);

            Assert.IsTrue(selector.EsPermitido(ModosServicio.TODO_JUNTO));
            Assert.AreEqual("Motivo 2.", SelectorModos.MotivoDe(sugerencia, ModosServicio.SEGUN_VAYA_ENTRANDO));
            Assert.AreEqual("Motivo 2.", selector.Opciones.Single(o => o.Codigo == ModosServicio.SEGUN_VAYA_ENTRANDO).Ayuda);
        }

        // ---------- El detalle de pedido ----------

        private static PedidoVentaDTO Pedido(int numero, byte modo, bool notaEntrega = false) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "15191",
            contacto = "0",
            contactoCobro = "0",
            plazosPago = "30",
            periodoFacturacion = "NRM",
            notaEntrega = notaEntrega,
            modoFacturacion = modo,
            mantenerJunto = ModosFacturacion.EsAlCompletar(modo),
            modoServicio = ModosServicio.TRAS_REPONER_DE_TIENDAS,
            Lineas = new List<LineaPedidoVentaDTO> { new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1 } }
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IDialogService dialogService = null) => new DetallePedidoViewModel(
            A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
            dialogService ?? A.Fake<IDialogService>(), A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());

        [TestMethod]
        public async Task Detalle_PedidoGrabadoCuyoModoYaNoVale_PasaAlSugeridoYAvisa_PorqueGuardarDariaUn400()
        {
            // A diferencia del modo de servicio, la API comprueba el modo de facturación siempre que se manda
            // informado (y Nesto lo manda siempre). Cambiar los plazos a unos que no son de la ficha deja «por
            // entregas» sin sentido: se pasa al que vale y se avisa, como hacía a escondidas el trigger.
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoFacturacionSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosFacturacion.AL_COMPLETAR, ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosFacturacion.POR_ENTREGAS));

            await vm.RefrescarModosFacturacionPermitidos();

            CollectionAssert.AreEqual(new[] { ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES },
                vm.OpcionesModoFacturacion.Where(o => o.Habilitado).Select(o => o.Codigo).ToArray());
            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.pedido.ModoFacturacion);
            Assert.IsTrue(vm.pedido.mantenerJunto);
            Assert.IsTrue(vm.HayAvisoModoFacturacion, "Nada de cambiar en silencio");
            StringAssert.Contains(vm.AvisoModoFacturacion, "Los plazos de pago no son los de la ficha");
        }

        [TestMethod]
        public async Task Detalle_PedidoGrabadoCuyoModoSigueValiendo_NoSeToca()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoFacturacionSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, 1, 2, 3));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));

            await vm.RefrescarModosFacturacionPermitidos();

            Assert.IsTrue(vm.OpcionesModoFacturacion.All(o => o.Habilitado));
            Assert.AreEqual(ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES, vm.pedido.ModoFacturacion);
            Assert.IsFalse(vm.HayAvisoModoFacturacion);
        }

        [TestMethod]
        public async Task Detalle_SiElModoQueEligioElUsuarioDejaDeValer_PasaAlSugeridoYAvisa()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoFacturacionSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosFacturacion.AL_COMPLETAR, ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosFacturacion.AL_COMPLETAR));

            vm.pedido.ModoFacturacion = ModosFacturacion.POR_ENTREGAS; // elección del usuario
            await vm.RefrescarModosFacturacionPermitidos();

            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.pedido.ModoFacturacion);
            Assert.IsTrue(vm.pedido.mantenerJunto, "El bit sigue coherente con el modo");
            Assert.IsTrue(vm.HayAvisoModoFacturacion);
            StringAssert.Contains(vm.AvisoModoFacturacion, "Por entregas");
            StringAssert.Contains(vm.AvisoModoFacturacion, "Los plazos de pago no son los de la ficha");
        }

        [TestMethod]
        public async Task Detalle_PedidoNuevoSinTocar_ElDefectoQueNoValeSeSustituyeSinAviso()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoFacturacionSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosFacturacion.AL_COMPLETAR, ModosFacturacion.AL_COMPLETAR, ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(0, ModosFacturacion.POR_ENTREGAS));

            await vm.RefrescarModosFacturacionPermitidos();

            Assert.AreEqual(ModosFacturacion.AL_COMPLETAR, vm.pedido.ModoFacturacion);
            Assert.IsFalse(vm.HayAvisoModoFacturacion, "Es la preselección, como en la plantilla");
        }

        [TestMethod]
        public async Task Detalle_MontarLineas_NoVuelveAPreguntar_CambiarPlazosSi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ModoFacturacionSugerido(A<PedidoVentaDTO>._)).Returns(Sugerencia(ModosFacturacion.POR_ENTREGAS, 1, 2, 3));
            DetallePedidoViewModel vm = Vm(servicio);
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosFacturacion.POR_ENTREGAS));

            await vm.RefrescarModosFacturacionPermitidos();
            vm.pedido.Model.Lineas.Add(new LineaPedidoVentaDTO { id = 2, Producto = "12345", Cantidad = 7, almacen = "ALG", tipoLinea = 1 });
            await vm.RefrescarModosFacturacionPermitidos();
            Assert.AreEqual(1, vm.PeticionesModosFacturacionEnviadas, "Las líneas no cambian los modos de facturación (NestoAPI#517)");

            vm.pedido.plazosPago = "60";
            await vm.RefrescarModosFacturacionPermitidos();
            Assert.AreEqual(2, vm.PeticionesModosFacturacionEnviadas);
        }

        [TestMethod]
        public void Detalle_NotaDeEntrega_NoPuedeElegirModo_YEnlazaConElPedidoOrigen()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>());
            PedidoVentaDTO nota = Pedido(926969, ModosFacturacion.POR_ENTREGAS, notaEntrega: true);
            nota.pedidoOrigen = 926346;
            nota.albaranOrigen = 512345;
            nota.Lineas.First().recoger = 2;
            nota.Lineas.First().yaFacturado = true;

            vm.pedido = new PedidoVentaWrapper(nota);

            Assert.IsFalse(vm.PuedeElegirModoFacturacion);
            Assert.IsTrue(vm.HayPedidoOrigen);
            Assert.AreEqual("Nota de entrega del pedido 926346 (albarán 512345)", vm.TextoPedidoOrigen);
            Assert.IsTrue(vm.AbrirPedidoOrigenCommand.CanExecute(null));
            Assert.IsTrue(vm.HayLineasARecoger);
            Assert.AreEqual(2, vm.pedido.Lineas[0].recoger);
            Assert.IsTrue(vm.pedido.Lineas[0].yaFacturado);
        }

        [TestMethod]
        public void Detalle_PedidoNormal_PuedeElegirModo_YSinPedidoOrigen()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>());

            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosFacturacion.POR_ENTREGAS));

            Assert.IsTrue(vm.PuedeElegirModoFacturacion);
            Assert.IsFalse(vm.HayPedidoOrigen);
            Assert.AreEqual(string.Empty, vm.TextoPedidoOrigen);
            Assert.IsFalse(vm.AbrirPedidoOrigenCommand.CanExecute(null));
            Assert.IsFalse(vm.HayLineasARecoger);
        }

        [TestMethod]
        public void Detalle_TodoAhora_EnsenaElAvisoDePortes()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>());
            vm.pedido = new PedidoVentaWrapper(Pedido(926700, ModosFacturacion.POR_ENTREGAS));
            Assert.IsFalse(vm.EsModoFacturacionTodoAhora);

            vm.pedido.ModoFacturacion = ModosFacturacion.TODO_AHORA_Y_LO_PENDIENTE_DESPUES;

            Assert.IsTrue(vm.EsModoFacturacionTodoAhora);
            StringAssert.Contains(vm.AvisoPortesTodoAhora, "portes");
            Assert.IsFalse(vm.pedido.mantenerJunto);
        }
    }
}
