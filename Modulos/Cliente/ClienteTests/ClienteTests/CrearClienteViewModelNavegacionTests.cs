using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.Cliente;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#490 (4C.4): adónde navega CrearClienteViewModel y que, tras crear el cliente, cierra la
    /// pestaña activa. Escritas contra el IRegionManager de Prism ANTES de migrar la navegación; al pasar
    /// a IServicioNavegacion solo cambia el fake (el cierre lo hace ahora CerrarVistaActiva, probado en
    /// ServicioNavegacionPrismTests con el mismo Deactivate + Remove).
    /// </summary>
    [TestClass]
    public class CrearClienteViewModelNavegacionTests
    {
        private IServicioNavegacion _navegacion;
        private IClienteService _servicio;
        private CrearClienteViewModel _vm;

        [TestInitialize]
        public void Inicializar()
        {
            _navegacion = A.Fake<IServicioNavegacion>();
            _servicio = A.Fake<IClienteService>();
            _vm = new CrearClienteViewModel(_navegacion, A.Fake<IConfiguracion>(), _servicio, new WeakReferenceMessenger(), A.Fake<IServicioDialogos>());
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

            A.CallTo(() => _navegacion.RequestNavigate("MainRegion", vista)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void CrearCliente_SiSeCrea_CierraLaPestanaActiva()
        {
            A.CallTo(() => _servicio.CrearCliente(A<ClienteCrear>._)).Returns(new Clientes { Nº_Cliente = "1", Contacto = "0" });

            _vm.CrearClienteCommand.Execute(null);

            A.CallTo(() => _navegacion.CerrarVistaActiva("MainRegion")).MustHaveHappenedOnceExactly();
        }
    }
}
