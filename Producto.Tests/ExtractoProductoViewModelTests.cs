using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Producto.Tests
{
    /// <summary>
    /// Sugerencia 1482 (Manuel, Compras; OK de Carlos 26/11/26): la referencia del proveedor en la ficha y la ventana «Extracto del producto».
    /// </summary>
    [TestClass]
    public class ExtractoProductoViewModelTests
    {
        private static readonly DateTime HOY = new DateTime(2026, 11, 26);
        private readonly IServicioExtractoProducto servicio = A.Fake<IServicioExtractoProducto>();

        private static MovimientoExtractoProducto Mov(int orden, decimal? importe = null, string proveedor = null) => new()
        {
            NumeroOrden = orden, Empresa = "1", Fecha = HOY, Almacen = "ALG", Cantidad = -1, Saldo = 10, Importe = importe, Proveedor = proveedor
        };

        private static ExtractoProducto Extracto(int total, params MovimientoExtractoProducto[] movs) => new()
        {
            Producto = "17404", Nombre = "CREMA", SaldoInicial = 5, SaldoFinal = 12, Total = total, Movimientos = movs.ToList()
        };

        [TestMethod]
        public void PorDefecto_LosUltimos12Meses()
        {
            var vm = new ExtractoProductoViewModel(servicio, () => HOY);

            Assert.AreEqual(HOY, vm.Hasta);
            Assert.AreEqual(new DateTime(2025, 11, 27), vm.Desde);
            Assert.IsNull(vm.Almacen, "Vacío = todos");
        }

        [TestMethod]
        public async Task AlLlegarConProducto_LeeLaPrimeraPagina()
        {
            A.CallTo(() => servicio.LeerExtracto("1", "17404", null, A<DateTime?>._, A<DateTime?>._, 1, 200)).Returns(Extracto(1, Mov(1)));
            var vm = new ExtractoProductoViewModel(servicio, () => HOY);

            vm.Producto = "17404";
            await vm.CargarAsync();

            Assert.AreEqual(1, vm.Movimientos.Count);
            Assert.AreEqual(5, vm.SaldoInicial);
            Assert.AreEqual(12, vm.SaldoFinal);
            Assert.AreEqual("Extracto de 17404 CREMA", vm.Titulo);
            Assert.IsFalse(vm.HayMas);
            Assert.IsFalse(vm.MostrarImporte);
            Assert.IsFalse(vm.MostrarProveedor);
        }

        [TestMethod]
        public async Task CargarMas_PideLaPaginaSiguienteYAnade()
        {
            A.CallTo(() => servicio.LeerExtracto("1", "17404", "REI", A<DateTime?>._, A<DateTime?>._, 1, 200)).Returns(Extracto(3, Mov(3), Mov(2)));
            A.CallTo(() => servicio.LeerExtracto("1", "17404", "REI", A<DateTime?>._, A<DateTime?>._, 2, 200)).Returns(Extracto(3, Mov(1)));
            var vm = new ExtractoProductoViewModel(servicio, () => HOY) { Producto = "17404", Almacen = "rei" };

            await vm.CargarAsync();
            Assert.IsTrue(vm.HayMas);
            Assert.IsTrue(vm.CargarMasCommand.CanExecute(null));
            await vm.CargarMasAsync();

            CollectionAssert.AreEqual(new[] { 3, 2, 1 }, vm.Movimientos.Select(m => m.NumeroOrden).ToArray());
            Assert.IsFalse(vm.HayMas);
            Assert.AreEqual("3 de 3 movimientos", vm.TextoCuantos);
        }

        [TestMethod]
        public async Task ImporteYProveedor_SoloSiLlegan()
        {
            A.CallTo(() => servicio.LeerExtracto(A<string>._, A<string>._, A<string>._, A<DateTime?>._, A<DateTime?>._, A<int>._, A<int>._))
                .Returns(Extracto(2, Mov(2), Mov(1, importe: 3.5M, proveedor: "1234")));
            var vm = new ExtractoProductoViewModel(servicio, () => HOY) { Producto = "17404" };

            await vm.CargarAsync();

            Assert.IsTrue(vm.MostrarImporte);
            Assert.IsTrue(vm.MostrarProveedor);
        }

        [TestMethod]
        public async Task El403_ConSuMensaje()
        {
            A.CallTo(() => servicio.LeerExtracto(A<string>._, A<string>._, A<string>._, A<DateTime?>._, A<DateTime?>._, A<int>._, A<int>._))
                .Throws(new FichaProductoApiException(403, "El extracto del producto no está disponible para los vendedores."));
            var vm = new ExtractoProductoViewModel(servicio, () => HOY) { Producto = "17404" };

            await vm.CargarAsync();

            Assert.AreEqual("El extracto del producto no está disponible para los vendedores.", vm.Mensaje);
            Assert.AreEqual(0, vm.Movimientos.Count);
        }

        [TestMethod]
        public async Task DesdePosteriorAHasta_NoLlamaALaApi()
        {
            var vm = new ExtractoProductoViewModel(servicio, () => HOY) { Producto = "17404", Desde = HOY.AddDays(1) };

            await vm.CargarAsync();

            A.CallTo(() => servicio.LeerExtracto(A<string>._, A<string>._, A<string>._, A<DateTime?>._, A<DateTime?>._, A<int>._, A<int>._)).MustNotHaveHappened();
            StringAssert.Contains(vm.Mensaje, "no puede ser posterior");
        }

        [TestMethod]
        public void SinProducto_NoSePuedeVer()
        {
            var vm = new ExtractoProductoViewModel(servicio, () => HOY);

            Assert.IsFalse(vm.CargarCommand.CanExecute(null));
            vm.AlLlegar(new ParametrosNavegacion());
            A.CallTo(() => servicio.LeerExtracto(A<string>._, A<string>._, A<string>._, A<DateTime?>._, A<DateTime?>._, A<int>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Url_ConFiltros()
        {
            Assert.AreEqual("Productos/Extracto?producto=17404&empresa=1&almacen=ALG&desde=2025-11-27&hasta=2026-11-26&pagina=2&tamano=200",
                ServicioExtractoProducto.UrlExtracto(null, "17404", "ALG", new DateTime(2025, 11, 27), HOY, 2, 200));
        }

        // ---------- Quien solo es de Tiendas: la API manda su almacén (Carlos 26/11/26) ----------

        [TestMethod]
        public async Task SiLaApiDevuelveOtroAlmacen_ElFiltroSePoneAEseYSeBloquea()
        {
            ExtractoProducto extracto = Extracto(1, Mov(1));
            extracto.Almacen = "REI";
            A.CallTo(() => servicio.LeerExtracto("1", "17404", null, A<DateTime?>._, A<DateTime?>._, 1, 200)).Returns(extracto);
            var vm = new ExtractoProductoViewModel(servicio, () => HOY) { Producto = "17404" };

            await vm.CargarAsync();

            Assert.AreEqual("REI", vm.Almacen);
            Assert.IsTrue(vm.AlmacenFijadoPorLaApi);
            Assert.IsFalse(vm.PuedeCambiarAlmacen);
            Assert.AreEqual("Solo se muestran los movimientos de su almacén (REI).", vm.TextoAlmacenFijado);
        }

        [TestMethod]
        public async Task SiSePideUnAlmacenYLaApiDevuelveOtro_TambienSeBloquea()
        {
            ExtractoProducto extracto = Extracto(1, Mov(1));
            extracto.Almacen = "ALC";
            A.CallTo(() => servicio.LeerExtracto("1", "17404", "ALG", A<DateTime?>._, A<DateTime?>._, 1, 200)).Returns(extracto);
            var vm = new ExtractoProductoViewModel(servicio, () => HOY) { Producto = "17404", Almacen = "ALG" };

            await vm.CargarAsync();

            Assert.AreEqual("ALC", vm.Almacen);
            Assert.IsFalse(vm.PuedeCambiarAlmacen);
        }

        [TestMethod]
        public async Task SiLaApiDevuelveElAlmacenPedidoOTodos_ElFiltroSigueLibre()
        {
            ExtractoProducto todos = Extracto(1, Mov(1)); // Almacen null = todos
            ExtractoProducto alg = Extracto(1, Mov(1));
            alg.Almacen = "ALG";
            A.CallTo(() => servicio.LeerExtracto("1", "17404", null, A<DateTime?>._, A<DateTime?>._, 1, 200)).Returns(todos);
            A.CallTo(() => servicio.LeerExtracto("1", "17404", "ALG", A<DateTime?>._, A<DateTime?>._, 1, 200)).Returns(alg);
            var vm = new ExtractoProductoViewModel(servicio, () => HOY) { Producto = "17404" };

            await vm.CargarAsync();
            Assert.IsNull(vm.Almacen);
            Assert.IsTrue(vm.PuedeCambiarAlmacen);
            Assert.IsNull(vm.TextoAlmacenFijado);

            vm.Almacen = "alg";
            await vm.CargarAsync();
            Assert.AreEqual("ALG", vm.Almacen);
            Assert.IsTrue(vm.PuedeCambiarAlmacen);
        }

        // ---------- Referencia del proveedor en la ficha ----------

        [TestMethod]
        public void ReferenciaProveedor_LaDelHabitualYLosDemasEnElTooltip()
        {
            var refs = new List<ReferenciaProveedorProducto>
            {
                new() { Proveedor = "1234", NombreProveedor = "LISAP", ReferenciaProveedor = "LS-01", Orden = 1, EsHabitual = true },
                new() { Proveedor = "999", NombreProveedor = "OTRO", ReferenciaProveedor = "X9", Orden = 2 }
            };

            Assert.AreEqual("Ref. proveedor: LS-01", ReglasReferenciaProveedor.Texto(refs));
            string ayuda = ReglasReferenciaProveedor.Ayuda(refs);
            StringAssert.Contains(ayuda, "Proveedor habitual: 1234 LISAP (ref. LS-01)");
            StringAssert.Contains(ayuda, "· 999 OTRO (ref. X9)");
        }

        [TestMethod]
        public void ReferenciaProveedor_SinListaOSinHabitual_NoSePinta()
        {
            Assert.IsNull(ReglasReferenciaProveedor.Texto(null));
            Assert.IsNull(ReglasReferenciaProveedor.Texto(new List<ReferenciaProveedorProducto>()));
            Assert.IsNull(ReglasReferenciaProveedor.Texto(new List<ReferenciaProveedorProducto> { new() { ReferenciaProveedor = "X", EsHabitual = false } }));
        }
    }
}
