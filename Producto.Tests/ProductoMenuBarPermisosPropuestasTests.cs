using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;

namespace Producto.Tests
{
    /// <summary>Carlos 17/11/26: las propuestas de stock, solo Dirección; «Cambiar estado de productos» sigue para Compras y Dirección.</summary>
    [TestClass]
    public class ProductoMenuBarPermisosPropuestasTests
    {
        private static ProductoMenuBarViewModel Menu(string grupo)
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            return new ProductoMenuBarViewModel(A.Fake<IServicioNavegacion>(), configuracion, A.Fake<Nesto.Infrastructure.Services.IServicioDiferenciasReposicion>());
        }

        [TestMethod]
        public void Compras_NoVeLasPropuestas_PeroSiCambiaEstados()
        {
            ProductoMenuBarViewModel menu = Menu(Constantes.GruposSeguridad.COMPRAS);

            Assert.IsFalse(menu.PuedeVerPropuestasStock);
            Assert.IsFalse(menu.AbrirPropuestasStockCommand.CanExecute(null));
            Assert.IsTrue(menu.PuedeCambiarEstadoProductos);
            Assert.IsTrue(menu.AbrirCambiarEstadoProductosCommand.CanExecute(null));
            Assert.IsTrue(menu.PuedeVerGrupoCompras);
        }

        [TestMethod]
        public void Direccion_VeLasPropuestasYCambiaEstados()
        {
            ProductoMenuBarViewModel menu = Menu(Constantes.GruposSeguridad.DIRECCION);

            Assert.IsTrue(menu.PuedeVerPropuestasStock);
            Assert.IsTrue(menu.PuedeCambiarEstadoProductos);
        }
    }
}
