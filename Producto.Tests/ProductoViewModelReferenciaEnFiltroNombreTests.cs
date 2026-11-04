using ControlesUsuario.Models;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#617: en el filtro de nombre de la ficha, una referencia (solo cifras) o un código de barras + Intro abre directamente
    /// ese producto, como si se hubiera puesto en la casilla de la referencia, sin buscar por nombre. Lo demás, como siempre.
    /// </summary>
    [TestClass]
    public class ProductoViewModelReferenciaEnFiltroNombreTests
    {
        private const string GUANTES_M = "32565";
        private const string EAN = "8437017506379";

        private static ProductoViewModel CrearViewModel(out IProductoService servicio)
        {
            var fake = A.Fake<IProductoService>();
            servicio = fake;
            A.CallTo(() => fake.LeerProducto(A<string>._)).Throws(new Exception("El producto no se ha podido cargar correctamente"));
            A.CallTo(() => fake.LeerProducto(GUANTES_M)).Returns(new ProductoModel { Producto = GUANTES_M, Nombre = "GUANTES NITRILO T/M" });
            A.CallTo(() => fake.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            A.CallTo(() => fake.LeerCodigosBarras(A<string>._)).Returns(new List<CodigoBarrasProductoModel>());
            return new ProductoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio,
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IServicioAutenticacion>());
        }

        [TestMethod]
        public void UnaReferenciaEnElFiltroNombre_AbreLaFichaSinBuscarPorNombre()
        {
            var sut = CrearViewModel(out var servicio);
            sut.FiltroNombre = " " + GUANTES_M + " ";

            bool abierta = sut.AbrirReferenciaDelFiltroNombre();

            Assert.IsTrue(abierta);
            Assert.AreEqual(GUANTES_M, sut.ReferenciaBuscar);
            Assert.AreEqual(GUANTES_M, sut.ProductoActual.Producto);
            A.CallTo(() => servicio.LeerProducto(GUANTES_M)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.BuscarProductos(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.BuscarProductosContextual(A<string>._, A<bool>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void UnCodigoDeBarrasEnElFiltroNombre_AbreLaFichaDeSuProducto()
        {
            var sut = CrearViewModel(out var servicio);
            A.CallTo(() => servicio.BuscarProductosPorCodigoBarras(EAN))
                .Returns(new List<ProductoCodigoBarrasDuplicado> { new() { Producto = GUANTES_M, Nombre = "GUANTES M" } });
            sut.FiltroNombre = EAN;

            bool abierta = sut.AbrirReferenciaDelFiltroNombre();

            Assert.IsTrue(abierta);
            Assert.AreEqual(GUANTES_M, sut.ProductoActual.Producto);
            Assert.AreEqual(GUANTES_M, sut.ReferenciaBuscar);
            A.CallTo(() => servicio.BuscarProductos(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void UnTextoQueNoEsReferencia_SigueComoSiempreSinAbrirNada()
        {
            var sut = CrearViewModel(out var servicio);
            sut.FiltroNombre = "guantes nitrilo";

            bool abierta = sut.AbrirReferenciaDelFiltroNombre();

            Assert.IsFalse(abierta);
            Assert.IsNull(sut.ReferenciaBuscar);
            Assert.IsNull(sut.ProductoActual);
            A.CallTo(() => servicio.LeerProducto(A<string>._)).MustNotHaveHappened();
            Assert.IsTrue(sut.BuscarProductoCommand.CanExecute(null), "La búsqueda por nombre sigue disponible");
        }

        [TestMethod]
        public void UnFiltroVacio_NoAbreNada()
        {
            var sut = CrearViewModel(out var servicio);

            Assert.IsFalse(sut.AbrirReferenciaDelFiltroNombre());
            A.CallTo(() => servicio.LeerProducto(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void EsReferencia_SoloCifras()
        {
            Assert.IsTrue(ProductoViewModel.EsReferencia("32565"));
            Assert.IsTrue(ProductoViewModel.EsReferencia(" 8437017506379 "));
            Assert.IsFalse(ProductoViewModel.EsReferencia("AB123"));
            Assert.IsFalse(ProductoViewModel.EsReferencia("crema 50"));
            Assert.IsFalse(ProductoViewModel.EsReferencia(""));
            Assert.IsFalse(ProductoViewModel.EsReferencia(null));
        }

        [TestMethod]
        public void ElPatronDeLaCasillaDeReferencia_NoAbreLaListaConCifrasYSiConNombre()
        {
            Assert.IsTrue(ControlesUsuario.Behaviors.AutocompleteBehavior.EsTextoSinBusqueda(EAN, ProductoViewModel.PATRON_REFERENCIA));
            Assert.IsTrue(ControlesUsuario.Behaviors.AutocompleteBehavior.EsTextoSinBusqueda(GUANTES_M, ProductoViewModel.PATRON_REFERENCIA));
            Assert.IsFalse(ControlesUsuario.Behaviors.AutocompleteBehavior.EsTextoSinBusqueda("guantes", ProductoViewModel.PATRON_REFERENCIA));
        }
    }
}
