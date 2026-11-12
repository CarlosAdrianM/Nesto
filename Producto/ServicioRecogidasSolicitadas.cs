using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Nesto#635 (API en NestoAPI#855): las recogidas que piden los vendedores (NestoApp o Nesto) y que el almacén confirma (nace la
    /// nota de recogida NR{pedido}), rechaza con motivo o anula. Nesto no valida reglas de negocio: lo hace la API y aquí se enseña
    /// su mensaje tal cual (400, 403, 404, 409; 503 si falta el script de la tabla en el servidor).
    /// </summary>
    public interface IServicioRecogidasSolicitadas
    {
        /// <summary>GET api/NotasEntrega/Recogidas/Solicitudes/Pendientes?empresa=: las «Solicitada», de la más antigua a la más reciente.</summary>
        /// <exception cref="RecogidaSolicitadaException">403 (no es de Almacén, Administración, Dirección ni Informática), 503…</exception>
        Task<List<RecogidaSolicitada>> LeerPendientes(string empresa);

        /// <summary>GET api/NotasEntrega/Recogidas/Solicitudes?cliente=&amp;empresa=&amp;contacto=: las del cliente, la más reciente primero (máx. 50).</summary>
        Task<List<RecogidaSolicitada>> LeerDelCliente(string empresa, string cliente, string contacto);

        /// <summary>POST …/{id}/Confirmar { Bultos?, Texto? }: null en lo que no se corrige.</summary>
        Task<RecogidaSolicitada> Confirmar(int id, short? bultos, string texto);

        /// <summary>POST …/{id}/Rechazar { Motivo } (obligatorio).</summary>
        Task<RecogidaSolicitada> Rechazar(int id, string motivo);

        /// <summary>DELETE …/{id}?motivo=. Devuelve la solicitud «Anulada» con AvisoAnulacion si queda algo por hacer a mano.</summary>
        Task<RecogidaSolicitada> Anular(int id, string motivo);
    }

    /// <summary>NestoAPI#855 (RecogidaSolicitadaDTO).</summary>
    public class RecogidaSolicitada
    {
        public const string ESTADO_SOLICITADA = "Solicitada";
        public const string ESTADO_CONFIRMADA = "Confirmada";
        public const string ESTADO_EN_REPARTO = "EnReparto";
        public const string ESTADO_RECOGIDA = "Recogida";
        public const string ESTADO_RECHAZADA = "Rechazada";
        public const string ESTADO_ANULADA = "Anulada";
        public const string RECOGE_REPARTO = "Reparto";
        public const string RECOGE_VENDEDOR = "Vendedor";

        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string NombreCliente { get; set; }
        public string Direccion { get; set; }
        public string CodPostal { get; set; }
        public string Poblacion { get; set; }
        public string Telefono { get; set; }
        public string Ruta { get; set; }
        /// <summary>Qué hay que recoger.</summary>
        public string Texto { get; set; }
        public short Bultos { get; set; }
        public string FacturaOriginal { get; set; }
        /// <summary>«Reparto» (la furgoneta, solo ruta propia) o «Vendedor» (la recoge él al pasar por el cliente).</summary>
        public string QuienRecoge { get; set; }
        public string Vendedor { get; set; }
        public string UsuarioSolicita { get; set; }
        public DateTime FechaSolicitud { get; set; }
        /// <summary>Solicitada, Confirmada, EnReparto, Recogida, Rechazada o Anulada (lo calcula la API).</summary>
        public string Estado { get; set; }
        public int? PedidoNota { get; set; }
        /// <summary>«NR{PedidoNota}»: el código que se lee en Algete; null sin nota.</summary>
        public string CodigoDocumento { get; set; }
        public List<string> CodigosBultos { get; set; } = new List<string>();
        public string UsuarioResuelve { get; set; }
        public DateTime? FechaResolucion { get; set; }
        public string MotivoRechazo { get; set; }
        public string UsuarioAnula { get; set; }
        public DateTime? FechaAnulacion { get; set; }
        public string MotivoAnulacion { get; set; }
        /// <summary>Cuándo se avisó de que llevaba un día laborable sin contestar, o null.</summary>
        public DateTime? FechaAvisoSinRespuesta { get; set; }
        /// <summary>Al anularla: lo que queda por hacer a mano, o null.</summary>
        public string AvisoAnulacion { get; set; }

        // ----------------------------------------------------------- para la pantalla

        [JsonIgnore]
        public bool LaRecogeElVendedor => string.Equals(QuienRecoge?.Trim(), RECOGE_VENDEDOR, StringComparison.OrdinalIgnoreCase);

        /// <summary>«Reparto propio» o «La recoge el vendedor».</summary>
        [JsonIgnore]
        public string QuienRecogeTexto => LaRecogeElVendedor ? "La recoge el vendedor" : "Reparto propio";

        /// <summary>«15191/0».</summary>
        [JsonIgnore]
        public string ClienteContacto => $"{Cliente?.Trim()}/{Contacto?.Trim()}";

        /// <summary>«C/ Mayor 1, 28001 Madrid».</summary>
        [JsonIgnore]
        public string DireccionCompleta
        {
            get
            {
                string poblacion = string.Join(" ", new[] { CodPostal?.Trim(), Poblacion?.Trim() }.Where(t => !string.IsNullOrEmpty(t)));
                return string.Join(", ", new[] { Direccion?.Trim(), poblacion }.Where(t => !string.IsNullOrEmpty(t)));
            }
        }

        /// <summary>«carlos (vendedor CAM) el 12/11/26 10:15».</summary>
        [JsonIgnore]
        public string Pedida
        {
            get
            {
                string quien = UsuarioSolicita?.Trim();
                if (!string.IsNullOrWhiteSpace(Vendedor))
                {
                    quien = string.IsNullOrEmpty(quien) ? $"vendedor {Vendedor.Trim()}" : $"{quien} (vendedor {Vendedor.Trim()})";
                }
                return $"{quien} el {FechaSolicitud:dd/MM/yy HH:mm}".Trim();
            }
        }

        [JsonIgnore]
        public bool SinRespuesta => FechaAvisoSinRespuesta.HasValue;

        /// <summary>«Sin respuesta (avisado el 13/11/26 9:00)» o vacío.</summary>
        [JsonIgnore]
        public string AvisoSinRespuestaTexto => FechaAvisoSinRespuesta.HasValue
            ? $"Sin respuesta (avisado el {FechaAvisoSinRespuesta.Value:dd/MM/yy H:mm})"
            : string.Empty;

        /// <summary>El estado como lo lee una persona («En reparto»).</summary>
        [JsonIgnore]
        public string EstadoTexto => Estado == ESTADO_EN_REPARTO ? "En reparto" : Estado;

        /// <summary>Lo que se sabe de cómo se resolvió (motivo del rechazo o de la anulación, quién y cuándo).</summary>
        [JsonIgnore]
        public string Resolucion
        {
            get
            {
                if (Estado == ESTADO_ANULADA)
                {
                    return $"Anulada por {UsuarioAnula?.Trim()}{Fecha(FechaAnulacion)}{Motivo(MotivoAnulacion)}";
                }
                if (Estado == ESTADO_RECHAZADA)
                {
                    return $"Rechazada por {UsuarioResuelve?.Trim()}{Fecha(FechaResolucion)}{Motivo(MotivoRechazo)}";
                }
                if (!string.IsNullOrWhiteSpace(UsuarioResuelve))
                {
                    return $"Confirmada por {UsuarioResuelve.Trim()}{Fecha(FechaResolucion)}";
                }
                return string.Empty;
            }
        }

        /// <summary>Lo que la API deja anular (lo recogido, rechazado o anulado da 409): quién puede, lo decide ella (403).</summary>
        [JsonIgnore]
        public bool SePuedeAnular => Estado == ESTADO_SOLICITADA || Estado == ESTADO_CONFIRMADA || Estado == ESTADO_EN_REPARTO;

        private static string Fecha(DateTime? fecha) => fecha.HasValue ? $" el {fecha.Value:dd/MM/yy HH:mm}" : string.Empty;

        private static string Motivo(string motivo) => string.IsNullOrWhiteSpace(motivo) ? string.Empty : $": {motivo.Trim()}";
    }

    public class RecogidaSolicitadaException : Exception
    {
        public RecogidaSolicitadaException(string mensaje, int codigo) : base(mensaje)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP: 400, 403, 404, 409, 503…</summary>
        public int Codigo { get; }
    }

    public class ServicioRecogidasSolicitadas : IServicioRecogidasSolicitadas
    {
        internal const string URL = "NotasEntrega/Recogidas/Solicitudes";
        internal const string MENSAJE_SIN_PERMISO = "Su usuario no puede ver ni contestar las recogidas solicitadas: es cosa de Almacén, " +
            "Administración, Dirección e Informática.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioRecogidasSolicitadas(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        private static string Escapar(string valor) => Uri.EscapeDataString(valor?.Trim() ?? string.Empty);

        internal static string UrlPendientes(string empresa) => $"{URL}/Pendientes?empresa={Escapar(empresa)}";

        internal static string UrlDelCliente(string empresa, string cliente, string contacto)
            => $"{URL}?cliente={Escapar(cliente)}&empresa={Escapar(empresa)}"
               + (string.IsNullOrWhiteSpace(contacto) ? string.Empty : $"&contacto={Escapar(contacto)}");

        internal static string UrlAnular(int id, string motivo)
            => $"{URL}/{id}" + (string.IsNullOrWhiteSpace(motivo) ? string.Empty : $"?motivo={Escapar(motivo)}");

        public async Task<List<RecogidaSolicitada>> LeerPendientes(string empresa)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(UrlPendientes(empresa)).ConfigureAwait(false);
                return await LeerRespuesta<List<RecogidaSolicitada>>(response).ConfigureAwait(false) ?? new List<RecogidaSolicitada>();
            }
        }

        public async Task<List<RecogidaSolicitada>> LeerDelCliente(string empresa, string cliente, string contacto)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(UrlDelCliente(empresa, cliente, contacto)).ConfigureAwait(false);
                return await LeerRespuesta<List<RecogidaSolicitada>>(response).ConfigureAwait(false) ?? new List<RecogidaSolicitada>();
            }
        }

        public Task<RecogidaSolicitada> Confirmar(int id, short? bultos, string texto)
            => Enviar($"{URL}/{id}/Confirmar", new { Bultos = bultos, Texto = texto });

        public Task<RecogidaSolicitada> Rechazar(int id, string motivo)
            => Enviar($"{URL}/{id}/Rechazar", new { Motivo = motivo });

        public async Task<RecogidaSolicitada> Anular(int id, string motivo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.DeleteAsync(UrlAnular(id, motivo)).ConfigureAwait(false);
                return await LeerRespuesta<RecogidaSolicitada>(response).ConfigureAwait(false);
            }
        }

        private async Task<RecogidaSolicitada> Enviar(string url, object cuerpo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                return await LeerRespuesta<RecogidaSolicitada>(response).ConfigureAwait(false);
            }
        }

        private static async Task<T> LeerRespuesta<T>(HttpResponseMessage response)
        {
            string cuerpo = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                int codigo = (int)response.StatusCode;
                string motivo = ProductoService.MotivoDelErrorDeTexto(cuerpo);
                if (string.IsNullOrWhiteSpace(motivo))
                {
                    motivo = codigo == 403 ? MENSAJE_SIN_PERMISO : $"El servidor ha contestado con el error {codigo}.";
                }
                throw new RecogidaSolicitadaException(motivo, codigo);
            }
            return JsonConvert.DeserializeObject<T>(cuerpo ?? string.Empty);
        }
    }
}
