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
using System.IO.Packaging;
using Nesto.Models.Nesto.Models;
using System.Linq;
using Nesto.Modulos.CanalesExternos.Models;

namespace Nesto.Modulos.CanalesExternos.ViewModels
{
    public class CanalesExternosPedidosViewModel : ViewModelBase
    {
        private IRegionManager RegionManager { get; }
        private IConfiguracion Configuracion { get; }
        private IDialogService DialogService { get; }
        public IPedidoVentaService PedidoVentaService { get; }

        public event EventHandler CanalSeleccionadoHaCambiado;

        private ICanalExternoPedidos _canalSeleccionado;
        private ColeccionFiltrable _listaPedidos;
        private PedidoCanalExterno _pedidoSeleccionado;

        private Dictionary<string, ICanalExternoPedidos> _factory = new Dictionary<string, ICanalExternoPedidos>();
        
        public CanalesExternosPedidosViewModel(IRegionManager regionManager, IConfiguracion configuracion, IDialogService dialogService, IPedidoVentaService pedidoVentaService)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            DialogService = dialogService;
            PedidoVentaService = pedidoVentaService;

            Factory.Add("Amazon", new CanalExternoPedidosAmazon(configuracion));
            Factory.Add("PrestashopNV", new CanalExternoPedidosPrestashopNuevaVision(configuracion));

            CrearComandos();

            ListaPedidos = new ColeccionFiltrable(new ObservableCollection<PedidoCanalExterno>());
            ListaPedidos.TieneDatosIniciales = true;
            ListaPedidos.ElementoSeleccionadoChanged += (sender, args) => { CargarPedidoSeleccionado(); };

            Titulo = "Canales Externos Pedidos";
        }

