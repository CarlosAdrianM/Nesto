using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Nesto#650 (gemela de NestoAPI#884): las muestras (subgrupo MMP) sin código de fabricante no se etiquetan unidad a unidad, sino
    /// con UNA etiqueta por referencia en el cajón del mostrador (REI, ALC) o en la gaveta / el hueco (ALG), con el EAN-13 interno del 29.
    /// Toda la lógica está en la API; Nesto pide el ensayo, lo enseña, pregunta y la repite de verdad.
    /// </summary>
    public interface IServicioMuestras
    {
        /// <summary>POST api/Productos/Muestras/CodigoInterno?empresa=&amp;ensayo=: dar el interno a las muestras sin código.</summary>
        /// <exception cref="MuestrasException">403 sin permiso, 503…</exception>
        Task<ResultadoCodigoInternoMuestras> DarCodigoInterno(string empresa, bool ensayo);

        /// <summary>POST api/Almacen/EtiquetasProducto/Muestras?empresa=&amp;ensayo=: las etiquetas del cajón o la gaveta.</summary>
        /// <exception cref="MuestrasException">400 con el motivo (más de 500, sin impresora…), 403, 502 si Windows no deja imprimir.</exception>
        Task<ResultadoEtiquetasMuestras> Etiquetas(string empresa, PeticionEtiquetasMuestras peticion, bool ensayo);

        /// <summary>
        /// GET api/Almacen/EtiquetasProducto/Impresoras. Null si la API todavía no lo tiene (404) o no contesta: entonces no se
        /// ofrece elegir impresora y se imprime en la del almacén.
        /// </summary>
        Task<List<ImpresoraEtiquetasMuestras>> LeerImpresoras();

        /// <summary>GET api/Productos/Muestras/Hoja: el PDF de las muestras por marca.</summary>
        /// <exception cref="MuestrasException">404 «No hay ninguna muestra activa con esos filtros.», 400, 403.</exception>
        Task<FicheroMuestras> Hoja(string empresa, string almacen, string familia, bool soloConStock, bool conFotos);
    }

    /// <summary>NestoAPI#884 (ResultadoCodigoInternoMuestrasDTO).</summary>
    public class ResultadoCodigoInternoMuestras
    {
        public bool Ensayo { get; set; }
        public int Total { get; set; }
        public int Asignados { get; set; }
        public int Errores { get; set; }
        public string Mensaje { get; set; }
        public List<MuestraCodigoInterno> Muestras { get; set; } = new List<MuestraCodigoInterno>();
    }

    public class MuestraCodigoInterno
    {
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string Familia { get; set; }
        /// <summary>El que le toca (null en el ensayo = uno del contador 299…).</summary>
        public string Codigo { get; set; }
        public bool Asignado { get; set; }
        public string Error { get; set; }
    }

    /// <summary>El cuerpo de POST api/Almacen/EtiquetasProducto/Muestras.</summary>
    public class PeticionEtiquetasMuestras
    {
        /// <summary>ALG, REI o ALC (obligatorio).</summary>
        public string Almacen { get; set; }
        /// <summary>Código o nombre de la marca; null = todas.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Familia { get; set; }
        public bool SoloConStock { get; set; }
        /// <summary>Solo estas referencias (para imprimir unas pocas); null = todas las de los filtros.</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public List<string> Productos { get; set; }
        /// <summary>La impresora elegida; null = la del almacén (la decide la API).</summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string Impresora { get; set; }
    }

    /// <summary>NestoAPI#884 (ResultadoEtiquetasMuestrasDTO).</summary>
    public class ResultadoEtiquetasMuestras
    {
        public string Almacen { get; set; }
        public bool Ensayo { get; set; }
        public int Total { get; set; }
        public int Impresas { get; set; }
        public string Impresora { get; set; }
        /// <summary>De dónde ha salido la impresora (puede no venir).</summary>
        public string OrigenImpresora { get; set; }
        public int CodigosAsignados { get; set; }
        /// <summary>Las que no tienen código y recibirán el interno al imprimir.</summary>
        public int SinCodigo { get; set; }
        public string Mensaje { get; set; }
        public List<string> NoSonMuestras { get; set; } = new List<string>();
        public List<EtiquetaMuestra> Etiquetas { get; set; } = new List<EtiquetaMuestra>();
    }

    public class EtiquetaMuestra
    {
        public int Orden { get; set; }
        public string Producto { get; set; }
        public string Nombre { get; set; }
        public string Familia { get; set; }
        /// <summary>«003/001/002» en ALG; null en las tiendas.</summary>
        public string Hueco { get; set; }
        public int Stock { get; set; }
        /// <summary>Null = no tiene: se le dará el interno al imprimir.</summary>
        public string CodigoBarras { get; set; }
        public bool CodigoInterno { get; set; }
        public bool CodigoAsignado { get; set; }
        public List<string> Lineas { get; set; } = new List<string>();
    }

    /// <summary>GET api/Almacen/EtiquetasProducto/Impresoras.</summary>
    public class ImpresoraEtiquetasMuestras
    {
        public string Almacen { get; set; }
        public string Nombre { get; set; }
        public string Impresora { get; set; }
        public bool EsLaDelUsuario { get; set; }
    }

    public class FicheroMuestras
    {
        public string Nombre { get; set; }
        public byte[] Contenido { get; set; }
    }

    public class MuestrasException : Exception
    {
        public MuestrasException(string mensaje, int codigo) : base(mensaje)
        {
            Codigo = codigo;
        }

        /// <summary>El código HTTP: 400, 403, 404, 502…</summary>
        public int Codigo { get; }
    }

    public class ServicioMuestras : IServicioMuestras
    {
        internal const string URL_CODIGO_INTERNO = "Productos/Muestras/CodigoInterno";
        internal const string URL_ETIQUETAS = "Almacen/EtiquetasProducto/Muestras";
        internal const string URL_IMPRESORAS = "Almacen/EtiquetasProducto/Impresoras";
        internal const string URL_HOJA = "Productos/Muestras/Hoja";
        internal const string MENSAJE_SIN_PERMISO = "No tiene permiso para esto.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioMuestras(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        private static string Texto(string valor) => Uri.EscapeDataString(valor?.Trim() ?? string.Empty);

        public async Task<ResultadoCodigoInternoMuestras> DarCodigoInterno(string empresa, bool ensayo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = URL_CODIGO_INTERNO + "?empresa=" + Texto(empresa) + "&ensayo=" + (ensayo ? "true" : "false");
                var contenido = new StringContent("{}", Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                string json = await LeerOLanzar(response).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<ResultadoCodigoInternoMuestras>(json ?? string.Empty) ?? new ResultadoCodigoInternoMuestras();
            }
        }

        public async Task<ResultadoEtiquetasMuestras> Etiquetas(string empresa, PeticionEtiquetasMuestras peticion, bool ensayo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = URL_ETIQUETAS + "?empresa=" + Texto(empresa) + "&ensayo=" + (ensayo ? "true" : "false");
                var contenido = new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                string json = await LeerOLanzar(response).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<ResultadoEtiquetasMuestras>(json ?? string.Empty) ?? new ResultadoEtiquetasMuestras();
            }
        }

        public async Task<List<ImpresoraEtiquetasMuestras>> LeerImpresoras()
        {
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync(URL_IMPRESORAS).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<List<ImpresoraEtiquetasMuestras>>(json ?? string.Empty);
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
            {
                // Elegir impresora es una comodidad: sin la lista se imprime en la del almacén
                return null;
            }
        }

        public async Task<FicheroMuestras> Hoja(string empresa, string almacen, string familia, bool soloConStock, bool conFotos)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = URL_HOJA + "?empresa=" + Texto(empresa)
                    + "&almacen=" + Texto(almacen)
                    + "&soloConStock=" + (soloConStock ? "true" : "false")
                    + "&conFotos=" + (conFotos ? "true" : "false");
                if (!string.IsNullOrWhiteSpace(familia))
                {
                    url += "&familia=" + Texto(familia);
                }
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _ = await LeerOLanzar(response).ConfigureAwait(false);
                }
                byte[] contenido = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                string nombre = response.Content.Headers.ContentDisposition?.FileNameStar
                    ?? response.Content.Headers.ContentDisposition?.FileName;
                return new FicheroMuestras { Nombre = nombre?.Trim('"', ' '), Contenido = contenido };
            }
        }

        /// <summary>El cuerpo si ha ido bien; si no, lanza con el motivo que da la API.</summary>
        private static async Task<string> LeerOLanzar(HttpResponseMessage response)
        {
            string cuerpo = response.Content == null ? null : await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                return cuerpo;
            }
            int codigo = (int)response.StatusCode;
            string motivo = ProductoService.MotivoDelErrorDeTexto(cuerpo);
            if (string.IsNullOrWhiteSpace(motivo))
            {
                motivo = response.StatusCode == HttpStatusCode.Forbidden
                    ? MENSAJE_SIN_PERMISO
                    : $"El servidor ha contestado con el error {codigo}.";
            }
            throw new MuestrasException(motivo, codigo);
        }
    }
}
