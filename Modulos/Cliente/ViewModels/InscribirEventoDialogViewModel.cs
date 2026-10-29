using CommunityToolkit.Mvvm.Input;
using ControlesUsuario;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
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
    /// <para>Carlos 29/10/26: el cliente y el contacto con el selector de clientes de Nesto; el correo se elige entre los de las
    /// personas de contacto (SelectorCorreo) o se escribe; y, lo más importante, el enlace de pago se copia solo al portapapeles
    /// al inscribir y no se puede cerrar el diálogo sin haberlo copiado (al segundo intento sí, avisado: queda en los inscritos).
    /// Cerrar con la X después de inscribir devuelve también la inscripción.</para>
    /// </summary>
    public class InscribirEventoDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "InscribirEventoDialog";

        private readonly IEventosService _servicio;
        private readonly IServicioPortapapeles _portapapeles;

        public InscribirEventoDialogViewModel(IEventosService servicio, IServicioPortapapeles portapapeles = null)
        {
            _servicio = servicio;
            _portapapeles = portapapeles ?? new ServicioPortapapeles();
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
            CopiarEnlaceCommand = new RelayCommand(CopiarEnlace, () => TieneEnlace);
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
                    // NestoAPI#741: con una modalidad de pago, el enlace viene marcado (el pago lo confirma todo solo).
                    if (value != null && value.Valor != InscribirEventoModel.MODALIDAD_GRATUITA)
                    {
                        GenerarEnlacePago = true;
                    }
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
                    ProgramarCargaPersonas();
                }
            }
        }

        private string _contacto;
        public string Contacto
        {
            get => _contacto;
            set
            {
                if (SetProperty(ref _contacto, value))
                {
                    ProgramarCargaPersonas();
                }
            }
        }

        // ---------------- Nesto#566 (NestoAPI#731): la persona que asiste (opcional) ----------------

        /// <summary>«(sin indicar)» y las personas de contacto del cliente (de su contacto, si se ha escrito).</summary>
        public ObservableCollection<PersonaContactoEventoModel> Personas { get; } = new() { InscripcionEventoModel.SIN_PERSONA };

        private PersonaContactoEventoModel _personaAsiste = InscripcionEventoModel.SIN_PERSONA;
        /// <summary>La persona que asiste; «(sin indicar)» = no se manda.</summary>
        public PersonaContactoEventoModel PersonaAsiste
        {
            get => _personaAsiste;
            set
            {
                if (SetProperty(ref _personaAsiste, value ?? InscripcionEventoModel.SIN_PERSONA))
                {
                    ActualizarCorreos();
                }
            }
        }

        /// <summary>
        /// Los correos de las personas de contacto que se ofrecen en el SelectorCorreo; el de la persona que asiste, el primero
        /// (el selector lo pone solo si el usuario no ha escrito otro).
        /// </summary>
        public ObservableCollection<string> Correos { get; } = new();

        private void ActualizarCorreos()
        {
            List<string> correos = Personas
                .Where(p => p != null && p != InscripcionEventoModel.SIN_PERSONA && !string.IsNullOrWhiteSpace(p.Correo))
                .OrderBy(p => p == PersonaAsiste ? 0 : 1)
                .Select(p => p.Correo.Trim())
                .ToList();
            if (correos.SequenceEqual(Correos))
            {
                return;
            }
            Correos.Clear();
            foreach (string correo in correos)
            {
                Correos.Add(correo);
            }
        }

        /// <summary>Se espera un poco tras teclear el cliente o el contacto para no pedir las personas en cada tecla.</summary>
        public int EsperaCargaPersonasMs { get; set; } = 400;

        private int _versionPersonas;

        private void ProgramarCargaPersonas()
        {
            int version = ++_versionPersonas;
            _ = EsperarYCargarPersonasAsync(version);
        }

        private async Task EsperarYCargarPersonasAsync(int version)
        {
            if (EsperaCargaPersonasMs > 0)
            {
                await Task.Delay(EsperaCargaPersonasMs);
            }
            if (version == _versionPersonas)
            {
                await CargarPersonasAsync();
            }
        }

        /// <summary>
        /// Lee las personas de contacto del cliente (GET api/Eventos/PersonasContacto). Un fallo no molesta: el combo se queda
        /// en «(sin indicar)» y se puede inscribir igual.
        /// </summary>
        public async Task CargarPersonasAsync()
        {
            int version = _versionPersonas;
            string numeroAnterior = PersonaAsiste?.Numero;
            List<PersonaContactoEventoModel> personas = new();
            if (!string.IsNullOrWhiteSpace(Cliente))
            {
                try
                {
                    personas = await _servicio.LeerPersonasContacto(Empresa, Cliente, Contacto) ?? new List<PersonaContactoEventoModel>();
                }
                catch
                {
                    personas = new List<PersonaContactoEventoModel>();
                }
            }
            if (version != _versionPersonas)
            {
                return; // Han cambiado el cliente o el contacto mientras se leía.
            }
            Personas.Clear();
            Personas.Add(InscripcionEventoModel.SIN_PERSONA);
            foreach (PersonaContactoEventoModel persona in personas.Where(p => p != null && !string.IsNullOrWhiteSpace(p.Numero)))
            {
                Personas.Add(persona);
            }
            PersonaAsiste = Personas.FirstOrDefault(p => !string.IsNullOrEmpty(numeroAnterior) && p.Numero?.Trim() == numeroAnterior.Trim())
                ?? InscripcionEventoModel.SIN_PERSONA;
            ActualizarCorreos();
        }

        private string _nombreCliente;
        public string NombreCliente { get => _nombreCliente; private set => SetProperty(ref _nombreCliente, value); }

        /// <summary>Desde la ficha el cliente viene dado y no se cambia.</summary>
        private bool _clienteFijo;
        public bool ClienteFijo
        {
            get => _clienteFijo;
            private set
            {
                if (SetProperty(ref _clienteFijo, value))
                {
                    OnPropertyChanged(nameof(ClienteElegible));
                    OnPropertyChanged(nameof(TextoClienteFijo));
                }
            }
        }

        /// <summary>Desde el mantenimiento: se busca con el selector de clientes de Nesto (por nombre, número, NIF…).</summary>
        public bool ClienteElegible => !ClienteFijo;

        /// <summary>«15191/0 · NOMBRE DEL CLIENTE» (desde la ficha).</summary>
        public string TextoClienteFijo => !ClienteFijo
            ? string.Empty
            : $"{Cliente?.Trim()}/{(string.IsNullOrWhiteSpace(Contacto) ? "(principal)" : Contacto.Trim())}"
              + (string.IsNullOrWhiteSpace(NombreCliente) ? string.Empty : $" · {NombreCliente}");

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
                    InscribirCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool EsTarjeta => !EsGratuita && FormaPago?.Valor == MarcarPagoInscripcionModel.TARJETA;

        private bool _generarEnlacePago = true;
        public bool GenerarEnlacePago
        {
            get => _generarEnlacePago;
            set
            {
                if (SetProperty(ref _generarEnlacePago, value))
                {
                    InscribirCommand?.NotifyCanExecuteChanged();
                }
            }
        }

        private string _correo;
        /// <summary>Opcional: si se pone, la API manda el enlace de pago a ese correo.</summary>
        public string Correo
        {
            get => _correo;
            set
            {
                if (SetProperty(ref _correo, value))
                {
                    OnPropertyChanged(nameof(CorreoValido));
                    InscribirCommand.NotifyCanExecuteChanged();
                }
            }
        }

        /// <summary>Vacío o con forma de correo (el mismo criterio que el SelectorCorreo).</summary>
        public bool CorreoValido => CorreosElegibles.EsValido(Correo);

        /// <summary>El correo solo se manda con tarjeta y enlace; si no, da igual lo que ponga.</summary>
        private bool SeMandaCorreo => EsTarjeta && GenerarEnlacePago;

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
                    OnPropertyChanged(nameof(TextoEnlace));
                    OnPropertyChanged(nameof(HayAvisos));
                    CopiarEnlaceCommand.NotifyCanExecuteChanged();
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
              (TieneEnlace ? " Envíele al cliente el enlace de pago:" : string.Empty);

        private bool _enlaceCopiado;
        /// <summary>El enlace de pago está en el portapapeles (se copia solo al inscribir, o con «Copiar enlace»).</summary>
        public bool EnlaceCopiado
        {
            get => _enlaceCopiado;
            private set
            {
                if (SetProperty(ref _enlaceCopiado, value))
                {
                    OnPropertyChanged(nameof(TextoEnlace));
                }
            }
        }

        /// <summary>El aviso junto al enlace.</summary>
        public string TextoEnlace => !TieneEnlace
            ? string.Empty
            : EnlaceCopiado
                ? "Enlace copiado: péguelo en WhatsApp o donde quiera."
                : "No se ha podido copiar el enlace. Pulse «Copiar enlace» antes de cerrar.";

        public RelayCommand CopiarEnlaceCommand { get; }

        private void CopiarEnlace()
        {
            if (!TieneEnlace)
            {
                return;
            }
            bool copiado = _portapapeles.Copiar(Inscripcion.UrlPago.Trim());
            EnlaceCopiado = copiado || EnlaceCopiado;
            Mensaje = copiado ? null : "Windows no ha dejado copiar el enlace (puede que otro programa esté usando el portapapeles). Vuelva a intentarlo o selecciónelo y cópielo con Ctrl+C.";
        }

        public AsyncRelayCommand InscribirCommand { get; }
        public RelayCommand CerrarCommand { get; }

        private bool PuedeInscribir() => !EstaOcupado && !Inscrito && Evento != null && !string.IsNullOrWhiteSpace(Cliente)
            && (!SeMandaCorreo || CorreoValido);

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
                    Correo = SeMandaCorreo && !string.IsNullOrWhiteSpace(Correo) ? Correo.Trim() : null,
                    PersonaContacto = string.IsNullOrWhiteSpace(PersonaAsiste?.Numero) ? null : PersonaAsiste.Numero.Trim()
                });
                if (TieneEnlace)
                {
                    // Carlos 29/10/26: lo más importante es que el enlace no se pierda; se copia solo.
                    EnlaceCopiado = _portapapeles.Copiar(Inscripcion.UrlPago.Trim());
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

        public const string AVISO_ENLACE_SIN_COPIAR = "No ha copiado el enlace de pago. Cópielo con «Copiar enlace» antes de cerrar. " +
            "Si cierra de todos modos, lo tiene en la lista de inscritos del evento y en la ficha del cliente («Copiar enlace»).";

        /// <summary>Ya se avisó una vez de que el enlace no está copiado: al segundo intento se deja cerrar.</summary>
        private bool _avisadoEnlaceSinCopiar;
        private bool _cerrandoConResultado;

        /// <summary>
        /// Con el enlace sin copiar, el primer intento de cerrar no cierra: avisa. Si no hay inscripción, cierra con Cancel; si
        /// la hay, con OK y la inscripción (para que quien lo abrió recargue).
        /// </summary>
        private void Cerrar()
        {
            if (Inscripcion == null)
            {
                _cerrandoConResultado = true;
                RaiseRequestClose(ResultadoBoton.Cancel);
                return;
            }
            if (!PuedeCerrarSinPerderEnlace())
            {
                return;
            }
            _cerrandoConResultado = true;
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "inscripcion", Inscripcion } }));
        }

        private bool PuedeCerrarSinPerderEnlace()
        {
            if (!TieneEnlace || EnlaceCopiado || _avisadoEnlaceSinCopiar)
            {
                return true;
            }
            _avisadoEnlaceSinCopiar = true;
            Mensaje = AVISO_ENLACE_SIN_COPIAR;
            return false;
        }

        /// <summary>
        /// La X de la ventana: sin inscripción, cierra. Con inscripción no se cierra así (se perdería el resultado y quien lo
        /// abrió no recargaría): si el enlace no está copiado, avisa; si lo está, pide cerrar con el resultado en cuanto termine
        /// este cierre (no se puede cerrar una ventana mientras se está cerrando).
        /// </summary>
        public override bool CanCloseDialog()
        {
            if (_cerrandoConResultado || Inscripcion == null)
            {
                return true;
            }
            if (PuedeCerrarSinPerderEnlace())
            {
                _ = CerrarDespuesAsync();
            }
            return false;
        }

        private async Task CerrarDespuesAsync()
        {
            await Task.Yield();
            Cerrar();
        }
    }
}
