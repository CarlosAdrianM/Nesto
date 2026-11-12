using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Nesto#636 (API en NestoAPI#857, fase 3): los envíos de proveedor que la API no ha sabido asignar a un pedido de compra (ningún
    /// candidato o varios) y que Compras asigna a mano, descarta («no es una compra») o deja recordado por el remitente del paquete.
    /// Nesto no decide nada: la API pone la fecha de recepción y contesta con su Mensaje, que se enseña tal cual (400, 403, 404, 409;
    /// 503 si falta Scripts/Issue857d en el servidor).
    /// </summary>
    public interface IServicioEnviosProveedorPorAsignar
    {
        /// <summary>GET api/Compras/AgenciasEntrada/PorAsignar?dias=: los envíos por asignar y todos los pedidos de compra en el aire.</summary>
        /// <exception cref="EnvioProveedorPorAsignarException">403 (no es de Compras, Dirección ni Informática), 503…</exception>
        Task<EnviosProveedorPorAsignar> LeerPorAsignar(int dias);

        /// <summary>
        /// POST api/Compras/AgenciasEntrada/Correos/{correo}/Asignar?reasignar= { Empresa, Pedido, RecordarRemitente }. Basta con la Clave
        /// del envío: la API lo propaga a los demás correos del mismo envío. 409 si ya está aplicado a otro pedido (con reasignar = true se
        /// aplica al nuevo y la fecha del anterior no se deshace).
        /// </summary>
        Task<RespuestaAsignacionEnvioProveedor> Asignar(int correo, string empresa, int pedido, bool recordarRemitente, bool reasignar);

        /// <summary>POST api/Compras/AgenciasEntrada/Correos/{correo}/Descartar: no es de ninguna compra (409 si ya está aplicado).</summary>
        Task<RespuestaDescarteEnvioProveedor> Descartar(int correo);

        /// <summary>GET api/Compras/AgenciasEntrada/RemitentesPaquete: quién expide el paquete → de qué proveedor es.</summary>
        Task<List<RemitentePaqueteRecordado>> LeerRemitentesPaquete();

        /// <summary>DELETE api/Compras/AgenciasEntrada/RemitentesPaquete/{id} (404 si ya no está).</summary>
        Task BorrarRemitentePaquete(int id);
    }

    /// <summary>NestoAPI#857 (PorAsignarCorreosCompraDTO).</summary>
    public class EnviosProveedorPorAsignar
    {
        public List<EnvioProveedorPorAsignar> Envios { get; set; } = new List<EnvioProveedorPorAsignar>();
        public List<PedidoCompraPorAsignar> PedidosEnAire { get; set; } = new List<PedidoCompraPorAsignar>();
    }

    /// <summary>NestoAPI#857 (EnvioPorAsignarDTO): un envío (los correos que comparten código de seguimiento).</summary>
    public class EnvioProveedorPorAsignar
    {
        /// <summary>El Id del primer correo por asignar del envío: con él se asigna o se descarta todo el envío.</summary>
        public int Clave { get; set; }
        public DateTime PrimerCorreo { get; set; }
        public List<CorreoEnvioProveedor> Correos { get; set; } = new List<CorreoEnvioProveedor>();
        public List<string> Codigos { get; set; } = new List<string>();
        public string Agencia { get; set; }
        /// <summary>La entrega estimada más tardía de sus correos (null si ninguno la dice).</summary>
        public DateTime? EntregaEstimadaDia { get; set; }
        /// <summary>La fecha de recepción que se pondría al asignarlo hoy.</summary>
        public DateTime FechaRecepcionPropuesta { get; set; }
        /// <summary>Quién expide el paquete según el aviso (no siempre es el proveedor).</summary>
        public string RemitentePaquete { get; set; }
        public string ProveedorPropuesto { get; set; }
        /// <summary>Los pedidos candidatos (Motivo, líneas y, como mucho uno, el Sugerido).</summary>
        public List<PedidoCompraPorAsignar> Candidatos { get; set; } = new List<PedidoCompraPorAsignar>();

        // ----------------------------------------------------------- para la pantalla

        [JsonIgnore]
        public string CodigosTexto => string.Join(", ", (Codigos ?? new List<string>()).Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()));

        [JsonIgnore]
        public string AgenciaTexto => string.IsNullOrWhiteSpace(Agencia) ? "Agencia sin identificar" : Agencia.Trim();

        [JsonIgnore]
        public bool HayRemitentePaquete => !string.IsNullOrWhiteSpace(RemitentePaquete);

        [JsonIgnore]
        public string EntregaEstimadaTexto => EntregaEstimadaDia.HasValue
            ? EntregaEstimadaDia.Value.ToString("dddd dd/MM/yy", CultureInfo.GetCultureInfo("es-ES"))
            : "El correo no la dice";

        [JsonIgnore]
        public string FechaRecepcionPropuestaTexto => FechaRecepcionPropuesta.ToString("dddd dd/MM/yy", CultureInfo.GetCultureInfo("es-ES"));

        /// <summary>«1 candidato», «3 candidatos» o «Sin candidatos».</summary>
        [JsonIgnore]
        public string CandidatosTexto
        {
            get
            {
                int n = Candidatos?.Count ?? 0;
                return n == 0 ? "Sin candidatos" : n == 1 ? "1 candidato" : $"{n} candidatos";
            }
        }

        /// <summary>¿Es de este envío el correo (el de la campana)?</summary>
        public bool Contiene(int correo) => Clave == correo || (Correos?.Any(c => c.Id == correo) ?? false);
    }

    /// <summary>NestoAPI#857 (CorreoSeguimientoCompraDTO): lo que se enseña de cada correo del envío.</summary>
    public class CorreoEnvioProveedor
    {
        public int Id { get; set; }
        public DateTime Recibido { get; set; }
        public string RemitenteDireccion { get; set; }
        public string RemitenteDominio { get; set; }
        public string Asunto { get; set; }
        public string Agencia { get; set; }
        public string Codigo { get; set; }
        public string Url { get; set; }
        public string Extracto { get; set; }
        public string ProveedorPropuesto { get; set; }
        public DateTime? EntregaEstimadaDia { get; set; }
        public string RemitentePaquete { get; set; }

        [JsonIgnore]
        public string Remitente => string.IsNullOrWhiteSpace(RemitenteDireccion) ? RemitenteDominio?.Trim() : RemitenteDireccion.Trim();
    }

    /// <summary>
    /// NestoAPI#857: un pedido de compra en el aire. La API usa dos DTO con casi lo mismo (CandidatoPorAsignarDTO, con Motivo y Sugerido,
    /// y PedidoEnAireDTO, con EsCandidato y Envios); aquí van en una sola clase para elegirlos igual en la pantalla.
    /// </summary>
    public class PedidoCompraPorAsignar
    {
        public const string RESTO = "resto de un pedido ya recibido en parte";

        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string Proveedor { get; set; }
        public string NombreProveedor { get; set; }
        /// <summary>Solo en los candidatos: por qué lo propone la API.</summary>
        public string Motivo { get; set; }
        /// <summary>Cuando se envió al proveedor.</summary>
        public DateTime? FechaEnvio { get; set; }
        public int LineasPendientes { get; set; }
        /// <summary>Mayor que 0: lo pendiente es un resto.</summary>
        public int LineasYaRecibidas { get; set; }
        /// <summary>Solo en los candidatos: el que propone la API (como mucho uno por envío). No se elige solo en la API: aquí sale preseleccionado.</summary>
        public bool Sugerido { get; set; }
        /// <summary>Solo en los pedidos en el aire: es candidato de algún envío de la lista.</summary>
        public bool EsCandidato { get; set; }
        /// <summary>Solo en los pedidos en el aire: las claves de los envíos de los que es candidato.</summary>
        public List<int> Envios { get; set; } = new List<int>();

        // ----------------------------------------------------------- para la pantalla

        [JsonIgnore]
        public string EmpresaPedido => string.IsNullOrWhiteSpace(Empresa) ? Constantes.Empresas.EMPRESA_DEFECTO : Empresa.Trim();

        /// <summary>«COSMÉTICOS EJEMPLO (1234)», o solo el código si no viene el nombre.</summary>
        [JsonIgnore]
        public string ProveedorTexto => string.IsNullOrWhiteSpace(NombreProveedor)
            ? Proveedor?.Trim()
            : $"{NombreProveedor.Trim()} ({Proveedor?.Trim()})";

        [JsonIgnore]
        public string FechaEnvioTexto => FechaEnvio.HasValue ? FechaEnvio.Value.ToString("dd/MM/yy HH:mm") : string.Empty;

        [JsonIgnore]
        public bool EsResto => LineasYaRecibidas > 0;

        /// <summary>«3 pendientes» o «3 pendientes y 2 ya recibidas: resto de un pedido ya recibido en parte».</summary>
        [JsonIgnore]
        public string LineasTexto => EsResto
            ? $"{LineasPendientes} pendientes y {LineasYaRecibidas} ya {(LineasYaRecibidas == 1 ? "recibida" : "recibidas")}: {RESTO}"
            : $"{LineasPendientes} pendientes";

        [JsonIgnore]
        public string SugeridoTexto => Sugerido ? "Sugerido" : string.Empty;

        /// <summary>El mismo pedido (empresa y número), venga de los candidatos o de los pedidos en el aire.</summary>
        public bool EsElMismo(PedidoCompraPorAsignar otro)
            => otro != null && otro.Pedido == Pedido && string.Equals(otro.EmpresaPedido, EmpresaPedido, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>NestoAPI#857 (RespuestaAsignacionCorreoCompraDTO).</summary>
    public class RespuestaAsignacionEnvioProveedor
    {
        public int CorreoId { get; set; }
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string Proveedor { get; set; }
        public int LineasPendientes { get; set; }
        public int LineasCambiadas { get; set; }
        public List<DateTime?> FechasAnteriores { get; set; } = new List<DateTime?>();
        public DateTime FechaNueva { get; set; }
        public bool YaEstabaAplicado { get; set; }
        public string EmpresaAnterior { get; set; }
        public int? PedidoAnterior { get; set; }
        public string RemitentePaquete { get; set; }
        public bool RemitenteRecordado { get; set; }
        public int CorreosDelMismoEnvio { get; set; }
        public bool? EligioSugerido { get; set; }
        public string Mensaje { get; set; }
    }

    /// <summary>NestoAPI#857 (RespuestaDescarteCorreoCompraDTO).</summary>
    public class RespuestaDescarteEnvioProveedor
    {
        public int CorreoId { get; set; }
        public int CorreosDelMismoEnvio { get; set; }
        public string Mensaje { get; set; }
    }

    /// <summary>NestoAPI#857 (RemitentePaqueteRecordado): quién expide el paquete → de qué proveedor es.</summary>
    public class RemitentePaqueteRecordado
    {
        public int Id { get; set; }
        public string RemitentePaquete { get; set; }
        public string Empresa { get; set; }
        public string Proveedor { get; set; }
        public string Usuario { get; set; }
        public DateTime? FechaModificacion { get; set; }

        [JsonIgnore]
        public string FechaModificacionTexto => FechaModificacion.HasValue ? FechaModificacion.Value.ToString("dd/MM/yy HH:mm") : string.Empty;
    }

    public class EnvioProveedorPorAsignarException : Exception
    {
        public EnvioProveedorPorAsignarException(string mensaje, int codigo) : base(mensaje)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP: 400, 403, 404, 409, 503…</summary>
        public int Codigo { get; }
    }

    public class ServicioEnviosProveedorPorAsignar : IServicioEnviosProveedorPorAsignar
    {
        internal const string URL = "Compras/AgenciasEntrada";
        internal const string MENSAJE_SIN_PERMISO = "Su usuario no puede ver ni asignar los envíos de proveedor: es cosa de Compras, " +
            "Dirección e Informática.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEnviosProveedorPorAsignar(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        internal static string UrlPorAsignar(int dias) => $"{URL}/PorAsignar?dias={dias.ToString(CultureInfo.InvariantCulture)}";

        internal static string UrlAsignar(int correo, bool reasignar)
            => $"{URL}/Correos/{correo.ToString(CultureInfo.InvariantCulture)}/Asignar?reasignar={(reasignar ? "true" : "false")}";

        internal static string UrlDescartar(int correo) => $"{URL}/Correos/{correo.ToString(CultureInfo.InvariantCulture)}/Descartar";

        internal static string UrlRemitentesPaquete() => $"{URL}/RemitentesPaquete";

        internal static string UrlBorrarRemitentePaquete(int id) => $"{URL}/RemitentesPaquete/{id.ToString(CultureInfo.InvariantCulture)}";

        public async Task<EnviosProveedorPorAsignar> LeerPorAsignar(int dias)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(UrlPorAsignar(dias)).ConfigureAwait(false);
                EnviosProveedorPorAsignar leido = await LeerRespuesta<EnviosProveedorPorAsignar>(response).ConfigureAwait(false)
                    ?? new EnviosProveedorPorAsignar();
                leido.Envios ??= new List<EnvioProveedorPorAsignar>();
                leido.PedidosEnAire ??= new List<PedidoCompraPorAsignar>();
                return leido;
            }
        }

        public async Task<RespuestaAsignacionEnvioProveedor> Asignar(int correo, string empresa, int pedido, bool recordarRemitente, bool reasignar)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var cuerpo = new { Empresa = empresa?.Trim(), Pedido = pedido, RecordarRemitente = recordarRemitente };
                var contenido = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(UrlAsignar(correo, reasignar), contenido).ConfigureAwait(false);
                return await LeerRespuesta<RespuestaAsignacionEnvioProveedor>(response).ConfigureAwait(false);
            }
        }

        public async Task<RespuestaDescarteEnvioProveedor> Descartar(int correo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(string.Empty, Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(UrlDescartar(correo), contenido).ConfigureAwait(false);
                return await LeerRespuesta<RespuestaDescarteEnvioProveedor>(response).ConfigureAwait(false);
            }
        }

        public async Task<List<RemitentePaqueteRecordado>> LeerRemitentesPaquete()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(UrlRemitentesPaquete()).ConfigureAwait(false);
                return await LeerRespuesta<List<RemitentePaqueteRecordado>>(response).ConfigureAwait(false) ?? new List<RemitentePaqueteRecordado>();
            }
        }

        public async Task BorrarRemitentePaquete(int id)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.DeleteAsync(UrlBorrarRemitentePaquete(id)).ConfigureAwait(false);
                _ = await LeerRespuesta<object>(response).ConfigureAwait(false);
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
                    motivo = codigo switch
                    {
                        403 => MENSAJE_SIN_PERMISO,
                        404 => "Ya no existe: lo habrá borrado o resuelto otra persona.",
                        _ => $"El servidor ha contestado con el error {codigo}."
                    };
                }
                throw new EnvioProveedorPorAsignarException(motivo, codigo);
            }
            return string.IsNullOrWhiteSpace(cuerpo) ? default : JsonConvert.DeserializeObject<T>(cuerpo);
        }
    }
}
