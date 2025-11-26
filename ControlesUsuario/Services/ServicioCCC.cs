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
    /// Implementación del servicio de CCCs (cuentas bancarias).
    /// Carlos 20/11/2024: Creado para el control SelectorCCC siguiendo el patrón de ServicioDireccionesEntrega.
    /// TEMPORAL: Usa endpoint PlantillaVentas/DireccionesEntrega hasta que se despliegue api/Clientes/CCCs
    /// </summary>
    public class ServicioCCC : IServicioCCC
    {
        private readonly IConfiguracion _configuracion;

        public ServicioCCC(IConfiguracion configuracion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        }

        /// <summary>
        /// Obtiene los CCCs para un cliente/contacto desde la API.
        /// </summary>
        public async Task<IEnumerable<CCCItem>> ObtenerCCCs(
            string empresa,
            string cliente,
            string contacto)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(empresa))
                throw new ArgumentException("Empresa es requerida", nameof(empresa));

            if (string.IsNullOrWhiteSpace(cliente))
                throw new ArgumentException("Cliente es requerido", nameof(cliente));

            if (string.IsNullOrWhiteSpace(contacto))
                throw new ArgumentException("Contacto es requerido", nameof(contacto));

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Usar endpoint correcto de CCCs
                // Nota: servidorAPI ya incluye "/api/" al final
                string urlConsulta = $"Clientes/CCCs?empresa={empresa}&cliente={cliente}&contacto={contacto}";
                Debug.WriteLine($"[ServicioCCC] URL Base: {_configuracion.servidorAPI}, Consulta: {urlConsulta}");

                HttpResponseMessage response = await client.GetAsync(urlConsulta);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error al obtener CCCs: {response.StatusCode} - {response.ReasonPhrase}");
                }

                string resultado = await response.Content.ReadAsStringAsync();

                // Deserializar directamente a CCCItems
                var cccs = JsonConvert.DeserializeObject<List<CCCItem>>(resultado);

                if (cccs == null || !cccs.Any())
                    return Enumerable.Empty<CCCItem>();

                return cccs;
            }
        }
    }
}
