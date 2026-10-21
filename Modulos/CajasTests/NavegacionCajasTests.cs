using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.ViewModels;
using Nesto.Modulos.PedidoCompra;
using System;
using System.Threading.Tasks;
using ControlesUsuario.Dialogs;

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
            Assert.IsFalse(System.Linq.Enumerable.Any(viewModel.GetInterfaces(), i => i.FullName == "Prism.Regions.INavigationAware"));
        }
        // Nesto#490 (4C.4, 6.º tramo): Cajas y Bancos heredan de ViewModelBase, que ya no implementa INavigationAware
        // (IsNavigationTarget = false) sino IReceptorNavegacionPestanaNueva: siguen abriendo una pestaña nueva cada vez.
        [DataTestMethod]
        [DataRow(typeof(CajasViewModel))]
        [DataRow(typeof(BancosViewModel))]
        public void AbrenPestanaNuevaSinPrism(Type viewModel)
        {
            Assert.IsTrue(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(viewModel));
            Assert.IsFalse(System.Linq.Enumerable.Any(viewModel.GetInterfaces(), i => i.FullName == "Prism.Regions.INavigationAware"));
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
                A.Fake<IServicioDialogos>(), A.Fake<IPedidoCompraService>(), A.Fake<IAbridorPedidos>(), A.Fake<IRecursosHumanosService>());

            sut.AlLlegar(new ParametrosNavegacion());
            for (int i = 0; i < 100 && sut.BancoSeleccionado is null; i++)
            {
                await Task.Delay(20);
            }

            Assert.AreEqual("7", sut.BancoSeleccionado?.Banco.Codigo);
            Assert.AreEqual(new DateTime(2026, 9, 1), sut.FechaDesde);
            Assert.AreEqual(new DateTime(2026, 9, 30), sut.FechaHasta);
        }

        [TestMethod]
        public void Bancos_AbrirPedido_AbreElPedidoDelPrepagoConElAbridorDePedidos()
        {
            // Nesto#490 (4C.5, tramo 3a): antes, PedidoVentaViewModel.CargarPedido con el IUnityContainer
            var abridor = A.Fake<IAbridorPedidos>();
            var sut = new BancosViewModel(A.Fake<IBancosService>(), A.Fake<IContabilidadService>(), A.Fake<IConfiguracion>(),
                A.Fake<IServicioDialogos>(), A.Fake<IPedidoCompraService>(), abridor, A.Fake<IRecursosHumanosService>());
            var banco = A.Fake<IBancoConciliacion>();
            A.CallTo(() => banco.Banco).Returns(new Nesto.Modulos.Cajas.Models.BancoDTO { Empresa = "3", Codigo = "9" });
            sut.BancoSeleccionado = banco;

            sut.AbrirPedidoCommand.Execute(new Nesto.Models.PrepagoDTO { Pedido = 123456 });

            A.CallTo(() => abridor.Abrir("3", 123456)).MustHaveHappenedOnceExactly();
        }
    }
}
