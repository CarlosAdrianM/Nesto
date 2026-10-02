using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Ganavisiones.Interfaces;
using Nesto.Modulos.Ganavisiones.ViewModels;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4): «Abrir producto» de Ganavisiones navega a la ficha del producto. Escritas contra el
    /// IRegionManager de Prism ANTES de migrar la navegación (d7b3b306); al pasar a
    /// IServicioNavegacion solo ha cambiado el tipo del fake: mismas regiones, vistas y claves.
    /// </summary>
    [TestClass]
    public class GanavisionesViewModelNavegacionTests
    {
        private IServicioNavegacion _navegacion = null!;
        private GanavisionesViewModel _vm = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IServicioNavegacion>();
            _vm = new GanavisionesViewModel(A.Fake<IGanavisionesService>(), A.Fake<IConfiguracion>(), A.Fake<IServicioDialogos>(), _navegacion);
        }

        [TestMethod]
        public void AbrirProducto_AbreLaFichaDelProductoSinEspacios()
        {
            ParametrosNavegacion recibidos = null!;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView", A<ParametrosNavegacion>._))
                .Invokes((string _, string _, ParametrosNavegacion p) => recibidos = p);

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
