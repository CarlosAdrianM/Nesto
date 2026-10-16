using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Nesto#534 (NestoAPI#654, sugerencia 546): la etiqueta de código de barras de un producto (30×20 mm). Como las de hueco,
    /// Nesto no imprime en local: la API sabe la impresora de etiquetas de producto del usuario, monta la etiqueta y la manda.
    /// </summary>
    public interface IServicioEtiquetasProducto
    {
        /// <exception cref="EtiquetasProductoException">La API no la ha impreso (sin permiso, producto que no existe…).</exception>
        Task<ResultadoEtiquetasProducto> Imprimir(string empresa, string producto, int copias, bool ensayo = false);
    }

    public class ServicioEtiquetasProducto : IServicioEtiquetasProducto
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioEtiquetasProducto(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<ResultadoEtiquetasProducto> Imprimir(string empresa, string producto, int copias, bool ensayo = false)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = "Almacen/EtiquetasProducto/Imprimir?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty)
                    + "&ensayo=" + (ensayo ? "true" : "false");
                var peticion = new PeticionEtiquetasProducto { Producto = producto?.Trim(), Copias = copias };
                var contenido = new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    // Mismo formato de error que las etiquetas de hueco (cadena JSON u objeto de error)
                    throw new EtiquetasProductoException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return JsonConvert.DeserializeObject<ResultadoEtiquetasProducto>(json) ?? new ResultadoEtiquetasProducto();
            }
        }
    }
}
