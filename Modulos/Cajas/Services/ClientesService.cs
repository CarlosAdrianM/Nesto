using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    public class ClientesService : IClientesService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly IClienteApiFactory _clienteApiFactory;

        public ClientesService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion, IClienteApiFactory clienteApiFactory = null)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
            _clienteApiFactory = clienteApiFactory;
        }

        // Nesto#369: usa el HttpClient de la factory (base + JWT centralizados, usuario en ELMAH).
        // Si la factory no está disponible, conserva EXACTAMENTE la autenticación manual de antes
        // (incluido el throw si la autorización falla).
        private async Task<HttpClient> CrearClienteAutenticado()
        {
            if (_clienteApiFactory != null)
            {
                return _clienteApiFactory.Crear();
            }

            var client = new HttpClient { BaseAddress = new Uri(_configuracion.servidorAPI) };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }

        public async Task<List<ExtractoClienteDTO>> LeerDeudas(string cliente)
        {
            List<ExtractoClienteDTO> deudas;
            using (HttpClient client = await CrearClienteAutenticado())
            {
                HttpResponseMessage response;

                try
                {
                    string urlConsulta = $"ExtractosCliente?cliente={cliente}";

                    response = await client.GetAsync(urlConsulta);

                    if (response.IsSuccessStatusCode)
                    {
                        string resultado = await response.Content.ReadAsStringAsync();
                        deudas = JsonConvert.DeserializeObject<List<ExtractoClienteDTO>>(resultado);
                    }
                    else
                    {
                        throw new Exception("Las deudas no se han podido cargar correctamente");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception("Las deudas no se han podido cargar correctamente", ex);
                }
            }

            return deudas;
        }
    }
}
