using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#588 (NestoAPI#764): «fechas futuras» en la lista de pedidos se calcula con la fecha mínima de la API (con el código
    /// postal de entrega: los pedidos de ruta interna con día de reparto van al próximo día de su ruta), no con la copia vieja
    /// del cálculo (hoy o mañana según la hora). Si la API falla, el cálculo de siempre.
    /// </summary>
    [TestClass]
    public class ListaPedidosFechasFuturasRutaInternaTests
    {
        private static readonly DateTime MIERCOLES = new DateTime(2026, 11, 4);

        private static LineaPedidoVentaDTO Linea(DateTime fecha, short estado = 1) => new LineaPedidoVentaDTO { estado = estado, fechaEntrega = fecha };

        private static (ListaPedidosVentaViewModel vm, IPedidoVentaService servicio) Crear()
        {
            var servicio = A.Fake<IPedidoVentaService>();
            var vm = new ListaPedidosVentaViewModel(A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(),
                A.Fake<IServicioNavegacion>());
            return (vm, servicio);
        }

        [TestMethod]
        public void TieneFechasFuturas_SoloLineasVivasPosterioresALaFechaMinima()
        {
            Assert.IsFalse(ListaPedidosVentaViewModel.TieneFechasFuturas(new List<LineaPedidoVentaDTO> { Linea(MIERCOLES) }, MIERCOLES));
            Assert.IsTrue(ListaPedidosVentaViewModel.TieneFechasFuturas(new List<LineaPedidoVentaDTO> { Linea(MIERCOLES.AddDays(1)) }, MIERCOLES));
            Assert.IsFalse(ListaPedidosVentaViewModel.TieneFechasFuturas(new List<LineaPedidoVentaDTO> { Linea(MIERCOLES.AddDays(7), estado: 2) }, MIERCOLES),
                "Una línea en albarán no cuenta");
            Assert.IsFalse(ListaPedidosVentaViewModel.TieneFechasFuturas(null, MIERCOLES));
        }

        [TestMethod]
        public async Task FechaEntregaMinima_LaPideALaApiConElCodigoPostal()
        {
            var (vm, servicio) = Crear();
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, "AT", A<string>._, "28001")).Returns(MIERCOLES.AddDays(7));

            Assert.AreEqual(MIERCOLES.AddDays(7), await vm.FechaEntregaMinima("AT", "28001"));
        }

        [TestMethod]
        public async Task FechaEntregaMinima_SiLaApiFalla_ElCalculoDeSiempre()
        {
            var (vm, servicio) = Crear();
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).Throws(new Exception("sin API"));

            DateTime fecha = await vm.FechaEntregaMinima("FW", "28001");

            Assert.IsTrue(fecha == DateTime.Today || fecha == DateTime.Today.AddDays(1), fecha.ToString());
        }

        [TestMethod]
        public async Task ActualizarResumen_PedidoDeRutaInternaQueSaleSuDia_NoTieneFechasFuturas()
        {
            // La línea va al miércoles de la semana que viene (su día de reparto); la API dice que esa es la fecha mínima
            var (vm, servicio) = Crear();
            DateTime suDia = DateTime.Today.AddDays(6);
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, "AT", A<string>._, "28001")).Returns(suDia);
            var resumen = new ResumenPedido { empresa = "1", numero = 929999, codPostal = "28001", tieneFechasFuturas = true };
            vm.ListaPedidos.ElementoSeleccionado = resumen;
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 929999, ruta = "AT", fecha = DateTime.Today };
            pedido.Lineas.Add(Linea(suDia));

            await vm.ActualizarResumenAsync(pedido);

            Assert.IsFalse(resumen.tieneFechasFuturas, "Con la copia vieja del cálculo salía «con fechas futuras»");
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, "AT", A<string>._, "28001")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ActualizarResumen_LineaPosteriorASuDia_SiTieneFechasFuturas()
        {
            var (vm, servicio) = Crear();
            DateTime suDia = DateTime.Today.AddDays(6);
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).Returns(suDia);
            var resumen = new ResumenPedido { empresa = "1", numero = 929998, codPostal = "28001" };
            vm.ListaPedidos.ElementoSeleccionado = resumen;
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 929998, ruta = "AT", fecha = DateTime.Today };
            pedido.Lineas.Add(Linea(suDia.AddDays(7)));

            await vm.ActualizarResumenAsync(pedido);

            Assert.IsTrue(resumen.tieneFechasFuturas);
        }
    

        // Nesto#588 (revisión 30/10): al elegir un pedido de la lista, «fechas futuras» ya viene de la API en la lista; no se pide
        // la fecha mínima por cada pedido que se selecciona (el icono salía con retraso). Solo si el pedido cambia.
        private static (ResumenPedido resumen, PedidoVentaDTO pedido) SeleccionarDeLaLista(ListaPedidosVentaViewModel vm, bool tieneFechasFuturas)
        {
            var resumen = new ResumenPedido { empresa = "1", numero = 929997, codPostal = "28001", tieneFechasFuturas = tieneFechasFuturas };
            vm.ListaPedidos.ListaOriginal = new ObservableCollection<IFiltrableItem> { resumen };
            vm.ListaPedidos.ElementoSeleccionado = resumen;
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 929997, ruta = "AT", fecha = DateTime.Today };
            pedido.Lineas.Add(Linea(DateTime.Today.AddDays(30)));
            return (resumen, pedido);
        }

        [TestMethod]
        public async Task ActualizarResumen_CargaDelPedidoElegidoEnLaLista_UsaElDatoDeLaApiSinLlamarPorPedido()
        {
            var (vm, servicio) = Crear();
            var (resumen, pedido) = SeleccionarDeLaLista(vm, tieneFechasFuturas: false);

            await vm.ActualizarResumenAsync(pedido, esCarga: true);

            Assert.IsFalse(resumen.tieneFechasFuturas, "Se queda con lo que mandó la API en la lista");
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ActualizarResumen_PedidoModificado_PideLaFechaMinima()
        {
            var (vm, servicio) = Crear();
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).Returns(DateTime.Today);
            var (resumen, pedido) = SeleccionarDeLaLista(vm, tieneFechasFuturas: false);

            await vm.ActualizarResumenAsync(pedido, esCarga: false);

            Assert.IsTrue(resumen.tieneFechasFuturas);
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ActualizarResumen_SegundaCargaDelMismoPedido_PideLaFechaMinima()
        {
            // Tras crear el albarán, facturar… el detalle recarga el pedido: el dato de la lista puede haberse quedado viejo
            var (vm, servicio) = Crear();
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).Returns(DateTime.Today);
            var (resumen, pedido) = SeleccionarDeLaLista(vm, tieneFechasFuturas: false);
            await vm.ActualizarResumenAsync(pedido, esCarga: true);

            await vm.ActualizarResumenAsync(pedido, esCarga: true);

            Assert.IsTrue(resumen.tieneFechasFuturas);
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ActualizarResumen_CargaDeUnPedidoQueNoVieneDeLaLista_PideLaFechaMinima()
        {
            // Número escrito en el filtro que no está en la lista: el resumen no trae «fechas futuras» de la API
            var (vm, servicio) = Crear();
            A.CallTo(() => servicio.CalcularFechaEntregaAjustada(A<DateTime>._, A<string>._, A<string>._, A<string>._)).Returns(DateTime.Today);
            var resumen = new ResumenPedido { empresa = "1", numero = 929996, codPostal = "28001" };
            vm.ListaPedidos.ElementoSeleccionado = resumen;
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 929996, ruta = "AT", fecha = DateTime.Today };
            pedido.Lineas.Add(Linea(DateTime.Today.AddDays(30)));

            await vm.ActualizarResumenAsync(pedido, esCarga: true);

            Assert.IsTrue(resumen.tieneFechasFuturas);
        }
    }
}
