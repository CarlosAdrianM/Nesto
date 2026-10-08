using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.ViewModels;
using Nesto.Modulos.PedidoCompra;
using System;
using System.Threading.Tasks;
using Unity;

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
        // Nesto#490 (4C.4, 6.º tramo): Cajas y Bancos heredan de ViewModelBase, que ya no implementa INavigationAware
        // (IsNavigationTarget = false) sino IReceptorNavegacionPestanaNueva: siguen abriendo una pestaña nueva cada vez.
        [DataTestMethod]
        [DataRow(typeof(CajasViewModel))]
        [DataRow(typeof(BancosViewModel))]
        public void AbrenPestanaNuevaSinPrism(Type viewModel)
        {
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(viewModel));
            Assert.IsFalse(typeof(Prism.Regions.INavigationAware).IsAssignableFrom(viewModel));
        }

        [TestMethod]
        public async Task Bancos_AlLlegar_SeleccionaElUltimoBancoDelUsuarioConSusFechas()
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro("1", Parametros.Claves.ConciliacionBancariaUltimoBanco)).Returns(Task.FromResult("7"));
            A.CallTo(() => configuracion.leerParametro("1", Parametros.Claves.ConciliacionBancariaFechaDesde))
                .Returns(Task.FromResult("{\"7\":\"01/09/26\"}"));
            A.CallTo(() => configuracion.leerParametro("1", Parametros.Claves.ConciliacionBancariaFechaHasta))
                .Returns(Task.FromResult("{\"7\":\"30/09/26\"}"));
            var sut = new BancosViewModel(A.Fake<IBancosService>(), A.Fake<IContabilidadService>(), configuracion,
                A.Fake<IServicioDialogos>(), A.Fake<IPedidoCompraService>(), A.Fake<IUnityContainer>(), A.Fake<IRecursosHumanosService>());

            sut.AlLlegar(new ParametrosNavegacion());
            for (int i = 0; i < 100 && sut.BancoSeleccionado is null; i++)
            {
                await Task.Delay(20);
            }

            Assert.AreEqual("7", sut.BancoSeleccionado?.Banco.Codigo);
            Assert.AreEqual(new DateTime(2026, 9, 1), sut.FechaDesde);
            Assert.AreEqual(new DateTime(2026, 9, 30), sut.FechaHasta);
        }
    }
}
