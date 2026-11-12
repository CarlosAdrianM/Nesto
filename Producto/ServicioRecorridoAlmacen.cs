using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Nesto#528 (corte 4 de NestoAPI#639, API en NestoAPI#642): las calles del recorrido de preparación del almacén (en qué orden se
    /// recorren los pasillos, en qué sentido y desde qué fila se llega con la mano). Nesto no valida reglas de negocio: lo hace la API
    /// y aquí se enseña su mensaje tal cual. Leer lo puede cualquiera; guardar, Almacén y Dirección (la API da 403 al resto).
    /// </summary>
    public interface IServicioRecorridoAlmacen
    {
        /// <summary>GET api/Almacen/Recorrido?almacen=&amp;empresa=.</summary>
        /// <exception cref="RecorridoAlmacenException">La API no lo ha devuelto (503 si falta configurarlo en el servidor…).</exception>
        Task<RecorridoAlmacen> Leer(string empresa, string almacen);

        /// <summary>
        /// PUT api/Almacen/Recorrido: sustituye el recorrido entero del almacén y devuelve lo que queda (como el GET). Con
        /// <paramref name="ensayo"/> no guarda nada: es la vista previa.
        /// </summary>
        /// <exception cref="RecorridoAlmacenException">400 (lo pedido no vale, con el motivo), 403 (sin permiso), 503 (faltan las tablas).</exception>
        Task<RecorridoAlmacen> Guardar(string empresa, string almacen, PeticionRecorridoAlmacen peticion, bool ensayo);
    }

    /// <summary>NestoAPI#642 (RecorridoAlmacenDTO): las calles en el orden del recorrido, los pasillos sin calle y los tramos que salen.</summary>
    public class RecorridoAlmacen
    {
        public string Empresa { get; set; }
        public string Almacen { get; set; }
        /// <summary>False si en el servidor aún no están las tablas del recorrido: todo vacío y el orden, el de siempre.</summary>
        public bool TablasCreadas { get; set; }
        /// <summary>Desde qué fila se llega con la mano (las de arriba son de escalera).</summary>
        public int FilaAMano { get; set; }
        public List<CalleRecorrido> Calles { get; set; } = new List<CalleRecorrido>();
        public List<PasilloRecorrido> PasillosSinCalle { get; set; } = new List<PasilloRecorrido>();
        public List<TramoRecorrido> Tramos { get; set; } = new List<TramoRecorrido>();
        /// <summary>True en la respuesta de una vista previa: no se ha guardado nada.</summary>
        public bool Ensayo { get; set; }
    }

    public class CalleRecorrido
    {
        /// <summary>Solo en la respuesta (cambia en cada guardado); en el PUT no se manda.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? Id { get; set; }
        public string Calle { get; set; }
        /// <summary>Solo en la respuesta: en el PUT lo pone la API según el orden de la lista.</summary>
        public int Orden { get; set; }
        /// <summary>«A» (columnas ascendentes, ida) o «D» (descendentes, vuelta).</summary>
        public string Sentido { get; set; }
        public string Descripcion { get; set; }
        public List<PasilloRecorrido> Pasillos { get; set; } = new List<PasilloRecorrido>();
    }

    public class PasilloRecorrido
    {
        /// <summary>«004» (en el PUT vale «4»).</summary>
        public string Pasillo { get; set; }
        /// <summary>Solo en la respuesta.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public int? Posicion { get; set; }
        public string Nombre { get; set; }
        /// <summary>Solo en la respuesta: huecos con algo ubicado.</summary>
        public int Huecos { get; set; }
    }

    public class TramoRecorrido
    {
        public int Orden { get; set; }
        /// <summary>Null si el pasillo no va en ninguna calle.</summary>
        public string Calle { get; set; }
        public string Pasillo { get; set; }
        public string Columna { get; set; }
        /// <summary>«AMano», «Escalera» o null (sin calle).</summary>
        public string Nivel { get; set; }
        public List<string> Filas { get; set; } = new List<string>();
    }

    /// <summary>NestoAPI#642 (PeticionRecorridoAlmacenDTO): el cuerpo del PUT. El orden de las listas es el del recorrido.</summary>
    public class PeticionRecorridoAlmacen
    {
        /// <summary>Null: no se toca.</summary>
        public int? FilaAMano { get; set; }
        public List<PeticionCalleRecorrido> Calles { get; set; } = new List<PeticionCalleRecorrido>();
        public List<PeticionPasilloRecorrido> PasillosSinCalle { get; set; } = new List<PeticionPasilloRecorrido>();
    }

    public class PeticionCalleRecorrido
    {
        public string Calle { get; set; }
        public string Sentido { get; set; }
        public string Descripcion { get; set; }
        public List<PeticionPasilloRecorrido> Pasillos { get; set; } = new List<PeticionPasilloRecorrido>();
    }

    public class PeticionPasilloRecorrido
    {
        public string Pasillo { get; set; }
        public string Nombre { get; set; }
    }

    public class RecorridoAlmacenException : Exception
    {
        public RecorridoAlmacenException(string mensaje, int codigo) : base(mensaje)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP: 400, 403, 503…</summary>
        public int Codigo { get; }
    }

    public class ServicioRecorridoAlmacen : IServicioRecorridoAlmacen
    {
        internal const string URL = "Almacen/Recorrido";
        internal const string MENSAJE_SIN_PERMISO = "Solo Almacén y Dirección pueden cambiar el recorrido del almacén.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioRecorridoAlmacen(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        internal static string Consulta(string empresa, string almacen)
            => "?almacen=" + Uri.EscapeDataString(almacen?.Trim() ?? string.Empty)
                + "&empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);

        public async Task<RecorridoAlmacen> Leer(string empresa, string almacen)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(URL + Consulta(empresa, almacen)).ConfigureAwait(false);
                return await LeerRespuesta(response).ConfigureAwait(false);
            }
        }

        public async Task<RecorridoAlmacen> Guardar(string empresa, string almacen, PeticionRecorridoAlmacen peticion, bool ensayo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json");
                string url = URL + Consulta(empresa, almacen) + "&ensayo=" + (ensayo ? "true" : "false");
                HttpResponseMessage response = await client.PutAsync(url, contenido).ConfigureAwait(false);
                return await LeerRespuesta(response).ConfigureAwait(false);
            }
        }

        private static async Task<RecorridoAlmacen> LeerRespuesta(HttpResponseMessage response)
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
                throw new RecorridoAlmacenException(motivo, codigo);
            }
            return JsonConvert.DeserializeObject<RecorridoAlmacen>(cuerpo ?? string.Empty) ?? new RecorridoAlmacen();
        }
    }
}
