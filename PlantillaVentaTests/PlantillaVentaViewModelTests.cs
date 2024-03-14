using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using Prism.Events;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.ObjectModel;
using Unity;

namespace PlantillaVentaTests
{
    [TestClass]
    public class PlantillaVentaViewModelTests
    {
        //[TestMethod]
        //public void PlantillaVenta_CargarClientes_SiEsUnClienteDeEstado5NoPasaALosProductos()
        //{
        //    IUnityContainer container = A.Fake<IUnityContainer>();
        //    IRegionManager regionManager = A.Fake<IRegionManager>();
        //    IConfiguracion configuracion = A.Fake<IConfiguracion>();
        //    IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
        //    PlantillaVentaViewModel viewModel = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio);
        //    ClienteJson cliente = new ClienteJson
        //    {
        //        estado = 5
        //    };
        //    A.CallTo(() => servicio.CargarClientesVendedor("busca", null, true)).Returns(new ObservableCollection<ClienteJson>
        //    {
        //        cliente
        //    });

        //    viewModel.filtroCliente = "busca";
        //    viewModel.cmdCargarClientesVendedor.Execute();
        //    viewModel.clienteSeleccionado = cliente;

        //    Assert.AreEqual("Selección del cliente", viewModel.CurrentWizardPage?.Title);
        //}

        [TestMethod]
        public void PlantillaVenta_BaseImponible_RedondeaLosDecimales()
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            PlantillaVentaViewModel vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio, eventAggregator, dialogService, pedidoVentaService);
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 1,
                precio = 4.5M,
                descuento = .35M
            });

            decimal baseImponible = vm.baseImponiblePedido;

            Assert.AreEqual(2.92M, baseImponible);
        }

        [TestMethod]
        public void PlantillaVenta_BaseImponiblePortes_RedondeaLosDecimales()
        {
            IUnityContainer container = A.Fake<IUnityContainer>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IEventAggregator eventAggregator = A.Fake<IEventAggregator>();
            IDialogService dialogService = A.Fake<IDialogService>();
            IPedidoVentaService pedidoVentaService = A.Fake<IPedidoVentaService>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            PlantillaVentaViewModel vm = new PlantillaVentaViewModel(container, regionManager, configuracion, servicio, eventAggregator, dialogService, pedidoVentaService);
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta
            {
                cantidad = 1,
                precio = 4.5M,
                descuento = .35M
            });

            decimal baseImponiblePortes = vm.baseImponibleParaPortes;

            Assert.AreEqual(2.92M, baseImponiblePortes);
        }
    }
}
