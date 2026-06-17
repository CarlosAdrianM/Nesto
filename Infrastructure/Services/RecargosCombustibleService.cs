using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    public class RecargosCombustibleService : IServicioRecargosCombustible
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public RecargosCombustibleService(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<RecargoCombustibleAgencia>> LeerRecargos()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Agencias/RecargosCombustible").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<RecargoCombustibleAgencia>>(json);
            }
        }

        public async Task GuardarRecargo(int numero, decimal recargoCombustible)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var dto = new RecargoCombustibleAgencia { Numero = numero, RecargoCombustible = recargoCombustible };
                var content = new StringContent(JsonConvert.SerializeObject(dto), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PutAsync($"Agencias/{numero}/RecargoCombustible", content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}
