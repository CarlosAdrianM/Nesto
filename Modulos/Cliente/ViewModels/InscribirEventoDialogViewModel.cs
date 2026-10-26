using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#517 (NestoAPI#611): inscribe a un cliente en un evento, sin pedido. Se abre desde el mantenimiento de eventos
    /// (con «eventoId») o desde la ficha del cliente (con «empresa», «cliente», «contacto» y «nombre»). Cómo va a pagar:
    /// con tarjeta se genera el enlace de pago (opcional), por transferencia o en efectivo en la tienda; en los dos últimos la
    /// inscripción se queda Reservada hasta que alguien la marque como pagada. Tras inscribir se queda abierto enseñando el
    /// enlace para copiarlo, y al cerrar devuelve «inscripcion» (InscripcionEventoModel).
    /// </summary>
    public class InscribirEventoDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "InscribirEventoDialog";

        private readonly IEventosService _servicio;

        public InscribirEventoDialogViewModel(IEventosService servicio)
        {
            _servicio = servicio;
            Title = "Inscribir en un evento";
            Origenes = new List<OpcionEvento>
            {
                new(InscribirEventoModel.ORIGEN_NESTO, "Nesto (en la tienda o la oficina)"),
                new(InscribirEventoModel.ORIGEN_TELEFONO, "Teléfono")
            };
            FormasPago = new List<OpcionEvento>
            {
                new(MarcarPagoInscripcionModel.TARJETA, "Tarjeta (enlace de pago)"),
                new(MarcarPagoInscripcionModel.TRANSFERENCIA, "Transferencia"),
                new(MarcarPagoInscripcionModel.EFECTIVO, "Efectivo en la tienda")
            };
            _origen = Origenes[0];
            _formaPago = FormasPago[0];
            InscribirCommand = new AsyncRelayCommand(InscribirAsync, PuedeInscribir);
            CerrarCommand = new RelayCommand(Cerrar);
        }

        public List<OpcionEvento> Origenes { get; }
        public List<OpcionEvento> FormasPago { get; }

        public ObservableCollection<EventoModel> Eventos { get; } = new();

        private EventoModel _evento;
        public EventoModel Evento
        {
            get => _evento;
            set
            {
                if (SetProperty(ref _evento, value))
                {
                    PonerModalidades();
                    OnPropertyChanged(nameof(TextoEvento));
                    InscribirCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>«Señal: 50,00 € · Plazas: 3 / 12 · Reina».</summary>
        public string TextoEvento => Evento == null
            ? string.Empty
            : $"Señal: {Evento.TextoSenales} · Plazas: {Evento.TextoPlazas}"
              + (string.IsNullOrWhiteSpace(Evento.TextoHorario) ? string.Empty : $" · {Evento.TextoHorario}")
              + (string.IsNullOrWhiteSpace(Evento.NombreTienda) ? string.Empty : $" · {Evento.NombreTienda}");

        // NestoAPI#690 (contrato provisional): la modalidad fija lo que se cobra y si hay cupón.

        private List<OpcionEvento> _modalidades = new();
        public List<OpcionEvento> Modalidades { get => _modalidades; private set => SetProperty(ref _modalidades, value); }

        private OpcionEvento _modalidad;
        public OpcionEvento Modalidad
        {
            get => _modalidad;
            set
            {
                if (SetProperty(ref _modalidad, value))
                {
                    OnPropertyChanged(nameof(EsGratuita));
                    OnPropertyChanged(nameof(HayQuePagar));
                    OnPropertyChanged(nameof(EsTarjeta));
                    OnPropertyChanged(nameof(AvisoFormaPago));
                }
            }
        }

        /// <summary>Solo se elige si el evento tiene más de una (la API de antes de #690 solo conoce la reembolsable).</summary>
        public bool HayVariasModalidades => Modalidades.Count > 1;

        public bool EsGratuita => Modalidad?.Valor == InscribirEventoModel.MODALIDAD_GRATUITA;
        public bool HayQuePagar => !EsGratuita;

        /// <summary>
        /// Las que manda la API en EventoDTO.Modalidades (NestoAPI#690). Con la API de antes, que no las manda: gratuito, solo
        /// Gratuita; si no, la reembolsable y, si la tiene, la no reembolsable.
        /// </summary>
        public static List<OpcionEvento> ModalidadesDe(EventoModel evento)
        {
            var lista = new List<OpcionEvento>();
            if (evento == null)
            {
                return lista;
            }
            IEnumerable<string> valores = evento.Modalidades != null && evento.Modalidades.Any()
                ? evento.Modalidades
                : evento.EsGratuito
                    ? new[] { InscribirEventoModel.MODALIDAD_GRATUITA }
                    : evento.ImporteSenalNoReembolsable > 0
                        ? new[] { InscribirEventoModel.MODALIDAD_REEMBOLSABLE, InscribirEventoModel.MODALIDAD_NO_REEMBOLSABLE }
                        : new[] { InscribirEventoModel.MODALIDAD_REEMBOLSABLE };
            foreach (string valor in valores)
            {
                switch (valor)
                {
                    case InscribirEventoModel.MODALIDAD_GRATUITA:
                        lista.Add(new OpcionEvento(valor, "Gratuita (sin señal)"));
                        break;
                    case InscribirEventoModel.MODALIDAD_NO_REEMBOLSABLE:
                        lista.Add(new OpcionEvento(valor, $"No reembolsable: {evento.ImporteSenalNoReembolsable ?? 0:C}, sin cupón"));
                        break;
                    default:
                        lista.Add(new OpcionEvento(valor, $"Reembolsable: {evento.ImporteSenal:C}, con cupón por el mismo importe"));
                        break;
                }
            }
            return lista;
        }

        private void PonerModalidades()
        {
            Modalidades = ModalidadesDe(Evento);
            Modalidad = Modalidades.FirstOrDefault();
            OnPropertyChanged(nameof(HayVariasModalidades));
        }

        private string _empresa = Constantes.Empresas.EMPRESA_DEFECTO;
        public string Empresa { get => _empresa; set => SetProperty(ref _empresa, value); }

        private string _cliente;
        public string Cliente
        {
            get => _cliente;
            set
            {
                if (SetProperty(ref _cliente, value))
                {
                    InscribirCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private string _contacto;
        public string Contacto { get => _contacto; set => SetProperty(ref _contacto, value); }

        private string _nombreCliente;
        public string NombreCliente { get => _nombreCliente; private set => SetProperty(ref _nombreCliente, value); }

        /// <summary>Desde la ficha el cliente viene dado y no se cambia.</summary>
        private bool _clienteFijo;
        public bool ClienteFijo { get => _clienteFijo; private set => SetProperty(ref _clienteFijo, value); }

        private OpcionEvento _origen;
        public OpcionEvento Origen { get => _origen; set => SetProperty(ref _origen, value); }

        private OpcionEvento _formaPago;
        public OpcionEvento FormaPago
        {
            get => _formaPago;
            set
            {
                if (SetProperty(ref _formaPago, value))
                {
                    OnPropertyChanged(nameof(EsTarjeta));
                    OnPropertyChanged(nameof(AvisoFormaPago));
                }
            }
        }

        public bool EsTarjeta => !EsGratuita && FormaPago?.Valor == MarcarPagoInscripcionModel.TARJETA;

        private bool _generarEnlacePago = true;
        public bool GenerarEnlacePago { get => _generarEnlacePago; set => SetProperty(ref _generarEnlacePago, value); }

        private string _correo;
        /// <summary>Opcional: si se pone, la API manda el enlace de pago a ese correo.</summary>
        public string Correo { get => _correo; set => SetProperty(ref _correo, value); }

        public string AvisoFormaPago => EsGratuita
            ? "Evento gratuito: el cliente no paga nada y pasa directamente a la lista de asistencia."
            : FormaPago?.Valor switch
        {
            MarcarPagoInscripcionModel.TRANSFERENCIA => "La inscripción queda reservada hasta que Administración marque la transferencia como señal del evento, o hasta que se marque como pagada.",
            MarcarPagoInscripcionModel.EFECTIVO => "La inscripción queda reservada hasta que se marque como pagada. El cobro se mete en la caja de la tienda, como cualquier otro cobro en efectivo.",
            _ => "Con el enlace, la inscripción pasa sola a pagada cuando el cliente paga. Hay que pagarla antes de que falten para el evento los días que este marque (7 por defecto): se avisa antes de que venza y, si no, la plaza se libera sola y queda como «Caducada»."
        };

        private string _mensaje;
        /// <summary>El error de la API, a la vista en el propio diálogo.</summary>
        public string Mensaje { get => _mensaje; private set => SetProperty(ref _mensaje, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    InscribirCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private InscripcionEventoModel _inscripcion;
        /// <summary>La inscripción creada (null hasta inscribir).</summary>
        public InscripcionEventoModel Inscripcion
        {
            get => _inscripcion;
            private set
            {
                if (SetProperty(ref _inscripcion, value))
                {
                    OnPropertyChanged(nameof(Inscrito));
                    OnPropertyChanged(nameof(SinInscribir));
                    OnPropertyChanged(nameof(TextoInscrito));
                    OnPropertyChanged(nameof(TieneEnlace));
                    OnPropertyChanged(nameof(HayAvisos));
                    OnPropertyChanged(nameof(TextoAvisos));
                    InscribirCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool Inscrito => Inscripcion != null;
        public bool SinInscribir => Inscripcion == null;
        public bool TieneEnlace => !string.IsNullOrWhiteSpace(Inscripcion?.UrlPago);

        /// <summary>
        /// Sugerencia 776: lo que avisa la API al inscribir (p. ej. que a la ficha le falta el teléfono o el correo). No impide
        /// nada: la inscripción ya está hecha; se enseña en el propio diálogo para que completen la ficha.
        /// </summary>
        public IReadOnlyList<string> Avisos => Inscripcion?.Avisos?
            .Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToList() ?? new List<string>();

        public bool HayAvisos => Avisos.Any();

        public string TextoAvisos => string.Join(Environment.NewLine, Avisos);

        public string TextoInscrito => Inscripcion == null
            ? string.Empty
            : $"Inscrito en «{Inscripcion.Evento}» del {Inscripcion.FechaEvento:dd/MM/yyyy}: {Inscripcion.EstadoTexto ?? Inscripcion.Estado}" +
              (Inscripcion.FechaLimitePago.HasValue ? $", se puede pagar hasta el {Inscripcion.FechaLimitePago.Value:dd/MM/yyyy}." : ".") +
              (TieneEnlace ? " Copie el enlace de pago y envíeselo al cliente:" : string.Empty);

        public AsyncRelayCommand InscribirCommand { get; }
        public RelayCommand CerrarCommand { get; }

        private bool PuedeInscribir() => !EstaOcupado && !Inscrito && Evento != null && !string.IsNullOrWhiteSpace(Cliente);

        public override void OnDialogOpened(ParametrosDialogo parameters)
        {
            if (parameters.ContainsKey("cliente") && !string.IsNullOrWhiteSpace(parameters.GetValue<string>("cliente")))
            {
                Cliente = parameters.GetValue<string>("cliente").Trim();
                Contacto = parameters.ContainsKey("contacto") ? parameters.GetValue<string>("contacto")?.Trim() : null;
                Empresa = parameters.ContainsKey("empresa") && !string.IsNullOrWhiteSpace(parameters.GetValue<string>("empresa"))
                    ? parameters.GetValue<string>("empresa").Trim()
                    : Constantes.Empresas.EMPRESA_DEFECTO;
                NombreCliente = parameters.ContainsKey("nombre") ? parameters.GetValue<string>("nombre")?.Trim() : null;
                ClienteFijo = true;
            }
            int? eventoId = parameters.ContainsKey("eventoId") ? parameters.GetValue<int>("eventoId") : null;
            _ = CargarEventosAsync(eventoId);
        }

        /// <summary>Los eventos activos que todavía no han pasado (a uno pasado la API no deja inscribir).</summary>
        public async Task CargarEventosAsync(int? eventoId)
        {
            try
            {
                EstaOcupado = true;
                Mensaje = null;
                List<EventoModel> eventos = await _servicio.LeerEventos(true) ?? new List<EventoModel>();
                Eventos.Clear();
                foreach (EventoModel evento in eventos.Where(e => e.Fecha.Date >= DateTime.Today || e.Id == eventoId))
                {
                    Eventos.Add(evento);
                }
                Evento = Eventos.FirstOrDefault(e => e.Id == eventoId) ?? Eventos.FirstOrDefault();
                if (!Eventos.Any())
                {
                    Mensaje = "No hay ningún evento activo próximo. Los eventos los da de alta cada tienda en Clientes → Eventos.";
                }
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

        public async Task InscribirAsync()
        {
            if (!PuedeInscribir())
            {
                return;
            }
            try
            {
                EstaOcupado = true;
                Mensaje = null;
                Inscripcion = await _servicio.Inscribir(Evento.Id, new InscribirEventoModel
                {
                    Empresa = string.IsNullOrWhiteSpace(Empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : Empresa.Trim(),
                    Cliente = Cliente.Trim(),
                    Contacto = string.IsNullOrWhiteSpace(Contacto) ? null : Contacto.Trim(),
                    Origen = Origen?.Valor ?? InscribirEventoModel.ORIGEN_NESTO,
                    FormaPago = EsGratuita ? null : FormaPago?.Valor,
                    // Con la API de antes de #690 (el evento no trae Modalidades) no se manda la modalidad.
                    Modalidad = Evento.Modalidades != null && Evento.Modalidades.Any() ? Modalidad?.Valor : null,
                    GenerarEnlacePago = EsTarjeta && GenerarEnlacePago,
                    Correo = EsTarjeta && GenerarEnlacePago && !string.IsNullOrWhiteSpace(Correo) ? Correo.Trim() : null
                });
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
            if (Inscripcion == null)
            {
                RaiseRequestClose(ResultadoBoton.Cancel);
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "inscripcion", Inscripcion } }));
        }
    }
}
