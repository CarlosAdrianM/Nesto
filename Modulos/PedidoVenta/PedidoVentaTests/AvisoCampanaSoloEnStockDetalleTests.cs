using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#889 (Black Friday 2026, Carlos 24/11/26): antes de guardar, si la API dice que algo de una campaña «solo en stock»
    /// no tiene stock, se pregunta. Sí (por defecto) → CampanaSoloEnStock = QuitarDescuento; No → MantenerDescuento; sin aviso o si
    /// la API falla → Nothing y sin preguntar. Al modificar, las dos preguntas (contado y stock) solo si el guardado cambia algo que importa.
    /// </summary>
    [TestClass]
    public class AvisoCampanaSoloEnStockDetalleTests
    {
        private const string MENSAJE = "Del 45929 (CREMA) hay 2 en stock y pide 5: 3 irían sin el descuento de Black Friday.";

        private static PedidoVentaDTO Pedido(int numero) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = numero,
            cliente = "6316",
            contacto = "0",
            formaPago = "RCB",
            plazosPago = "1/30",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "45929", Cantidad = 5, almacen = "ALG", tipoLinea = 1, fechaEntrega = DateTime.Today }
            }
        };

        private static IServicioDialogos DialogoQueResponde(bool acepta)
        {
            IServicioDialogos dialogService = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogService.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(acepta));
            return dialogService;
        }

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogService, int numero)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogService, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(Pedido(numero));
            vm.MarcarComoGuardado();
            return vm;
        }

        /// <summary>Servicio que avisa de stock (y del contado) y apunta lo que viaja en CampanaSoloEnStock al crear y al modificar.</summary>
        private static IPedidoVentaService Servicio(List<string> enviados, bool avisaStock = true)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ConsultarAvisoCampanaSoloEnStock(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new AvisoCampanaSoloEnStockRespuesta
            {
                HayAviso = avisaStock,
                Campana = "BLACK-FRIDAY-2026",
                Mensaje = avisaStock ? MENSAJE : null
            }));
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._))
                .Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta { Avisar = false }));
            A.CallTo(() => servicio.CrearPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                enviados.Add(p.CampanaSoloEnStock);
                return Task.FromResult(930001);
            });
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                enviados.Add(p.CampanaSoloEnStock);
                return Task.FromResult(new List<AvisoPedidoModel>());
            });
            return servicio;
        }

        private static readonly string TEXTO = AvisoCampanaSoloEnStock.TextoPregunta(MENSAJE);

        [TestMethod]
        public async Task Crear_ConAvisoYDiceQueSi_QuitaElDescuento()
        {
            var enviados = new List<string>();
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(Servicio(enviados), dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCampanaSoloEnStock.TITULO, TEXTO)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { "QuitarDescuento" }, enviados);
        }

        [TestMethod]
        public async Task Crear_ConAvisoYDiceQueNo_MantieneElDescuento()
        {
            var enviados = new List<string>();
            IServicioDialogos dialogos = DialogoQueResponde(false);
            DetallePedidoViewModel vm = Vm(Servicio(enviados), dialogos, 0);

            await vm.ModificarPedidoAsync();

            CollectionAssert.AreEqual(new[] { "MantenerDescuento" }, enviados);
        }

        [TestMethod]
        public async Task Crear_SinAviso_NoPreguntaYNoViaja()
        {
            var enviados = new List<string>();
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(Servicio(enviados, avisaStock: false), dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCampanaSoloEnStock.TITULO, A<string>._)).MustNotHaveHappened();
            CollectionAssert.AreEqual(new string[] { null }, enviados);
        }

        [TestMethod]
        public async Task Crear_LaApiFalla_SeCreaSinPreguntar()
        {
            var enviados = new List<string>();
            IPedidoVentaService servicio = Servicio(enviados);
            A.CallTo(() => servicio.ConsultarAvisoCampanaSoloEnStock(A<PedidoVentaDTO>._)).ThrowsAsync(new InvalidOperationException("404"));
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCampanaSoloEnStock.TITULO, A<string>._)).MustNotHaveHappened();
            CollectionAssert.AreEqual(new string[] { null }, enviados);
            Assert.AreEqual(930001, vm.pedido.numero);
        }

        [TestMethod]
        public async Task Crear_LaApiDevuelveNothing_SeCreaSinPreguntar()
        {
            var enviados = new List<string>();
            IPedidoVentaService servicio = Servicio(enviados);
            A.CallTo(() => servicio.ConsultarAvisoCampanaSoloEnStock(A<PedidoVentaDTO>._)).Returns(Task.FromResult<AvisoCampanaSoloEnStockRespuesta>(null));
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCampanaSoloEnStock.TITULO, A<string>._)).MustNotHaveHappened();
            CollectionAssert.AreEqual(new string[] { null }, enviados);
        }

        [TestMethod]
        public async Task Crear_ConContadoYStock_PrimeroElContado()
        {
            var enviados = new List<string>();
            IPedidoVentaService servicio = Servicio(enviados);
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta
            {
                Avisar = true, Mensaje = "¿Cambiar a contado?", PlazosPagoPropuestos = "CONTADO"
            }));
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 0);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCondicionesCampana.TITULO, A<string>._)).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => dialogos.ShowConfirmationAsync(AvisoCampanaSoloEnStock.TITULO, A<string>._)).MustHaveHappenedOnceExactly());
        }

        [TestMethod]
        public async Task Modificar_SinCambiosQueImporten_NoConsultaNiPregunta()
        {
            var enviados = new List<string>();
            IPedidoVentaService servicio = Servicio(enviados);
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 929057);
            vm.pedido.Model.comentarios = "Otro comentario";

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.ConsultarAvisoCampanaSoloEnStock(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            CollectionAssert.AreEqual(new string[] { null }, enviados);
        }

        [TestMethod]
        public async Task Modificar_SubeLaCantidad_PreguntaStockPeroNoContado()
        {
            var enviados = new List<string>();
            IPedidoVentaService servicio = Servicio(enviados);
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 929057);
            System.Linq.Enumerable.First(vm.pedido.Model.Lineas).Cantidad = 8;

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.ConsultarAvisoCampanaSoloEnStock(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            CollectionAssert.AreEqual(new[] { "QuitarDescuento" }, enviados);
            Assert.IsNull(vm.pedido.Model.CampanaSoloEnStock, "Tras guardar bien se olvida para el siguiente guardado");
        }

        [TestMethod]
        public async Task Modificar_AnadeLinea_PreguntaLasDos()
        {
            var enviados = new List<string>();
            IPedidoVentaService servicio = Servicio(enviados);
            IServicioDialogos dialogos = DialogoQueResponde(false);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 929057);
            vm.pedido.Model.Lineas.Add(new LineaPedidoVentaDTO { id = 0, Producto = "45930", Cantidad = 2, almacen = "ALG", tipoLinea = 1, fechaEntrega = DateTime.Today });

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.ConsultarAvisoCampanaSoloEnStock(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { "MantenerDescuento" }, enviados);
        }

        [TestMethod]
        public async Task Modificar_CambiaPlazos_PreguntaContadoPeroNoStock()
        {
            var enviados = new List<string>();
            IPedidoVentaService servicio = Servicio(enviados);
            IServicioDialogos dialogos = DialogoQueResponde(true);
            DetallePedidoViewModel vm = Vm(servicio, dialogos, 929057);
            vm.pedido.Model.plazosPago = "2/60";

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.ConsultarAvisoCampanaSoloEnStock(A<PedidoVentaDTO>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void MostrarAvisos_EnsenaLoAplicado()
        {
            var vistos = new List<string>();
            AvisoPedidoModel.MostrarCursosVendidosAMano(new List<AvisoPedidoModel>
            {
                new AvisoPedidoModel { Tipo = AvisoPedidoModel.TIPO_CAMPANA_SOLO_EN_STOCK_APLICADO, Mensaje = "Se ha partido una línea." }
            }, (titulo, mensaje) => vistos.Add(titulo + "|" + mensaje));

            CollectionAssert.AreEqual(new[] { "Oferta solo para lo que hay en stock|Se ha partido una línea." }, vistos);
        }

        [TestMethod]
        public void DelAlta_LeeLosAvisosGrabacionDelPedido()
        {
            var pedido = new PedidoVentaDTO
            {
                AvisosGrabacion = Newtonsoft.Json.Linq.JArray.Parse("[{\"Tipo\":\"CampanaSoloEnStockAplicado\",\"Mensaje\":\"Hecho\"}]")
            };

            List<AvisoPedidoModel> avisos = AvisoPedidoModel.DelAlta(pedido);

            Assert.AreEqual(1, avisos.Count);
            Assert.AreEqual("Hecho", avisos[0].Mensaje);
            Assert.AreEqual(0, AvisoPedidoModel.DelAlta(new PedidoVentaDTO()).Count);
        }
    }

    /// <summary>Black Friday 2026: la regla pura de «¿hay que preguntar al modificar?».</summary>
    [TestClass]
    public class ReglaPreguntasCampanaAlModificarTests
    {
        private static PedidoVentaDTO P(string plazos, params (int id, string producto, int cantidad)[] lineas)
        {
            var p = new PedidoVentaDTO { formaPago = "RCB", plazosPago = plazos, Lineas = new List<LineaPedidoVentaDTO>() };
            foreach (var l in lineas)
                p.Lineas.Add(new LineaPedidoVentaDTO { id = l.id, Producto = l.producto, Cantidad = l.cantidad, tipoLinea = 1 });
            return p;
        }

        [TestMethod]
        public void SinCambios_NoPregunta()
        {
            var original = P("1/30", (1, "45929", 5));
            var actual = P("1/30 ", (1, "45929", 5));
            Assert.IsFalse(ReglaPreguntasCampanaAlModificar.HayQuePreguntarContado(original, actual));
            Assert.IsFalse(ReglaPreguntasCampanaAlModificar.HayQuePreguntarStock(original, actual));
        }

        [TestMethod]
        public void BajaCantidadOQuitaLinea_NoPregunta()
        {
            var original = P("1/30", (1, "45929", 5), (2, "45930", 1));
            var actual = P("1/30", (1, "45929", 3));
            Assert.IsFalse(ReglaPreguntasCampanaAlModificar.HayQuePreguntarStock(original, actual));
            Assert.IsFalse(ReglaPreguntasCampanaAlModificar.HayQuePreguntarContado(original, actual));
        }

        [TestMethod]
        public void LineaNueva_PreguntaLasDos()
        {
            var original = P("1/30", (1, "45929", 5));
            var actual = P("1/30", (1, "45929", 5), (0, "45930", 1));
            Assert.IsTrue(ReglaPreguntasCampanaAlModificar.HayQuePreguntarContado(original, actual));
            Assert.IsTrue(ReglaPreguntasCampanaAlModificar.HayQuePreguntarStock(original, actual));
        }

        [TestMethod]
        public void LineaDeTextoNueva_NoCuenta()
        {
            var original = P("1/30", (1, "45929", 5));
            var actual = P("1/30", (1, "45929", 5));
            actual.Lineas.Add(new LineaPedidoVentaDTO { id = 0, Producto = "", tipoLinea = 0 });
            Assert.IsFalse(ReglaPreguntasCampanaAlModificar.HayQuePreguntarStock(original, actual));
        }

        [TestMethod]
        public void SubeCantidad_SoloStock()
        {
            var original = P("1/30", (1, "45929", 5));
            var actual = P("1/30", (1, "45929", 6));
            Assert.IsTrue(ReglaPreguntasCampanaAlModificar.HayQuePreguntarStock(original, actual));
            Assert.IsFalse(ReglaPreguntasCampanaAlModificar.HayQuePreguntarContado(original, actual));
        }

        [TestMethod]
        public void CambiaPlazosOFormaDePago_SoloContado()
        {
            var original = P("1/30", (1, "45929", 5));
            var plazos = P("CONTADO", (1, "45929", 5));
            var forma = P("1/30", (1, "45929", 5));
            forma.formaPago = "EFC";
            Assert.IsTrue(ReglaPreguntasCampanaAlModificar.HayQuePreguntarContado(original, plazos));
            Assert.IsTrue(ReglaPreguntasCampanaAlModificar.HayQuePreguntarContado(original, forma));
            Assert.IsFalse(ReglaPreguntasCampanaAlModificar.HayQuePreguntarStock(original, plazos));
        }

        [TestMethod]
        public void SinOriginal_Pregunta()
        {
            var actual = P("1/30", (1, "45929", 5));
            Assert.IsTrue(ReglaPreguntasCampanaAlModificar.HayQuePreguntarContado(null, actual));
            Assert.IsTrue(ReglaPreguntasCampanaAlModificar.HayQuePreguntarStock(null, actual));
        }

        [TestMethod]
        public async Task PreguntarYAplicar_YaElegido_NoConsultaOtraVez()
        {
            var pedido = new PedidoVentaDTO { CampanaSoloEnStock = "MantenerDescuento" };
            bool consultado = false;
            bool preguntado = await AvisoCampanaSoloEnStock.PreguntarYAplicar(pedido,
                () => { consultado = true; return Task.FromResult(new AvisoCampanaSoloEnStockRespuesta { HayAviso = true, Mensaje = "x" }); },
                (t, m) => Task.FromResult(true));
            Assert.IsFalse(preguntado);
            Assert.IsFalse(consultado);
            Assert.AreEqual("MantenerDescuento", pedido.CampanaSoloEnStock);
        }

        [TestMethod]
        public void CampanaSoloEnStock_SoloViajaConValor()
        {
            string sin = Newtonsoft.Json.JsonConvert.SerializeObject(new PedidoVentaDTO());
            string con = Newtonsoft.Json.JsonConvert.SerializeObject(new PedidoVentaDTO { CampanaSoloEnStock = "QuitarDescuento" });
            StringAssert.DoesNotMatch(sin, new System.Text.RegularExpressions.Regex("CampanaSoloEnStock"));
            StringAssert.Contains(con, "\"CampanaSoloEnStock\":\"QuitarDescuento\"");
            StringAssert.DoesNotMatch(con, new System.Text.RegularExpressions.Regex("AvisosGrabacion"));
        }
    }
}
