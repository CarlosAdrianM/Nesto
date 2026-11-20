using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>Novedades 1258: una fila de la lista con los textos ya preparados.</summary>
    public class FilaAutorizacionPedido
    {
        private static readonly CultureInfo ES = CultureInfo.GetCultureInfo("es-ES");

        public FilaAutorizacionPedido(AutorizacionPedidoModel solicitud)
        {
            Solicitud = solicitud ?? throw new ArgumentNullException(nameof(solicitud));
        }

        public AutorizacionPedidoModel Solicitud { get; }
        public int Id => Solicitud.Id;
        public string Estado => Solicitud.Estado?.Trim();
        public bool EstaPendiente => Solicitud.EstaPendiente;
        public string Empresa => string.IsNullOrWhiteSpace(Solicitud.Empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : Solicitud.Empresa.Trim();
        /// <summary>«15191/0 · Peluquería Prueba».</summary>
        public string Cliente
        {
            get
            {
                string numero = Solicitud.Cliente?.Trim();
                string contacto = Solicitud.Contacto?.Trim();
                if (!string.IsNullOrEmpty(contacto))
                {
                    numero += "/" + contacto;
                }
                string nombre = Solicitud.NombreCliente?.Trim();
                return string.IsNullOrEmpty(nombre) ? numero : $"{numero} · {nombre}";
            }
        }
        /// <summary>El nombre de quien la pidió o, si no viene, su usuario.</summary>
        public string Vendedor => string.IsNullOrWhiteSpace(Solicitud.NombreSolicitante) ? Solicitud.Solicitante?.Trim() : Solicitud.NombreSolicitante.Trim();
        public string TextoImporte => Solicitud.Importe.HasValue ? Solicitud.Importe.Value.ToString("C2", ES) : string.Empty;
        public string TextoFecha => Solicitud.FechaSolicitud.HasValue ? Solicitud.FechaSolicitud.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) : string.Empty;
        public string TextoMotivos => string.Join(" · ", (Solicitud.Motivos ?? new List<string>()).Where(m => !string.IsNullOrWhiteSpace(m)).Select(m => m.Trim()));
        public bool HayResolucion => !EstaPendiente;
        /// <summary>«Denegada por Carlos el 19/11/2026 10:15: no es cliente de la promoción.» (null si sigue pendiente).</summary>
        public string TextoResolucion
        {
            get
            {
                if (EstaPendiente)
                {
                    return null;
                }
                string texto = Estado ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(Solicitud.UsuarioResolucion))
                {
                    texto += " por " + Solicitud.UsuarioResolucion.Trim();
                }
                if (Solicitud.FechaResolucion.HasValue)
                {
                    texto += " el " + Solicitud.FechaResolucion.Value.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
                }
                if (!string.IsNullOrWhiteSpace(Solicitud.MotivoResolucion))
                {
                    texto += ": " + Solicitud.MotivoResolucion.Trim();
                }
                if (Solicitud.PedidoCreado.HasValue && Solicitud.PedidoCreado.Value > 0)
                {
                    texto += $" (pedido {Solicitud.PedidoCreado.Value})";
                }
                return texto.TrimEnd('.') + ".";
            }
        }
    }

    /// <summary>Novedades 1258: una línea del borrador, marcada si su producto aparece en los errores de la validación.</summary>
    public class LineaAutorizacionPedido
    {
        private static readonly CultureInfo ES = CultureInfo.GetCultureInfo("es-ES");

        public string Producto { get; set; }
        public string Descripcion { get; set; }
        public int Cantidad { get; set; }
        public decimal Precio { get; set; }
        public decimal Descuento { get; set; }
        public int? Oferta { get; set; }
        public bool ConError => !string.IsNullOrWhiteSpace(MotivoError);
        public string MotivoError { get; set; }
        public string TextoPrecio => Precio.ToString("C2", ES);
        public string TextoDescuento => Descuento == 0 ? string.Empty : (Descuento * 100).ToString("0.##", ES) + " %";
        public string TextoOferta => Oferta.HasValue && Oferta.Value != 0 ? Oferta.Value.ToString(CultureInfo.InvariantCulture) : string.Empty;

        /// <summary>PURA. Las líneas del borrador con el motivo de los errores de su producto (sin distinguir espacios ni mayúsculas).</summary>
        public static List<LineaAutorizacionPedido> DesdeBorrador(PedidoVentaDTO pedido, IEnumerable<ErrorAutorizacionPedido> errores)
        {
            var motivosPorProducto = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (ErrorAutorizacionPedido error in errores ?? Enumerable.Empty<ErrorAutorizacionPedido>())
            {
                string producto = error?.Producto?.Trim();
                if (string.IsNullOrEmpty(producto) || string.IsNullOrWhiteSpace(error.Motivo))
                {
                    continue;
                }
                if (!motivosPorProducto.TryGetValue(producto, out List<string> motivos))
                {
                    motivos = new List<string>();
                    motivosPorProducto[producto] = motivos;
                }
                if (!motivos.Contains(error.Motivo.Trim()))
                {
                    motivos.Add(error.Motivo.Trim());
                }
            }
            var lineas = new List<LineaAutorizacionPedido>();
            foreach (LineaPedidoVentaDTO linea in pedido?.Lineas ?? new List<LineaPedidoVentaDTO>())
            {
                if (linea == null)
                {
                    continue;
                }
                string producto = linea.Producto?.Trim();
                lineas.Add(new LineaAutorizacionPedido
                {
                    Producto = producto,
                    Descripcion = linea.texto?.Trim(),
                    Cantidad = linea.Cantidad,
                    Precio = linea.PrecioUnitario,
                    Descuento = linea.DescuentoLinea,
                    Oferta = linea.oferta,
                    MotivoError = !string.IsNullOrEmpty(producto) && motivosPorProducto.TryGetValue(producto, out List<string> m) ? string.Join(" · ", m) : null
                });
            }
            return lineas;
        }
    }

    /// <summary>
    /// Novedades 1258: «Autorizaciones de pedidos». Quien no puede crear un pedido sin pasar la validación de ofertas y descuentos lo
    /// pide desde la plantilla; aquí se ven las solicitudes (pendientes; con «Ver también las resueltas», todas). Quien puede autorizar
    /// (el mismo criterio que el «¿Desea crearlo de todos modos?», sin el de las tiendas) ve todas y puede «Autorizar y crear el pedido» o
    /// «Denegar»; los demás ven solo las suyas y pueden anularlas. Quién ve y hace qué lo decide al final la API (403).
    /// </summary>
    public class AutorizacionesPedidoViewModel : ObservableObject, IReceptorNavegacion
    {
        public const string TITULO_AUTORIZAR = "Autorizar y crear el pedido";
        public const string TITULO_DENEGAR = "Denegar la autorización";
        public const string TITULO_ANULAR = "Anular mi solicitud";
        public const string MENSAJE_MOTIVO_VACIO = "Escriba el motivo por el que deniega la autorización.";
        public const int LONGITUD_MAXIMA_TEXTO = 500;
        public static readonly string MENSAJE_TEXTO_LARGO = $"El texto no puede pasar de {LONGITUD_MAXIMA_TEXTO} caracteres.";

        private readonly IServicioAutorizacionesPedido _servicio;
        private readonly IServicioDialogos _dialogos;
        private readonly IAbridorPedidos _abridorPedidos;
        private readonly IConfiguracion _configuracion;
        private int? _idAElegir;
        private bool _avisarResolucionAlElegir;
        private bool _permisosLeidos;
        private bool _cargandoFiltro;

        public AutorizacionesPedidoViewModel(IServicioAutorizacionesPedido servicio, IServicioDialogos dialogos, IAbridorPedidos abridorPedidos, IConfiguracion configuracion)
        {
            _servicio = servicio;
            _dialogos = dialogos;
            _abridorPedidos = abridorPedidos;
            _configuracion = configuracion;
            CargarCommand = new AsyncRelayCommand(CargarAsync);
            AutorizarCommand = new AsyncRelayCommand(AutorizarAsync, () => !EstaOcupado && PuedeAutorizarSeleccionada);
            DenegarCommand = new AsyncRelayCommand(DenegarAsync, () => !EstaOcupado && PuedeAutorizarSeleccionada);
            AnularCommand = new AsyncRelayCommand(AnularAsync, () => !EstaOcupado && PuedeAnularSeleccionada);
            AbrirPedidoCreadoCommand = new RelayCommand(AbrirPedidoCreado, () => Seleccionada?.Solicitud.PedidoCreado > 0);
        }

        public string Titulo => "Autorizaciones de pedidos";
        public string Ayuda => PuedeAutorizar
            ? "Pedidos que no pasan la validación de ofertas y descuentos y cuyo autor pide autorización. Elija una para ver las líneas: " +
              "las marcadas son las que no pasan. «Autorizar y crear el pedido» lo crea tal cual; «Denegar» pide el motivo, que le llega a quien lo pidió."
            : "Sus solicitudes de autorización de pedidos que no pasan la validación de ofertas y descuentos. Cuando contesten le avisaremos por la campana. " +
              "Mientras siga pendiente, puede anularla.";

        public ObservableCollection<FilaAutorizacionPedido> Solicitudes { get; } = new ObservableCollection<FilaAutorizacionPedido>();
        public ObservableCollection<LineaAutorizacionPedido> Lineas { get; } = new ObservableCollection<LineaAutorizacionPedido>();

        private bool _puedeAutorizar;
        /// <summary>Dirección, Almacén o el parámetro PermitirCrearPedidoConErroresValidacion (el criterio de «de todos modos»).</summary>
        public bool PuedeAutorizar
        {
            get => _puedeAutorizar;
            private set
            {
                if (SetProperty(ref _puedeAutorizar, value))
                {
                    OnPropertyChanged(nameof(Ayuda));
                    AvisarComandos();
                }
            }
        }

        private bool _verResueltas;
        /// <summary>«Ver también las resueltas». Al cambiarlo se vuelve a leer.</summary>
        public bool VerResueltas
        {
            get => _verResueltas;
            set
            {
                if (SetProperty(ref _verResueltas, value) && !_cargandoFiltro)
                {
                    _ = CargarAsync();
                }
            }
        }

        private FilaAutorizacionPedido _seleccionada;
        public FilaAutorizacionPedido Seleccionada
        {
            get => _seleccionada;
            set
            {
                if (SetProperty(ref _seleccionada, value))
                {
                    OnPropertyChanged(nameof(HaySeleccionada));
                    OnPropertyChanged(nameof(PuedeAutorizarSeleccionada));
                    OnPropertyChanged(nameof(PuedeAnularSeleccionada));
                    Lineas.Clear();
                    Comentario = value?.Solicitud.Comentario?.Trim();
                    AvisarComandos();
                    if (value != null)
                    {
                        _ = CargarDetalleAsync(value);
                    }
                }
            }
        }

        public bool HaySeleccionada => Seleccionada != null;
        public bool PuedeAutorizarSeleccionada => PuedeAutorizar && Seleccionada?.EstaPendiente == true;
        /// <summary>El solicitante, mientras siga pendiente (quien no autoriza solo ve las suyas).</summary>
        public bool PuedeAnularSeleccionada => Seleccionada?.EstaPendiente == true && (!PuedeAutorizar || EsDelUsuario(Seleccionada.Solicitud));

        private string _comentario;
        /// <summary>El comentario de quien la pidió.</summary>
        public string Comentario { get => _comentario; private set => SetProperty(ref _comentario, value); }

        private bool _estaOcupado;
        public bool EstaOcupado
        {
            get => _estaOcupado;
            private set
            {
                if (SetProperty(ref _estaOcupado, value))
                {
                    AvisarComandos();
                }
            }
        }

        private string _mensaje;
        public string Mensaje { get => _mensaje; set => SetProperty(ref _mensaje, value); }

        public string Resumen
        {
            get
            {
                int pendientes = Solicitudes.Count(s => s.EstaPendiente);
                int resueltas = Solicitudes.Count - pendientes;
                string textoPendientes = pendientes == 0 ? "Ninguna pendiente" : pendientes == 1 ? "1 pendiente" : $"{pendientes} pendientes";
                return VerResueltas ? $"{textoPendientes} y {(resueltas == 1 ? "1 resuelta" : $"{resueltas} resueltas")}" : textoPendientes;
            }
        }

        public IAsyncRelayCommand CargarCommand { get; }
        public IAsyncRelayCommand AutorizarCommand { get; }
        public IAsyncRelayCommand DenegarCommand { get; }
        public IAsyncRelayCommand AnularCommand { get; }
        public IRelayCommand AbrirPedidoCreadoCommand { get; }

        /// <summary>Cada vez que se llega (el menú o la campana) se vuelve a leer; con Datos["id"] de la campana, se deja elegida.</summary>
        public void AlLlegar(ParametrosNavegacion parametros)
        {
            _idAElegir = LeerId(parametros);
            _avisarResolucionAlElegir = _idAElegir.HasValue;
            if (!EstaOcupado)
            {
                _ = CargarAsync();
            }
        }

        internal static int? LeerId(ParametrosNavegacion parametros)
        {
            if (parametros == null || !parametros.ContainsKey(NotificacionBuzon.PARAMETRO_AUTORIZACION_PEDIDO_ID))
            {
                return null;
            }
            object valor = parametros.GetValue<object>(NotificacionBuzon.PARAMETRO_AUTORIZACION_PEDIDO_ID);
            return valor != null && int.TryParse(valor.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int id) && id > 0 ? id : (int?)null;
        }

        public async Task CargarAsync()
        {
            if (_servicio == null)
            {
                return;
            }
            Mensaje = null;
            EstaOcupado = true;
            try
            {
                if (!_permisosLeidos)
                {
                    PuedeAutorizar = await PermisosValidacionPedido.PuedeOmitirValidacion(_configuracion, Constantes.Empresas.EMPRESA_DEFECTO, () => false).ConfigureAwait(true);
                    _permisosLeidos = true;
                }
                Poner(await _servicio.Leer(!VerResueltas, !PuedeAutorizar).ConfigureAwait(true));
                if (_idAElegir.HasValue && Seleccionada == null && !VerResueltas)
                {
                    // La de la campana ya no está pendiente (contestada o anulada): se enseña con las resueltas
                    _cargandoFiltro = true;
                    VerResueltas = true;
                    _cargandoFiltro = false;
                    Poner(await _servicio.Leer(false, !PuedeAutorizar).ConfigureAwait(true));
                }
                if (_idAElegir.HasValue && Seleccionada == null)
                {
                    Mensaje = $"La solicitud {_idAElegir.Value} ya no está en la lista.";
                }
                else if (_avisarResolucionAlElegir && Seleccionada != null && !Seleccionada.EstaPendiente && _dialogos != null
                    && string.Equals(Seleccionada.Estado, AutorizacionPedidoModel.ESTADO_DENEGADA, StringComparison.OrdinalIgnoreCase))
                {
                    _dialogos.ShowNotification("Autorización denegada", $"Su solicitud de autorización del pedido de {Seleccionada.Cliente}: {Seleccionada.TextoResolucion}");
                }
            }
            catch (ApiRepartoException ex)
            {
                Mensaje = ex.Message;
            }
            catch (Exception ex)
            {
                Mensaje = "No se ha podido hablar con el servidor: " + ex.Message;
            }
            finally
            {
                _idAElegir = null;
                _avisarResolucionAlElegir = false;
                EstaOcupado = false;
            }
        }

        private void Poner(IEnumerable<AutorizacionPedidoModel> leidas)
        {
            int? anterior = _idAElegir ?? Seleccionada?.Id;
            Seleccionada = null;
            Solicitudes.Clear();
            foreach (AutorizacionPedidoModel solicitud in (leidas ?? Enumerable.Empty<AutorizacionPedidoModel>()).Where(s => s != null))
            {
                if (!VerResueltas && !solicitud.EstaPendiente)
                {
                    continue;
                }
                Solicitudes.Add(new FilaAutorizacionPedido(solicitud));
            }
            if (anterior.HasValue)
            {
                Seleccionada = Solicitudes.FirstOrDefault(s => s.Id == anterior.Value);
            }
            OnPropertyChanged(nameof(Resumen));
        }

        /// <summary>El borrador (GET {id}) con las líneas marcadas. Si falla, se dice y la fila sigue elegida.</summary>
        internal async Task CargarDetalleAsync(FilaAutorizacionPedido fila)
        {
            try
            {
                AutorizacionPedidoModel detalle = await _servicio.LeerUna(fila.Id).ConfigureAwait(true);
                if (!ReferenceEquals(Seleccionada, fila) || detalle == null)
                {
                    return;
                }
                Lineas.Clear();
                foreach (LineaAutorizacionPedido linea in LineaAutorizacionPedido.DesdeBorrador(detalle.Pedido, detalle.Errores ?? fila.Solicitud.Errores))
                {
                    Lineas.Add(linea);
                }
            }
            catch (Exception ex)
            {
                if (ReferenceEquals(Seleccionada, fila))
                {
                    Mensaje = ex is ApiRepartoException ? ex.Message : "No se han podido leer las líneas: " + ex.Message;
                }
            }
        }

        private async Task AutorizarAsync()
        {
            FilaAutorizacionPedido fila = Seleccionada;
            if (fila == null || _dialogos == null)
            {
                return;
            }
            string comentario = PedirTexto(TITULO_AUTORIZAR,
                $"Se creará el pedido de {fila.Cliente} tal cual lo mandó {fila.Vendedor}. Si quiere, escriba un comentario (puede dejarlo en blanco).", false);
            if (comentario == null)
            {
                return;
            }
            EstaOcupado = true;
            ResultadoAutorizarPedido resultado = null;
            try
            {
                resultado = await _servicio.Autorizar(fila.Id, comentario).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(TextoError(ex, "No se ha podido autorizar"));
            }
            finally
            {
                EstaOcupado = false;
            }
            if (resultado == null)
            {
                await CargarAsync().ConfigureAwait(true);
                return;
            }
            await CargarAsync().ConfigureAwait(true);
            if (resultado.Pedido.HasValue && resultado.Pedido.Value > 0)
            {
                if (_dialogos.ShowConfirmationAnswer(TITULO_AUTORIZAR, $"Autorizada: se ha creado el pedido {resultado.Pedido.Value}. ¿Desea abrirlo?"))
                {
                    _abridorPedidos?.Abrir(fila.Empresa, resultado.Pedido.Value);
                }
            }
            else
            {
                _dialogos.ShowNotification(TITULO_AUTORIZAR, "Autorizada: se ha creado el pedido.");
            }
        }

        private async Task DenegarAsync()
        {
            FilaAutorizacionPedido fila = Seleccionada;
            if (fila == null || _dialogos == null)
            {
                return;
            }
            string motivo = PedirTexto(TITULO_DENEGAR, $"¿Por qué deniega la autorización del pedido de {fila.Cliente}? Le llegará a {fila.Vendedor}.", true);
            if (motivo == null)
            {
                return;
            }
            await Ejecutar(() => _servicio.Denegar(fila.Id, motivo), "Autorización denegada.", "No se ha podido denegar").ConfigureAwait(true);
        }

        private async Task AnularAsync()
        {
            FilaAutorizacionPedido fila = Seleccionada;
            if (fila == null || _dialogos == null)
            {
                return;
            }
            if (!_dialogos.ShowConfirmationAnswer(TITULO_ANULAR, $"¿Desea anular su solicitud de autorización del pedido de {fila.Cliente}?", true))
            {
                return;
            }
            await Ejecutar(() => _servicio.Anular(fila.Id), "Solicitud anulada.", "No se ha podido anular").ConfigureAwait(true);
        }

        private async Task Ejecutar(Func<Task<AutorizacionPedidoModel>> accion, string hecho, string queFallo)
        {
            EstaOcupado = true;
            bool bien = false;
            try
            {
                await accion().ConfigureAwait(true);
                bien = true;
            }
            catch (Exception ex)
            {
                _dialogos.ShowError(TextoError(ex, queFallo));
            }
            finally
            {
                EstaOcupado = false;
            }
            await CargarAsync().ConfigureAwait(true);
            if (bien)
            {
                Mensaje = hecho;
            }
        }

        /// <summary>Pide un texto (null si cancela). Obligatorio: vacío no vale. Más de 500, tampoco.</summary>
        private string PedirTexto(string titulo, string pregunta, bool obligatorio)
        {
            while (true)
            {
                string texto = _dialogos.GetText(titulo, pregunta);
                if (texto == null)
                {
                    return null;
                }
                texto = texto.Trim();
                if (obligatorio && texto.Length == 0)
                {
                    _dialogos.ShowError(MENSAJE_MOTIVO_VACIO);
                    continue;
                }
                if (texto.Length > LONGITUD_MAXIMA_TEXTO)
                {
                    _dialogos.ShowError(MENSAJE_TEXTO_LARGO);
                    continue;
                }
                return texto;
            }
        }

        private static string TextoError(Exception ex, string queFallo) => ex is ApiRepartoException ? ex.Message : $"{queFallo}: {ex.Message}";

        private void AbrirPedidoCreado()
        {
            FilaAutorizacionPedido fila = Seleccionada;
            if (fila?.Solicitud.PedidoCreado > 0)
            {
                _abridorPedidos?.Abrir(fila.Empresa, fila.Solicitud.PedidoCreado.Value);
            }
        }

        /// <summary>Si la solicitud es del usuario de Nesto (con o sin dominio, sin distinguir mayúsculas).</summary>
        internal bool EsDelUsuario(AutorizacionPedidoModel solicitud)
        {
            string suyo = SinDominio(solicitud?.Solicitante);
            if (string.IsNullOrEmpty(suyo) || _configuracion == null)
            {
                return false;
            }
            return string.Equals(suyo, SinDominio(_configuracion.usuario), StringComparison.OrdinalIgnoreCase)
                || string.Equals(suyo, SinDominio(_configuracion.UsuarioSinDominio), StringComparison.OrdinalIgnoreCase);
        }

        private static string SinDominio(string usuario)
        {
            string limpio = usuario?.Trim();
            if (string.IsNullOrEmpty(limpio))
            {
                return null;
            }
            int barra = limpio.LastIndexOf('\\');
            return barra >= 0 ? limpio.Substring(barra + 1) : limpio;
        }

        private void AvisarComandos()
        {
            OnPropertyChanged(nameof(PuedeAutorizarSeleccionada));
            OnPropertyChanged(nameof(PuedeAnularSeleccionada));
            AutorizarCommand?.NotifyCanExecuteChanged();
            DenegarCommand?.NotifyCanExecuteChanged();
            AnularCommand?.NotifyCanExecuteChanged();
            AbrirPedidoCreadoCommand?.NotifyCanExecuteChanged();
        }
    }
}
