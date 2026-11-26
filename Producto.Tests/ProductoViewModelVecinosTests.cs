using FakeItEasy;
using CommunityToolkit.Mvvm.Messaging;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>Sugerencia 1478 (Manuel, Compras): flechas a la referencia anterior y a la siguiente de la ficha (las da la API).</summary>
    [TestClass]
    public class ProductoViewModelVecinosTests
    {
        private const string PRODUCTO = "45957";
        private const string OTRO = "45958";

        private static (ProductoViewModel vm, IProductoService servicio, IServicioVecinosProducto vecinos) CrearViewModel()
        {
            var servicio = A.Fake<IProductoService>();
            A.CallTo(() => servicio.LeerProducto(A<string>._)).ReturnsLazily((string p) => new ProductoModel { Producto = p, Nombre = "CREMA " + p });
            A.CallTo(() => servicio.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            var vm = new ProductoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio,
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IServicioAutenticacion>());
            var vecinos = A.Fake<IServicioVecinosProducto>();
            A.CallTo(() => vecinos.Leer(A<string>._, A<string>._)).Returns(Task.FromResult<VecinosProducto>(null!));
            vm.ServicioVecinos = vecinos;
            vm.ServicioProximaEntrada = A.Fake<IServicioProximaEntradaProducto>();
            return (vm, servicio, vecinos);
        }

        [TestMethod]
        public async Task AlCargar_LeeLosVecinosYHabilitaLasFlechas()
        {
            var (vm, _, vecinos) = CrearViewModel();
            A.CallTo(() => vecinos.Leer(Constantes.Empresas.EMPRESA_DEFECTO, PRODUCTO))
                .Returns(new VecinosProducto { Producto = PRODUCTO, Anterior = "45956", Siguiente = OTRO });

            await vm.CargarProducto(PRODUCTO);

            A.CallTo(() => vecinos.Leer(Constantes.Empresas.EMPRESA_DEFECTO, PRODUCTO)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("45956", vm.ReferenciaAnterior);
            Assert.AreEqual(OTRO, vm.ReferenciaSiguiente);
            Assert.IsTrue(vm.IrAReferenciaAnteriorCommand.CanExecute(null));
            Assert.IsTrue(vm.IrAReferenciaSiguienteCommand.CanExecute(null));
            StringAssert.Contains(vm.AyudaReferenciaAnterior, "Referencia anterior (45956)");
            StringAssert.Contains(vm.AyudaReferenciaSiguiente, "Referencia siguiente (45958)");
        }

        [TestMethod]
        public async Task PrimeraReferencia_SinAnterior_LaFlechaQuedaDeshabilitada()
        {
            var (vm, _, vecinos) = CrearViewModel();
            A.CallTo(() => vecinos.Leer(A<string>._, PRODUCTO))
                .Returns(new VecinosProducto { Producto = PRODUCTO, Anterior = null, Siguiente = OTRO });
            bool avisado = false;
            vm.IrAReferenciaAnteriorCommand.CanExecuteChanged += (_, _) => avisado = true;

            await vm.CargarProducto(PRODUCTO);

            Assert.IsNull(vm.ReferenciaAnterior);
            Assert.IsFalse(vm.IrAReferenciaAnteriorCommand.CanExecute(null));
            Assert.IsTrue(vm.IrAReferenciaSiguienteCommand.CanExecute(null));
            Assert.AreEqual("No hay referencia anterior", vm.AyudaReferenciaAnterior);
            Assert.IsFalse(avisado, "Sin cambio de vecino no hace falta avisar");
        }

        [TestMethod]
        public async Task IrAlSiguiente_CambiaLaReferenciaYCargaEseProducto()
        {
            var (vm, servicio, vecinos) = CrearViewModel();
            A.CallTo(() => vecinos.Leer(A<string>._, PRODUCTO))
                .Returns(new VecinosProducto { Producto = PRODUCTO, Anterior = "45956", Siguiente = OTRO });
            A.CallTo(() => vecinos.Leer(A<string>._, OTRO))
                .Returns(new VecinosProducto { Producto = OTRO, Anterior = PRODUCTO, Siguiente = "45960" });
            await vm.CargarProducto(PRODUCTO);

            vm.IrAReferenciaSiguienteCommand.Execute(null);
            // El setter carga en segundo plano; con los fakes todo termina en el acto
            await Task.Yield();

            Assert.AreEqual(OTRO, vm.ReferenciaBuscar);
            A.CallTo(() => servicio.LeerProducto(OTRO)).MustHaveHappened();
            Assert.AreEqual(OTRO, vm.ProductoActual.Producto);
            Assert.AreEqual(PRODUCTO, vm.ReferenciaAnterior);
            Assert.AreEqual("45960", vm.ReferenciaSiguiente);
        }

        [TestMethod]
        public async Task IrAlAnterior_CambiaLaReferencia()
        {
            var (vm, _, vecinos) = CrearViewModel();
            A.CallTo(() => vecinos.Leer(A<string>._, PRODUCTO))
                .Returns(new VecinosProducto { Producto = PRODUCTO, Anterior = "45956", Siguiente = OTRO });
            await vm.CargarProducto(PRODUCTO);

            vm.IrAReferenciaAnteriorCommand.Execute(null);
            await Task.Yield();

            Assert.AreEqual("45956", vm.ReferenciaBuscar);
        }

        [TestMethod]
        public async Task RespuestaDeOtroProducto_SeDescarta()
        {
            var (vm, _, vecinos) = CrearViewModel();
            A.CallTo(() => vecinos.Leer(A<string>._, PRODUCTO))
                .Returns(new VecinosProducto { Producto = "11111", Anterior = "11110", Siguiente = "11112" });

            await vm.CargarProducto(PRODUCTO);

            Assert.IsNull(vm.ReferenciaAnterior);
            Assert.IsNull(vm.ReferenciaSiguiente);
            Assert.IsFalse(vm.IrAReferenciaAnteriorCommand.CanExecute(null));
            Assert.IsFalse(vm.IrAReferenciaSiguienteCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task RespuestaQueLlegaConOtroProductoEnPantalla_SeDescarta()
        {
            var (vm, _, vecinos) = CrearViewModel();
            var lenta = new TaskCompletionSource<VecinosProducto>();
            A.CallTo(() => vecinos.Leer(A<string>._, PRODUCTO)).Returns(lenta.Task);
            A.CallTo(() => vecinos.Leer(A<string>._, OTRO))
                .Returns(new VecinosProducto { Producto = OTRO, Anterior = PRODUCTO, Siguiente = "45960" });

            await vm.CargarProducto(PRODUCTO);
            await vm.CargarProducto(OTRO);
            lenta.SetResult(new VecinosProducto { Producto = PRODUCTO, Anterior = "45956", Siguiente = OTRO });
            await Task.Yield();

            Assert.AreEqual(OTRO, vm.ProductoActual.Producto);
            Assert.AreEqual(PRODUCTO, vm.ReferenciaAnterior);
            Assert.AreEqual("45960", vm.ReferenciaSiguiente);
        }

        [TestMethod]
        public async Task AlEmpezarACargarOtro_SeQuitanLasFlechasDelAnterior()
        {
            var (vm, _, vecinos) = CrearViewModel();
            A.CallTo(() => vecinos.Leer(A<string>._, PRODUCTO))
                .Returns(new VecinosProducto { Producto = PRODUCTO, Anterior = "45956", Siguiente = OTRO });
            A.CallTo(() => vecinos.Leer(A<string>._, OTRO)).Returns(new TaskCompletionSource<VecinosProducto>().Task);
            await vm.CargarProducto(PRODUCTO);

            await vm.CargarProducto(OTRO);

            Assert.IsNull(vm.ReferenciaAnterior);
            Assert.IsNull(vm.ReferenciaSiguiente);
            Assert.IsFalse(vm.IrAReferenciaSiguienteCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinRespuestaDeLaApi_SinFlechasYLaFichaSeAbre()
        {
            var (vm, _, _) = CrearViewModel();

            await vm.CargarProducto(PRODUCTO);

            Assert.AreEqual(PRODUCTO, vm.ProductoActual.Producto);
            Assert.IsNull(vm.ReferenciaAnterior);
            Assert.IsNull(vm.ReferenciaSiguiente);
            Assert.IsFalse(vm.IrAReferenciaAnteriorCommand.CanExecute(null));
            Assert.IsFalse(vm.IrAReferenciaSiguienteCommand.CanExecute(null));
            Assert.AreEqual("No hay referencia siguiente", vm.AyudaReferenciaSiguiente);
        }

        [TestMethod]
        public async Task SiFallaElServicio_LaFichaSeAbreIgual()
        {
            var (vm, _, vecinos) = CrearViewModel();
            A.CallTo(() => vecinos.Leer(A<string>._, A<string>._)).Throws(new Exception("caída"));

            await vm.CargarProducto(PRODUCTO);

            Assert.AreEqual(PRODUCTO, vm.ProductoActual.Producto);
            Assert.IsFalse(vm.IrAReferenciaSiguienteCommand.CanExecute(null));
        }

        [TestMethod]
        public void ServicioVecinos_Url()
        {
            Assert.AreEqual("Productos/Vecinos?producto=45957&empresa=1&saltarAnuladas=true", ServicioVecinosProducto.Url(null, " 45957 "));
        }
    }
}
