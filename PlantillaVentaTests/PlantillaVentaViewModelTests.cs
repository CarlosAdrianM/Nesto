using Microsoft.VisualStudio.TestTools.UnitTesting;

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
    }
}
