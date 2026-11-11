using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;

namespace PedidoVentaTests
{
    /// <summary>
    /// Sugerencia 1184 (Paloma): casilla «Mostrar Sólo Retenidos» en la lista de pedidos y etiqueta «Retenido» en la tarjeta.
    /// El dato (estaRetenido) lo manda la API en la lista; al cargar o guardar el pedido se recalcula con su retención.
    /// </summary>
    [TestClass]
    public class ListaPedidosRetenidosTests
    {
        private IPedidoVentaService servicio;
        private ListaPedidosVentaViewModel vm;

        [TestInitialize]
        public void Preparar()
        {
            servicio = A.Fake<IPedidoVentaService>();
            vm = new ListaPedidosVentaViewModel(A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IServicioNavegacion>());
        }

        private void CargarLista(params ResumenPedido[] pedidos)
        {
            A.CallTo(() => servicio.cargarListaPedidos(A<string>._, A<bool>._, A<bool>._)).Returns(new ObservableCollection<ResumenPedido>(pedidos));
            vm.cmdCargarListaPedidos.Execute(null);
        }

        [TestMethod]
        public void MostrarSoloRetenidos_DejaSoloLosRetenidosYAlQuitarloVuelvenTodos()
        {
            CargarLista(
                new ResumenPedido { empresa = "1", numero = 1 },
                new ResumenPedido { empresa = "1", numero = 2, estaRetenido = true },
                new ResumenPedido { empresa = "1", numero = 3 });
            Assert.AreEqual(3, vm.ListaPedidos.Lista.Count);

            vm.mostrarSoloRetenidos = true;

            CollectionAssert.AreEqual(new[] { 2 }, vm.ListaPedidos.Lista.Cast<ResumenPedido>().Select(p => p.numero).ToArray());

            vm.mostrarSoloRetenidos = false;

            Assert.AreEqual(3, vm.ListaPedidos.Lista.Count);
        }

        [TestMethod]
        public void MostrarPresupuestos_QuitaLaCasillaDeRetenidos()
        {
            CargarLista(new ResumenPedido { empresa = "1", numero = 2, estaRetenido = true });
            vm.mostrarSoloRetenidos = true;

            vm.mostrarPresupuestos = true;

            Assert.IsFalse(vm.mostrarSoloRetenidos);
        }

        [TestMethod]
        public void LaListaDeLaApi_TraeEstaRetenido()
        {
            var lista = JsonConvert.DeserializeObject<ObservableCollection<ResumenPedido>>("[{\"empresa\":\"1\",\"numero\":7,\"estaRetenido\":true},{\"empresa\":\"1\",\"numero\":8}]");

            Assert.IsTrue(lista[0].estaRetenido);
            Assert.IsFalse(lista[1].estaRetenido);
        }

        [DataTestMethod]
        [DataRow(true)]
        [DataRow(false)]
        public async Task AlCargarElPedido_LaEtiquetaSigueSuRetencion(bool retenido)
        {
            var resumen = new ResumenPedido { empresa = "1", numero = 929980, estaRetenido = !retenido };
            vm.ListaPedidos.ListaOriginal = new ObservableCollection<IFiltrableItem> { resumen };
            vm.ListaPedidos.ElementoSeleccionado = resumen;
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 929980, fecha = DateTime.Today, tieneFechasFuturas = false };
            if (retenido)
            {
                pedido.retencion = new RetencionPedidoDTO { Empresa = "1", Pedido = 929980, Motivo = "Pendiente de pago" };
            }

            await vm.ActualizarResumenAsync(pedido, esCarga: true);

            Assert.AreEqual(retenido, resumen.estaRetenido);
        }
    }
}
