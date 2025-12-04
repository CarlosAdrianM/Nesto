using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Implementación del servicio de Formas de Venta.
    /// Carlos 04/12/2024: Issue #252 - Creado para el control SelectorFormaVenta siguiendo el patrón de ServicioCCC.
    /// </summary>
    public class ServicioFormaVenta : IServicioFormaVenta
    {
        private readonly IConfiguracion _configuracion;

        public ServicioFormaVenta(IConfiguracion configuracion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        }

        /// <summary>
        /// Obtiene las formas de venta para una empresa desde la API.
        /// </summary>
        public async Task<IEnumerable<FormaVentaItem>> ObtenerFormasVenta(string empresa)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(empresa))
                throw new ArgumentException("Empresa es requerida", nameof(empresa));

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Endpoint: api/FormasVenta?empresa=1
                string urlConsulta = $"FormasVenta?empresa={empresa}";
                Debug.WriteLine($"[ServicioFormaVenta] URL Base: {_configuracion.servidorAPI}, Consulta: {urlConsulta}");

                HttpResponseMessage response = await client.GetAsync(urlConsulta);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error al obtener Formas de Venta: {response.StatusCode} - {response.ReasonPhrase}");
                }

                string resultado = await response.Content.ReadAsStringAsync();

                // Deserializar la respuesta de la API
                // La API devuelve FormaVenta de EF con propiedades: Empresa, Número, Descripción, VisiblePorComerciales
                var formasVentaApi = JsonConvert.DeserializeObject<List<FormaVentaApiResponse>>(resultado);

                if (formasVentaApi == null || !formasVentaApi.Any())
                    return Enumerable.Empty<FormaVentaItem>();

                // Mapear a FormaVentaItem
                return formasVentaApi.Select(f => new FormaVentaItem
                {
                    Empresa = f.Empresa?.Trim(),
                    Numero = f.Número?.Trim(),
                    Descripcion = f.Descripción?.Trim(),
                    VisiblePorComerciales = f.VisiblePorComerciales
                });
            }
        }

        /// <summary>
        /// Clase interna para deserializar la respuesta de la API (nombres con tildes del modelo EF).
        /// </summary>
        private class FormaVentaApiResponse
        {
            public string Empresa { get; set; }
            public string Número { get; set; }
            public string Descripción { get; set; }
            public bool VisiblePorComerciales { get; set; }
        }
    }
}
