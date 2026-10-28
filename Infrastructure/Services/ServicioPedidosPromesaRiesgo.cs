using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
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
        /// <summary>NestoAPI#693: qué avisos recibe el usuario y las opciones (GET api/PedidosVenta/PromesaEnRiesgo/Avisos).</summary>
        Task<AvisosPromesaRiesgo> LeerAvisos();
        /// <summary>NestoAPI#693: cambia lo que recibe (PUT). Devuelve cómo queda.</summary>
        Task<AvisosPromesaRiesgo> GuardarAvisos(string valor);
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
            public async Task<AvisosPromesaRiesgo> LeerAvisos()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("PedidosVenta/PromesaEnRiesgo/Avisos").ConfigureAwait(false);
                return await Avisos(response).ConfigureAwait(false);
            }
        }

        public async Task<AvisosPromesaRiesgo> GuardarAvisos(string valor)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(new { Valor = valor }), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PutAsync("PedidosVenta/PromesaEnRiesgo/Avisos", contenido).ConfigureAwait(false);
                return await Avisos(response).ConfigureAwait(false);
            }
        }

        private static async Task<AvisosPromesaRiesgo> Avisos(HttpResponseMessage response)
        {
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new PedidosPromesaRiesgoException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode), (int)response.StatusCode);
            }
            return JsonConvert.DeserializeObject<AvisosPromesaRiesgo>(json) ?? new AvisosPromesaRiesgo();
        }
}
}
