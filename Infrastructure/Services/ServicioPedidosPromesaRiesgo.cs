using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Nesto#546 (NestoAPI#693): los pedidos con la fecha prometida en riesgo (GET api/PedidosVenta/PromesaEnRiesgo). Solo
    /// lectura: la detección, el registro y los avisos los hace la API. Quién ve qué lo decide la API con el usuario del
    /// token: sin vendedor, el suyo y su equipo (Dirección, todos).
    /// </summary>
    public interface IServicioPedidosPromesaRiesgo
    {
        /// <param name="incluirResueltos">Con true, detrás de los pendientes vienen los resueltos de los últimos días.</param>
        /// <exception cref="PedidosPromesaRiesgoException">La API no los ha devuelto (401, 403, 500…).</exception>
        Task<List<PedidoPromesaRiesgo>> Leer(string empresa, bool incluirResueltos);
    }

    public class ServicioPedidosPromesaRiesgo : IServicioPedidosPromesaRiesgo
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioPedidosPromesaRiesgo(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<PedidoPromesaRiesgo>> Leer(string empresa, bool incluirResueltos)
        {
            string url = "PedidosVenta/PromesaEnRiesgo?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty)
                + (incluirResueltos ? "&resueltos=true" : string.Empty);
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new PedidosPromesaRiesgoException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode), (int)response.StatusCode);
                }
                return JsonConvert.DeserializeObject<List<PedidoPromesaRiesgo>>(json) ?? new List<PedidoPromesaRiesgo>();
            }
        }
    }
}
