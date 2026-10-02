using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.Cliente;
using Prism.Regions;
using System.Collections.Generic;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#490 (4C.4): adónde navega CrearClienteViewModel y que, tras crear el cliente, cierra la
    /// pestaña activa. Escritas contra el IRegionManager de Prism ANTES de migrar la navegación.
    /// </summary>
    [TestClass]
    public class CrearClienteViewModelNavegacionTests
    {
        private IRegionManager _regionManager;
        private IClienteService _servicio;
        private CrearClienteViewModel _vm;

        [TestInitialize]
        public void Inicializar()
        {
            _regionManager = A.Fake<IRegionManager>();
            _servicio = A.Fake<IClienteService>();
            _vm = new CrearClienteViewModel(_regionManager, A.Fake<IConfiguracion>(), _servicio, new WeakReferenceMessenger(), A.Fake<IServicioDialogos>());
        }

        [DataTestMethod]
        [DataRow(nameof(CrearClienteViewModel.AbrirModuloCommand), "CrearClienteView")]
        [DataRow(nameof(CrearClienteViewModel.AbrirModelo347Command), "Modelo347View")]
        [DataRow(nameof(CrearClienteViewModel.AbrirExtractoClienteCommand), "ExtractoClienteView")]
        [DataRow(nameof(CrearClienteViewModel.AbrirNifIncorrectosCommand), "ClientesNifIncorrectosView")]
        [DataRow(nameof(CrearClienteViewModel.AbrirCodigosPostalesCommand), "MantenimientoCodigosPostalesView")]
        public void Comando_AbreSuVista(string comando, string vista)
        {
            var command = (System.Windows.Input.ICommand)typeof(CrearClienteViewModel).GetProperty(comando).GetValue(_vm);

            command.Execute(null);

            A.CallTo(() => _regionManager.RequestNavigate("MainRegion", vista)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void CrearCliente_SiSeCrea_CierraLaPestanaActiva()
        {
            var region = A.Fake<IRegion>();
            var vistaActiva = new object();
            var activas = A.Fake<IViewsCollection>();
            A.CallTo(() => activas.GetEnumerator()).ReturnsLazily(() => new List<object> { vistaActiva }.GetEnumerator());
            A.CallTo(() => region.ActiveViews).Returns(activas);
            A.CallTo(() => _regionManager.Regions["MainRegion"]).Returns(region);
            A.CallTo(() => _servicio.CrearCliente(A<ClienteCrear>._)).Returns(new Clientes { Nº_Cliente = "1", Contacto = "0" });

            _vm.CrearClienteCommand.Execute(null);

            A.CallTo(() => region.Deactivate(vistaActiva)).MustHaveHappenedOnceExactly();
            A.CallTo(() => region.Remove(vistaActiva)).MustHaveHappenedOnceExactly();
        }
    }
}
