using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Implementación del servicio de series de facturación.
    /// Carlos 09/12/25: Issue #245
    /// </summary>
    public class ServicioSeries : IServicioSeries
    {
        private readonly IConfiguracion _configuracion;

        public ServicioSeries(IConfiguracion configuracion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        }

        public async Task<List<SerieItem>> ObtenerSeries()
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                try
                {
                    var response = await client.GetAsync("PedidosVenta/Series");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[ServicioSeries] Error HTTP: {response.StatusCode}");
                        return GetSeriesDefault();
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var series = JsonConvert.DeserializeObject<List<SerieItem>>(json);

                    return series ?? GetSeriesDefault();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ServicioSeries] Error: {ex.Message}");
                    return GetSeriesDefault();
                }
            }
        }

        private List<SerieItem> GetSeriesDefault()
        {
            return new List<SerieItem>
            {
                new SerieItem { Codigo = "NV", Nombre = "Nueva Visión" },
                new SerieItem { Codigo = "CV", Nombre = "Cursos" },
                new SerieItem { Codigo = "UL", Nombre = "Unión Láser" },
                new SerieItem { Codigo = "VC", Nombre = "Visnú Cosméticos" },
                new SerieItem { Codigo = "DV", Nombre = "Deuda Vencida" }
            };
        }
    }
}
