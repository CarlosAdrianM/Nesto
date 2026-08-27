using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    public class FamiliasMantenimientoService : IServicioFamiliasMantenimiento
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public FamiliasMantenimientoService(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<FamiliaMantenimiento>> LeerFamilias(string empresa)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync($"Familias?empresa={empresa}").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<FamiliaMantenimiento>>(json);
            }
        }

        public async Task GuardarFamilia(FamiliaMantenimiento familia)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var content = new StringContent(JsonConvert.SerializeObject(familia), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PutAsync("Familias", content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}
