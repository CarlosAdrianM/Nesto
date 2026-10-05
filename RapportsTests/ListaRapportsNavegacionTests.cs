using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Rapports;
using Unity;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#490 (4C.4): la lista de rapports navega con IServicioNavegacion en vez del IRegionManager de
    /// Prism: mismas regiones, vistas y claves que antes.
    /// </summary>
    [TestClass]
    public class ListaRapportsNavegacionTests
    {
        private IServicioNavegacion _navegacion;

        [TestInitialize]
        public void Inicializar() => _navegacion = A.Fake<IServicioNavegacion>();

        private ListaRapportsViewModel Nuevo() => new ListaRapportsViewModel(_navegacion, A.Fake<IConfiguracion>(), A.Fake<IRapportService>(),
            A.Fake<IUnityContainer>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());

        [TestMethod]
        public void AbrirModulo_NavegaALaListaDeRapports()
        {
            Nuevo().cmdAbrirModulo.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ListaRapportsView")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirFichaProducto_NavegaALaFichaConElNumeroDelProducto()
        {
            ParametrosNavegacion recibidos = null;
            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "ProductoView", A<ParametrosNavegacion>._))
                .Invokes((string _, string _, ParametrosNavegacion p) => recibidos = p);

            Nuevo().AbrirFichaProductoCommand.Execute(new VentaClienteResumenDTO { Nombre = "17404 - CERA TIBIA" });

            Assert.IsNotNull(recibidos);
            Assert.AreEqual("17404", recibidos.GetValue<string>("numeroProductoParameter"));
        }
    }
}
