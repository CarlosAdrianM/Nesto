using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.ViewModels;
using System;

namespace CajasTests
{
    /// <summary>
    /// Nesto#490 (4C.4): las pantallas de Cajas que reutilizan su pestaña reciben la navegación por
    /// <see cref="IReceptorNavegacion"/>, sin tipos de Prism. Sin INavigationAware, Prism reutiliza la vista abierta,
    /// que es lo que hacía su IsNavigationTarget = true.
    /// </summary>
    [TestClass]
    public class NavegacionCajasTests
    {
        [DataTestMethod]
        [DataRow(typeof(MayorCuentaViewModel))]
        [DataRow(typeof(AuditoriaEnlacesPagoViewModel))]
        [DataRow(typeof(FacturasPendientesVerifactuViewModel))]
        public void RecibenLaNavegacionSinPrism(Type viewModel)
        {
            Assert.IsTrue(typeof(IReceptorNavegacion).IsAssignableFrom(viewModel));
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(viewModel));
        }
    }
}
