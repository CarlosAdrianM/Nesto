using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.4): la plantilla de venta navega con IServicioNavegacion en vez del IRegionManager de
    /// Prism: mismas regiones, vistas y claves que antes.
    /// </summary>
    [TestClass]
    public class PlantillaVentaNavegacionTests
    {
        private IServicioNavegacion _navegacion;

        [TestInitialize]
        public void Inicializar() => _navegacion = A.Fake<IServicioNavegacion>();

        private PlantillaVentaViewModel Nuevo()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            return new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), _navegacion, configuracion,
                A.Fake<IPlantillaVentaService>(), new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(),
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
        }

        [TestMethod]
        public void AbrirPlantillaVenta_NavegaALaPlantilla()
        {
            Nuevo().cmdAbrirPlantillaVenta.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "PlantillaVentaView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void CargarProducto_AbreLaFichaDelProductoDeLaLinea()
        {
            ParametrosNavegacion recibidos = null;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView", A<ParametrosNavegacion>._))
                .Invokes((string _, string _, ParametrosNavegacion p) => recibidos = p);
            var vm = Nuevo();
            vm.productoPedidoSeleccionado = new LineaPlantillaVenta { producto = "17404" };

            vm.CargarProductoCommand.Execute(null);

            Assert.IsNotNull(recibidos);
            Assert.AreEqual("17404", recibidos.GetValue<string>("numeroProductoParameter"));
        }
    }
}
