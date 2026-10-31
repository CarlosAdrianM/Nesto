using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ControlesUsuario.Notificaciones.Urgentes
{
    /// <summary>
    /// Nesto#593 (NestoAPI#775): una notificación urgente en su ventana roja. Para la autorización del repartidor (entregar un reembolso
    /// sin cobrar) pinta repartidor, cliente, importe y motivo, con Autorizar y Denegar dentro. Si otro la contesta antes, se queda con el
    /// aviso «Lo ha autorizado/denegado X» y se cierra sola (lo hace el <see cref="GestorNotificacionesUrgentes"/>).
    /// </summary>
    public class AvisoUrgenteViewModel : ObservableObject
    {
        private static readonly CultureInfo Espanol = new CultureInfo("es-ES");

        private readonly IBuzonNotificacionesService _buzon;
        private readonly Action<AvisoUrgenteViewModel> _alTerminar;
        private readonly Action<AvisoUrgenteViewModel> _alOcultar;

        internal AvisoUrgenteViewModel(NotificacionUrgente notificacion, IBuzonNotificacionesService buzon, DateTime desde,
            Action<AvisoUrgenteViewModel> alTerminar, Action<AvisoUrgenteViewModel> alOcultar)
        {
            Notificacion = notificacion ?? throw new ArgumentNullException(nameof(notificacion));
            _buzon = buzon ?? throw new ArgumentNullException(nameof(buzon));
            _alTerminar = alTerminar;
            _alOcultar = alOcultar;
            Desde = desde;
            AutorizarCommand = new AsyncRelayCommand(() => Responder(true), PuedeResponder);
            DenegarCommand = new AsyncRelayCommand(() => Responder(false), PuedeResponder);
            OcultarCommand = new RelayCommand(() => _alOcultar?.Invoke(this));
        }

        public NotificacionUrgente Notificacion { get; private set; }

        /// <summary>«AutorizacionReembolsoReparto|12»: con lo que se encuentra al llegar «notificacionResuelta».</summary>
        public string Clave => ClaveDe(Notificacion.Tipo, Notificacion.Referencia);

        internal static string ClaveDe(string tipo, string referencia) => $"{tipo?.Trim()}|{referencia?.Trim()}";

        /// <summary>Desde cuándo pita (al abrirse o al escalarse); deja de pitar a los <see cref="GestorNotificacionesUrgentes.MAXIMO_PITANDO"/>.</summary>
        public DateTime Desde { get; internal set; }

        public bool EsAutorizacionReembolso => Notificacion.Tipo == NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_REPARTO;

        public string Cabecera => EsAutorizacionReembolso
            ? (Escalada ? "URGENTE (sin respuesta): entregar sin cobrar" : "URGENTE: entregar sin cobrar")
            : "URGENTE";

        public string Titulo => Notificacion.Titulo;
        public string Cuerpo => Notificacion.Cuerpo;
        public string Repartidor => Notificacion.Dato("repartidor");

        /// <summary>«15191 · Peluquería Lucía».</summary>
        public string Cliente
        {
            get
            {
                string codigo = Notificacion.Dato("cliente");
                string nombre = Notificacion.Dato("nombre");
                return codigo == null ? nombre : nombre == null ? codigo : $"{codigo} · {nombre}";
            }
        }

        public string Importe => decimal.TryParse(Notificacion.Dato("importe"), NumberStyles.Number, CultureInfo.InvariantCulture, out decimal importe)
            ? importe.ToString("N2", Espanol) + " €"
            : null;

        public string Motivo => Notificacion.Dato("motivo");

        /// <summary>«Envío 251300 · pedido 912345».</summary>
        public string Envio
        {
            get
            {
                string envio = Notificacion.Dato("envio");
                string pedido = Notificacion.Dato("pedido");
                return envio == null ? null : pedido == null ? $"Envío {envio}" : $"Envío {envio} · pedido {pedido}";
            }
        }

        public bool Escalada => Notificacion.Escalada;

        private bool _resuelta;
        /// <summary>Ya contestada (aquí, en otro Nesto o por teléfono): sin botones, y se cierra sola enseguida.</summary>
        public bool Resuelta
        {
            get => _resuelta;
            private set
            {
                if (SetProperty(ref _resuelta, value))
                {
                    OnPropertyChanged(nameof(PuedeContestar));
                    AutorizarCommand.NotifyCanExecuteChanged();
                    DenegarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        private bool _enviando;
        public bool Enviando
        {
            get => _enviando;
            private set
            {
                if (SetProperty(ref _enviando, value))
                {
                    AutorizarCommand.NotifyCanExecuteChanged();
                    DenegarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public bool PuedeContestar => EsAutorizacionReembolso && !Resuelta;

        private string _mensaje;
        public string Mensaje
        {
            get => _mensaje;
            private set
            {
                if (SetProperty(ref _mensaje, value))
                {
                    OnPropertyChanged(nameof(HayMensaje));
                }
            }
        }
        public bool HayMensaje => !string.IsNullOrWhiteSpace(Mensaje);

        public IAsyncRelayCommand AutorizarCommand { get; }
        public IAsyncRelayCommand DenegarCommand { get; }
        /// <summary>«Más tarde»: se cierra aquí, pero sigue pendiente (vuelve a salir al abrir Nesto, al reconectar o si se escala).</summary>
        public IRelayCommand OcultarCommand { get; }

        private bool PuedeResponder() => PuedeContestar && !Enviando;

        /// <summary>Ha vuelto a llegar (p. ej. escalada): se cambian los datos y vuelve a pitar desde <paramref name="desde"/>.</summary>
        internal void Actualizar(NotificacionUrgente notificacion, DateTime desde)
        {
            if (notificacion == null || Resuelta)
            {
                return;
            }
            bool escalaAhora = notificacion.Escalada && !Notificacion.Escalada;
            Notificacion = notificacion;
            if (escalaAhora)
            {
                Desde = desde;
            }
            OnPropertyChanged(string.Empty);
        }

        internal async Task Responder(bool autorizar)
        {
            int? id = int.TryParse(Notificacion.Dato("autorizacionId") ?? Notificacion.Referencia, out int valor) ? valor : (int?)null;
            if (!id.HasValue || !PuedeResponder())
            {
                return;
            }
            Enviando = true;
            Mensaje = null;
            try
            {
                _ = await _buzon.ResponderAutorizacionReembolso(id.Value, autorizar, null);
                Mensaje = autorizar
                    ? "Autorizado: el repartidor ya puede entregar sin cobrar."
                    : "Denegado: el repartidor tendrá que cobrarlo o apuntar la incidencia «Reembolso no cobrado».";
                Terminar();
            }
            catch (Exception ex)
            {
                Mensaje = ex.Message;
                // 409: ya la contestó otro (o la autorizaron por teléfono). Otro fallo (red, permisos…): se deja abierta para reintentar.
                if ((ex.Message ?? string.Empty).IndexOf(" ya ", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Terminar();
                }
            }
            finally
            {
                Enviando = false;
            }
        }

        /// <summary>«notificacionResuelta»: la ha contestado alguien (puede ser este mismo Nesto: entonces ya estaba resuelta).</summary>
        internal void Resolver(NotificacionResuelta resuelta)
        {
            if (Resuelta)
            {
                return;
            }
            Mensaje = TextoResuelta(resuelta);
            Terminar();
        }

        internal static string TextoResuelta(NotificacionResuelta resuelta)
        {
            string quien = string.IsNullOrWhiteSpace(resuelta?.Respondio) ? "otra persona" : resuelta.Respondio.Trim();
            switch (resuelta?.Resultado?.Trim())
            {
                case "Autorizada":
                    return $"Lo ha autorizado {quien}.";
                case "Denegada":
                    return $"Lo ha denegado {quien}.";
                case "Telefonica":
                    return $"Autorizado por teléfono («{quien}»).";
                default:
                    return $"Ya lo ha contestado {quien}.";
            }
        }

        private void Terminar()
        {
            Resuelta = true;
            MarcarLeidaSinFallar();
            _alTerminar?.Invoke(this);
        }

        private async void MarcarLeidaSinFallar()
        {
            try
            {
                if (Notificacion.IdBuzon > 0)
                {
                    await _buzon.MarcarLeida(Notificacion.IdBuzon);
                }
            }
            catch (Exception)
            {
                // Lo importante (la respuesta) ya está hecho; la campana la enseñará sin leer
            }
        }
    }
}
