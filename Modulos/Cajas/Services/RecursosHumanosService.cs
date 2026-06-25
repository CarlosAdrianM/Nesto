using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Newtonsoft.Json;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    public class RecursosHumanosService : IRecursosHumanosService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;
        private readonly IClienteApiFactory _clienteApiFactory;

        public RecursosHumanosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion, IClienteApiFactory clienteApiFactory = null)
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

        public async Task<bool> EsFestivo(DateTime fecha, string delegacion)
        {
            using (HttpClient _httpClient = await CrearClienteAutenticado())
            {
                // Convertimos la fecha al formato esperado por la API
                string fechaString = fecha.ToString("yyyy-MM-dd");

                // Construimos la URL de la API
                string url = $"RecursosHumanos/EsFestivo?fecha={fechaString}&delegacion={delegacion}";

                // Realizamos la petición GET
                HttpResponseMessage response = await _httpClient.GetAsync(url);

                // Si la respuesta es exitosa, leemos el resultado
                if (response.IsSuccessStatusCode)
                {
                    string jsonResult = await response.Content.ReadAsStringAsync();
                    return JsonConvert.DeserializeObject<bool>(jsonResult);

                }

                // Si algo sale mal, puedes manejar el error aquí (lanzar excepción, retornar false, etc.)
                throw new HttpRequestException($"Error al llamar a la API: {response.StatusCode}");
            }
        }
    }
}
