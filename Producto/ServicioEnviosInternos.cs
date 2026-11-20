using ControlesUsuario.Services;
using Nesto.Infrastructure.Contracts;
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
    /// Sugerencia 1277 (Alfredo, Carlos 20/11/26): a las tiendas se manda, además de las reposiciones, material sin stock (papel
    /// higiénico, café, folios) y se mueven aparatos de demostración o de clientes de una sede a otra. Hasta ahora iba sin etiqueta ni
    /// registro; ahora es un «envío interno» con sus bultos EI{id}-{n}, que la furgoneta carga y entrega como los demás. Nesto no valida
    /// reglas de negocio (lo hace la API): enseña su mensaje. Lanza <see cref="ApiRepartoException"/> (403 de usted; 404/503 de un
    /// servidor que aún no lo tiene: «Esto estará disponible cuando se actualice el servidor.»; 409 EN_FURGONETA al anular).
    /// </summary>
    public interface IServicioEnviosInternos
    {
        /// <summary>POST api/RepartoPropio/EnviosInternos { Origen, Destino, Descripcion, Bultos, Impresora? } → 201.</summary>
        Task<EnvioInterno> Crear(NuevoEnvioInterno envio);

        /// <summary>POST api/RepartoPropio/EnviosInternos/{id}/ImprimirEtiquetas { Impresora? }.</summary>
        Task<EnvioInterno> ImprimirEtiquetas(int id, string impresora);

        /// <summary>GET api/RepartoPropio/EnviosInternos?dias=&amp;soloVivos=.</summary>
        Task<List<EnvioInterno>> Leer(int dias, bool soloVivos);

        /// <summary>DELETE api/RepartoPropio/EnviosInternos/{id}?motivo=.</summary>
        Task Anular(int id, string motivo);
    }

    /// <summary>El cuerpo del alta (mismos nombres que la API).</summary>
    public class NuevoEnvioInterno
    {
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Descripcion { get; set; }
        public int Bultos { get; set; }
        /// <summary>Opcional: sin ella, la que decida el servidor.</summary>
        public string Impresora { get; set; }
    }

    /// <summary>Un bulto del envío interno y dónde está («En Alcobendas, sin recoger», «En la furgoneta de Miguel», «Entregado»…).</summary>
    public class BultoEnvioInterno
    {
        public string Codigo { get; set; }
        public string Situacion { get; set; }
    }

    /// <summary>Un envío interno (la respuesta del alta, de reimprimir y cada fila de la lista).</summary>
    public class EnvioInterno
    {
        public const string ESTADO_PENDIENTE = "Pendiente";
        public const string ESTADO_EN_CAMINO = "EnCamino";
        public const string ESTADO_ENTREGADO = "Entregado";
        public const string ESTADO_ANULADO = "Anulado";

        public int Id { get; set; }
        public string Origen { get; set; }
        public string Destino { get; set; }
        public string Descripcion { get; set; }
        public int Bultos { get; set; }
        public string Estado { get; set; }
        public string UsuarioAlta { get; set; }
        public DateTime? FechaAlta { get; set; }
        public string Receptor { get; set; }
        public DateTime? FechaEntrega { get; set; }
        public List<BultoEnvioInterno> BultosDetalle { get; set; } = new List<BultoEnvioInterno>();
        /// <summary>Solo en el alta y al reimprimir: los códigos de las etiquetas (EI12-1…).</summary>
        public List<string> Codigos { get; set; } = new List<string>();
        /// <summary>Solo en el alta y al reimprimir: si han salido por la impresora.</summary>
        public bool EtiquetasImpresas { get; set; }

        // ----------------------------------------------------------- para la pantalla

        /// <summary>«Algete → Alcobendas».</summary>
        [JsonIgnore]
        public string Trayecto => $"{SedesEnvioInterno.Nombre(Origen)} → {SedesEnvioInterno.Nombre(Destino)}";

        /// <summary>«En camino» en vez de «EnCamino».</summary>
        [JsonIgnore]
        public string EstadoTexto => Estado == ESTADO_EN_CAMINO ? "En camino" : Estado;

        /// <summary>«carlos el 20/11/26 10:15».</summary>
        [JsonIgnore]
        public string Alta => FechaAlta.HasValue ? $"{UsuarioAlta?.Trim()} el {FechaAlta.Value.ToString("dd/MM/yy H:mm", CultureInfo.InvariantCulture)}".Trim() : UsuarioAlta?.Trim();

        /// <summary>«Recibido por Paloma el 20/11/26 13:05» o vacío.</summary>
        [JsonIgnore]
        public string Entrega
        {
            get
            {
                if (!FechaEntrega.HasValue && string.IsNullOrWhiteSpace(Receptor))
                {
                    return string.Empty;
                }
                string quien = string.IsNullOrWhiteSpace(Receptor) ? "Recibido" : $"Recibido por {Receptor.Trim()}";
                return FechaEntrega.HasValue ? $"{quien} el {FechaEntrega.Value.ToString("dd/MM/yy H:mm", CultureInfo.InvariantCulture)}" : quien;
            }
        }

        /// <summary>Una línea por bulto: «EI12-1: En la furgoneta de Miguel».</summary>
        [JsonIgnore]
        public string DondeEstan => string.Join(Environment.NewLine, (BultosDetalle ?? new List<BultoEnvioInterno>())
            .Where(b => b != null)
            .Select(b => $"{b.Codigo?.Trim()}: {b.Situacion?.Trim()}"));

        /// <summary>Lo que se puede reimprimir o anular (no lo entregado ni lo anulado; la API decide el resto).</summary>
        [JsonIgnore]
        public bool EstaVivo => Estado != ESTADO_ENTREGADO && Estado != ESTADO_ANULADO;
    }

    /// <summary>Las sedes entre las que hay envíos internos (los códigos de almacén de la API).</summary>
    public static class SedesEnvioInterno
    {
        public const string ALGETE = "ALG";
        public const string REINA = "REI";
        public const string ALCOBENDAS = "ALC";

        public static IReadOnlyList<string> Codigos { get; } = new[] { ALGETE, REINA, ALCOBENDAS };

        public static string Nombre(string codigo) => codigo?.Trim().ToUpperInvariant() switch
        {
            ALGETE => "Algete",
            REINA => "Reina",
            ALCOBENDAS => "Alcobendas",
            null => string.Empty,
            _ => codigo.Trim()
        };
    }

    public class ServicioEnviosInternos : IServicioEnviosInternos
    {
        internal const string URL = "RepartoPropio/EnviosInternos";
        public const string MENSAJE_SIN_PERMISO = "Los envíos internos son cosa de Almacén, Dirección e Informática.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEnviosInternos(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        internal static string UrlLeer(int dias, bool soloVivos) => $"{URL}?dias={dias}&soloVivos={(soloVivos ? "true" : "false")}";
        internal static string UrlImprimir(int id) => $"{URL}/{id}/ImprimirEtiquetas";
        internal static string UrlAnular(int id, string motivo)
            => $"{URL}/{id}" + (string.IsNullOrWhiteSpace(motivo) ? string.Empty : $"?motivo={Uri.EscapeDataString(motivo.Trim())}");
        internal static string CuerpoCrear(NuevoEnvioInterno envio) => JsonConvert.SerializeObject(envio,
            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        internal static string CuerpoImprimir(string impresora) => JsonConvert.SerializeObject(new { Impresora = string.IsNullOrWhiteSpace(impresora) ? null : impresora.Trim() });

        public Task<EnvioInterno> Crear(NuevoEnvioInterno envio) => Post(URL, CuerpoCrear(envio));

        public Task<EnvioInterno> ImprimirEtiquetas(int id, string impresora) => Post(UrlImprimir(id), CuerpoImprimir(impresora));

        public async Task<List<EnvioInterno>> Leer(int dias, bool soloVivos)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (HttpResponseMessage respuesta = await client.GetAsync(UrlLeer(dias, soloVivos)).ConfigureAwait(false))
            {
                string cuerpo = await Cuerpo(respuesta).ConfigureAwait(false);
                return (string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<List<EnvioInterno>>(cuerpo)) ?? new List<EnvioInterno>();
            }
        }

        public async Task Anular(int id, string motivo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (HttpResponseMessage respuesta = await client.DeleteAsync(UrlAnular(id, motivo)).ConfigureAwait(false))
            {
                _ = await Cuerpo(respuesta).ConfigureAwait(false);
            }
        }

        private async Task<EnvioInterno> Post(string url, string cuerpoPeticion)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            using (var contenido = new StringContent(cuerpoPeticion, Encoding.UTF8, "application/json"))
            using (HttpResponseMessage respuesta = await client.PostAsync(url, contenido).ConfigureAwait(false))
            {
                string cuerpo = await Cuerpo(respuesta).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<EnvioInterno>(cuerpo);
            }
        }

        /// <summary>El cuerpo de una respuesta buena; si no lo es, lanza con el mensaje para el usuario.</summary>
        private static async Task<string> Cuerpo(HttpResponseMessage respuesta)
        {
            string cuerpo = respuesta.Content == null ? string.Empty : await respuesta.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!respuesta.IsSuccessStatusCode)
            {
                throw ErroresApiReparto.Excepcion((int)respuesta.StatusCode, cuerpo, MENSAJE_SIN_PERMISO);
            }
            return cuerpo;
        }
    }
}
