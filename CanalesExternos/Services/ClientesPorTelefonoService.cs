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

        public async Task<List<ClientePorTelefono>> BuscarClientesPorNifAsync(string nif)
        {
            if (string.IsNullOrWhiteSpace(nif))
            {
                return new List<ClientePorTelefono>();
            }
            using HttpClient client = await CrearClienteAsync();
            HttpResponseMessage respuesta = await client.GetAsync($"Clientes/PorNif?nif={Uri.EscapeDataString(nif.Trim())}");
            string cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new Exception($"Error {(int)respuesta.StatusCode} al buscar clientes por NIF: {cuerpo}");
            }
            return JsonConvert.DeserializeObject<List<ClientePorTelefono>>(cuerpo) ?? new List<ClientePorTelefono>();
        }

        public async Task<int> BuscarPedidoPorReferenciaCanalAsync(string referencia)
        {
            if (string.IsNullOrWhiteSpace(referencia))
            {
                return 0;
            }
            using HttpClient client = await CrearClienteAsync();
            HttpResponseMessage respuesta = await client.GetAsync($"PedidosVenta/PorReferenciaCanal?referencia={Uri.EscapeDataString(referencia.Trim())}");
            string cuerpo = await respuesta.Content.ReadAsStringAsync();
            if (!respuesta.IsSuccessStatusCode)
            {
                throw new Exception($"Error {(int)respuesta.StatusCode} al buscar el pedido por referencia: {cuerpo}");
            }
            return JsonConvert.DeserializeObject<int>(cuerpo);
        }

        public async Task<string> LeerIvaProductoAsync(string empresa, string producto)
        {
            if (string.IsNullOrWhiteSpace(producto))
            {
                return null;
            }
            try
            {
                using HttpClient client = await CrearClienteAsync();
                HttpResponseMessage respuesta = await client.GetAsync($"Productos?empresa={Uri.EscapeDataString(empresa?.Trim() ?? "1")}&id={Uri.EscapeDataString(producto.Trim())}");
                if (!respuesta.IsSuccessStatusCode)
                {
                    return null;
                }
                var datos = JsonConvert.DeserializeObject<IvaProductoRespuesta>(await respuesta.Content.ReadAsStringAsync());
                return string.IsNullOrWhiteSpace(datos?.iva) ? null : datos.iva.Trim();
            }
            catch (Exception)
            {
                return null; // sin ficha, se deduce del precio como antes
            }
        }

        private class IvaProductoRespuesta
        {
            public string iva { get; set; }
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
