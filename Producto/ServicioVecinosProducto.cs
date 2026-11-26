using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modules.Producto
{
    /// <summary>
    /// Sugerencia 1478 (Manuel, Compras): la referencia anterior y la siguiente que existen, para las flechas de la ficha
    /// (GET api/Productos/Vecinos). Nesto no decide qué referencia va antes o después: lo dice la API.
    /// </summary>
    public interface IServicioVecinosProducto
    {
        /// <summary>Null si la API falla, es anterior al endpoint o el producto no existe (404): la ficha se abre igual, sin flechas.</summary>
        Task<VecinosProducto> Leer(string empresa, string producto);
    }

    /// <summary>La respuesta de GET api/Productos/Vecinos. Anterior o Siguiente son null si no hay (primera o última referencia).</summary>
    public class VecinosProducto
    {
        public string Producto { get; set; }
        public string Anterior { get; set; }
        public string Siguiente { get; set; }
    }

    public class ServicioVecinosProducto : IServicioVecinosProducto
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioVecinosProducto(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory ?? throw new ArgumentNullException(nameof(clienteApiFactory));
        }

        internal static string Url(string empresa, string producto) =>
            $"Productos/Vecinos?producto={Uri.EscapeDataString(producto?.Trim() ?? string.Empty)}" +
            $"&empresa={Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim())}" +
            // Sugerencia 1478: las flechas saltan las referencias anuladas (una API que no lo conoce lo ignora)
            "&saltarAnuladas=true";

        public async Task<VecinosProducto> Leer(string empresa, string producto)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync(Url(empresa, producto)).ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(json) ? null : JsonConvert.DeserializeObject<VecinosProducto>(json);
                }
            }
            catch (Exception)
            {
                // Es una ayuda para moverse: sin ella, la ficha se abre igual
                return null;
            }
        }
    }
}
