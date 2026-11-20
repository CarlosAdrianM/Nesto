using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Carlos 20/11/26: un error de un endpoint nuevo del reparto propio (nota de un envío, anular o reprogramar una recogida, envíos
    /// internos). El texto ya es para el usuario: el «Message» de la API o, si el servidor todavía no tiene el endpoint (404 de ruta,
    /// 405, 501 o 503 sin mensaje), <see cref="ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR"/>.
    /// </summary>
    public class ApiRepartoException : InvalidOperationException
    {
        public ApiRepartoException(string mensaje, int codigoHttp, string codigoApi, bool servidorSinActualizar) : base(mensaje)
        {
            CodigoHttp = codigoHttp;
            CodigoApi = codigoApi;
            ServidorSinActualizar = servidorSinActualizar;
        }

        /// <summary>400, 403, 404, 409, 503…</summary>
        public int CodigoHttp { get; }

        /// <summary>El «Codigo» del cuerpo («EN_FURGONETA», «RECOGIDA_YA_RECOGIDA»…), o null.</summary>
        public string CodigoApi { get; }

        /// <summary>True si el servidor aún no tiene el endpoint: la pantalla lo dice y sigue funcionando.</summary>
        public bool ServidorSinActualizar { get; }
    }

    /// <summary>Carlos 20/11/26: cómo se traduce la respuesta de error de los endpoints nuevos del reparto propio.</summary>
    public static class ErroresApiReparto
    {
        public const string MENSAJE_SERVIDOR_SIN_ACTUALIZAR = "Esto estará disponible cuando se actualice el servidor.";

        /// <summary>Lo que dice Web API cuando la ruta no existe (servidor anterior): no es un mensaje para el usuario.</summary>
        private const string RUTA_NO_ENCONTRADA = "No HTTP resource was found";
        private const string METODO_NO_ADMITIDO = "does not support http method";

        /// <summary>PURA. La excepción para un código HTTP de error y su cuerpo.</summary>
        /// <param name="mensajeSinPermiso">El texto de usted para un 401/403 que no trae mensaje.</param>
        public static ApiRepartoException Excepcion(int codigoHttp, string cuerpo, string mensajeSinPermiso)
        {
            string mensaje = MensajeDelCuerpo(cuerpo, out string codigoApi);
            bool rutaQueNoExiste = mensaje != null
                && (mensaje.IndexOf(RUTA_NO_ENCONTRADA, StringComparison.OrdinalIgnoreCase) >= 0
                    || mensaje.IndexOf(METODO_NO_ADMITIDO, StringComparison.OrdinalIgnoreCase) >= 0);
            if (codigoHttp == 405 || codigoHttp == 501 || rutaQueNoExiste
                || ((codigoHttp == 404 || codigoHttp == 503) && string.IsNullOrWhiteSpace(mensaje)))
            {
                return new ApiRepartoException(MENSAJE_SERVIDOR_SIN_ACTUALIZAR, codigoHttp, codigoApi, true);
            }
            if (string.IsNullOrWhiteSpace(mensaje))
            {
                mensaje = codigoHttp == 401 || codigoHttp == 403
                    ? mensajeSinPermiso ?? "No tiene permiso para hacer esto."
                    : $"El servidor ha contestado con el error {codigoHttp}.";
            }
            return new ApiRepartoException(mensaje, codigoHttp, codigoApi, codigoHttp == 503);
        }

        /// <summary>PURA. El «Message» (o «message» de «error») de un JSON, o la cadena si es una cadena JSON; el texto tal cual si no es JSON.</summary>
        internal static string MensajeDelCuerpo(string cuerpo, out string codigoApi)
        {
            codigoApi = null;
            string limpio = cuerpo?.Trim();
            if (string.IsNullOrEmpty(limpio))
            {
                return null;
            }
            try
            {
                JToken token = JToken.Parse(limpio);
                if (token.Type == JTokenType.String)
                {
                    return ((string)token)?.Trim();
                }
                if (token is JObject objeto)
                {
                    codigoApi = (string)objeto.GetValue("Codigo", StringComparison.OrdinalIgnoreCase);
                    string mensaje = (string)objeto.GetValue("Message", StringComparison.OrdinalIgnoreCase);
                    if (string.IsNullOrWhiteSpace(mensaje) && objeto["error"] is JObject error)
                    {
                        mensaje = (string)error["message"];
                        codigoApi ??= (string)error["code"];
                    }
                    return mensaje?.Trim();
                }
                return null;
            }
            catch (JsonException)
            {
                // Un HTML o un texto: si es corto, tal cual; si no, no se enseña
                return limpio.StartsWith("<") || limpio.Length > 500 ? null : limpio;
            }
        }
    }

    /// <summary>Carlos 20/11/26: la respuesta de POST api/EnviosAgencias/{id}/Nota (la fila que ha quedado en la historia del envío).</summary>
    public class NotaEnvioModel
    {
        public int Numero { get; set; }
        public string Texto { get; set; }
        public string Usuario { get; set; }
        public DateTime? Fecha { get; set; }
    }

    /// <summary>Carlos 20/11/26: una nota libre en la historia de un envío.</summary>
    public interface IServicioNotasEnvio
    {
        /// <summary>POST api/EnviosAgencias/{id}/Nota { Texto }. Lanza <see cref="ApiRepartoException"/> (403, 404, 503…).</summary>
        Task<NotaEnvioModel> Anadir(int numeroEnvio, string texto);
    }

    public class ServicioNotasEnvio : IServicioNotasEnvio
    {
        public const string MENSAJE_SIN_PERMISO = "No tiene permiso para añadir notas a los envíos.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioNotasEnvio(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public static string RutaNota(int numeroEnvio) => $"EnviosAgencias/{numeroEnvio}/Nota";

        public static string CuerpoNota(string texto) => JsonConvert.SerializeObject(new { Texto = texto });

        public async Task<NotaEnvioModel> Anadir(int numeroEnvio, string texto)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var contenido = new StringContent(CuerpoNota(texto), Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await client.PostAsync(RutaNota(numeroEnvio), contenido).ConfigureAwait(false))
            {
                string cuerpo = respuesta.Content == null ? string.Empty : await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw ErroresApiReparto.Excepcion((int)respuesta.StatusCode, cuerpo, MENSAJE_SIN_PERMISO);
                }
                return string.IsNullOrWhiteSpace(cuerpo) ? new NotaEnvioModel { Texto = texto } : JsonConvert.DeserializeObject<NotaEnvioModel>(cuerpo);
            }
        }
    }

    /// <summary>
    /// Carlos 20/11/26: «Añadir nota» a un envío, el mismo paso en Agencias › Tramitados y en la pestaña «Entrega» del pedido. Pide el
    /// texto (1..500), llama a la API y enseña el resultado o el error de usted. Devuelve la nota guardada o null (cancelado, no válido o error).
    /// </summary>
    public class AnotadorEnvios
    {
        public const string TITULO = "Añadir nota";
        public const int LONGITUD_MAXIMA = 500;
        public const string MENSAJE_VACIA = "Escriba el texto de la nota.";
        public static readonly string MENSAJE_LARGA = $"La nota no puede pasar de {LONGITUD_MAXIMA} caracteres.";

        private readonly Func<int, string, Task<NotaEnvioModel>> _anadir;
        private readonly IServicioDialogos _dialogos;
        private readonly Action<Exception> _registrarError;

        public AnotadorEnvios(Func<int, string, Task<NotaEnvioModel>> anadir, IServicioDialogos dialogos, Action<Exception> registrarError = null)
        {
            _anadir = anadir ?? throw new ArgumentNullException(nameof(anadir));
            _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
            _registrarError = registrarError;
        }

        public AnotadorEnvios(IServicioNotasEnvio servicio, IServicioDialogos dialogos, Action<Exception> registrarError = null)
            : this((servicio ?? throw new ArgumentNullException(nameof(servicio))).Anadir, dialogos, registrarError)
        {
        }

        /// <summary>PURA. El motivo por el que el texto no vale, o null si vale (ya recortado).</summary>
        public static string MotivoTextoNoValido(string texto)
        {
            string limpio = texto?.Trim();
            if (string.IsNullOrEmpty(limpio))
            {
                return MENSAJE_VACIA;
            }
            return limpio.Length > LONGITUD_MAXIMA ? MENSAJE_LARGA : null;
        }

        public async Task<NotaEnvioModel> AnadirNota(int numeroEnvio, int? pedido)
        {
            if (numeroEnvio <= 0)
            {
                return null;
            }
            string descripcion = pedido.HasValue ? $"el envío {numeroEnvio} (pedido {pedido})" : $"el envío {numeroEnvio}";
            string texto = _dialogos.GetText(TITULO, $"Escriba la nota para {descripcion}. Queda en la historia del envío con su usuario y la hora.");
            if (texto == null)
            {
                return null; // cancelado
            }
            string motivo = MotivoTextoNoValido(texto);
            if (motivo != null)
            {
                _dialogos.ShowError(motivo);
                return null;
            }
            try
            {
                NotaEnvioModel nota = await _anadir(numeroEnvio, texto.Trim());
                _dialogos.ShowNotification(TITULO, $"Nota añadida a {descripcion}.");
                return nota ?? new NotaEnvioModel { Texto = texto.Trim() };
            }
            catch (Exception ex)
            {
                if (!(ex is ApiRepartoException api) || (api.CodigoHttp >= 500 && !api.ServidorSinActualizar))
                {
                    try
                    {
                        _registrarError?.Invoke(ex);
                    }
                    catch
                    {
                        // el registro nunca tapa el error al usuario
                    }
                }
                _dialogos.ShowError(ex is ApiRepartoException ? ex.Message : $"No se ha podido añadir la nota a {descripcion}:\r{ex.Message}");
                return null;
            }
        }
    }
}
