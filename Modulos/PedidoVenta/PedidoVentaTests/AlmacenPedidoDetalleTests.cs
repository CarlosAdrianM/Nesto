using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#510 (sugerencia 508, pedido 927808): el almacén del pedido entero se cambia en cualquier serie si no hay nada
    /// que lo impida, avisando si hay una reposición viajando hacia el almacén actual de las líneas.
    /// </summary>
    [TestClass]
    public class AlmacenPedidoDetalleTests
    {
        private static LineaPedidoVentaDTO Linea(int id, string producto, short estado = -1, int picking = 0) => new LineaPedidoVentaDTO
        {
            id = id, Producto = producto, Cantidad = 1, almacen = "ALC", tipoLinea = 1, estado = estado, picking = picking
        };

        private static PedidoVentaDTO Pedido(string serie, params LineaPedidoVentaDTO[] lineas) => new PedidoVentaDTO
        {
            empresa = "1", numero = 927808, cliente = "15191", contacto = "0", serie = serie, Lineas = lineas.ToList()
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogos, PedidoVentaDTO pedido)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
            vm.pedido = new PedidoVentaWrapper(pedido);
            return vm;
        }

        // ---------- Cuándo se habilita el selector ----------

        [TestMethod]
        public void PuedeEditarSelectorAlmacen_SerieNVTodoPendienteSinPicking_SeHabilita()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(),
                Pedido("NV", Linea(1, "45146"), Linea(2, "45148", estado: 1)));

            Assert.IsTrue(vm.PuedeEditarSelectorAlmacen);
            Assert.IsFalse(vm.PuedeEditarSelectoresLinea, "La forma de venta sigue siendo solo de cursos");
        }

        [TestMethod]
        public void PuedeEditarSelectorAlmacen_PresupuestoEnCualquierSerie_SeHabilita()
        {
            Assert.IsTrue(DetallePedidoViewModel.PuedeCambiarseAlmacen(new[] { Linea(1, "45146", estado: -3) }, false));
        }

        [TestMethod]
        public void PuedeEditarSelectorAlmacen_ConPicking_NoSeHabilita()
        {
            var vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(),
                Pedido("NV", Linea(1, "45146"), Linea(2, "45148", estado: 1, picking: 12345)));

            Assert.IsFalse(vm.PuedeEditarSelectorAlmacen);
        }

        [TestMethod]
        public void PuedeEditarSelectorAlmacen_AlbaranFacturaONotaDeEntrega_NoSeHabilita()
        {
            Assert.IsFalse(DetallePedidoViewModel.PuedeCambiarseAlmacen(new[] { Linea(1, "45146"), Linea(2, "45148", estado: 2) }, true));
            Assert.IsFalse(DetallePedidoViewModel.PuedeCambiarseAlmacen(new[] { Linea(1, "45146", estado: 4) }, false));
            Assert.IsFalse(DetallePedidoViewModel.PuedeCambiarseAlmacen(new[] { Linea(1, "45146", estado: -2) }, false));
            var conFactura = Linea(1, "45146");
            conFactura.Factura = "NV26/001234";
            Assert.IsFalse(DetallePedidoViewModel.PuedeCambiarseAlmacen(new[] { conFactura }, false));
            var conAlbaran = Linea(1, "45146");
            conAlbaran.Albaran = 777;
            Assert.IsFalse(DetallePedidoViewModel.PuedeCambiarseAlmacen(new[] { conAlbaran }, false));
        }

        [TestMethod]
        public void PuedeEditarSelectorAlmacen_SinLineasDeProducto_SoloEnCursos()
        {
            Assert.IsFalse(DetallePedidoViewModel.PuedeCambiarseAlmacen(new List<LineaPedidoVentaDTO>(), false));
            Assert.IsTrue(DetallePedidoViewModel.PuedeCambiarseAlmacen(new List<LineaPedidoVentaDTO>(), true));
        }

        // ---------- El aviso de la reposición en tránsito ----------

        private static List<ProductoEnTransito> EnTransito() => new List<ProductoEnTransito>
        {
            new ProductoEnTransito { Producto = "45146", Unidades = 4, Traspaso = 80885, Origen = "ALG" }
        };

        [TestMethod]
        public async Task CambiarAlmacen_ConReposicionEnTransito_AvisaYSiAceptaLoCambia()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerEnTransito("1", "ALC", A<IEnumerable<string>>._)).Returns(Task.FromResult(EnTransito()));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            string mensaje = null;
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._))
                .Invokes((string t, string m) => mensaje = m).Returns(Task.FromResult(true));
            var vm = Vm(servicio, dialogos, Pedido("NV", Linea(1, "45146"), Linea(2, "45148")));
            Assert.AreEqual("ALC", vm.AlmacenSeleccionadoParaLineas);

            vm.AlmacenSeleccionadoParaLineas = "ALG";
            await vm.CambioAlmacenEnCurso;

            StringAssert.Contains(mensaje, "Hay 4 unidades de 45146 en la reposición 80885 que viajan a Alcobendas, puede que para este pedido.");
            StringAssert.Contains(mensaje, "¿Cambiar el almacén de todas formas?");
            A.CallTo(() => servicio.LeerEnTransito("1", "ALC", A<IEnumerable<string>>.That.Matches(p => p.OrderBy(x => x).SequenceEqual(new[] { "45146", "45148" }))))
                .MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.pedido.Model.Lineas.All(l => l.almacen == "ALG"));
            Assert.AreEqual("ALG", vm.AlmacenSeleccionadoParaLineas);
        }

        [TestMethod]
        public async Task CambiarAlmacen_ConReposicionEnTransito_SiCancelaRestauraElSelectorYNoTocaLasLineas()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerEnTransito("1", "ALC", A<IEnumerable<string>>._)).Returns(Task.FromResult(EnTransito()));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
            var vm = Vm(servicio, dialogos, Pedido("NV", Linea(1, "45146"), Linea(2, "45148")));

            vm.AlmacenSeleccionadoParaLineas = "ALG";
            await vm.CambioAlmacenEnCurso;

            Assert.AreEqual("ALC", vm.AlmacenSeleccionadoParaLineas);
            Assert.IsTrue(vm.pedido.Model.Lineas.All(l => l.almacen == "ALC"));
        }

        [TestMethod]
        public async Task CambiarAlmacen_SinNadaEnTransito_NoPreguntaYLoCambia()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerEnTransito(A<string>._, A<string>._, A<IEnumerable<string>>._))
                .Returns(Task.FromResult(new List<ProductoEnTransito>()));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = Vm(servicio, dialogos, Pedido("NV", Linea(1, "45146")));

            vm.AlmacenSeleccionadoParaLineas = "ALG";
            await vm.CambioAlmacenEnCurso;

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual("ALG", vm.pedido.Model.Lineas.Single().almacen);
        }

        [TestMethod]
        public async Task CambiarAlmacen_SiFallaLaConsulta_LoCambiaIgualPorqueEsUnAviso()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerEnTransito(A<string>._, A<string>._, A<IEnumerable<string>>._))
                .ThrowsAsync(new System.Exception("sin conexión"));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = Vm(servicio, dialogos, Pedido("NV", Linea(1, "45146")));

            vm.AlmacenSeleccionadoParaLineas = "ALG";
            await vm.CambioAlmacenEnCurso;

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual("ALG", vm.pedido.Model.Lineas.Single().almacen);
        }

        [TestMethod]
        public void TextoAvisoEnTransito_ReposicionEnPreparacionYUnaUnidad()
        {
            string texto = DetallePedidoViewModel.TextoAvisoEnTransito(new[]
            {
                new ProductoEnTransito { Producto = "45148", Unidades = 1, Traspaso = null, Origen = "REI" }
            }, "ALG");

            StringAssert.Contains(texto, "Hay 1 unidad de 45148 en una reposición en preparación en REI que viaja a Algete");
        }
    }
}
