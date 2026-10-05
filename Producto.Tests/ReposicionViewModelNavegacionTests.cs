using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): Reposición no hacía nada al recibir la navegación y reutilizaba su pestaña
    /// (IsNavigationTarget = true), que es lo que hace Prism sin INavigationAware: ya no lo implementa.
    /// </summary>
    [TestClass]
    public class ReposicionViewModelNavegacionTests
    {
        [TestMethod]
        public void NoDependeDeLaNavegacionDePrism()
        {
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(typeof(ReposicionViewModel)));
        }
    }
}
