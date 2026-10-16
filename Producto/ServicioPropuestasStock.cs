using Nesto.Infrastructure.Contracts;
using Nesto.Modules.Producto.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Nesto#529 (gemela de NestoAPI#631): las propuestas mensuales de máximo y mínimo del motor de tendencias y la decisión
    /// de Compras sobre cada una. Nesto no decide nada de negocio: lo hace la API y aquí se enseña su mensaje.
    /// </summary>
    public interface IServicioPropuestasStock
    {
        /// <summary>
        /// GET api/PropuestasStock. <paramref name="mes"/> AAAA-MM (null: el último guardado); <paramref name="estado"/> null: todas.
        /// Sugerencia 644: <paramref name="filtro"/> (familia, grupo, acción y texto; null o vacío: sin filtro).
        /// </summary>
        /// <exception cref="PropuestasStockException">403 (solo Compras y Dirección), 400, 404…</exception>
        Task<List<PropuestaStockModel>> LeerPropuestas(string mes, string estado, int top, FiltroPropuestasStock filtro);

        /// <summary>GET api/PropuestasStock/Resumen: cuántas hay en cada estado y cuánto exceso suman (sugerencia 644: solo las del filtro).</summary>
        /// <exception cref="PropuestasStockException">403, 404…</exception>
        Task<List<ResumenEstadoPropuestasModel>> LeerResumen(string mes, FiltroPropuestasStock filtro);

        /// <summary>
        /// Sugerencia 644: GET api/PropuestasStock/Filtros: familias, grupos y acciones del mes. Nunca lanza: si la API no lo
        /// tiene todavía o falla, listas vacías (los desplegables solo ofrecen «(todas)»).
        /// </summary>
        Task<OpcionesFiltroPropuestasModel> LeerOpcionesFiltro(string mes);

        /// <summary>
        /// GET api/PropuestasStock/MotivosSilencio. Nunca lanza: si la API no lo tiene todavía o falla, los de reserva
        /// (<see cref="PropuestasStockConstantes.MOTIVOS_SILENCIO_RESERVA"/>).
        /// </summary>
        Task<List<string>> LeerMotivosSilencio();

        /// <summary>POST api/PropuestasStock/{id}/Decision.</summary>
        /// <exception cref="PropuestasStockException">400, 403, 404 o 409 con el Message de la API.</exception>
        Task<ResultadoDecisionPropuestaStock> Decidir(int id, DecisionPropuestaStockPeticion peticion);
    }

    public class ServicioPropuestasStock : IServicioPropuestasStock
    {
        internal const string MENSAJE_SIN_PERMISO = "Solo Compras y Dirección pueden ver y decidir las propuestas de stock.";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioPropuestasStock(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        public async Task<List<PropuestaStockModel>> LeerPropuestas(string mes, string estado, int top, FiltroPropuestasStock filtro)
        {
            var parametros = new List<string> { "top=" + top };
            if (!string.IsNullOrWhiteSpace(mes))
            {
                parametros.Add("mes=" + Uri.EscapeDataString(mes.Trim()));
            }
            if (!string.IsNullOrWhiteSpace(estado))
            {
                parametros.Add("estado=" + Uri.EscapeDataString(estado.Trim()));
            }
            parametros.AddRange(filtro?.ParametrosUrl() ?? new List<string>());
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("PropuestasStock?" + string.Join("&", parametros)).ConfigureAwait(false);
                return await Leer<List<PropuestaStockModel>>(response).ConfigureAwait(false) ?? new List<PropuestaStockModel>();
            }
        }

        public async Task<List<ResumenEstadoPropuestasModel>> LeerResumen(string mes, FiltroPropuestasStock filtro)
        {
            var parametros = new List<string>();
            if (!string.IsNullOrWhiteSpace(mes))
            {
                parametros.Add("mes=" + Uri.EscapeDataString(mes.Trim()));
            }
            parametros.AddRange(filtro?.ParametrosUrl() ?? new List<string>());
            string url = "PropuestasStock/Resumen" + (parametros.Count == 0 ? "" : "?" + string.Join("&", parametros));
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                return await Leer<List<ResumenEstadoPropuestasModel>>(response).ConfigureAwait(false) ?? new List<ResumenEstadoPropuestasModel>();
            }
        }

        public async Task<OpcionesFiltroPropuestasModel> LeerOpcionesFiltro(string mes)
        {
            string url = "PropuestasStock/Filtros" + (string.IsNullOrWhiteSpace(mes) ? "" : "?mes=" + Uri.EscapeDataString(mes.Trim()));
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return new OpcionesFiltroPropuestasModel();
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    OpcionesFiltroPropuestasModel opciones = JsonConvert.DeserializeObject<OpcionesFiltroPropuestasModel>(json) ?? new OpcionesFiltroPropuestasModel();
                    opciones.Familias ??= new List<string>();
                    opciones.Grupos ??= new List<string>();
                    opciones.Acciones ??= new List<string>();
                    return opciones;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
            {
                return new OpcionesFiltroPropuestasModel();
            }
        }

        public async Task<List<string>> LeerMotivosSilencio()
        {
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync("PropuestasStock/MotivosSilencio").ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return Reserva();
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    List<string> motivos = LeerListaMotivos(json);
                    return motivos.Count == 0 ? Reserva() : motivos;
                }
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException || ex is JsonException)
            {
                return Reserva();
            }
        }

        public async Task<ResultadoDecisionPropuestaStock> Decidir(int id, DecisionPropuestaStockPeticion peticion)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync($"PropuestasStock/{id}/Decision", contenido).ConfigureAwait(false);
                return await Leer<ResultadoDecisionPropuestaStock>(response).ConfigureAwait(false);
            }
        }

        private static List<string> Reserva() => PropuestasStockConstantes.MOTIVOS_SILENCIO_RESERVA.ToList();

        /// <summary>
        /// La lista de motivos tal como venga: <c>["Nuevo", ...]</c> o <c>[{"Motivo": "Nuevo"}, ...]</c> (también Codigo,
        /// Nombre, Valor o Texto), o un objeto con la lista dentro (<c>{"Motivos": [...]}</c>).
        /// </summary>
        internal static List<string> LeerListaMotivos(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return new List<string>();
            }
            JToken token = JToken.Parse(json);
            if (token is JObject objeto)
            {
                token = objeto.Properties().Select(p => p.Value).FirstOrDefault(v => v.Type == JTokenType.Array);
            }
            if (!(token is JArray lista))
            {
                return new List<string>();
            }
            string[] campos = { "Motivo", "Codigo", "Nombre", "Valor", "Texto" };
            return lista
                .Select(e => e.Type == JTokenType.String
                    ? e.ToString()
                    : e is JObject o
                        ? campos.Select(c => o.GetValue(c, StringComparison.OrdinalIgnoreCase)?.ToString()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
                        : null)
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Select(m => m.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static async Task<T> Leer<T>(HttpResponseMessage response)
        {
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                int codigo = (int)response.StatusCode;
                string motivo = ProductoService.MotivoDelErrorDeTexto(json);
                if (string.IsNullOrWhiteSpace(motivo))
                {
                    motivo = codigo == 403 ? MENSAJE_SIN_PERMISO : $"El servidor ha contestado con el error {codigo}.";
                }
                throw new PropuestasStockException(motivo, codigo);
            }
            return JsonConvert.DeserializeObject<T>(json);
        }
    }
}
