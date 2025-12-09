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
    /// Implementación del servicio de almacenes.
    /// Carlos 09/12/25: Issue #253/#52
    /// </summary>
    public class ServicioAlmacenes : IServicioAlmacenes
    {
        private readonly IConfiguracion _configuracion;

        public ServicioAlmacenes(IConfiguracion configuracion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        }

        public async Task<List<AlmacenItem>> ObtenerAlmacenes()
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                try
                {
                    var response = await client.GetAsync("PedidosVenta/Almacenes");

                    if (!response.IsSuccessStatusCode)
                    {
                        Debug.WriteLine($"[ServicioAlmacenes] Error HTTP: {response.StatusCode}");
                        return GetAlmacenesDefault();
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var almacenes = JsonConvert.DeserializeObject<List<AlmacenItem>>(json);

                    return almacenes ?? GetAlmacenesDefault();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ServicioAlmacenes] Error: {ex.Message}");
                    return GetAlmacenesDefault();
                }
            }
        }

        private List<AlmacenItem> GetAlmacenesDefault()
        {
            // Almacenes por defecto en caso de fallo (los principales destacados)
            return new List<AlmacenItem>
            {
                new AlmacenItem { Codigo = "ALG", Nombre = "Algete", EsFicticio = false, PermiteNegativo = false },
                new AlmacenItem { Codigo = "REI", Nombre = "Reina", EsFicticio = false, PermiteNegativo = false },
                new AlmacenItem { Codigo = "ALC", Nombre = "Alcobendas", EsFicticio = false, PermiteNegativo = false }
            };
        }
    }
}
