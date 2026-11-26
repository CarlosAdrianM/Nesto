using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.BultosPendientes
{
    /// <summary>
    /// NestoAPI#880 (corte 2): los bultos que faltaron en una entrega incompleta del reparto propio. En Agencias (pestaña «Bultos
    /// pendientes»), como ventana (la campana <c>BultosPendientesReparto</c> abre aquí) y, filtrada por el pedido, en la pestaña «Entrega»
    /// del pedido. «Cerrar…» (Almacén, Dirección e Informática; la API decide y su 403 se enseña con su mensaje) elige el cierre y pide
    /// el motivo. Sin temporizadores: se lee al enseñarse, tras cerrar y con «Actualizar».
    /// </summary>
    public class BultosPendientesViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string NOMBRE_VISTA = "BultosPendientesView";

        private readonly IServicioBultosPendientes _servicio;
        private readonly IServicioDialogos _dialogos;

        public BultosPendientesViewModel(IServicioBultosPendientes servicio, IServicioDialogos dialogos)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            CargarCommand = new AsyncRelayCommand(CargarAsync, () => !EstaOcupado);
            AbrirCierreCommand = new RelayCommand(AbrirCierre, () => !EstaOcupado && Seleccionado?.EsPendiente == true);
            CerrarBultoCommand = new AsyncRelayCommand(CerrarBultoAsync, () => !EstaOcupado && MostrandoCierre
                && ReglasBultosPendientes.MotivoNoSePuedeCerrar(Seleccionado, CierreElegido, MotivoCierre) == null);
            CancelarCierreCommand = new RelayCommand(() => MostrandoCierre = false);
        }

        public string Titulo => "Bultos pendientes del reparto";

        public IAsyncRelayCommand CargarCommand { get; }
        public RelayCommand AbrirCierreCommand { get; }
        public IAsyncRelayCommand CerrarBultoCommand { get; }
        public RelayCommand CancelarCierreCommand { get; }

        public ObservableCollection<BultoPendienteModel> Bultos { get; } = new();

        /// <summary>Los cierres posibles (código de la API y texto).</summary>
        public IReadOnlyList<KeyValuePair<string, string>> Cierres { get; } =
            ReglasBultosPendientes.CIERRES.Select(c => new KeyValuePair<string, string>(c, ReglasBultosPendientes.NombreCierre(c))).ToList();

        public string Empresa { get; private set; }
        /// <summary>Con pedido, solo los de ese pedido (la pestaña «Entrega»).</summary>
        public int? Pedido { get; private set; }

        private bool _soloPendientes = true;
        public bool SoloPendientes
        {
            get => _soloPendientes;
            set
            {
                if (SetProperty(ref _soloPendientes, value) && !EstaOcupado)
                {
                    _ = CargarAsync();
                }
            }
        }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    Notificar();
                }
            }
        }

        private bool _disponible = true;
        /// <summary>False con 503 o 403 al leer: en la pestaña del pedido no se pinta nada.</summary>
        public bool Disponible { get => _disponible; private set => SetProperty(ref _disponible, value); }

        /// <summary>En la pestaña «Entrega»: solo se ve si hay alguno.</summary>
        public bool HayBultos => Bultos.Any();

        /// <summary>Sin pedido (Agencias, la ventana), siempre; en la pestaña del pedido, solo si hay alguno (con 403/503 o vacío, nada).</summary>
        public bool SeVe => !_deUnPedido || (Disponible && HayBultos);

        /// <summary>Se ha pedido la lista de un pedido (aunque sea uno nuevo, sin número: entonces no se lee nada y no se ve).</summary>
        private bool _deUnPedido;

        private string _mensaje;
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private BultoPendienteModel _seleccionado;
        public BultoPendienteModel Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (SetProperty(ref _seleccionado, value))
                {
                    MostrandoCierre = false;
                    Notificar();
                }
            }
        }

        private bool _mostrandoCierre;
        public bool MostrandoCierre
        {
            get => _mostrandoCierre;
            private set
            {
                if (SetProperty(ref _mostrandoCierre, value))
                {
                    Notificar();
                }
            }
        }

        private string _cierreElegido;
        public string CierreElegido
        {
            get => _cierreElegido;
            set
            {
                if (SetProperty(ref _cierreElegido, value))
                {
                    Notificar();
                }
            }
        }

        private string _motivoCierre;
        public string MotivoCierre
        {
            get => _motivoCierre;
            set
            {
                if (SetProperty(ref _motivoCierre, value))
                {
                    Notificar();
                }
            }
        }

        private void Notificar()
        {
            CargarCommand?.NotifyCanExecuteChanged();
            AbrirCierreCommand?.NotifyCanExecuteChanged();
            CerrarBultoCommand?.NotifyCanExecuteChanged();
        }

        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _ = CargarAsync(null, null);
        }

        /// <summary>Con <paramref name="pedido"/>, solo los de ese pedido (todos, también cerrados, para que se vea su historia).</summary>
        public Task CargarAsync(string empresa, int? pedido)
        {
            Empresa = string.IsNullOrWhiteSpace(empresa) ? null : empresa.Trim();
            Pedido = pedido.HasValue && pedido.Value > 0 ? pedido : null;
            _deUnPedido = pedido.HasValue;
            if (_deUnPedido && !Pedido.HasValue)
            {
                Bultos.Clear(); // pedido nuevo: nada que enseñar
                OnPropertyChanged(nameof(HayBultos));
                OnPropertyChanged(nameof(SeVe));
                return Task.CompletedTask;
            }
            if (Pedido.HasValue)
            {
                _soloPendientes = false;
                OnPropertyChanged(nameof(SoloPendientes));
            }
            return CargarAsync();
        }

        internal async Task CargarAsync()
        {
            EstaOcupado = true;
            try
            {
                List<BultoPendienteModel> lista = await _servicio.Listar(SoloPendientes, Empresa, Pedido, null);
                int? idSeleccionado = Seleccionado?.Id;
                Bultos.Clear();
                foreach (BultoPendienteModel b in lista ?? new List<BultoPendienteModel>())
                {
                    Bultos.Add(b);
                }
                Seleccionado = Bultos.FirstOrDefault(b => b.Id == idSeleccionado);
                Disponible = true;
                Mensaje = Bultos.Any() ? null : (SoloPendientes ? "No hay bultos pendientes." : "No hay bultos pendientes ni cerrados.");
            }
            catch (BultosPendientesApiException ex) when (ex.Estado == 503 || ex.Estado == 403)
            {
                Bultos.Clear();
                Disponible = false;
                Mensaje = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            finally
            {
                EstaOcupado = false;
                OnPropertyChanged(nameof(HayBultos));
                OnPropertyChanged(nameof(SeVe));
            }
        }

        private void AbrirCierre()
        {
            CierreElegido = null;
            MotivoCierre = null;
            MostrandoCierre = true;
        }

        internal async Task CerrarBultoAsync()
        {
            BultoPendienteModel bulto = Seleccionado;
            string motivo = ReglasBultosPendientes.MotivoNoSePuedeCerrar(bulto, CierreElegido, MotivoCierre);
            if (motivo != null)
            {
                _dialogos.ShowError(motivo);
                return;
            }
            EstaOcupado = true;
            try
            {
                await _servicio.Cerrar(bulto.Id, CierreElegido, MotivoCierre);
                MostrandoCierre = false;
                _dialogos.ShowNotification("Bultos pendientes",
                    $"Se ha cerrado el bulto {bulto.Codigo?.Trim()} ({ReglasBultosPendientes.NombreCierre(CierreElegido)}).");
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(ex.Message); // 400/403/404/409 de la API, de usted
                EstaOcupado = false;
                return;
            }
            EstaOcupado = false;
            await CargarAsync();
        }
    }
}
