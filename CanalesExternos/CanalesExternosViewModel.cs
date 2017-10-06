using Microsoft.Practices.Prism.Commands;
using Microsoft.Practices.Prism.Interactivity.InteractionRequest;
using Microsoft.Practices.Prism.Regions;
using Nesto.Contratos;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using static Nesto.Models.PedidoVenta;

namespace Nesto.Modulos.CanalesExternos
{
    public class CanalesExternosViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }

        public event EventHandler CanalSeleccionadoHaCambiado;

        private ICanalExternoPedidos _canalSeleccionado;
        private ObservableCollection<PedidoVentaDTO> _listaPedidos;
        private PedidoVentaDTO _pedidoSeleccionado;

        private Dictionary<string, ICanalExternoPedidos> _factory = new Dictionary<string, ICanalExternoPedidos>();
        
        public CanalesExternosViewModel(IRegionManager regionManager, IConfiguracion configuracion)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;

            Factory.Add("Amazon", new CanalExternoPedidosAmazon(configuracion));
            Factory.Add("PrestashopNV", new CanalExternoPedidosPrestashopNuevaVision(configuracion));
            
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
            set {
                SetProperty(ref _canalSeleccionado, value);
                CanalSeleccionadoHaCambiado?.Invoke(this, new EventArgs());
            }
        }

        private bool _estaOcupado;

        public bool EstaOcupado
        {
            get { return _estaOcupado; }
            set { SetProperty(ref _estaOcupado, value); }
        }

        public Dictionary<string, ICanalExternoPedidos> Factory
        {
            get => _factory;
            set => SetProperty(ref _factory, value);
        }

        public ObservableCollection<PedidoVentaDTO> ListaPedidos
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
            return Environment.UserName.ToLower() == "carlos" || Environment.UserName.ToLower() == "laura";
        }
        private void OnAbrirModulo()
        {
            RegionManager.RequestNavigate("MainRegion", "CanalesExternosView");
        }

        public ICommand CargarPedidosCommand { get; private set; }
        private async void OnCargarPedidos()
        {
            if (CanalSeleccionado == null)
            {
                CanalSeleccionado = Factory["Amazon"];
            }
            try
            {
                EstaOcupado = true;
                ListaPedidos = await CanalSeleccionado.GetAllPedidosAsync();
            } finally
            {
                EstaOcupado = false;
            }
            
        }

        public ICommand CrearPedidoCommand { get; private set; }
        private bool CanCrearPedido(PedidoVentaDTO pedido)
        {
            return true;
        }
        private async void OnCrearPedidoAsync(PedidoVentaDTO pedido)
        {
            try
            {
                EstaOcupado = true;
                string resultado = await PedidoVentaViewModel.CrearPedidoAsync(pedido, Configuracion);
                EstaOcupado = false;
                NotificationRequest.Raise(new Notification { Content = resultado, Title = "Crear Pedido" });
            } finally
            {
                EstaOcupado = false;
            }            
        }
#endregion

        private async Task CrearComandosAsync()
        {
            CanalSeleccionadoHaCambiado += OnCanalSeleccionadoHaCambiadoAsync;

            AbrirModuloCommand = new DelegateCommand(OnAbrirModulo, CanAbrirModulo);
            CargarPedidosCommand = new DelegateCommand(OnCargarPedidos);
            CrearPedidoCommand = new DelegateCommand<PedidoVentaDTO>(OnCrearPedidoAsync, CanCrearPedido);
        }

        async void OnCanalSeleccionadoHaCambiadoAsync(object sender, EventArgs e)
        {
            try
            {
                EstaOcupado = true;
                ListaPedidos = await CanalSeleccionado.GetAllPedidosAsync();
            } finally
            {
                EstaOcupado = false;
            }
        }
    }
}
