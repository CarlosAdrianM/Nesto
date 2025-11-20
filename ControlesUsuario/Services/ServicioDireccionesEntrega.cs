using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Implementación del servicio de direcciones de entrega.
    /// Carlos 20/11/2024: Extraído desde SelectorDireccionEntrega.cargarDatos() para hacerlo testeable (FASE 3).
    /// </summary>
    public class ServicioDireccionesEntrega : IServicioDireccionesEntrega
    {
        private readonly IConfiguracion _configuracion;

        public ServicioDireccionesEntrega(IConfiguracion configuracion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        }

        /// <summary>
        /// Obtiene las direcciones de entrega para un cliente desde la API.
        /// Endpoint: PlantillaVentas/DireccionesEntrega
        /// </summary>
        public async Task<IEnumerable<DireccionesEntregaCliente>> ObtenerDireccionesEntrega(
            string empresa,
            string cliente,
            decimal? totalPedido = null)
        {
            // Validaciones
            if (string.IsNullOrWhiteSpace(empresa))
                throw new ArgumentException("Empresa es requerida", nameof(empresa));

            if (string.IsNullOrWhiteSpace(cliente))
                throw new ArgumentException("Cliente es requerido", nameof(cliente));

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Construir URL con parámetros
                string urlConsulta = $"PlantillaVentas/DireccionesEntrega?empresa={empresa}&clienteDirecciones={cliente}";

                if (totalPedido.HasValue && totalPedido.Value != 0)
                {
                    // Usar cultura en-US para formato decimal con punto (no coma)
                    string totalFormateado = totalPedido.Value.ToString(CultureInfo.GetCultureInfo("en-US"));
                    urlConsulta += $"&totalPedido={totalFormateado}";
                }

                HttpResponseMessage response = await client.GetAsync(urlConsulta);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error al obtener direcciones de entrega: {response.StatusCode} - {response.ReasonPhrase}");
                }

                string resultado = await response.Content.ReadAsStringAsync();

                var direcciones = JsonConvert.DeserializeObject<IEnumerable<DireccionesEntregaCliente>>(resultado);

                return direcciones ?? Enumerable.Empty<DireccionesEntregaCliente>();
            }
        }
    }
}
