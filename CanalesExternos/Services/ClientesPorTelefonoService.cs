using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    /// <summary>
    /// Nesto#340: cliente del endpoint api/Clientes/PorTelefono de NestoAPI. Sustituye la
    /// consulta EF directa a la BD que hacía CanalExternoPedidosAmazon.BuscarCliente.
    /// </summary>
    public class ClientesPorTelefonoService : IClientesPorTelefonoService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ClientesPorTelefonoService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<ClientePorTelefono>> BuscarClientesPorTelefonoAsync(string telefono)
        {
            if (string.IsNullOrWhiteSpace(telefono))
            {
                return new List<ClientePorTelefono>();
            }
            using HttpClient client = await CrearClienteAsync();
            HttpResponseMessage respuesta = await client.GetAsync($"Clientes/PorTelefono?telefono={Uri.EscapeDataString(telefono.Trim())}");
            string cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new Exception($"Error {(int)respuesta.StatusCode} al buscar clientes por teléfono: {cuerpo}");
            }
            return JsonConvert.DeserializeObject<List<ClientePorTelefono>>(cuerpo) ?? new List<ClientePorTelefono>();
        }

        private async Task<HttpClient> CrearClienteAsync()
        {
            HttpClient client = new()
            {
                BaseAddress = new Uri(_configuracion.servidorAPI)
            };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }
    }
}
