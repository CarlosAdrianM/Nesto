using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Carlos 20/11/26: la respuesta de POST api/RepartoPropio/Recogidas/{pedido}/Anular (la nota de recogida ya no hace falta y su
    /// parada sale del reparto).
    /// </summary>
    public class AnulacionRecogidaModel
    {
        public int Pedido { get; set; }
        /// <summary>«NR930553».</summary>
        public string CodigoDocumento { get; set; }
        public int? EnvioId { get; set; }
        /// <summary>«Anulada».</summary>
        public string Estado { get; set; }
        public string Mensaje { get; set; }
    }

    /// <summary>
    /// Carlos 20/11/26: una fila de GET api/RepartoPropio/Recogidas/NoRecogidas: el repartidor marcó «No se ha recogido» y Almacén tiene
    /// que decidir si se vuelve a mandar o se cierra del todo.
    /// </summary>
    public class RecogidaNoRecogidaModel
    {
        public int Pedido { get; set; }
        public string CodigoDocumento { get; set; }
        public int? EnvioId { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string Poblacion { get; set; }
        /// <summary>Lo que dijo el repartidor.</summary>
        public string Motivo { get; set; }
        public string Repartidor { get; set; }
        public DateTime? FechaHora { get; set; }

        // ----------------------------------------------------------- para la pantalla

        /// <summary>«NR930553» (o «NR» + pedido si la API no lo trae).</summary>
        [JsonIgnore]
        public string Codigo => string.IsNullOrWhiteSpace(CodigoDocumento) ? $"NR{Pedido}" : CodigoDocumento.Trim();

        /// <summary>«15191/0 SALÓN EJEMPLO».</summary>
        [JsonIgnore]
        public string ClienteTexto => $"{Cliente?.Trim()}/{Contacto?.Trim()} {Nombre?.Trim()}".Trim();

        /// <summary>«C/ Mayor 1, Madrid».</summary>
        [JsonIgnore]
        public string DireccionTexto => string.Join(", ", new[] { Direccion?.Trim(), Poblacion?.Trim() }.Where(t => !string.IsNullOrEmpty(t)));

        /// <summary>«Andre el 20/11/26 9:42».</summary>
        [JsonIgnore]
        public string QuienCuando
        {
            get
            {
                string quien = string.IsNullOrWhiteSpace(Repartidor) ? "El repartidor" : Repartidor.Trim();
                return FechaHora.HasValue ? $"{quien} el {FechaHora.Value.ToString("dd/MM/yy H:mm", CultureInfo.InvariantCulture)}" : quien;
            }
        }
    }

    /// <summary>Carlos 20/11/26: la respuesta de POST api/RepartoPropio/Recogidas/{pedido}/Reprogramar (la misma nota vuelve al reparto).</summary>
    public class ReprogramacionRecogidaModel
    {
        public int Pedido { get; set; }
        public string CodigoDocumento { get; set; }
        public int? EnvioId { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public string Mensaje { get; set; }
    }

    /// <summary>
    /// Carlos 20/11/26: anular la recogida de una nota que ya está en reparto (y, desde el 26/11/26, la que figura como recogida por
    /// error: misma llamada, solo Almacén o Dirección, 409 RECOGIDA_ABONADA si ya tiene abono), y las «no recogidas» que decide Almacén. Lanza
    /// <see cref="ApiRepartoException"/> con el mensaje para el usuario (400, 403, 404, 409; 503 o ruta que no existe: servidor sin actualizar).
    /// </summary>
    public interface IServicioRecogidasReparto
    {
        /// <summary>POST api/RepartoPropio/Recogidas/{pedido}/Anular { Motivo } (1..500, obligatorio).</summary>
        Task<AnulacionRecogidaModel> Anular(int pedido, string motivo);

        /// <summary>GET api/RepartoPropio/Recogidas/NoRecogidas.</summary>
        Task<List<RecogidaNoRecogidaModel>> LeerNoRecogidas();

        /// <summary>POST api/RepartoPropio/Recogidas/{pedido}/Reprogramar { Fecha? } (sin fecha, el siguiente día de reparto del cliente).</summary>
        Task<ReprogramacionRecogidaModel> Reprogramar(int pedido, DateTime? fecha);
    }

    public class ServicioRecogidasReparto : IServicioRecogidasReparto
    {
        public const string MENSAJE_SIN_PERMISO = "Anular o volver a mandar una recogida es cosa de Almacén, Administración, Dirección e Informática.";
        public const string RUTA_NO_RECOGIDAS = "RepartoPropio/Recogidas/NoRecogidas";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioRecogidasReparto(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public static string RutaAnular(int pedido) => $"RepartoPropio/Recogidas/{pedido}/Anular";
        public static string RutaReprogramar(int pedido) => $"RepartoPropio/Recogidas/{pedido}/Reprogramar";
        public static string CuerpoAnular(string motivo) => JsonConvert.SerializeObject(new { Motivo = motivo });

        /// <summary>{ "Fecha": "yyyy-MM-dd" } o, sin fecha, { "Fecha": null } (el servidor elige el siguiente día de reparto).</summary>
        public static string CuerpoReprogramar(DateTime? fecha)
            => JsonConvert.SerializeObject(new { Fecha = fecha?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) });

        public Task<AnulacionRecogidaModel> Anular(int pedido, string motivo)
            => Post<AnulacionRecogidaModel>(RutaAnular(pedido), CuerpoAnular(motivo));

        public Task<ReprogramacionRecogidaModel> Reprogramar(int pedido, DateTime? fecha)
            => Post<ReprogramacionRecogidaModel>(RutaReprogramar(pedido), CuerpoReprogramar(fecha));

        public async Task<List<RecogidaNoRecogidaModel>> LeerNoRecogidas()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (HttpResponseMessage respuesta = await client.GetAsync(RUTA_NO_RECOGIDAS).ConfigureAwait(false))
            {
                string cuerpo = respuesta.Content == null ? string.Empty : await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw ErroresApiReparto.Excepcion((int)respuesta.StatusCode, cuerpo, MENSAJE_SIN_PERMISO);
                }
                return (string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<List<RecogidaNoRecogidaModel>>(cuerpo))
                    ?? new List<RecogidaNoRecogidaModel>();
            }
        }

        private async Task<T> Post<T>(string ruta, string cuerpoPeticion) where T : class
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var contenido = new StringContent(cuerpoPeticion, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await client.PostAsync(ruta, contenido).ConfigureAwait(false))
            {
                string cuerpo = respuesta.Content == null ? string.Empty : await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!respuesta.IsSuccessStatusCode)
                {
                    throw ErroresApiReparto.Excepcion((int)respuesta.StatusCode, cuerpo, MENSAJE_SIN_PERMISO);
                }
                return string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<T>(cuerpo);
            }
        }
    }

    /// <summary>
    /// Carlos 20/11/26 (caso NR930553, parada 251104: una recogida que ya no hace falta y no se podía anular de ninguna manera):
    /// «Anular recogida», el mismo paso en Agencias › Tramitados, en la pestaña «Entrega» del pedido y en «Cerrar del todo» de las no
    /// recogidas. Pide el motivo (obligatorio, 1..500), confirma (por defecto «No») y enseña el resultado o el error de usted.
    /// Devuelve la anulación o null (cancelado, no válido o error).
    /// </summary>
    public class AnuladorRecogidas
    {
        public const string TITULO = "Anular recogida";
        public const int LONGITUD_MAXIMA = 500;
        public const string MENSAJE_FALTA_MOTIVO = "Indique por qué se anula la recogida. No se ha anulado nada.";
        public static readonly string MENSAJE_MOTIVO_LARGO = $"El motivo no puede pasar de {LONGITUD_MAXIMA} caracteres. No se ha anulado nada.";

        /// <summary>Quién puede (lo comprueba también la API con 403).</summary>
        public static bool PuedeAnular(IConfiguracion configuracion)
            => configuracion != null
               && (configuracion.UsuarioEnGrupo(Nesto.Infrastructure.Shared.Constantes.GruposSeguridad.ALMACEN)
                   || configuracion.UsuarioEnGrupo(Nesto.Infrastructure.Shared.Constantes.GruposSeguridad.ADMINISTRACION)
                   || configuracion.UsuarioEnGrupo(Nesto.Infrastructure.Shared.Constantes.GruposSeguridad.DIRECCION)
                   || configuracion.UsuarioEnGrupo(Nesto.Infrastructure.Shared.Constantes.GruposSeguridad.INFORMATICA));

        private readonly Func<int, string, Task<AnulacionRecogidaModel>> _anular;
        private readonly IServicioDialogos _dialogos;
        private readonly Action<Exception> _registrarError;

        public AnuladorRecogidas(Func<int, string, Task<AnulacionRecogidaModel>> anular, IServicioDialogos dialogos, Action<Exception> registrarError = null)
        {
            _anular = anular ?? throw new ArgumentNullException(nameof(anular));
            _dialogos = dialogos ?? throw new ArgumentNullException(nameof(dialogos));
            _registrarError = registrarError;
        }

        public AnuladorRecogidas(IServicioRecogidasReparto servicio, IServicioDialogos dialogos, Action<Exception> registrarError = null)
            : this((servicio ?? throw new ArgumentNullException(nameof(servicio))).Anular, dialogos, registrarError)
        {
        }

        /// <summary>PURA. El motivo por el que no vale lo escrito, o null si vale.</summary>
        public static string MotivoNoValido(string motivo)
        {
            string limpio = motivo?.Trim();
            if (string.IsNullOrEmpty(limpio))
            {
                return MENSAJE_FALTA_MOTIVO;
            }
            return limpio.Length > LONGITUD_MAXIMA ? MENSAJE_MOTIVO_LARGO : null;
        }

        /// <summary>«la recogida NR930553 (parada 251104)».</summary>
        public static string Descripcion(int pedido, int? envio)
            => envio.HasValue && envio.Value > 0 ? $"la recogida NR{pedido} (parada {envio})" : $"la recogida NR{pedido}";

        /// <summary>PURA. «La recogida NR930553 (parada 251104)».</summary>
        public static string DescripcionMayuscula(int pedido, int? envio)
        {
            string descripcion = Descripcion(pedido, envio);
            return char.ToUpperInvariant(descripcion[0]) + descripcion.Substring(1);
        }

        /// <summary>
        /// PURA. Lo que se pregunta para pedir el motivo. Con <paramref name="figuraRecogida"/> (caso 930553, Carlos 26/11/26: la parada
        /// ya está Entregada porque el repartidor la marcó «Recogido» por error) se dice que figura como recogida y que se anula por error.
        /// </summary>
        public static string TextoPreguntaMotivo(int pedido, int? envio, string nombreCliente, bool figuraRecogida)
        {
            string descripcion = Descripcion(pedido, envio);
            string deQuien = string.IsNullOrWhiteSpace(nombreCliente) ? string.Empty : $" de {nombreCliente.Trim()}";
            if (!figuraRecogida)
            {
                return $"¿Por qué anula {descripcion}{deQuien}? La parada sale del reparto y la nota queda cerrada. El motivo queda en la historia del envío.";
            }
            return $"{DescripcionMayuscula(pedido, envio)}{deQuien} figura como recogida. Si el repartidor la marcó «Recogido» por error, " +
                   "se va a anular: la parada queda cerrada como «Recogida anulada (marcada por error)» y, si algún bulto de esa recogida sigue " +
                   "en una furgoneta, se da por descargado. Solo pueden hacerlo Almacén y Dirección, y no si ya tiene abono.\n\n" +
                   "¿Por qué la anula? El motivo queda en la historia del envío.";
        }

        /// <summary>PURA. La confirmación (por defecto «No»).</summary>
        public static string TextoConfirmacion(int pedido, int? envio, string nombreCliente, string motivo, bool figuraRecogida)
        {
            string descripcion = Descripcion(pedido, envio);
            string deQuien = string.IsNullOrWhiteSpace(nombreCliente) ? string.Empty : $" de {nombreCliente.Trim()}";
            string pregunta = figuraRecogida
                ? $"{DescripcionMayuscula(pedido, envio)}{deQuien} figura como recogida. ¿Confirma que la anula por haberse marcado «Recogido» por error?"
                : $"¿Confirma que anula {descripcion}{deQuien}?";
            return $"{pregunta}\n\nMotivo: {motivo?.Trim()}";
        }

        /// <param name="figuraRecogida">Caso 930553 (Carlos 26/11/26): la parada de la nota ya está Entregada (marcada «Recogido» por
        /// error). La llamada es la misma; la API exige Almacén o Dirección (403 de usted) y que no tenga abono (409 RECOGIDA_ABONADA).</param>
        public async Task<AnulacionRecogidaModel> Anular(int pedido, int? envio, string nombreCliente = null, bool figuraRecogida = false)
        {
            if (pedido <= 0)
            {
                return null;
            }
            string descripcion = Descripcion(pedido, envio);
            string motivo = _dialogos.GetText(TITULO, TextoPreguntaMotivo(pedido, envio, nombreCliente, figuraRecogida));
            if (motivo == null)
            {
                return null; // cancelado
            }
            string noValido = MotivoNoValido(motivo);
            if (noValido != null)
            {
                _dialogos.ShowError(noValido);
                return null;
            }
            if (!_dialogos.ShowConfirmationAnswer(TITULO, TextoConfirmacion(pedido, envio, nombreCliente, motivo, figuraRecogida), true))
            {
                return null;
            }
            try
            {
                AnulacionRecogidaModel anulada = await _anular(pedido, motivo.Trim());
                _dialogos.ShowNotification(TITULO, string.IsNullOrWhiteSpace(anulada?.Mensaje)
                    ? (figuraRecogida ? $"Se ha anulado {descripcion} (marcada «Recogido» por error)." : $"Se ha anulado {descripcion}.")
                    : anulada.Mensaje.Trim());
                return anulada ?? new AnulacionRecogidaModel { Pedido = pedido, EnvioId = envio, Estado = "Anulada" };
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
                _dialogos.ShowError(ex is ApiRepartoException ? ex.Message : $"No se ha podido anular {descripcion}:\r{ex.Message}");
                return null;
            }
        }
    }
}
