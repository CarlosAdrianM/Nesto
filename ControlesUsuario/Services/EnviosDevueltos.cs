using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// NestoAPI#699 (corte 2): una fila de GET api/EnviosAgencias/Devueltos: un envío DEVUELTO o INCIDENTADO de los últimos
    /// 60 días con la decisión sobre él (sin decidir, reenviar o abonar). Es el POCO que llega por la red; la pestaña Devueltos
    /// de Agencias lo enlaza directamente y, desde la sugerencia 1356, también la pestaña «Entrega» del detalle del pedido.
    /// (Era una clase de Nesto.ViewModels; vive aquí para que la usen los dos sitios.)
    /// </summary>
    public class EnvioDevueltoModel
    {
        public const string SIN_DECIDIR = "SinDecidir";
        public const string REENVIAR = "Reenviar";
        public const string ABONAR = "Abonar";

        public const short ESTADO_INCIDENTADO = 3;
        public const short ESTADO_DEVUELTO = 4;

        public int Numero { get; set; }
        public string Empresa { get; set; }
        public int Agencia { get; set; }
        public string NombreAgencia { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public int? Pedido { get; set; }
        public short Estado { get; set; }
        public DateTime Fecha { get; set; }
        /// <summary>El texto de la agencia («DEVOLUCIÓN», «AUSENTE»…): el motivo.</summary>
        public string DetalleEstado { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Telefono { get; set; }
        public string Movil { get; set; }
        public string Observaciones { get; set; }
        public string CodigoBarras { get; set; }
        public decimal Reembolso { get; set; }

        public string Decision { get; set; }
        public int? EnvioNuevo { get; set; }
        public short? EstadoEnvioNuevo { get; set; }
        public bool EnvioNuevoConEtiqueta { get; set; }
        public bool PortesIncluidos { get; set; }
        public string UsuarioDecision { get; set; }
        public DateTime? FechaDecision { get; set; }

        // NestoAPI#870: si la caja ha vuelto físicamente a Algete (la lectura de Ariadna o «Ya está aquí») y si hay un reenvío
        // esperándola (decisión Reenviar, el reenvío sin tramitar y la caja sin volver: la API no lo deja salir).
        public bool HaVuelto { get; set; }
        public DateTime? FechaHaVuelto { get; set; }
        public string UsuarioHaVuelto { get; set; }
        /// <summary>«Lectura» (Ariadna), «YaEstaAqui» (botón de Nesto) o «AlImprimir» (forzado al imprimir el reenvío).</summary>
        public string ComoHaVuelto { get; set; }
        public bool ReenvioEsperandoCaja { get; set; }

        /// <summary>
        /// Sugerencia 1329: lo calcula la API (ReglaDevueltoResuelto): la caja ha vuelto, hay decisión y, si es Reenviar, el reenvío ya
        /// está tramitado. No queda nada por hacer. Con una API anterior no llega y vale False (se ve todo, como antes).
        /// </summary>
        public bool Resuelto { get; set; }

        /// <summary>
        /// Sugerencia 1329: lo que enseña la pestaña Devueltos: por defecto solo lo que tiene algo por hacer; con
        /// <paramref name="verResueltos"/>, todo. Conserva el orden de la API.
        /// </summary>
        public static List<EnvioDevueltoModel> Visibles(IEnumerable<EnvioDevueltoModel> lista, bool verResueltos)
        {
            if (lista == null)
            {
                return new List<EnvioDevueltoModel>();
            }
            return lista.Where(d => d != null && (verResueltos || !d.Resuelto)).ToList();
        }

        /// <summary>
        /// Sugerencia 1356: los envíos de un pedido que esperan una decisión (devueltos o incidentados sin decidir). Lo que enseña
        /// la pestaña «Entrega» del detalle del pedido; conserva el orden de la API.
        /// </summary>
        public static List<EnvioDevueltoModel> PendientesDeDecidirDelPedido(IEnumerable<EnvioDevueltoModel> lista, string empresa, int pedido)
        {
            if (lista == null)
            {
                return new List<EnvioDevueltoModel>();
            }
            string empresaLimpia = (empresa ?? string.Empty).Trim();
            return lista.Where(d => d != null && d.Pedido == pedido && d.SinDecidir
                                    && (string.IsNullOrEmpty(empresaLimpia) || string.IsNullOrWhiteSpace(d.Empresa) || d.Empresa.Trim() == empresaLimpia))
                        .ToList();
        }

        /// <summary>NestoAPI#870: la columna «Caja»: esperando, ha vuelto (con día y hora) o nada.</summary>
        public string TextoCaja
        {
            get
            {
                if (HaVuelto)
                {
                    return FechaHaVuelto.HasValue ? $"Ha vuelto ({FechaHaVuelto.Value:dd/MM HH:mm})" : "Ha vuelto";
                }
                if (ReenvioEsperandoCaja)
                {
                    return "Esperando la caja";
                }
                return string.Empty;
            }
        }

        /// <summary>NestoAPI#870: el tooltip de la columna «Caja»: quién y cómo la dio por vuelta.</summary>
        public string DetalleCaja
        {
            get
            {
                if (HaVuelto)
                {
                    string como = TextoComoHaVuelto(ComoHaVuelto);
                    string quien = string.IsNullOrWhiteSpace(UsuarioHaVuelto) ? string.Empty : $" por {UsuarioHaVuelto.Trim()}";
                    string cuando = FechaHaVuelto.HasValue ? $" el {FechaHaVuelto.Value:dd/MM/yyyy HH:mm}" : string.Empty;
                    return $"Ha vuelto{cuando}{quien}{como}";
                }
                if (ReenvioEsperandoCaja)
                {
                    return $"El reenvío {EnvioNuevo} no puede salir hasta que vuelva esta caja. Cuando llegue, léala en Ariadna («Ha vuelto un paquete») o pulse «Ya está aquí».";
                }
                return null;
            }
        }

        public static string TextoComoHaVuelto(string como)
        {
            switch (como)
            {
                case "Lectura": return " (leída en Ariadna)";
                case "YaEstaAqui": return " (botón «Ya está aquí»)";
                case "AlImprimir": return " (al imprimir el reenvío)";
                default: return string.Empty;
            }
        }

        public bool SinDecidir => string.IsNullOrWhiteSpace(Decision) || Decision == SIN_DECIDIR;

        public string TextoEstado => Estado == ESTADO_DEVUELTO ? "Devuelto" : Estado == ESTADO_INCIDENTADO ? "Incidentado" : $"Estado {Estado}";

        /// <summary>Sugerencia 1356: la línea del bloque del detalle del pedido: «Envío 251300 (GLS) · Devuelto el 18/11/2026: AUSENTE».</summary>
        public string Resumen
        {
            get
            {
                string agencia = string.IsNullOrWhiteSpace(NombreAgencia) ? string.Empty : $" ({NombreAgencia.Trim()})";
                string fecha = Fecha == default(DateTime) ? string.Empty : $" el {Fecha:dd/MM/yyyy}";
                string motivo = string.IsNullOrWhiteSpace(DetalleEstado) ? string.Empty : $": {DetalleEstado.Trim()}";
                return $"Envío {Numero}{agencia} · {TextoEstado}{fecha}{motivo}";
            }
        }

        /// <summary>Sugerencia 1356: a quién y adónde iba («NOMBRE · Calle 1, 28001 Madrid»), lo que no esté se omite.</summary>
        public string Destino
        {
            get
            {
                string lugar = string.Join(" ", new[] { CodPostal, Poblacion }.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()));
                string direccion = string.Join(", ", new[] { Direccion?.Trim(), lugar }.Where(t => !string.IsNullOrWhiteSpace(t)));
                return string.Join(" · ", new[] { Nombre?.Trim(), direccion }.Where(t => !string.IsNullOrWhiteSpace(t)));
            }
        }

        /// <summary>Lo que ve el almacén: qué hacer con la caja cuando llegue.</summary>
        public string TextoDecision
        {
            get
            {
                if (SinDecidir)
                {
                    return "Sin decidir";
                }
                if (Decision == ABONAR)
                {
                    return "Abonar";
                }
                if (Decision == REENVIAR)
                {
                    string portes = PortesIncluidos ? " (con portes)" : string.Empty;
                    return EnvioNuevoConEtiqueta
                        ? $"Reenviado en el envío {EnvioNuevo}{portes}"
                        : $"REENVIAR: envío {EnvioNuevo} listo para imprimir la etiqueta{portes}";
                }
                return Decision;
            }
        }
    }

    /// <summary>NestoAPI#699: el cuerpo de POST api/EnviosAgencias/{id}/Reenviar (mismos nombres que ReenviarEnvioDTO).</summary>
    public class ReenvioEnvioPeticion
    {
        public string Direccion { get; set; }
        public string CodPostal { get; set; }
        public string Poblacion { get; set; }
        public string Provincia { get; set; }
        public string Telefono { get; set; }
        public string Movil { get; set; }
        public string Observaciones { get; set; }
        public bool IncluirPortes { get; set; }
    }

    /// <summary>NestoAPI#699: la respuesta de Reenviar y de Abonar (DecisionReenvioDTO).</summary>
    public class DecisionReenvioModel
    {
        public int EnvioOrigen { get; set; }
        public int? Pedido { get; set; }
        public string Decision { get; set; }
        public int? EnvioNuevo { get; set; }
        public string CodigoBarras { get; set; }
        public bool PortesIncluidos { get; set; }
        public decimal Reembolso { get; set; }
        public List<string> Avisos { get; set; } = new List<string>();
        public string Mensaje { get; set; }
    }

    /// <summary>NestoAPI#699: leer los devueltos y decidir sobre uno (volver a enviar o abonar).</summary>
    public interface IServicioEnviosDevueltos
    {
        /// <summary>GET api/EnviosAgencias/Devueltos?empresa=: devueltos e incidentados de 60 días con su decisión.</summary>
        Task<List<EnvioDevueltoModel>> Cargar(string empresa);
        /// <summary>
        /// Sugerencia 1356 (fleco, 19/11/26): GET api/EnviosAgencias/Devueltos?empresa=&amp;pedido=: solo los de ese pedido (antes el
        /// detalle del pedido bajaba los 60 días de toda la empresa). Una API anterior ignora «pedido» y devuelve todos: quien llama
        /// sigue filtrando por el pedido.
        /// </summary>
        Task<List<EnvioDevueltoModel>> CargarDelPedido(string empresa, int pedido);
        Task<DecisionReenvioModel> Reenviar(int numeroEnvio, ReenvioEnvioPeticion peticion);
        Task<DecisionReenvioModel> Abonar(int numeroEnvio);
    }

    /// <summary>
    /// Las llamadas a la API de los devueltos: las usan Agencias (por AgenciaService) y el detalle del pedido. Lanza
    /// <see cref="InvalidOperationException"/> con el motivo de la API (400/404/409) y <see cref="UnauthorizedAccessException"/>
    /// con un mensaje de usted si la API contesta 401/403.
    /// </summary>
    public class ServicioEnviosDevueltos : IServicioEnviosDevueltos
    {
        public const string MENSAJE_SIN_PERMISO = "No tiene permiso para decidir sobre los envíos devueltos: lo deciden quien hizo el pedido, su vendedor, Almacén o Dirección. Pídaselo a alguno de ellos.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEnviosDevueltos(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public static string RutaDevueltos(string empresa) => $"EnviosAgencias/Devueltos?empresa={Uri.EscapeDataString((empresa ?? string.Empty).Trim())}";
        public static string RutaDevueltosDelPedido(string empresa, int pedido) => $"{RutaDevueltos(empresa)}&pedido={pedido}";
        public static string RutaReenviar(int numeroEnvio) => $"EnviosAgencias/{numeroEnvio}/Reenviar";
        public static string RutaAbonar(int numeroEnvio) => $"EnviosAgencias/{numeroEnvio}/Abonar";
        public static string CuerpoReenviar(ReenvioEnvioPeticion peticion) => JsonConvert.SerializeObject(peticion ?? new ReenvioEnvioPeticion());

        /// <summary>El texto de un error de la API para el usuario (el «Message» del JSON, la cadena JSON o el cuerpo tal cual).</summary>
        public static Exception ExcepcionDeRespuesta(HttpStatusCode codigo, string cuerpo)
        {
            if (codigo == HttpStatusCode.Forbidden || codigo == HttpStatusCode.Unauthorized)
            {
                return new UnauthorizedAccessException(MENSAJE_SIN_PERMISO);
            }
            return new InvalidOperationException(ServicioEnvioCorreoDocumentos.MensajeDeError(cuerpo));
        }

        public Task<List<EnvioDevueltoModel>> Cargar(string empresa) => Leer(RutaDevueltos(empresa));

        public Task<List<EnvioDevueltoModel>> CargarDelPedido(string empresa, int pedido) => Leer(RutaDevueltosDelPedido(empresa, pedido));

        private async Task<List<EnvioDevueltoModel>> Leer(string ruta)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (HttpResponseMessage respuesta = await client.GetAsync(ruta).ConfigureAwait(false))
            {
                string cuerpo = respuesta.Content == null ? string.Empty : await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw ExcepcionDeRespuesta(respuesta.StatusCode, cuerpo);
                }
                return (string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<List<EnvioDevueltoModel>>(cuerpo))
                    ?? new List<EnvioDevueltoModel>();
            }
        }

        public Task<DecisionReenvioModel> Reenviar(int numeroEnvio, ReenvioEnvioPeticion peticion)
            => PostDecision(RutaReenviar(numeroEnvio), CuerpoReenviar(peticion));

        public Task<DecisionReenvioModel> Abonar(int numeroEnvio) => PostDecision(RutaAbonar(numeroEnvio), "{}");

        private async Task<DecisionReenvioModel> PostDecision(string ruta, string cuerpoPeticion)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var contenido = new StringContent(cuerpoPeticion, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await client.PostAsync(ruta, contenido).ConfigureAwait(false))
            {
                string cuerpo = respuesta.Content == null ? string.Empty : await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw ExcepcionDeRespuesta(respuesta.StatusCode, cuerpo);
                }
                return string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<DecisionReenvioModel>(cuerpo);
            }
        }
    }

    public enum ResultadoDecisionDevuelto
    {
        /// <summary>No se ha hecho nada (sin envío, ya decidido o el usuario ha cancelado).</summary>
        SinCambios,
        /// <summary>La API ha apuntado la decisión: hay que recargar la lista.</summary>
        Decidido,
        /// <summary>La API ha fallado; el usuario ya ha visto el motivo.</summary>
        Error
    }

    /// <summary>
    /// NestoAPI#699 / sugerencia 1356: decidir sobre un envío devuelto o incidentado, el mismo paso en Agencias › Devueltos y en la
    /// pestaña «Entrega» del detalle del pedido. «Volver a enviar…» abre <see cref="ReenviarEnvioDialogViewModel"/> con la dirección
    /// y los teléfonos del envío para corregirlos; «Abonar» pide confirmación y solo apunta la decisión (el abono se hace con Copiar
    /// factura). Enseña el resultado o el error (de usted); quien lo llama solo recarga si devuelve <see cref="ResultadoDecisionDevuelto.Decidido"/>.
    /// </summary>
    public class DecisorEnviosDevueltos
    {
        public const string TITULO_REENVIAR = "Volver a enviar";
        public const string TITULO_ABONAR = "Abonar";

        private readonly Func<int, ReenvioEnvioPeticion, Task<DecisionReenvioModel>> _reenviar;
        private readonly Func<int, Task<DecisionReenvioModel>> _abonar;
        private readonly IServicioDialogos _dialogos;
        private readonly Action<Exception> _registrarError;
        private readonly Action<bool> _ocupado;

        /// <param name="registrarError">Para ELMAH (opcional): el error no se calla, también se le enseña al usuario.</param>
        /// <param name="ocupado">La pantalla está ocupada mientras se llama a la API (no mientras se ve el diálogo).</param>
        public DecisorEnviosDevueltos(Func<int, ReenvioEnvioPeticion, Task<DecisionReenvioModel>> reenviar, Func<int, Task<DecisionReenvioModel>> abonar,
            IServicioDialogos dialogos, Action<Exception> registrarError = null, Action<bool> ocupado = null)
        {
            _reenviar = reenviar ?? throw new ArgumentNullException(nameof(reenviar));
            _abonar = abonar ?? throw new ArgumentNullException(nameof(abonar));
            _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
            _registrarError = registrarError;
            _ocupado = ocupado;
        }

        public DecisorEnviosDevueltos(IServicioEnviosDevueltos servicio, IServicioDialogos dialogos, Action<Exception> registrarError = null, Action<bool> ocupado = null)
            : this((servicio ?? throw new ArgumentNullException(nameof(servicio))).Reenviar, servicio.Abonar, dialogos, registrarError, ocupado)
        {
        }

        public static ParametrosDialogo ParametrosReenvio(EnvioDevueltoModel devuelto)
        {
            return new ParametrosDialogo
            {
                { ReenviarEnvioDialogViewModel.PARAMETRO_ENVIO, devuelto.Numero },
                { ReenviarEnvioDialogViewModel.PARAMETRO_PEDIDO, devuelto.Pedido.HasValue ? devuelto.Pedido.Value.ToString() : string.Empty },
                { ReenviarEnvioDialogViewModel.PARAMETRO_NOMBRE, devuelto.Nombre },
                { ReenviarEnvioDialogViewModel.PARAMETRO_DIRECCION, devuelto.Direccion },
                { ReenviarEnvioDialogViewModel.PARAMETRO_COD_POSTAL, devuelto.CodPostal },
                { ReenviarEnvioDialogViewModel.PARAMETRO_POBLACION, devuelto.Poblacion },
                { ReenviarEnvioDialogViewModel.PARAMETRO_PROVINCIA, devuelto.Provincia },
                { ReenviarEnvioDialogViewModel.PARAMETRO_TELEFONO, devuelto.Telefono },
                { ReenviarEnvioDialogViewModel.PARAMETRO_MOVIL, devuelto.Movil },
                { ReenviarEnvioDialogViewModel.PARAMETRO_OBSERVACIONES, devuelto.Observaciones }
            };
        }

        public static ReenvioEnvioPeticion PeticionReenvio(ParametrosDialogo parametros)
        {
            return new ReenvioEnvioPeticion
            {
                Direccion = parametros.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_DIRECCION),
                CodPostal = parametros.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_COD_POSTAL),
                Poblacion = parametros.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_POBLACION),
                Provincia = parametros.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_PROVINCIA),
                Telefono = parametros.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_TELEFONO),
                Movil = parametros.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_MOVIL),
                Observaciones = parametros.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_OBSERVACIONES),
                IncluirPortes = parametros.GetValue<bool>(ReenviarEnvioDialogViewModel.PARAMETRO_INCLUIR_PORTES)
            };
        }

        /// <summary>«Pedido 925137» o, sin pedido, «envío 251300»: cómo se nombra en las preguntas.</summary>
        public static string Descripcion(EnvioDevueltoModel devuelto)
            => devuelto.Pedido.HasValue ? $"el pedido {devuelto.Pedido}" : $"el envío {devuelto.Numero}";

        public static string TextoResultadoReenvio(DecisionReenvioModel decision)
        {
            string mensaje = decision?.Mensaje ?? $"Envío nuevo {decision?.EnvioNuevo} creado: imprima la etiqueta cuando llegue la caja.";
            if (decision?.Avisos != null && decision.Avisos.Any())
            {
                mensaje += "\r\r" + string.Join("\r", decision.Avisos);
            }
            return mensaje;
        }

        /// <summary>«El cliente lo sigue queriendo: volver a enviar…».</summary>
        public async Task<ResultadoDecisionDevuelto> Reenviar(EnvioDevueltoModel devuelto)
        {
            if (devuelto == null || !devuelto.SinDecidir)
            {
                return ResultadoDecisionDevuelto.SinCambios;
            }
            ResultadoDialogo resultado = await _dialogos.ShowDialogAsync(ReenviarEnvioDialogViewModel.NOMBRE, ParametrosReenvio(devuelto));
            if (resultado == null || resultado.Result != ResultadoBoton.OK)
            {
                return ResultadoDecisionDevuelto.SinCambios; // cancelado
            }
            ReenvioEnvioPeticion peticion = PeticionReenvio(resultado.Parameters);
            try
            {
                _ocupado?.Invoke(true);
                DecisionReenvioModel decision = await _reenviar(devuelto.Numero, peticion);
                _ocupado?.Invoke(false);
                _dialogos.ShowNotification(TITULO_REENVIAR, TextoResultadoReenvio(decision));
                return ResultadoDecisionDevuelto.Decidido;
            }
            catch (Exception ex)
            {
                _ocupado?.Invoke(false);
                Registrar(ex);
                _dialogos.ShowError($"No se ha podido volver a enviar el envío {devuelto.Numero}:\r{ex.Message}");
                return ResultadoDecisionDevuelto.Error;
            }
        }

        /// <summary>«No lo quiere: abonar». Solo lo apunta: el abono se hace con Copiar factura.</summary>
        public async Task<ResultadoDecisionDevuelto> Abonar(EnvioDevueltoModel devuelto)
        {
            if (devuelto == null || !devuelto.SinDecidir)
            {
                return ResultadoDecisionDevuelto.SinCambios;
            }
            string descripcion = Descripcion(devuelto);
            if (!_dialogos.ShowConfirmationAnswer(TITULO_ABONAR, $"¿Confirma que el cliente no quiere {descripcion} y hay que abonarlo?"))
            {
                return ResultadoDecisionDevuelto.SinCambios;
            }
            try
            {
                _ocupado?.Invoke(true);
                DecisionReenvioModel decision = await _abonar(devuelto.Numero);
                _ocupado?.Invoke(false);
                _dialogos.ShowNotification(TITULO_ABONAR,
                    decision?.Mensaje ?? "Marcado para abonar: hágalo con Copiar factura (abono + rehacer si hiciera falta).");
                return ResultadoDecisionDevuelto.Decidido;
            }
            catch (Exception ex)
            {
                _ocupado?.Invoke(false);
                Registrar(ex);
                _dialogos.ShowError($"No se ha podido marcar para abonar {descripcion}:\r{ex.Message}");
                return ResultadoDecisionDevuelto.Error;
            }
        }

        private void Registrar(Exception ex)
        {
            if (ex is UnauthorizedAccessException)
            {
                return; // sin permiso: no es un error del sistema
            }
            try
            {
                _registrarError?.Invoke(ex);
            }
            catch
            {
                // el registro nunca tapa el error al usuario
            }
        }
    }
}