        private async void CargarPedidoSeleccionado()
        {
            PedidoCanalExterno pedidoSeleccionado = (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno);
            if (pedidoSeleccionado?.Pedido.fecha != null && !(bool)pedidoSeleccionado?.Pedido.comentarios.StartsWith("FBA"))
            {
                FechaDesde = (DateTime)(ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido.fecha;
                List<PedidoVentaModel.EnvioAgenciaDTO> listaEnlaces = await PedidoVentaService.CargarEnlacesSeguimiento(pedidoSeleccionado.Pedido.empresa, pedidoSeleccionado.PedidoNestoId);
                pedidoSeleccionado.UltimoSeguimiento = listaEnlaces.Where(e => e.Estado >= Constantes.Agencias.ESTADO_TRAMITADO_ENVIO).OrderByDescending(e => e.Fecha).FirstOrDefault()?.EnlaceSeguimiento;
            }
            RaisePropertyChanged(nameof(PedidoSeleccionadoDireccion));
            RaisePropertyChanged(nameof(PedidoSeleccionadoNombre));
            RaisePropertyChanged(nameof(PedidoSeleccionadoTelefonoFijo));
            RaisePropertyChanged(nameof(PedidoSeleccionadoTelefonoMovil));
            RaisePropertyChanged(nameof(PedidoSeleccionadoPoblacion));
            RaisePropertyChanged(nameof(PedidoSeleccionadoObservaciones));
            RaisePropertyChanged(nameof(PedidoSeleccionadoUltimoSeguimiento));
            RaisePropertyChanged(nameof(PedidoSeleccionadoLineas));
            RaisePropertyChanged(nameof(PedidoSeleccionadoContacto));
            RaisePropertyChanged(nameof(PedidoSeleccionadoCliente));
            CrearPedidoCommand.RaiseCanExecuteChanged();
            CrearEtiquetaCommand.RaiseCanExecuteChanged();
            ConfirmarEnvioCommand.RaiseCanExecuteChanged();
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
        private object _clienteCompleto;

        public int NumeroMaxPedidos
        {
            get { return _numeroMaxPedidos; }
            set { SetProperty(ref _numeroMaxPedidos, value); }
        }

        public ColeccionFiltrable ListaPedidos
        {
            get { return _listaPedidos; }
            set { SetProperty(ref _listaPedidos, value); }
        }
        
        //public PedidoCanalExterno PedidoSeleccionado
        //{
        //    get { return _pedidoSeleccionado; }
        //    set {
        //        SetProperty(ref _pedidoSeleccionado, value);
                
        //    }
        //}


        // Todas estas propiedades se podrían evitar creando un wrapper de PedidoCanalExterno que implemente INotifyPropertyChanged
        public string PedidoSeleccionadoDireccion
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Direccion; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno) != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Direccion = value;
                    CrearPedidoCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public string PedidoSeleccionadoNombre
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Nombre; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno) != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Nombre = value;
                    CrearPedidoCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public string PedidoSeleccionadoTelefonoFijo
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.TelefonoFijo; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno) != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).TelefonoFijo = value;
                    CrearPedidoCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public string PedidoSeleccionadoTelefonoMovil
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.TelefonoMovil; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno) != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).TelefonoMovil = value;
                    CrearPedidoCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public string PedidoSeleccionadoPoblacion
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Poblacion; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno) != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Poblacion = value;
                    CrearPedidoCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public string PedidoSeleccionadoObservaciones
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Observaciones; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno) != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Observaciones = value;
                    CrearPedidoCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public string PedidoSeleccionadoUltimoSeguimiento
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.UltimoSeguimiento; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno) != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).UltimoSeguimiento = value;
                    CrearPedidoCommand.RaiseCanExecuteChanged();
                }
            }
        }
        public ICollection<LineaPedidoVentaDTO> PedidoSeleccionadoLineas
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Pedido.Lineas; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Pedido != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido.Lineas = value;
                    CrearPedidoCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public string PedidoSeleccionadoCliente
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Pedido.cliente; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Pedido != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido.cliente = value;
                }
            }
        }

        public string PedidoSeleccionadoContacto
        {
            get { return (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Pedido.contacto; }
            set
            {
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Pedido != null)
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido.contacto = value;
                }
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
                ListaPedidos.Lista = new ObservableCollection<IFiltrableItem>(await CanalSeleccionado.GetAllPedidosAsync(FechaDesde, NumeroMaxPedidos));
                ListaPedidos.ListaOriginal = ListaPedidos.Lista;
                CrearPedidoCommand.RaiseCanExecuteChanged();
            } catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
                CrearPedidoCommand.RaiseCanExecuteChanged();
            }
            
        }

        public DelegateCommand<object> ConfirmarEnvioCommand { get; private set; }
        private bool CanConfirmarEnvio(object pedidoExternoObj)
        {
            PedidoCanalExterno pedidoExterno = ListaPedidos.ElementoSeleccionado as PedidoCanalExterno;
            return pedidoExterno != null && pedidoExterno.PedidoNestoId != 0 && !string.IsNullOrEmpty(PedidoSeleccionadoUltimoSeguimiento);
        }
        private async void OnConfirmarEnvioAsync(object pedidoExternoObj)
        {
            try
            {
                PedidoCanalExterno pedidoExterno = ListaPedidos.ElementoSeleccionado as PedidoCanalExterno;
                bool continuar = DialogService.ShowConfirmationAnswer("Confirmar envío", "¿Desea confirmar el envío?");
                if (!continuar)
                {
                    return;
                }

                EstaOcupado = true;
                string resultado = await CanalSeleccionado.ConfirmarPedido(pedidoExterno);
                EstaOcupado = false;
                DialogService.ShowNotification("Confirmar envío", resultado);
            }
            catch (Exception ex)
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
                    etiqueta.Reembolso = pedido.Pedido.Total;
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
                (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).PedidoNestoId = Int32.Parse(resultado.Split(' ')[1]);
                if (await CanalSeleccionado.EjecutarTrasCrearPedido(ListaPedidos.ElementoSeleccionado as PedidoCanalExterno))
                {
                    resultado += "\nCompletado el proceso";
                }
                CrearEtiquetaCommand.RaiseCanExecuteChanged();
                DialogService.ShowNotification("Crear Pedido", resultado);
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
            ConfirmarEnvioCommand = new DelegateCommand<object>(OnConfirmarEnvioAsync, CanConfirmarEnvio);
        }
        
        async void OnCanalSeleccionadoHaCambiadoAsync(object sender, EventArgs e)
        {
            try
            {
                EstaOcupado = true;
                ListaPedidos.Lista = new ObservableCollection<IFiltrableItem>(await CanalSeleccionado.GetAllPedidosAsync(FechaDesde, NumeroMaxPedidos));
                ListaPedidos.ListaOriginal = ListaPedidos.Lista;
                CrearPedidoCommand.RaiseCanExecuteChanged();
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
