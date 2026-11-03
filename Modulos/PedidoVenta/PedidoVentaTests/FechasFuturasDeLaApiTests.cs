using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#588: la API manda «tieneFechasFuturas» en el pedido (GET, crear y modificar) con la regla de la lista de pedidos
    /// (ruta interna con su día de reparto, NestoAPI#764). Nesto lo lee de ahí y ya no pide la fecha mínima a la API
    /// (FechaAjustada) para el icono. Si la API no lo manda (versión vieja), lo de antes.
    /// </summary>
    [TestClass]
    public class FechasFuturasDeLaApiTests
    {
        private IMessenger messenger;
        private IPedidoVentaService servicio;
        private ListaPedidosVentaViewModel vm;

        [TestInitialize]
        public void Preparar()
        {
            messenger = new WeakReferenceMessenger();
            servicio = A.Fake<IPedidoVentaService>();
            vm = new ListaPedidosVentaViewModel(A.Fake<IConfiguracion>(), servicio, messenger, A.Fake<IServicioDialogos>(), A.Fake<IServicioNavegacion>());
        }

        private static PedidoVentaDTO Pedido(int numero, bool? tieneFechasFuturas)
        {
            var pedido = new PedidoVentaDTO { empresa = "1", numero = numero, ruta = "AT", fecha = DateTime.Today, tieneFechasFuturas = tieneFechasFuturas };
            pedido.Lineas.Add(new LineaPedidoVentaDTO { estado = -1, fechaEntrega = DateTime.Today.AddDays(30) });
            return pedido;
        }

        private ResumenPedido Seleccionar(int numero, bool tieneFechasFuturas)
        {
            var resumen = new ResumenPedido { empresa = "1", numero = numero, codPostal = "28001", tieneFechasFuturas = tieneFechasFuturas };
            vm.ListaPedidos.ListaOriginal = new ObservableCollection<IFiltrableItem> { resumen };
            vm.ListaPedidos.ElementoSeleccionado = resumen;
            return resumen;
        }

        private void NoSePideLaFechaMinima()
        {
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task PedidoModificado_ConElDatoDeLaApi_LoUsaSinPedirLaFechaMinima(bool deLaApi)
        {
            // La línea es de dentro de 30 días: con el cálculo de Nesto saldría «futuro»; manda la API
            ResumenPedido resumen = Seleccionar(929990, !deLaApi);

            await vm.ActualizarResumenAsync(Pedido(929990, deLaApi), esCarga: false);

            Assert.AreEqual(deLaApi, resumen.tieneFechasFuturas);
            NoSePideLaFechaMinima();
        }

        [TestMethod]
        public async Task PedidoCargado_ConElDatoDeLaApi_LoUsa()
        {
            ResumenPedido resumen = Seleccionar(929991, tieneFechasFuturas: true);

            await vm.ActualizarResumenAsync(Pedido(929991, false), esCarga: true);

            Assert.IsFalse(resumen.tieneFechasFuturas, "el del GET del pedido, más reciente que el de la lista");
            NoSePideLaFechaMinima();
        }

        [TestMethod]
        public async Task PedidoModificado_ApiVieja_ComoAntes()
        {
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).Returns(DateTime.Today);
            ResumenPedido resumen = Seleccionar(929992, tieneFechasFuturas: false);

            await vm.ActualizarResumenAsync(Pedido(929992, null), esCarga: false);

            Assert.IsTrue(resumen.tieneFechasFuturas);
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public void PedidoCreado_ConElDatoDeLaApi_LoUsaSinPedirLaFechaMinima(bool deLaApi)
        {
            var resumen = new ResumenPedido { empresa = "1", numero = 0, esNuevo = true, tieneFechasFuturas = !deLaApi };
            vm.ListaPedidos.ElementoSeleccionado = resumen;

            messenger.Send(new PedidoCreadoMensaje(new PedidoCreadoEventArgs { Pedido = Pedido(929993, deLaApi), CodigoPostal = "28001" }));

            Assert.AreEqual(929993, resumen.numero);
            Assert.AreEqual(deLaApi, resumen.tieneFechasFuturas);
            NoSePideLaFechaMinima();
        }

        [TestMethod]
        public void PedidoCreado_ApiVieja_ComoAntes()
        {
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult(DateTime.Today));
            var resumen = new ResumenPedido { empresa = "1", numero = 0, esNuevo = true };
            vm.ListaPedidos.ElementoSeleccionado = resumen;

            messenger.Send(new PedidoCreadoMensaje(new PedidoCreadoEventArgs { Pedido = Pedido(929994, null), CodigoPostal = "28001" }));

            Assert.IsTrue(resumen.tieneFechasFuturas);
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, "AT", A<string>._, "28001")).MustHaveHappenedOnceExactly();
        }

        [DataTestMethod]
        [DataRow("{\"numero\":929995,\"tieneFechasFuturas\":true}", true)]
        [DataRow("{\"numero\":929995,\"tieneFechasFuturas\":false}", false)]
        [DataRow("{\"numero\":929995,\"tieneFechasFuturas\":null}", null)]
        [DataRow("{\"numero\":929995}", null)]
        [DataRow("", null)]
        [DataRow("esto no es JSON", null)]
        [DataRow("[1,2]", null)]
        public void LeerTieneFechasFuturas_DelCuerpoDelPost(string cuerpo, bool? esperado)
        {
            Assert.AreEqual(esperado, PedidoVentaService.LeerTieneFechasFuturas(cuerpo));
        }

        [TestMethod]
        public void PedidoVentaDTO_SinElDato_NoLoEnvia()
        {
            // Solo de salida: Nothing no viaja en el PUT/POST
            Assert.IsFalse(JsonConvert.SerializeObject(new PedidoVentaDTO { tieneFechasFuturas = null }).Contains("tieneFechasFuturas"));
            Assert.IsTrue(JsonConvert.DeserializeObject<PedidoVentaDTO>("{\"tieneFechasFuturas\":true}").tieneFechasFuturas == true);
            Assert.IsNull(JsonConvert.DeserializeObject<PedidoVentaDTO>("{}").tieneFechasFuturas);
        }

        [TestMethod]
        public void RespuestaDelPut_TraeElDato()
        {
            Assert.AreEqual(false, JsonConvert.DeserializeObject<RespuestaModificacionPedidoModel>("{\"Avisos\":[],\"tieneFechasFuturas\":false}").tieneFechasFuturas);
            Assert.IsNull(JsonConvert.DeserializeObject<RespuestaModificacionPedidoModel>("{\"Avisos\":[]}").tieneFechasFuturas);
        }
    }
}
