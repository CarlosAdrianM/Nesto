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

        public RecursosHumanosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }
        public async Task<bool> EsFestivo(DateTime fecha, string delegacion)
        {
            using (HttpClient _httpClient = new HttpClient())
            {
                _httpClient.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(_httpClient))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

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
