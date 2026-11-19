using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>«Dónde está lo que falta» (NestoAPI, 19/11/26): GET api/PedidosVenta/{empresa}/{pedido}/DondeEstaLoQueFalta.</summary>
    public interface IServicioDondeEstaLoQueFalta
    {
        /// <summary>Null si la API falla o es anterior al endpoint (es una ayuda: quien llama dice que no se ha podido saber).</summary>
        Task<DondeEstaLoQueFaltaDTO> Leer(string empresa, int pedido);
    }

    public class ServicioDondeEstaLoQueFalta : IServicioDondeEstaLoQueFalta
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioDondeEstaLoQueFalta(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public static string Ruta(string empresa, int pedido) =>
            $"PedidosVenta/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim())}/{pedido}/DondeEstaLoQueFalta";

        public async Task<DondeEstaLoQueFaltaDTO> Leer(string empresa, int pedido)
        {
            if (pedido <= 0 || _clienteApiFactory == null)
            {
                return null;
            }
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                using (HttpResponseMessage response = await client.GetAsync(Ruta(empresa, pedido)).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        return null;
                    }
                    string cuerpo = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return string.IsNullOrWhiteSpace(cuerpo) ? null : JsonConvert.DeserializeObject<DondeEstaLoQueFaltaDTO>(cuerpo);
                }
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
