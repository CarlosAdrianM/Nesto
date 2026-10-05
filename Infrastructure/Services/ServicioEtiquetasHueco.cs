using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Etiquetas de hueco: Nesto no imprime en local. La API sabe la impresora de etiquetas de producto del usuario,
    /// genera las etiquetas (las mismas que imprime Ariadna) y las manda; con ensayo solo dice cuáles saldrían.
    /// </summary>
    public interface IServicioEtiquetasHueco
    {
        /// <exception cref="EtiquetasHuecoException">La API no las ha hecho (sin permiso, hueco mal escrito…).</exception>
        Task<ResultadoEtiquetasHueco> Imprimir(string empresa, string almacen, PeticionEtiquetasHueco peticion, bool ensayo);
    }

    public class ServicioEtiquetasHueco : IServicioEtiquetasHueco
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEtiquetasHueco(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<ResultadoEtiquetasHueco> Imprimir(string empresa, string almacen, PeticionEtiquetasHueco peticion, bool ensayo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = "Almacen/EtiquetasHueco/Imprimir?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty)
                    + "&almacen=" + Uri.EscapeDataString(almacen?.Trim() ?? string.Empty)
                    + "&ensayo=" + (ensayo ? "true" : "false");
                var contenido = new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new EtiquetasHuecoException(Motivo(json, (int)response.StatusCode));
                }
                return JsonConvert.DeserializeObject<ResultadoEtiquetasHueco>(json) ?? new ResultadoEtiquetasHueco();
            }
        }

        /// <summary>El motivo que da la API: una cadena JSON (Content(403, motivo)) o un objeto de error.</summary>
        internal static string Motivo(string json, int codigo)
        {
            try
            {
                JToken token = JToken.Parse(json);
                if (token.Type == JTokenType.String)
                {
                    return token.ToString();
                }
                if (token is JObject objeto)
                {
                    return HttpErrorHelper.ParsearErrorHttp(objeto);
                }
            }
            catch (JsonException)
            {
                // No es JSON: se enseña el texto si lo hay
            }
            return string.IsNullOrWhiteSpace(json) ? $"El servidor ha contestado con el error {codigo}." : json.Trim();
        }
    }
}
