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
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Caso 931007 (NestoAPI): a las 13:33 se añadió el 39284 al pedido 931007, que ya tenía el picking 100125. Ahora la API contesta
    /// 409 PEDIDO_EN_PREPARACION sin guardar nada y Nesto pregunta: pedido nuevo con lo añadido, pedir a Almacén que lo meta en el
    /// envío de hoy (si la API lo ofrece) o cancelar; y repite el PUT con la elección.
    /// </summary>
    [TestClass]
    public class AmpliacionPedidoEnPreparacionDetalleTests
    {
        private const int NUMERO = 931007;
        private const string MENSAJE_409 = "El pedido 931007 ya está en preparación (picking 100125) y sale hoy. Haga un pedido nuevo con lo que quiera " +
            "añadir o, desde Nesto, pida a Almacén que lo meta en el mismo envío.";

        private static PedidoVentaDTO Pedido() => new PedidoVentaDTO
        {
            empresa = "1",
            numero = NUMERO,
            cliente = "15191",
            contacto = "0",
            Version = "v1",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "40010", Cantidad = 1, almacen = "ALG", tipoLinea = 1, picking = 100125, fechaEntrega = new DateTime(2026, 11, 18) }
            }
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogos, IAbridorPedidos abridor = null)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, abridor ?? A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Pedido());
            vm.pedido.Model.Lineas.Add(new LineaPedidoVentaDTO { id = 0, Producto = "39284", Cantidad = 1, almacen = "ALG", tipoLinea = 1 });
            vm.MarcarComoGuardado();
            return vm;
        }

        private static PedidoEnPreparacionException Rechazo(bool puedePedirAlmacen = true) => new PedidoEnPreparacionException(MENSAJE_409,
            new List<int> { 100125 }, null, false,
            puedePedirAlmacen ? new List<string> { "PedidoNuevo", "PedirAlmacen" } : new List<string> { "PedidoNuevo" });

        /// <summary>El diálogo de botones contesta <paramref name="boton"/> y apunta los parámetros.</summary>
        private static List<ParametrosDialogo> DialogoContesta(IServicioDialogos dialogos, ResultadoBoton boton)
        {
            var abiertos = new List<ParametrosDialogo>();
            A.CallTo(() => dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo parametros, Action<ResultadoDialogo> callback) =>
                {
                    abiertos.Add(parametros);
                    callback?.Invoke(new ResultadoDialogo(boton));
                });
            return abiertos;
        }

        /// <summary>El primer PUT da el 409; el segundo, lo que diga <paramref name="avisos"/>. Apunta la elección de cada PUT.</summary>
        private static List<string> ServicioQueRechazaUnaVez(IPedidoVentaService servicio, List<AvisoPedidoModel> avisos, bool puedePedirAlmacen = true)
        {
            var elecciones = new List<string>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                elecciones.Add(p.AmpliacionEnPreparacion);
                if (elecciones.Count == 1)
                {
                    throw Rechazo(puedePedirAlmacen);
                }
                return Task.FromResult(avisos);
            });
            return elecciones;
        }

        // ---------- Lo que llega de la API ----------

        [TestMethod]
        public void InterpretarRespuestaError_PedidoEnPreparacion_MensajeLimpioYDetalles()
        {
            string json = "{\"error\":{\"code\":\"PEDIDO_EN_PREPARACION\",\"message\":\"" + MENSAJE_409 + "\",\"details\":{\"empresa\":\"1\",\"pedido\":931007," +
                "\"Pickings\":[100125],\"Envio\":null,\"AgenciaYaLoTiene\":false,\"Opciones\":[\"PedidoNuevo\",\"PedirAlmacen\"],\"Productos\":[\"39284\"]}}}";

            var ex = PedidoVentaService.InterpretarRespuestaError(json) as PedidoEnPreparacionException;

            Assert.IsNotNull(ex);
            Assert.AreEqual(MENSAJE_409, ex.Message);
            CollectionAssert.AreEqual(new[] { 100125 }, ex.Pickings);
            Assert.IsNull(ex.Envio);
            Assert.IsFalse(ex.AgenciaYaLoTiene);
            Assert.IsTrue(ex.PuedePedirAlmacen);
        }

        [TestMethod]
        public void InterpretarRespuestaError_LaAgenciaYaLoTiene_SoloPedidoNuevo()
        {
            string json = "{\"error\":{\"code\":\"PEDIDO_EN_PREPARACION\",\"message\":\"…\",\"details\":{\"Pickings\":[100125],\"Envio\":251300," +
                "\"AgenciaYaLoTiene\":true,\"Opciones\":[\"PedidoNuevo\"]}}}";

            var ex = (PedidoEnPreparacionException)PedidoVentaService.InterpretarRespuestaError(json);

            Assert.AreEqual(251300, ex.Envio);
            Assert.IsTrue(ex.AgenciaYaLoTiene);
            Assert.IsFalse(ex.PuedePedirAlmacen);
        }

        [TestMethod]
        public void PedidoNuevo_DeLosAvisos()
        {
            var avisos = new List<AvisoPedidoModel>
            {
                new AvisoPedidoModel { Tipo = "Otro", Mensaje = "x" },
                new AvisoPedidoModel { Tipo = "AmpliacionEnPedidoNuevo", Mensaje = "…", Datos = JObject.Parse("{\"PedidoNuevo\":931050,\"Pedido\":931007}") }
            };

            Assert.AreEqual(931050, AmpliacionPedidoEnPreparacion.PedidoNuevo(avisos));
            Assert.IsNull(AmpliacionPedidoEnPreparacion.PedidoNuevo(new List<AvisoPedidoModel>()));
            Assert.IsNull(AmpliacionPedidoEnPreparacion.PedidoNuevo(null));
        }

        // ---------- El detalle del pedido ----------

        [TestMethod]
        public async Task Guardar_409_PedidoNuevo_RepiteConLaEleccionDiceElNumeroOfreceAbrirloYRecarga()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            List<string> elecciones = ServicioQueRechazaUnaVez(servicio, new List<AvisoPedidoModel>
            {
                new AvisoPedidoModel
                {
                    Tipo = "AmpliacionEnPedidoNuevo",
                    Mensaje = "El pedido 931007 ya estaba en preparación: 39284 (1 ud.) va en el pedido nuevo 931050, con sus propios portes.",
                    Datos = JObject.Parse("{\"PedidoNuevo\":931050,\"Pedido\":931007}")
                }
            });
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).Returns(Pedido());
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            List<ParametrosDialogo> preguntas = DialogoContesta(dialogos, ResultadoBoton.OK); // «Hacer un pedido nuevo con lo añadido»
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(true));
            IAbridorPedidos abridor = A.Fake<IAbridorPedidos>();
            DetallePedidoViewModel vm = Vm(servicio, dialogos, abridor);

            await vm.ModificarPedidoAsync();

            CollectionAssert.AreEqual(new[] { null, "PedidoNuevo" }, elecciones);
            ParametrosDialogo pregunta = preguntas.Single();
            Assert.AreEqual(MENSAJE_409, pregunta.GetValue<string>("message"));
            Assert.AreEqual(AmpliacionPedidoEnPreparacion.TEXTO_PEDIDO_NUEVO, pregunta.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_SI));
            Assert.AreEqual(AmpliacionPedidoEnPreparacion.TEXTO_PEDIR_ALMACEN, pregunta.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_TERCERO));
            A.CallTo(() => dialogos.ShowConfirmationAsync(AmpliacionPedidoEnPreparacion.TITULO_PEDIDO_NUEVO, A<string>.That.Contains("¿Desea abrir el pedido 931050?")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => abridor.Abrir("1", 931050)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.pedido.Model.Lineas.Any(l => l.Producto == "39284"), "Recargado: lo añadido ya no está en este pedido");
        }

        [TestMethod]
        public async Task Guardar_409_PedirAlmacen_RepiteConLaEleccionYDiceQueSeLoHaPedido()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            const string AVISO = "Se ha pedido a Almacén que meta 39284 (1 ud.) en el envío de hoy del pedido 931007. Le avisaremos con su respuesta; " +
                "si no contestan en 60 minutos, irá en un pedido nuevo con sus propios portes.";
            List<string> elecciones = ServicioQueRechazaUnaVez(servicio, new List<AvisoPedidoModel>
            {
                new AvisoPedidoModel { Tipo = "AmpliacionPedidaAAlmacen", Mensaje = AVISO, Datos = JObject.Parse("{\"Solicitud\":12}") }
            });
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            DialogoContesta(dialogos, ResultadoBoton.No); // el del medio: «Pedir a Almacén…»
            DetallePedidoViewModel vm = Vm(servicio, dialogos);

            await vm.ModificarPedidoAsync();

            CollectionAssert.AreEqual(new[] { null, "PedirAlmacen" }, elecciones);
            A.CallTo(() => dialogos.ShowNotification(AmpliacionPedidoEnPreparacion.TITULO, AVISO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_409_Cancelar_NoGuardaNiEnsenaError()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            List<string> elecciones = ServicioQueRechazaUnaVez(servicio, new List<AvisoPedidoModel>());
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            DialogoContesta(dialogos, ResultadoBoton.Cancel);
            DetallePedidoViewModel vm = Vm(servicio, dialogos);

            await vm.ModificarPedidoAsync();

            Assert.AreEqual(1, elecciones.Count, "Solo el primer PUT");
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>.That.Contains("modificado correctamente"))).MustNotHaveHappened();
            Assert.IsTrue(vm.pedido.Model.Lineas.Any(l => l.Producto == "39284"), "Lo añadido sigue en pantalla para decidir");
        }

        [TestMethod]
        public async Task Guardar_409_SinPedirAlmacen_SoloDosBotones()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            ServicioQueRechazaUnaVez(servicio, new List<AvisoPedidoModel>(), puedePedirAlmacen: false);
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            List<ParametrosDialogo> preguntas = DialogoContesta(dialogos, ResultadoBoton.Cancel);
            DetallePedidoViewModel vm = Vm(servicio, dialogos);

            await vm.ModificarPedidoAsync();

            ParametrosDialogo pregunta = preguntas.Single();
            Assert.AreEqual(AmpliacionPedidoEnPreparacion.TEXTO_PEDIDO_NUEVO, pregunta.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_SI));
            Assert.IsFalse(pregunta.ContainsKey(ConfirmationDialogViewModel.PARAMETRO_TEXTO_TERCERO), "Sin «Pedir a Almacén…»");
        }
    }
}
