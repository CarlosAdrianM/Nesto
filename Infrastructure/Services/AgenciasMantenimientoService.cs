using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    public class AgenciasMantenimientoService : IServicioAgenciasMantenimiento
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public AgenciasMantenimientoService(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<AgenciaMantenimiento>> LeerAgencias()
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Agencias").ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<AgenciaMantenimiento>>(json);
            }
        }

        public async Task CrearAgencia(AgenciaMantenimiento agencia)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var content = new StringContent(JsonConvert.SerializeObject(agencia), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("Agencias", content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task GuardarAgencia(AgenciaMantenimiento agencia)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var content = new StringContent(JsonConvert.SerializeObject(agencia), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PutAsync($"Agencias/{agencia.Numero}", content).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
            }
        }
    }
}
