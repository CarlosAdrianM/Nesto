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
using Nesto.Modulos.CanalesExternos.Interfaces;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ControlesUsuario.Models;
using Unity;

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
        
        private Dictionary<string, ICanalExternoPedidos> _factory = new Dictionary<string, ICanalExternoPedidos>();
        private readonly IUnityContainer _container;

        private readonly IFacturasAmazonService _facturasAmazonService;

        public CanalesExternosPedidosViewModel(IRegionManager regionManager, IConfiguracion configuracion, IDialogService dialogService, IPedidoVentaService pedidoVentaService, IUnityContainer container, IFacturasAmazonService facturasAmazonService, IClientesPorTelefonoService clientesPorTelefonoService)
        {
            RegionManager = regionManager;
            Configuracion = configuracion;
            DialogService = dialogService;
            PedidoVentaService = pedidoVentaService;
            _container = container;
            _facturasAmazonService = facturasAmazonService;

            Factory.Add("Miravia", new CanalExternoPedidosMiravia(configuracion, clientesPorTelefonoService));
            Factory.Add("Amazon", new CanalExternoPedidosAmazon(configuracion, clientesPorTelefonoService));
            Factory.Add("PrestashopNV", new CanalExternoPedidosPrestashopNuevaVision(configuracion, clientesPorTelefonoService));
            
            CrearComandos();

            ListaPedidos = new ColeccionFiltrable(new ObservableCollection<PedidoCanalExterno>());
            ListaPedidos.TieneDatosIniciales = true;
            ListaPedidos.ElementoSeleccionadoChanging += (sender, args) => 
            {
                
            };
            ListaPedidos.ElementoSeleccionadoChanged += (sender, args) => {
                CargarPedidoSeleccionado();                
            };

            Titulo = "Canales Externos Pedidos";
        }

        private async void CargarPedidoSeleccionado()
        {
            PedidoCanalExterno pedidoSeleccionado = (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno);
            if (pedidoSeleccionado?.Pedido.fecha != null && !(bool)pedidoSeleccionado?.Pedido.comentarios.StartsWith("FBA"))
            {
                FechaDesde = (DateTime)(ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido.fecha;
                List<PedidoVentaModel.EnvioAgenciaDTO> listaEnlaces = await PedidoVentaService.CargarEnlacesSeguimiento(pedidoSeleccionado.Pedido.empresa, pedidoSeleccionado.PedidoNestoId);
                // NestoAPI#258 slice (a): guardamos el envío completo (con los identificadores por
                // canal que declara la agencia en el servidor), no solo el enlace.
                PedidoVentaModel.EnvioAgenciaDTO ultimoEnvio = listaEnlaces.Where(e => e.Estado >= Constantes.Agencias.ESTADO_TRAMITADO_ENVIO).OrderByDescending(e => e.Fecha).FirstOrDefault();
                pedidoSeleccionado.UltimoEnvio = ultimoEnvio;
                pedidoSeleccionado.UltimoSeguimiento = ultimoEnvio?.EnlaceSeguimiento;
            }
            if (pedidoSeleccionado != null && !pedidoSeleccionado.Pedido.Lineas.Any())
            {
                try
                {
                    EstaOcupadoLineas = true;
                    pedidoSeleccionado.Pedido.Lineas = await CanalSeleccionado.GetLineas(pedidoSeleccionado);
                }
                catch (Exception ex)
                {
                    EstaOcupadoLineas = false;
                    DialogService.ShowError(ex.Message);
                }
                finally
                {
                    EstaOcupadoLineas = false;
                }
            }
            RaisePropertyChanged(nameof(PedidoSeleccionadoDireccion));
            RaisePropertyChanged(nameof(PedidoSeleccionadoNombre));
            RaisePropertyChanged(nameof(PedidoSeleccionadoTelefonoFijo));
            RaisePropertyChanged(nameof(PedidoSeleccionadoTelefonoMovil));
            RaisePropertyChanged(nameof(PedidoSeleccionadoPoblacion));
            RaisePropertyChanged(nameof(PedidoSeleccionadoObservaciones));
            RaisePropertyChanged(nameof(PedidoSeleccionadoUltimoSeguimiento));
            RaisePropertyChanged(nameof(PedidoSeleccionadoLineas));
            RaisePropertyChanged(nameof(PedidoSeleccionadoTotalLineas));
            RaisePropertyChanged(nameof(PedidoSeleccionadoCliente));
            RaisePropertyChanged(nameof(PedidoSeleccionadoContacto));            
            CrearPedidoCommand.RaiseCanExecuteChanged();
            CrearEtiquetaCommand.RaiseCanExecuteChanged();
            ConfirmarEnvioCommand.RaiseCanExecuteChanged();
            FacturarYSubirCommand.RaiseCanExecuteChanged();
        }

        #region "Propiedades Nesto"

        public ICanalExternoPedidos CanalSeleccionado
        {
            get { return _canalSeleccionado; }
            set {
                // Solo si cambia de verdad: re-asignar el mismo canal (re-binding del combo,
                // Loaded al volver de un pedido...) relanzaba la descarga completa del canal.
                if (SetProperty(ref _canalSeleccionado, value))
                {
                    CanalSeleccionadoHaCambiado?.Invoke(this, new EventArgs());
                }
            }
        }

        private ClienteDTO _clienteSeleccionado;
        public ClienteDTO ClienteSeleccionado { 
            get => _clienteSeleccionado; 
            set {
                _clienteSeleccionado = value;
                if (ListaPedidos == null || ListaPedidos.ElementoSeleccionado == null)
                {
                    return;
                }
                PedidoVentaDTO pedido = (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido;
                if (pedido.vendedor?.Trim() != _clienteSeleccionado.vendedor?.Trim())
                {
                    pedido.vendedor = _clienteSeleccionado.vendedor?.Trim();
                }
            }
        }

        private bool _estaOcupado;

        public bool EstaOcupado
        {
            get { return _estaOcupado; }
            set { SetProperty(ref _estaOcupado, value); }
        }
        private bool _estaOcupadoLineas;

        public bool EstaOcupadoLineas
        {
            get { return _estaOcupadoLineas; }
            set { SetProperty(ref _estaOcupadoLineas, value); }
        }

        public Dictionary<string, ICanalExternoPedidos> Factory
        {
            get => _factory;
            set => SetProperty(ref _factory, value);
        }

        private DateTime _fechaDesde = DateTime.Today.AddDays(-4);
        public DateTime FechaDesde
        {
            get { return _fechaDesde; }
            set { SetProperty(ref _fechaDesde, value); }
        }

        //private object _clienteCompleto;

        private int _numeroMaxPedidos = 100;
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
        public decimal PedidoSeleccionadoTotalLineas => PedidoSeleccionadoLineas?.Sum(l => l.Total) ?? 0;

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
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Pedido != null && (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido.cliente.Trim() != value.Trim())
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
                if ((ListaPedidos.ElementoSeleccionado as PedidoCanalExterno)?.Pedido != null && (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido.contacto?.Trim() != value?.Trim())
                {
                    (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).Pedido.contacto = value;
                }
            }
        }

        private bool _selectorClientesPausado;
        public bool SelectorClientesPausado
        {
            get => _selectorClientesPausado;
            set => SetProperty(ref _selectorClientesPausado, value);
        }
        #endregion

        #region "Comandos"


        public ICommand CargarPedidosCommand { get; private set; }
        private async void OnCargarPedidos()
        {
            if (CanalSeleccionado == null)
            {
                CanalSeleccionado = Factory["Miravia"];
            }
            try
            {
                EstaOcupado = true;
                ListaPedidos.Lista = new ObservableCollection<IFiltrableItem>(await CanalSeleccionado.GetAllPedidosAsync(FechaDesde, NumeroMaxPedidos));
                ListaPedidos.ListaOriginal = ListaPedidos.Lista;
                CrearPedidoCommand.RaiseCanExecuteChanged();
                await CargarEstadosFacturasAsync(); // Nesto#434: pinta la columna Factura
            } catch (Exception ex)
            {
                EstaOcupado = false;
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
                // Nesto#378: usar el servicio (lleva el token JWT) en vez del método estático
                // legacy de PedidoVentaViewModel, que llamaba a la API sin autenticar.
                int numeroPedido = await PedidoVentaService.CrearPedido(pedido);
                EstaOcupado = false;
                string resultado = $"Pedido {numeroPedido} creado correctamente";
                (ListaPedidos.ElementoSeleccionado as PedidoCanalExterno).PedidoNestoId = numeroPedido;
                if (await CanalSeleccionado.EjecutarTrasCrearPedido(ListaPedidos.ElementoSeleccionado as PedidoCanalExterno))
                {
                    resultado += "\nCompletado el proceso";
                }
                CrearEtiquetaCommand.RaiseCanExecuteChanged();
                FacturarYSubirCommand.RaiseCanExecuteChanged();
                FacturarYSubirPendientesCommand.RaiseCanExecuteChanged();
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

        // Nesto#434: facturar el pedido de Nesto (si no lo está) y subir la factura PDF a Amazon.
        // Todo el trabajo lo hace NestoAPI (#366); aquí solo se llama y se pinta el resultado.
        private bool EsCanalAmazon => CanalSeleccionado is CanalExternoPedidosAmazon;

        public DelegateCommand<PedidoCanalExterno> FacturarYSubirCommand { get; private set; }
        private bool CanFacturarYSubir(PedidoCanalExterno pedidoExterno)
        {
            return EsCanalAmazon && pedidoExterno != null && pedidoExterno.PedidoNestoId != 0;
        }
        private async void OnFacturarYSubirAsync(PedidoCanalExterno pedidoExterno)
        {
            string accion = pedidoExterno.FacturaSubida
                ? $"¿Volver a subir la factura del pedido {pedidoExterno.PedidoNestoId} a Amazon (reemplaza la anterior)?"
                : $"¿Facturar el pedido {pedidoExterno.PedidoNestoId} (si no lo está) y subir la factura a Amazon?";
            if (!DialogService.ShowConfirmationAnswer("Subir factura a Amazon", accion))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                string resultado = await FacturarYSubirPedidoAsync(pedidoExterno);
                DialogService.ShowNotification("Subir factura a Amazon", resultado);
            }
            catch (Exception ex)
            {
                DialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
                FacturarYSubirPendientesCommand.RaiseCanExecuteChanged();
            }
        }

        public DelegateCommand FacturarYSubirPendientesCommand { get; private set; }
        private bool CanFacturarYSubirPendientes()
        {
            return EsCanalAmazon && PedidosConFacturaPendiente().Any();
        }
        private async void OnFacturarYSubirPendientesAsync()
        {
            List<PedidoCanalExterno> pendientes = PedidosConFacturaPendiente().ToList();
            if (!DialogService.ShowConfirmationAnswer("Subir facturas a Amazon",
                $"Se van a facturar (si no lo están) y subir a Amazon las facturas de {pendientes.Count} pedidos. ¿Continuar?"))
            {
                return;
            }
            StringBuilder resumen = new();
            try
            {
                EstaOcupado = true;
                for (int i = 0; i < pendientes.Count; i++)
                {
                    try
                    {
                        resumen.AppendLine(await FacturarYSubirPedidoAsync(pendientes[i]));
                    }
                    catch (Exception ex)
                    {
                        resumen.AppendLine($"Pedido {pendientes[i].PedidoNestoId}: ERROR - {ex.Message}");
                    }
                    if (i < pendientes.Count - 1)
                    {
                        await Task.Delay(3100); // rate limit de Amazon: 1 subida cada 3 s
                    }
                }
            }
            finally
            {
                EstaOcupado = false;
                FacturarYSubirPendientesCommand.RaiseCanExecuteChanged();
            }
            DialogService.ShowNotification("Subir facturas a Amazon", resumen.ToString());
        }

        private IEnumerable<PedidoCanalExterno> PedidosConFacturaPendiente()
        {
            // Las OMITIDA son pedidos de clientes de factura simplificada (Amazon/tienda/público
            // final): el servidor no las sube, así que el lote no las intenta.
            return (ListaPedidos?.Lista ?? new ObservableCollection<IFiltrableItem>())
                .OfType<PedidoCanalExterno>()
                .Where(p => p.PedidoNestoId != 0 && !p.FacturaSubida && p.EstadoFacturaAmazon != "OMITIDA");
        }

        private async Task<string> FacturarYSubirPedidoAsync(PedidoCanalExterno pedidoExterno)
        {
            SubirFacturaAmazonResponse respuesta = await _facturasAmazonService.FacturarYSubirAsync(
                pedidoExterno.Pedido.empresa, pedidoExterno.PedidoNestoId);
            pedidoExterno.NumeroFactura = respuesta.NumeroFactura;
            pedidoExterno.EstadoFacturaAmazon = respuesta.Estado;
            string resultado = $"Pedido {respuesta.Pedido}: factura {respuesta.NumeroFactura} subida a Amazon ({respuesta.MarketplaceId})";
            if (respuesta.Avisos != null && respuesta.Avisos.Any())
            {
                resultado += "\n  " + string.Join("\n  ", respuesta.Avisos);
            }
            return resultado;
        }

        // Al recargar la lista se pregunta a la API qué pedidos tienen ya factura subida, para
        // pintar la columna Factura y que el lote solo coja los pendientes. Si falla, no rompe
        // la carga (el estado se queda vacío).
        private async Task CargarEstadosFacturasAsync()
        {
            if (!EsCanalAmazon)
            {
                return;
            }
            List<PedidoCanalExterno> conPedido = (ListaPedidos?.Lista ?? new ObservableCollection<IFiltrableItem>())
                .OfType<PedidoCanalExterno>()
                .Where(p => p.PedidoNestoId != 0)
                .ToList();
            if (!conPedido.Any())
            {
                return;
            }
            try
            {
                string empresa = conPedido.First().Pedido?.empresa ?? Constantes.Empresas.EMPRESA_DEFECTO;
                Dictionary<int, FacturaSubidaAmazon> subidas = await _facturasAmazonService
                    .ConsultarSubidasAsync(empresa, conPedido.Select(p => p.PedidoNestoId));
                foreach (PedidoCanalExterno pedido in conPedido)
                {
                    if (subidas.TryGetValue(pedido.PedidoNestoId, out FacturaSubidaAmazon subida))
                    {
                        pedido.NumeroFactura = subida.NumeroFactura;
                        pedido.EstadoFacturaAmazon = subida.Estado;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("[AmazonDiag] No se pudo cargar el estado de facturas subidas: " + ex.Message);
            }
            FacturarYSubirPendientesCommand.RaiseCanExecuteChanged();
        }

        // Nesto#374: abrir el pedido de Nesto asignado (doble clic en la fila de la lista).
        public DelegateCommand<PedidoCanalExterno> AbrirPedidoNestoCommand { get; private set; }
        private bool CanAbrirPedidoNesto(PedidoCanalExterno pedidoExterno)
        {
            return pedidoExterno != null && pedidoExterno.PedidoNestoId != 0 && pedidoExterno.Pedido != null;
        }
        private void OnAbrirPedidoNesto(PedidoCanalExterno pedidoExterno)
        {
            PedidoVentaViewModel.CargarPedido(pedidoExterno.Pedido.empresa, pedidoExterno.PedidoNestoId, _container);
        }

        #endregion

        private void CrearComandos()
        {
            CanalSeleccionadoHaCambiado += OnCanalSeleccionadoHaCambiadoAsync;

            CargarPedidosCommand = new DelegateCommand(OnCargarPedidos);
            CrearEtiquetaCommand = new DelegateCommand<PedidoCanalExterno>(OnCrearEtiquetaAsync, CanCrearEtiqueta);
            CrearPedidoCommand = new DelegateCommand<PedidoCanalExterno>(OnCrearPedidoAsync, CanCrearPedido);
            ConfirmarEnvioCommand = new DelegateCommand<object>(OnConfirmarEnvioAsync, CanConfirmarEnvio);
            AbrirPedidoNestoCommand = new DelegateCommand<PedidoCanalExterno>(OnAbrirPedidoNesto, CanAbrirPedidoNesto);
            FacturarYSubirCommand = new DelegateCommand<PedidoCanalExterno>(OnFacturarYSubirAsync, CanFacturarYSubir);
            FacturarYSubirPendientesCommand = new DelegateCommand(OnFacturarYSubirPendientesAsync, CanFacturarYSubirPendientes);
        }
        
        async void OnCanalSeleccionadoHaCambiadoAsync(object sender, EventArgs e)
        {
            try
            {
                EstaOcupado = true;
                ListaPedidos.Lista = new ObservableCollection<IFiltrableItem>(await CanalSeleccionado.GetAllPedidosAsync(FechaDesde, NumeroMaxPedidos));
                ListaPedidos.ListaOriginal = ListaPedidos.Lista;
                CrearPedidoCommand.RaiseCanExecuteChanged();
                await CargarEstadosFacturasAsync(); // Nesto#434: pinta la columna Factura
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
