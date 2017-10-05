using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Interactivity.InteractionRequest;
using Microsoft.Practices.Prism.Regions;
using Nesto.Contratos;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Windows.Input;
using static Nesto.Models.PedidoVenta;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternosViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }

        private ICanalExternoPedidos _canalSeleccionado;
        private List<PedidoVentaDTO> _listaPedidos;
        private PedidoVentaDTO _pedidoSeleccionado;

        private Dictionary<string, ICanalExternoPedidos> _factory = new Dictionary<string, ICanalExternoPedidos>();
        
        public CanalesExternosViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            Factory.Add("Amazon", new CanalExternoPedidosAmazon(configuracion));
            CanalSeleccionado = Factory["Amazon"];
            
            CrearComandosAsync();

            Titulo = "Canales Externos";
            NotificationRequest = new InteractionRequest<INotification>();
        }

        #region "Propiedades Prism"
        public InteractionRequest<INotification> NotificationRequest { get; private set; }
        #endregion

        #region "Propiedades Nesto"
        
        public ICanalExternoPedidos CanalSeleccionado
        {
            get { return _canalSeleccionado; }
            set { SetProperty(ref _canalSeleccionado, value); }
        }

        public Dictionary<string, ICanalExternoPedidos> Factory
        {
            get => _factory;
            set => SetProperty(ref _factory, value);
        }

        public List<PedidoVentaDTO> ListaPedidos
        {
            get { return _listaPedidos; }
            set { SetProperty(ref _listaPedidos, value); }
        }
        
        public PedidoVentaDTO PedidoSeleccionado
        {
            get { return _pedidoSeleccionado; }
            set {
                SetProperty(ref _pedidoSeleccionado, value);
            }
        }

        #endregion

        #region "Comandos"
        public ICommand AbrirModuloCommand { get; private set; }
        private bool CanAbrirModulo()
        {
            return Environment.UserName == "Carlos";
        }
        private void OnAbrirModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "CanalesExternosView");
        }

        public ICommand CargarPedidosCommand { get; private set; }
        private void OnCargarPedidos()
        {            
            ListaPedidos = CanalSeleccionado.GetAllPedidos();
        }

        public ICommand CrearPedidoCommand { get; private set; }
        private bool CanCrearPedido(PedidoVentaDTO pedido)
        {
            return true;
        }
        private async void OnCrearPedidoAsync(PedidoVentaDTO pedido)
        {
            string resultado = await PedidoVentaViewModel.CrearPedidoAsync(pedido, Configuracion);
            NotificationRequest.Raise(new Notification { Content = resultado, Title = "Crear Pedido" });
        }
#endregion

        private async System.Threading.Tasks.Task CrearComandosAsync()
        {
            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo, CanAbrirModulo);
            CargarPedidosCommand = new DelegateCommand(OnCargarPedidos);
            CrearPedidoCommand = new DelegateCommand<PedidoVentaDTO>(OnCrearPedidoAsync, CanCrearPedido);
        }
    }
}
