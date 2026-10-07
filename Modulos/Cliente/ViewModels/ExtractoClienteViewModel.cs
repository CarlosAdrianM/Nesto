using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#419 v1: ventana de Extracto de Cliente (contenedor que irá creciendo). Esta
    /// primera versión: consultar los movimientos pendientes y LIQUIDAR dos entre sí
    /// (NestoAPI#333; la lógica y las validaciones viven en la API, aquí solo se pinta y se
    /// pide). Driver: el paso de revisión de #332 exige poder liquidar antes de remesar.
    /// </summary>
    public class ExtractoClienteViewModel : ObservableObject, IReceptorNavegacion
    {
        private readonly IExtractoClienteService _servicio;
        private readonly IServicioDialogos _dialogService;
        private readonly IMessenger _messenger;
        private readonly Action<string> _abrirFichero;
        private readonly IEventosService _eventos;
        private readonly IConfiguracion _configuracion;

        public ExtractoClienteViewModel(IExtractoClienteService servicio, IServicioDialogos dialogService,
            IMessenger messenger)
            : this(servicio, dialogService, messenger, null)
        {
        }

        // abrirFichero: lo que se hace con el PDF ya en disco (por defecto abrirlo con el visor del
        // sistema); los tests inyectan una captura para no lanzar procesos.
        public ExtractoClienteViewModel(IExtractoClienteService servicio, IServicioDialogos dialogService,
            IMessenger messenger, Action<string> abrirFichero)
            : this(servicio, dialogService, messenger, abrirFichero, null, null)
        {
        }

        // NestoAPI#591: el que usa el contenedor (el más largo que puede resolver): con las señales de eventos.
        public ExtractoClienteViewModel(IExtractoClienteService servicio, IServicioDialogos dialogService,
            IMessenger messenger, IEventosService eventos, IConfiguracion configuracion)
            : this(servicio, dialogService, messenger, null, eventos, configuracion)
        {
        }

        private ExtractoClienteViewModel(IExtractoClienteService servicio, IServicioDialogos dialogService,
            IMessenger messenger, Action<string> abrirFichero, IEventosService eventos, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogService = dialogService;
            _messenger = messenger;
            _abrirFichero = abrirFichero ?? AbrirConElVisorDelSistema;
            _eventos = eventos;
            _configuracion = configuracion;
            Titulo = "Extracto de Cliente";
            CargarCommand = new RelayCommand(OnCargar, CanCargar);
            LiquidarCommand = new RelayCommand(OnLiquidar, CanLiquidar);
            AbrirFacturaCommand = new RelayCommand<ExtractoClienteModel>(OnAbrirFactura, CanAbrirFactura);
            MarcarSenalCommand = new AsyncRelayCommand(MarcarSenalAsync, CanMarcarSenal);
            QuitarSenalCommand = new AsyncRelayCommand(QuitarSenalAsync, CanQuitarSenal);
        }

        public string Titulo { get; }

        // Nesto#419: al navegar aquí desde Remesas (doble clic en un efecto) se recibe el cliente
        // como parámetro y se cargan sus movimientos automáticamente. Se reutiliza la MISMA pestaña de
        // Extracto y se le cambia el cliente, en vez de abrir otra. Nesto#490 (4C.4): llega por
        // IReceptorNavegacion; sin INavigationAware, Prism ya reutiliza la pestaña abierta.
        public void AlLlegar(ParametrosNavegacion parametros)
        {
            string cliente = parametros?.GetValue<string>("cliente");
            if (!string.IsNullOrWhiteSpace(cliente))
            {
                ClienteSeleccionado = cliente.Trim();
                _ = CargarAsync();
            }
        }

        private string _clienteSeleccionado;
        public string ClienteSeleccionado
        {
            get => _clienteSeleccionado;
            set
            {
                if (SetProperty(ref _clienteSeleccionado, value))
                {
                    CargarCommand.NotifyCanExecuteChanged();
                    // Cambiar de cliente invalida lo que hubiera en pantalla
                    Movimientos = new ObservableCollection<ExtractoClienteModel>();
                }
            }
        }

        private ObservableCollection<ExtractoClienteModel> _movimientos = new ObservableCollection<ExtractoClienteModel>();
        public ObservableCollection<ExtractoClienteModel> Movimientos
        {
            get => _movimientos;
            private set
            {
                if (_movimientos != null)
                {
                    foreach (ExtractoClienteModel movimiento in _movimientos)
                    {
                        movimiento.PropertyChanged -= MovimientoCambiado;
                    }
                }
                _ = SetProperty(ref _movimientos, value);
                foreach (ExtractoClienteModel movimiento in _movimientos)
                {
                    movimiento.PropertyChanged += MovimientoCambiado;
                }
                OnPropertyChanged(nameof(TotalPendiente));
                LiquidarCommand.NotifyCanExecuteChanged();
            }
        }

        private void MovimientoCambiado(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ExtractoClienteModel.Seleccionado))
            {
                LiquidarCommand.NotifyCanExecuteChanged();
            }
        }

        public decimal TotalPendiente => Movimientos?.Sum(m => m.ImportePendiente) ?? 0;

        public List<ExtractoClienteModel> Seleccionados =>
            Movimientos?.Where(m => m.Seleccionado).ToList() ?? new List<ExtractoClienteModel>();

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            set => SetProperty(ref _estaOcupado, value);
        }

        public RelayCommand CargarCommand { get; }
        private bool CanCargar() => !string.IsNullOrWhiteSpace(ClienteSeleccionado);
        private async void OnCargar() => await CargarAsync();

        // Function As Task para poder esperarla en los tests (patrón Fase 1C).
        public async Task CargarAsync()
        {
            if (!CanCargar())
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                List<ExtractoClienteModel> movimientos = await _servicio.LeerExtractoPendiente(ClienteSeleccionado);
                Movimientos = new ObservableCollection<ExtractoClienteModel>(movimientos);
                await CargarSenalesAsync();
            }
            catch (Exception ex)
            {
                Movimientos = new ObservableCollection<ExtractoClienteModel>();
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        // Nesto#478: abrir la factura del movimiento (doble clic en el nº de documento o el botón de la
        // fila, activo solo cuando el servidor dice que ese documento es una factura: NestoAPI#492).
        public RelayCommand<ExtractoClienteModel> AbrirFacturaCommand { get; }

        private static bool CanAbrirFactura(ExtractoClienteModel movimiento) => movimiento?.TieneFactura == true;

        private async void OnAbrirFactura(ExtractoClienteModel movimiento) => await AbrirFacturaAsync(movimiento);

        public async Task AbrirFacturaAsync(ExtractoClienteModel movimiento)
        {
            if (!CanAbrirFactura(movimiento))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                byte[] pdf = await _servicio.DescargarFacturaPdf(movimiento.Empresa?.Trim(), movimiento.Documento?.Trim());
                string ruta = Path.Combine(Path.GetTempPath(), NombreFicheroFactura(movimiento.Documento));
                File.WriteAllBytes(ruta, pdf);
                _abrirFichero(ruta);
            }
            catch (Exception ex)
            {
                // Series sin descarga permitida, factura antigua sin PDF, red: se dice, no se revienta.
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public static string NombreFicheroFactura(string documento)
        {
            string limpio = new string((documento ?? string.Empty).Trim().Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c).ToArray());
            return $"Factura_{limpio}.pdf";
        }

        private static void AbrirConElVisorDelSistema(string ruta)
        {
            _ = Process.Start(new ProcessStartInfo(ruta) { UseShellExecute = true });
        }

        public RelayCommand LiquidarCommand { get; }

        // Exactamente dos movimientos marcados; el resto de reglas (mismo cliente, signos
        // opuestos, remesas, estados bloqueados) las valida la API con mensaje claro.
        private bool CanLiquidar() => Seleccionados.Count == 2;

        private async void OnLiquidar() => await LiquidarAsync();

        public async Task LiquidarAsync()
        {
            List<ExtractoClienteModel> seleccionados = Seleccionados;
            if (seleccionados.Count != 2)
            {
                return;
            }
            ExtractoClienteModel origen = seleccionados[0];
            ExtractoClienteModel destino = seleccionados[1];

            if (!string.Equals(origen.Empresa?.Trim(), destino.Empresa?.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _dialogService.ShowError("Los dos movimientos deben ser de la misma empresa " +
                    $"(uno es de la {origen.Empresa?.Trim()} y otro de la {destino.Empresa?.Trim()}).");
                return;
            }

            bool confirmado = _dialogService.ShowConfirmationAnswer("Liquidar movimientos",
                $"¿Liquidar el movimiento {origen.Id} ({origen.ImportePendiente:C} pendiente) " +
                $"contra el {destino.Id} ({destino.ImportePendiente:C} pendiente)?" + Environment.NewLine +
                "Si los importes no coinciden, la diferencia quedará pendiente en el de mayor importe.");
            if (!confirmado)
            {
                return;
            }

            try
            {
                EstaOcupado = true;
                ResultadoLiquidacionModel resultado = await _servicio.LiquidarEfectos(
                    origen.Empresa?.Trim(), origen.Id, destino.Id);
                _dialogService.ShowNotification("Liquidación realizada",
                    $"Movimiento {origen.Id}: quedan {resultado.ImportePdteOrigen:C} pendientes. " +
                    $"Movimiento {destino.Id}: quedan {resultado.ImportePdteDestino:C} pendientes.");
                await CargarAsync(); // refrescar pendientes (los saldados a 0 desaparecen)

                // Avisar a la ventana de Remesas para que actualice EN SITIO esos efectos (sin
                // recargar candidatos, que perdería las marcas del usuario). Se envían los nuevos
                // importes pendientes y si el cliente sigue teniendo negativos (Movimientos ya
                // está refrescado por CargarAsync).
                _messenger?.Send(new EfectosLiquidadosMensaje(new EfectosLiquidadosPayload
                {
                    Empresa = origen.Empresa?.Trim(),
                    Cliente = ClienteSeleccionado?.Trim(),
                    NuevosImportesPendientes = new Dictionary<int, decimal>
                    {
                        [origen.Id] = resultado.ImportePdteOrigen,
                        [destino.Id] = resultado.ImportePdteDestino
                    },
                    ClienteSigueConNegativos = Movimientos.Any(m => m.ImportePendiente < 0)
                }));
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        // ---------------- NestoAPI#591: señales de eventos ----------------

        /// <summary>Solo Administración (y Dirección/Informática) marca y desmarca señales.</summary>
        public bool PuedeMarcarSenales => _eventos != null && _configuracion != null
            && (_configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)
                || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)
                || _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA));

        private ExtractoClienteModel _movimientoSeleccionado;
        public ExtractoClienteModel MovimientoSeleccionado
        {
            get => _movimientoSeleccionado;
            set
            {
                if (SetProperty(ref _movimientoSeleccionado, value))
                {
                    MarcarSenalCommand.NotifyCanExecuteChanged();
                    QuitarSenalCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _avisoSenales;
        /// <summary>Si no se han podido leer las señales del cliente (el extracto se ve igual).</summary>
        public string AvisoSenales
        {
            get => _avisoSenales;
            private set => SetProperty(ref _avisoSenales, value);
        }

        /// <summary>Pinta en cada apunte si es la señal de un evento. Un fallo aquí no tapa el extracto.</summary>
        public async Task CargarSenalesAsync()
        {
            AvisoSenales = null;
            if (_eventos == null || string.IsNullOrWhiteSpace(ClienteSeleccionado))
            {
                return;
            }
            try
            {
                List<SenalEventoModel> senales = await _eventos.LeerSenalesCliente(ClienteSeleccionado.Trim()) ?? new List<SenalEventoModel>();
                foreach (ExtractoClienteModel movimiento in Movimientos)
                {
                    movimiento.Senal = senales.FirstOrDefault(s => s.NumOrdenExtracto == movimiento.Id
                        && string.Equals(s.Empresa?.Trim(), movimiento.Empresa?.Trim(), StringComparison.OrdinalIgnoreCase));
                }
            }
            catch (Exception ex)
            {
                AvisoSenales = $"No se han podido cargar las señales de los eventos: {ex.Message}";
            }
            MarcarSenalCommand.NotifyCanExecuteChanged();
            QuitarSenalCommand.NotifyCanExecuteChanged();
        }

        public AsyncRelayCommand MarcarSenalCommand { get; }

        private bool CanMarcarSenal() => PuedeMarcarSenales && MovimientoSeleccionado != null
            && MovimientoSeleccionado.ImportePendiente < 0 && !MovimientoSeleccionado.EsSenal;

        /// <summary>«Es la señal del evento…»: elegir el evento (activos, los próximos primero) y marcarlo en la API.</summary>
        public async Task MarcarSenalAsync()
        {
            ExtractoClienteModel movimiento = MovimientoSeleccionado;
            if (!CanMarcarSenal())
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                List<EventoModel> eventos = await _eventos.LeerEventos(true) ?? new List<EventoModel>();
                EstaOcupado = false;
                if (!eventos.Any())
                {
                    _dialogService.ShowError("No hay ningún evento activo. Los eventos los da de alta Tienda online (Clientes → Eventos).");
                    return;
                }
                ResultadoDialogo resultado = await _dialogService.ShowDialogAsync(ElegirEventoDialogViewModel.NOMBRE, new ParametrosDialogo
                {
                    { "eventos", eventos },
                    { "apunte", $"{movimiento.Id} ({-movimiento.ImportePendiente:C} a favor, {movimiento.Concepto?.Trim()})" }
                });
                if (resultado?.Result != ResultadoBoton.OK || resultado.Parameters == null || !resultado.Parameters.ContainsKey("eventoId"))
                {
                    return;
                }
                int eventoId = resultado.Parameters.GetValue<int>("eventoId");
                EstaOcupado = true;
                SenalEventoModel senal = await _eventos.MarcarSenal(eventoId, new MarcarSenalEventoModel
                {
                    Empresa = movimiento.Empresa?.Trim(),
                    NumOrdenExtracto = movimiento.Id,
                    Cliente = movimiento.Cliente?.Trim(),
                    Contacto = movimiento.Contacto?.Trim()
                });
                movimiento.Senal = senal;
                MarcarSenalCommand.NotifyCanExecuteChanged();
                QuitarSenalCommand.NotifyCanExecuteChanged();
                _dialogService.ShowNotification("Señal marcada",
                    $"El apunte {movimiento.Id} es la señal del evento «{senal?.Evento}» del {senal?.FechaEvento:dd/MM/yyyy}.");
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        public AsyncRelayCommand QuitarSenalCommand { get; }

        private bool CanQuitarSenal() => PuedeMarcarSenales && MovimientoSeleccionado?.EsSenal == true;

        public async Task QuitarSenalAsync()
        {
            ExtractoClienteModel movimiento = MovimientoSeleccionado;
            if (!CanQuitarSenal())
            {
                return;
            }
            if (!_dialogService.ShowConfirmationAnswer("Quitar señal",
                $"¿Quitar la señal del evento «{movimiento.Senal.Evento}» del apunte {movimiento.Id}? " +
                "El cobro sigue a favor del cliente; solo deja de estar reservado para el evento."))
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                await _eventos.QuitarSenal(movimiento.Senal.Id);
                movimiento.Senal = null;
                MarcarSenalCommand.NotifyCanExecuteChanged();
                QuitarSenalCommand.NotifyCanExecuteChanged();
            }
            catch (Exception ex)
            {
                _dialogService.ShowError(ex.Message);
            }
            finally
            {
                EstaOcupado = false;
            }
        }
    }
}
