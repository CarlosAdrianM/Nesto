using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// NestoAPI#606: qué día entregamos el pedido a la agencia. Es una ayuda: si la API falla o es anterior al
    /// endpoint (404), devuelve null y no se enseña nada (sin diálogo ni ELMAH).
    /// </summary>
    public interface IServicioFechaEntregaAgencia
    {
        /// <summary>POST api/PedidosVenta/FechaEntregaAgencia con el pedido que se está montando (plantilla).</summary>
        Task<FechaEntregaAgenciaDTO> CalcularPlantilla(PedidoVentaDTO pedido);

        /// <summary>GET api/PedidosVenta/{empresa}/{numero}/FechaEntregaAgencia (detalle de un pedido grabado).</summary>
        Task<FechaEntregaAgenciaDTO> CalcularPedido(string empresa, int numero);
    }

    public class ServicioFechaEntregaAgencia : IServicioFechaEntregaAgencia
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioFechaEntregaAgencia(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<FechaEntregaAgenciaDTO> CalcularPlantilla(PedidoVentaDTO pedido)
        {
            if (pedido == null)
            {
                return null;
            }
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    var contenido = new StringContent(JsonConvert.SerializeObject(pedido), Encoding.UTF8, "application/json");
                    HttpResponseMessage response = await client.PostAsync("PedidosVenta/FechaEntregaAgencia", contenido).ConfigureAwait(false);
                    return await Leer(response).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        public async Task<FechaEntregaAgenciaDTO> CalcularPedido(string empresa, int numero)
        {
            if (numero <= 0 || string.IsNullOrWhiteSpace(empresa))
            {
                return null;
            }
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    string url = $"PedidosVenta/{Uri.EscapeDataString(empresa.Trim())}/{numero}/FechaEntregaAgencia";
                    HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                    return await Leer(response).ConfigureAwait(false);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Error (404 de una API antigua incluido) = null; un 200 con null = el pedido no tiene fecha.</summary>
        private static async Task<FechaEntregaAgenciaDTO> Leer(HttpResponseMessage response)
        {
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonConvert.DeserializeObject<FechaEntregaAgenciaDTO>(json) ?? new FechaEntregaAgenciaDTO();
        }
    }
}
