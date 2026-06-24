using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    public class ComparadorAgenciasService : IServicioComparadorAgencias
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ComparadorAgenciasService(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<OpcionEnvioAgencia> MasEconomica(string empresa, string codigoPostal, decimal peso, decimal reembolso, string pais = "ES")
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = "Agencias/MasEconomica" +
                    $"?codigoPostal={Uri.EscapeDataString(codigoPostal ?? string.Empty)}" +
                    $"&peso={peso.ToString(CultureInfo.InvariantCulture)}" +
                    $"&empresa={Uri.EscapeDataString(empresa ?? string.Empty)}" +
                    $"&reembolso={reembolso.ToString(CultureInfo.InvariantCulture)}" +
                    $"&pais={Uri.EscapeDataString(pais ?? "ES")}";

                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<OpcionEnvioAgencia>(json);
            }
        }

        public async Task<OpcionEnvioAgencia> CosteAgencia(string empresa, int numero, string codigoPostal, decimal peso, decimal reembolso, byte? servicioId = null, string pais = "ES")
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = $"Agencias/{numero}/Coste" +
                    $"?codigoPostal={Uri.EscapeDataString(codigoPostal ?? string.Empty)}" +
                    $"&peso={peso.ToString(CultureInfo.InvariantCulture)}" +
                    $"&empresa={Uri.EscapeDataString(empresa ?? string.Empty)}" +
                    $"&reembolso={reembolso.ToString(CultureInfo.InvariantCulture)}" +
                    $"&pais={Uri.EscapeDataString(pais ?? "ES")}";
                if (servicioId.HasValue)
                {
                    url += $"&servicioId={servicioId.Value}";
                }

                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound)
                {
                    return null;
                }
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<OpcionEnvioAgencia>(json);
            }
        }
    }
}
