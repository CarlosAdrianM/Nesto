using Prism.Commands;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Nesto.ViewModels;
using Nesto.Modulos.PedidoVenta;
using Prism.Services.Dialogs;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;

namespace Nesto.Modulos.CanalesExternos.ViewModels
{
    public class CanalesExternosPedidosViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        private IDialogService DialogService { get; }

        public event EventHandler CanalSeleccionadoHaCambiado;

        private ICanalExternoPedidos _canalSeleccionado;
        private ObservableCollection<PedidoCanalExterno> _listaPedidos;
        private PedidoCanalExterno _pedidoSeleccionado;

        private Dictionary<string, ICanalExternoPedidos> _factory = new Dictionary<string, ICanalExternoPedidos>();
        
        public CanalesExternosPedidosViewModel(IRegionManager regionManager, IConfiguracion configuracion, IDialogService dialogService)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            DialogService = dialogService;

            Factory.Add("Amazon", new CanalExternoPedidosAmazon(configuracion));
            Factory.Add("PrestashopNV", new CanalExternoPedidosPrestashopNuevaVision(configuracion));

            CrearComandos();

            Titulo = "Canales Externos Pedidos";
        }

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

        private DateTime _fechaDesde = DateTime.Today.AddDays(-7);
        public DateTime FechaDesde
        {
            get { return _fechaDesde; }
            set { SetProperty(ref _fechaDesde, value); }
        }

        private int _numeroMaxPedidos = 20;
        public int NumeroMaxPedidos
        {
            get { return _numeroMaxPedidos; }
            set { SetProperty(ref _numeroMaxPedidos, value); }
        }

        public ObservableCollection<PedidoCanalExterno> ListaPedidos
        {
            get { return _listaPedidos; }
            set { SetProperty(ref _listaPedidos, value); }
        }
        
        public PedidoCanalExterno PedidoSeleccionado
        {
            get { return _pedidoSeleccionado; }
            set {
                SetProperty(ref _pedidoSeleccionado, value);
                if (PedidoSeleccionado?.Pedido.fecha != null && !(bool)PedidoSeleccionado?.Pedido.comentarios.StartsWith("FBA"))
                {
                    FechaDesde = (DateTime)PedidoSeleccionado.Pedido.fecha;
                }
                RaisePropertyChanged(nameof(PedidoSeleccionadoDireccion));
                RaisePropertyChanged(nameof(PedidoSeleccionadoNombre));
                RaisePropertyChanged(nameof(PedidoSeleccionadoTelefonoFijo));
                RaisePropertyChanged(nameof(PedidoSeleccionadoTelefonoMovil));
                CrearPedidoCommand.RaiseCanExecuteChanged();
            }
        }
        public string PedidoSeleccionadoDireccion
        {
            get { return PedidoSeleccionado?.Direccion; }
            set
            {
                PedidoSeleccionado.Direccion = value;
                CrearPedidoCommand.RaiseCanExecuteChanged();
            }
        }
        public string PedidoSeleccionadoNombre
        {
            get { return PedidoSeleccionado?.Nombre; }
            set
            {
                PedidoSeleccionado.Nombre = value;
                CrearPedidoCommand.RaiseCanExecuteChanged();
            }
        }
        public string PedidoSeleccionadoTelefonoFijo
        {
            get { return PedidoSeleccionado?.TelefonoFijo; }
            set
            {
                PedidoSeleccionado.TelefonoFijo = value;
                CrearPedidoCommand.RaiseCanExecuteChanged();
            }
        }
        public string PedidoSeleccionadoTelefonoMovil
        {
            get { return PedidoSeleccionado?.TelefonoMovil; }
            set
            {
                PedidoSeleccionado.TelefonoMovil = value;
                CrearPedidoCommand.RaiseCanExecuteChanged();
            }
        }

        #endregion

        #region "Comandos"


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
                ListaPedidos = await CanalSeleccionado.GetAllPedidosAsync(FechaDesde, NumeroMaxPedidos);
            } catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
            
        }

        public DelegateCommand<PedidoCanalExterno> CrearEtiquetaCommand { get; private set; }
        private bool CanCrearEtiqueta(PedidoCanalExterno pedido)
        {
            return pedido != null && pedido.PedidoNestoId != 0;
        }
        private async void OnCrearEtiquetaAsync(PedidoCanalExterno pedido)
        {
            try
            {
                EstaOcupado = true;

                EnvioAgenciaWrapper etiqueta = new EnvioAgenciaWrapper
                {
                    Pedido = pedido.PedidoNestoId,
                    Nombre = pedido.Nombre,
                    Direccion = pedido.Direccion,
                    Poblacion = pedido.Poblacion,
                    Provincia = pedido.Provincia,
                    CodPostal = pedido.CodigoPostal,
                    Email = pedido.CorreoElectronico,
                    Telefono = pedido.TelefonoFijo,
                    Movil = pedido.TelefonoMovil,
                    PaisISO = pedido.PaisISO, 
                    Observaciones = pedido.Observaciones
                };
                
                if (pedido.Pedido.formaPago == Constantes.FormasPago.EFECTIVO)
                {
                    etiqueta.Reembolso = pedido.Pedido.total;
                }
                
                AgenciasViewModel.CrearEtiquetaPendiente(etiqueta, RegionManager, Configuracion, DialogService);

                EstaOcupado = false;
                DialogService.ShowNotification("Crear Etiqueta", "Etiqueta creada");
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public DelegateCommand<PedidoCanalExterno> CrearPedidoCommand { get; private set; }
        private bool CanCrearPedido(PedidoCanalExterno pedidoExterno)
        {
            return pedidoExterno != null && 
                (!string.IsNullOrEmpty(pedidoExterno.Nombre) && !string.IsNullOrEmpty(pedidoExterno.Direccion) && 
                (!string.IsNullOrWhiteSpace(pedidoExterno.TelefonoFijo) || !string.IsNullOrWhiteSpace(pedidoExterno.TelefonoMovil)) || 
                pedidoExterno.PedidoCanalId.StartsWith("FBA"));
        }
        private async void OnCrearPedidoAsync(PedidoCanalExterno pedidoExterno)
        {
            try
            {
                EstaOcupado = true;
                PedidoVentaDTO pedido = pedidoExterno.Pedido;
                string resultado = await PedidoVentaViewModel.CrearPedidoAsync(pedido, Configuracion);
                EstaOcupado = false;
                DialogService.ShowNotification("Crear Pedido", resultado);
                PedidoSeleccionado.PedidoNestoId = Int32.Parse(resultado.Split(' ')[1]);
                CrearEtiquetaCommand.RaiseCanExecuteChanged();
            } catch(Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }            
        }

        #endregion

        private void CrearComandos()
        {
            CanalSeleccionadoHaCambiado += OnCanalSeleccionadoHaCambiadoAsync;

            CargarPedidosCommand = new DelegateCommand(OnCargarPedidos);
            CrearEtiquetaCommand = new DelegateCommand<PedidoCanalExterno>(OnCrearEtiquetaAsync, CanCrearEtiqueta);
            CrearPedidoCommand = new DelegateCommand<PedidoCanalExterno>(OnCrearPedidoAsync, CanCrearPedido);
        }
        
        async void OnCanalSeleccionadoHaCambiadoAsync(object sender, EventArgs e)
        {
            try
            {
                EstaOcupado = true;
                ListaPedidos = await CanalSeleccionado.GetAllPedidosAsync(FechaDesde, NumeroMaxPedidos);
            } catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
