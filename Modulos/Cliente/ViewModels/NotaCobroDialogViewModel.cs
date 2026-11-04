using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#618 (NestoAPI#786): «Nueva nota de cobro». Una nota de solo COBRO para el reparto propio: el repartidor pasa por el
    /// cliente a cobrar, sin llevar nada (no pasa por picking ni packing). Se abre desde el detalle del pedido y desde la ficha del
    /// cliente con «empresa», «cliente», «contacto» y «nombre» propuestos. Se marcan los efectos del extracto pendiente (empresas 1
    /// y 3) en el orden en que se quieren cobrar; el importe se propone como la suma de lo pendiente de lo marcado (se puede cambiar,
    /// o dar solo importe sin efectos); el día (por defecto hoy) y un comentario para el repartidor. «Crear» → POST
    /// api/NotasEntrega/Cobro. Los errores de la API se enseñan tal cual. Al cerrar tras crear devuelve «nota» (NotaCobroCreadaModel).
    /// </summary>
    public class NotaCobroDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = PermisosNotasEntrega.DIALOGO_NOTA_COBRO;

        /// <summary>La API no deja más.</summary>
        public const int MAXIMO_EFECTOS = 50;
        public const int MAXIMO_COMENTARIO = 500;

        private static readonly CultureInfo ES = CultureInfo.GetCultureInfo("es-ES");

        private readonly IExtractoClienteService _extractos;
        private readonly INotaCobroService _servicio;
        private readonly List<EfectoACobrarModel> _marcados = new();

        public NotaCobroDialogViewModel(IExtractoClienteService extractos, INotaCobroService servicio)
        {
            _extractos = extractos;
            _servicio = servicio;
            Title = "Nueva nota de cobro";
            CrearCommand = new AsyncRelayCommand(CrearAsync, PuedeCrear);
            CerrarCommand = new RelayCommand(Cerrar);
            RecargarCommand = new AsyncRelayCommand(CargarExtractoAsync, () => !EstaOcupado && !Creada);
        }

        public AsyncRelayCommand CrearCommand { get; }
        public RelayCommand CerrarCommand { get; }
        public AsyncRelayCommand RecargarCommand { get; }

        // ---------------- Cliente y contacto ----------------

        private string _cliente;
        public string Cliente
        {
            get => _cliente;
            set
            {
                if (SetProperty(ref _cliente, value))
                {
                    NotificarCrear();
                    ProgramarCargaExtracto();
                }
            }
        }

        private string _contacto;
        /// <summary>A qué dirección va la parada. Vacío: el del primer efecto.</summary>
        public string Contacto { get => _contacto; set => SetProperty(ref _contacto, value); }

        private string _nombreCliente;
        public string NombreCliente { get => _nombreCliente; private set => SetProperty(ref _nombreCliente, value); }

        // ---------------- Extracto pendiente ----------------

        /// <summary>Lo pendiente del cliente (empresas 1 y 3), por vencimiento.</summary>
        public ObservableCollection<EfectoACobrarModel> Efectos { get; } = new();

        /// <summary>Los marcados, en el orden en que se marcaron (el orden en que se liquidan).</summary>
        public IReadOnlyList<EfectoACobrarModel> EfectosMarcados => _marcados.AsReadOnly();

        /// <summary>La suma de lo pendiente de lo marcado.</summary>
        public decimal ImporteMarcado => _marcados.Sum(e => e.ImportePendiente);

        public string TextoMarcados => _marcados.Count == 0
            ? "Sin efectos marcados: la nota es solo por el importe (al cobrarla, va a cuenta)."
            : $"{_marcados.Count} efecto{(_marcados.Count == 1 ? string.Empty : "s")} marcado{(_marcados.Count == 1 ? string.Empty : "s")}: {ImporteMarcado.ToString("N2", ES)} € pendientes. Se cobran en el orden en que los ha marcado.";

        /// <summary>Se espera un poco tras teclear el cliente para no pedir el extracto en cada tecla.</summary>
        public int EsperaCargaMs { get; set; } = 500;

        private int _versionExtracto;

        private void ProgramarCargaExtracto()
        {
            int version = ++_versionExtracto;
            _ = EsperarYCargarAsync(version);
        }

        private async Task EsperarYCargarAsync(int version)
        {
            if (EsperaCargaMs > 0)
            {
                await Task.Delay(EsperaCargaMs);
            }
            if (version == _versionExtracto)
            {
                await CargarExtractoAsync();
            }
        }

        /// <summary>GET api/ExtractosCliente?cliente= (lo pendiente, de todas las empresas). Quita lo marcado.</summary>
        public async Task CargarExtractoAsync()
        {
            int version = ++_versionExtracto;
            string cliente = Cliente?.Trim();
            List<ExtractoClienteModel> movimientos = new();
            string error = null;
            if (!string.IsNullOrWhiteSpace(cliente))
            {
                try
                {
                    EstaOcupado = true;
                    movimientos = await _extractos.LeerExtractoPendiente(cliente) ?? new List<ExtractoClienteModel>();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
                finally
                {
                    EstaOcupado = false;
                }
            }
            if (version != _versionExtracto)
            {
                return; // Han cambiado el cliente mientras se leía.
            }
            foreach (EfectoACobrarModel efecto in Efectos)
            {
                efecto.PropertyChanged -= AlCambiarEfecto;
            }
            Efectos.Clear();
            _marcados.Clear();
            foreach (ExtractoClienteModel movimiento in movimientos
                .Where(m => m != null && m.ImportePendiente != 0)
                .OrderBy(m => m.Vencimiento ?? m.Fecha).ThenBy(m => m.Id))
            {
                var efecto = new EfectoACobrarModel(movimiento);
                efecto.PropertyChanged += AlCambiarEfecto;
                Efectos.Add(efecto);
            }
            Mensaje = error;
            AlCambiarMarcados(proponerImporte: false);
        }

        private void AlCambiarEfecto(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(EfectoACobrarModel.Marcado) || sender is not EfectoACobrarModel efecto)
            {
                return;
            }
            if (efecto.Marcado && !_marcados.Contains(efecto))
            {
                _marcados.Add(efecto);
            }
            else if (!efecto.Marcado)
            {
                _ = _marcados.Remove(efecto);
            }
            AlCambiarMarcados(proponerImporte: true);
        }

        private void AlCambiarMarcados(bool proponerImporte)
        {
            for (int i = 0; i < _marcados.Count; i++)
            {
                _marcados[i].Orden = i + 1;
            }
            foreach (EfectoACobrarModel efecto in Efectos.Where(e => !e.Marcado))
            {
                efecto.Orden = 0;
            }
            if (proponerImporte)
            {
                // El importe propuesto es lo pendiente de lo marcado; si se desmarca todo, se queda vacío para teclearlo.
                Importe = _marcados.Count == 0 ? null : ImporteMarcado;
            }
            OnPropertyChanged(nameof(EfectosMarcados));
            OnPropertyChanged(nameof(ImporteMarcado));
            OnPropertyChanged(nameof(TextoMarcados));
            NotificarCrear();
        }

        // ---------------- Importe, fecha y comentario ----------------

        private decimal? _importe;
        /// <summary>Lo que hay que cobrar. Por defecto, la suma de lo pendiente de lo marcado; se puede cambiar.</summary>
        public decimal? Importe
        {
            get => _importe;
            set
            {
                if (SetProperty(ref _importe, value))
                {
                    NotificarCrear();
                }
            }
        }

        private DateTime? _fecha = DateTime.Today;
        /// <summary>El día que se va a cobrar (desde ese día sale en el reparto). Por defecto, hoy.</summary>
        public DateTime? Fecha
        {
            get => _fecha;
            set
            {
                if (SetProperty(ref _fecha, value))
                {
                    NotificarCrear();
                }
            }
        }

        public DateTime Hoy => DateTime.Today;

        private string _comentario;
        /// <summary>Para el repartidor (va en las observaciones de la parada).</summary>
        public string Comentario { get => _comentario; set => SetProperty(ref _comentario, value); }

        // ---------------- Estado ----------------

        private string _mensaje;
        /// <summary>El error (de la API, tal cual), a la vista en el propio diálogo.</summary>
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    NotificarCrear();
                }
            }
        }

        private NotaCobroCreadaModel _notaCreada;
        /// <summary>La nota creada (null hasta crearla).</summary>
        public NotaCobroCreadaModel NotaCreada
        {
            get => _notaCreada;
            private set
            {
                if (SetProperty(ref _notaCreada, value))
                {
                    OnPropertyChanged(nameof(Creada));
                    OnPropertyChanged(nameof(SinCrear));
                    OnPropertyChanged(nameof(TextoCreada));
                    NotificarCrear();
                }
            }
        }

        public bool Creada => NotaCreada != null;
        public bool SinCrear => NotaCreada == null;

        /// <summary>«Nota de cobro NC929700 creada: parada en el reparto del 06/11/26 por 134,16 €».</summary>
        public string TextoCreada => NotaCreada == null
            ? string.Empty
            : $"Nota de cobro {CodigoDocumento(NotaCreada)} creada: parada en el reparto del {NotaCreada.Fecha.ToString("dd/MM/yy", ES)} por {NotaCreada.Importe.ToString("N2", ES)} €";

        private static string CodigoDocumento(NotaCobroCreadaModel nota)
            => string.IsNullOrWhiteSpace(nota.CodigoDocumento) ? $"NC{nota.Pedido}" : nota.CodigoDocumento.Trim();

        /// <summary>Por qué no se puede crear todavía (vacío si se puede).</summary>
        public string MotivoNoCrear
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Cliente))
                {
                    return "Indique el cliente.";
                }
                if (_marcados.Count == 0 && (!Importe.HasValue || Importe.Value <= 0))
                {
                    return "Marque los efectos a cobrar o escriba el importe.";
                }
                if (Importe.HasValue && Importe.Value <= 0)
                {
                    return "El importe tiene que ser mayor que cero.";
                }
                if (_marcados.Count > MAXIMO_EFECTOS)
                {
                    return $"Como mucho se pueden cobrar {MAXIMO_EFECTOS} efectos en una nota.";
                }
                if (Fecha.HasValue && Fecha.Value.Date < DateTime.Today)
                {
                    return "La fecha no puede ser anterior a hoy.";
                }
                return string.Empty;
            }
        }

        private bool PuedeCrear() => !EstaOcupado && !Creada && string.IsNullOrEmpty(MotivoNoCrear);

        private void NotificarCrear()
        {
            OnPropertyChanged(nameof(MotivoNoCrear));
            CrearCommand?.NotifyCanExecuteChanged();
            RecargarCommand?.NotifyCanExecuteChanged();
        }

        /// <summary>Lo que se manda a la API: los efectos en el orden en que se marcaron.</summary>
        public CrearNotaCobroModel ConstruirPeticion() => new()
        {
            Empresa = Constantes.Empresas.EMPRESA_DEFECTO,
            Cliente = Cliente?.Trim(),
            Contacto = string.IsNullOrWhiteSpace(Contacto) ? null : Contacto.Trim(),
            Efectos = _marcados.Select(e => e.Id).ToList(),
            Importe = Importe,
            Fecha = Fecha?.Date,
            Comentario = string.IsNullOrWhiteSpace(Comentario)
                ? null
                : Comentario.Trim().Length > MAXIMO_COMENTARIO ? Comentario.Trim().Substring(0, MAXIMO_COMENTARIO) : Comentario.Trim()
        };

        /// <summary>POST api/NotasEntrega/Cobro. Si la API dice que no, su mensaje tal cual en <see cref="Mensaje"/>.</summary>
        public async Task CrearAsync()
        {
            if (!PuedeCrear())
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                Mensaje = null;
                NotaCreada = await _servicio.CrearNotaCobro(ConstruirPeticion());
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
            }
            finally
            {
                EstaOcupado = false;
            }
        }

        private void Cerrar()
        {
            if (Creada)
            {
                RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "nota", NotaCreada } }));
            }
            else
            {
                RaiseRequestClose(ResultadoBoton.Cancel);
            }
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            _ = AbrirAsync(
                parameters.GetValue<string>("cliente"),
                parameters.GetValue<string>("contacto"),
                parameters.GetValue<string>("nombre"));
        }

        /// <summary>Pone el cliente y el contacto propuestos y lee su extracto pendiente.</summary>
        public async Task AbrirAsync(string cliente, string contacto, string nombre)
        {
            // Sin pasar por el setter de Cliente, para no programar otra lectura.
            _cliente = cliente?.Trim();
            OnPropertyChanged(nameof(Cliente));
            Contacto = contacto?.Trim();
            NombreCliente = nombre?.Trim();
            NotificarCrear();
            await CargarExtractoAsync();
        }
    }
}
