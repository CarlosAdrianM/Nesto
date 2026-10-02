using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.ViewModels;

namespace Nesto.Modulos.OfertasCombinadasTests
{
    /// <summary>
    /// Nesto#490 (4C.4): qué vista abre cada botón del menú. Se escribieron contra el IRegionManager
    /// de Prism ANTES de migrar la navegación (commit 33c736cb) y, al pasar el menú a
    /// IServicioNavegacion, solo ha cambiado el tipo del fake: mismas regiones y mismas vistas.
    /// </summary>
    [TestClass]
    public class OfertasCombinadasMenuBarViewModelNavegacionTests
    {
        private IServicioNavegacion _navegacion = null!;
        private OfertasCombinadasMenuBarViewModel _menu = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IServicioNavegacion>();
            _menu = new OfertasCombinadasMenuBarViewModel(_navegacion, A.Fake<IConfiguracion>());
        }

        [TestMethod]
        public void AbrirModuloOfertasCombinadasCommand_AbreLaVistaOfertasCombinadas()
        {
            _menu.AbrirModuloOfertasCombinadasCommand.Execute(null);

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", "OfertasCombinadasView")).MustHaveHappenedOnceExactly();
        }
    }
}
