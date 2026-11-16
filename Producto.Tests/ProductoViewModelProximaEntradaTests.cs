using FakeItEasy;
using CommunityToolkit.Mvvm.Messaging;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>NestoAPI#869: la línea «Próxima entrada» de la ficha del producto (la frase la da la API; aquí solo se pinta).</summary>
    [TestClass]
    public class ProductoViewModelProximaEntradaTests
    {
        private const string PRODUCTO = "17404";

        private static async Task<(ProductoViewModel vm, IServicioProximaEntradaProducto servicio)> CrearViewModel(ProximaEntradaProducto respuesta)
        {
            var servicio = A.Fake<IProductoService>();
            A.CallTo(() => servicio.LeerProducto(PRODUCTO)).Returns(new ProductoModel { Producto = PRODUCTO, Nombre = "CREMA" });
            A.CallTo(() => servicio.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            var vm = new ProductoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio,
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IServicioAutenticacion>());
            var proximaEntrada = A.Fake<IServicioProximaEntradaProducto>();
            A.CallTo(() => proximaEntrada.Leer(A<string>._, A<string>._, A<string>._)).Returns(respuesta);
            vm.ServicioProximaEntrada = proximaEntrada;
            await vm.CargarProducto(PRODUCTO);
            return (vm, proximaEntrada);
        }

        [TestMethod]
        public async Task AlCargar_PideLaDeAlgeteYPintaLaFraseDeLaApi()
        {
            var (vm, servicio) = await CrearViewModel(new ProximaEntradaProducto
            {
                Tipo = ProximaEntradaProducto.TIPO_ORIENTATIVA,
                Fecha = new DateTime(2026, 12, 5),
                EtiquetaCerteza = "Orientativa",
                Texto = "Orientativa: no hay pedido; si se pidiera hoy, llegaría hacia el 28/11.",
                ProveedorHabitual = "123",
                NombreProveedorHabitual = "COSMÉTICA EJEMPLO, S.L.",
                CadenciaDias = 14,
                PlazoDias = 12,
                PlazoPropio = true
            });

            A.CallTo(() => servicio.Leer(Constantes.Empresas.EMPRESA_DEFECTO, PRODUCTO, Constantes.Almacenes.ALMACEN_ALGETE)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.HayProximaEntrada);
            Assert.AreEqual("Próxima entrada: Orientativa: no hay pedido; si se pidiera hoy, llegaría hacia el 28/11.", vm.TextoProximaEntrada);
            StringAssert.Contains(vm.AyudaProximaEntrada, "Proveedor habitual: 123 (COSMÉTICA EJEMPLO, S.L.)");
            StringAssert.Contains(vm.AyudaProximaEntrada, "Se le suele pedir cada 14 días");
            StringAssert.Contains(vm.AyudaProximaEntrada, "Suele tardar hasta 12 días");
        }

        [TestMethod]
        public async Task AlCargar_ConPedido_LaFraseDelPedido()
        {
            var (vm, _) = await CrearViewModel(new ProximaEntradaProducto
            {
                Tipo = ProximaEntradaProducto.TIPO_PEDIDO,
                Pedido = 220856,
                Recalculada = true,
                EtiquetaCerteza = "Aproximada",
                Texto = "Pedido 220856: 27/11 (recalculada, la prevista había pasado; fecha aproximada)",
                NombreProveedorPedido = "COSMÉTICA EJEMPLO, S.L.",
                FechaSiSePidieraHoy = new DateTime(2026, 11, 28)
            });

            Assert.AreEqual("Próxima entrada: Pedido 220856: 27/11 (recalculada, la prevista había pasado; fecha aproximada)", vm.TextoProximaEntrada);
            StringAssert.Contains(vm.AyudaProximaEntrada, "Proveedor del pedido 220856: COSMÉTICA EJEMPLO, S.L.");
            StringAssert.Contains(vm.AyudaProximaEntrada, "Si se pidiera hoy, llegaría hacia el 28/11/2026");
        }

        [TestMethod]
        public async Task SinRespuestaDeLaApi_NoSeVeLaLinea()
        {
            var (vm, _) = await CrearViewModel(null);

            Assert.IsFalse(vm.HayProximaEntrada);
            Assert.IsNull(vm.TextoProximaEntrada);
        }

        [TestMethod]
        public async Task SiFallaLaApi_LaFichaSeAbreIgual()
        {
            var servicio = A.Fake<IProductoService>();
            A.CallTo(() => servicio.LeerProducto(PRODUCTO)).Returns(new ProductoModel { Producto = PRODUCTO, Nombre = "CREMA" });
            A.CallTo(() => servicio.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            var dialogos = A.Fake<IServicioDialogos>();
            var vm = new ProductoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio,
                new WeakReferenceMessenger(), dialogos, A.Fake<IServicioAutenticacion>());
            var proximaEntrada = A.Fake<IServicioProximaEntradaProducto>();
            A.CallTo(() => proximaEntrada.Leer(A<string>._, A<string>._, A<string>._)).Throws(new Exception("caída"));
            vm.ServicioProximaEntrada = proximaEntrada;

            await vm.CargarProducto(PRODUCTO);

            Assert.AreEqual(PRODUCTO, vm.ProductoActual.Producto);
            Assert.IsFalse(vm.HayProximaEntrada);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void ServicioProximaEntrada_Url()
        {
            Assert.AreEqual("Productos/ProximaEntrada?producto=17404&almacen=ALG&empresa=1", ServicioProximaEntradaProducto.Url("1", " 17404 ", null));
        }
    }
}
