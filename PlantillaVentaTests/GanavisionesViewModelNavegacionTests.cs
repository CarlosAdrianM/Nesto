using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Ganavisiones.Interfaces;
using Nesto.Modulos.Ganavisiones.ViewModels;
using Prism.Regions;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4): «Abrir producto» de Ganavisiones navega a la ficha del producto. Escritas contra el
    /// IRegionManager de Prism ANTES de migrar la navegación, para que la migración no cambie nada.
    /// </summary>
    [TestClass]
    public class GanavisionesViewModelNavegacionTests
    {
        private IRegionManager _navegacion = null!;
        private GanavisionesViewModel _vm = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IRegionManager>();
            _vm = new GanavisionesViewModel(A.Fake<IGanavisionesService>(), A.Fake<IConfiguracion>(), A.Fake<IServicioDialogos>(), _navegacion);
        }

        [TestMethod]
        public void AbrirProducto_AbreLaFichaDelProductoSinEspacios()
        {
            NavigationParameters recibidos = null!;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => recibidos = p);

            _vm.AbrirProductoCommand.Execute(new GanavisionWrapper { ProductoId = " 12345 " });

            Assert.IsNotNull(recibidos);
            Assert.AreEqual("12345", recibidos.GetValue<string>("numeroProductoParameter"));
        }

        [TestMethod]
        public void AbrirProducto_SinProducto_NoNavega()
        {
            _vm.AbrirProductoCommand.Execute(new GanavisionWrapper { ProductoId = " " });

            A.CallTo(_navegacion).Where(c => c.Method.Name == "RequestNavigate").MustNotHaveHappened();
        }
    }
}
