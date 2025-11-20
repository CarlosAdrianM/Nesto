using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
        /// TEMPORAL: Usa PlantillaVentas/DireccionesEntrega (endpoint existente)
        /// TODO: Cambiar a api/Clientes/CCCs cuando se despliegue
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

                // TEMPORAL: Usar endpoint de direcciones de entrega y extraer CCCs
                string urlConsulta = $"PlantillaVentas/DireccionesEntrega?empresa={empresa}&clienteDirecciones={cliente}";

                HttpResponseMessage response = await client.GetAsync(urlConsulta);

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Error al obtener CCCs: {response.StatusCode} - {response.ReasonPhrase}");
                }

                string resultado = await response.Content.ReadAsStringAsync();

                // Deserializar direcciones y convertir a CCCItems
                var direcciones = JsonConvert.DeserializeObject<List<DireccionEntregaDTO>>(resultado);

                if (direcciones == null || !direcciones.Any())
                    return Enumerable.Empty<CCCItem>();

                // Convertir direcciones a CCCItems
                var cccs = direcciones
                    .Select(d => new CCCItem
                    {
                        empresa = empresa,
                        cliente = cliente,
                        contacto = d.contacto?.Trim() ?? "0",
                        numero = d.ccc?.Trim(),
                        entidad = null, // No disponible en DireccionesEntrega
                        oficina = null,
                        estado = 1, // Asumir válido por defecto
                        Descripcion = GenerarDescripcion(d)
                    })
                    .Where(c => !string.IsNullOrWhiteSpace(c.numero)) // Solo CCCs con valor
                    .OrderBy(c => c.contacto)
                    .ToList();

                return cccs;
            }
        }

        private string GenerarDescripcion(DireccionEntregaDTO direccion)
        {
            if (string.IsNullOrWhiteSpace(direccion.ccc))
                return $"Contacto {direccion.contacto}: Sin CCC";

            string cccCorto = direccion.ccc.Length > 8
                ? "..." + direccion.ccc.Substring(Math.Max(0, direccion.ccc.Length - 8))
                : direccion.ccc;

            string nombre = !string.IsNullOrWhiteSpace(direccion.nombre)
                ? direccion.nombre.Trim()
                : "Sin nombre";

            return $"Contacto {direccion.contacto} ({nombre}): {cccCorto}";
        }

        // DTO temporal para parsear DireccionesEntrega
        private class DireccionEntregaDTO
        {
            public string contacto { get; set; }
            public string ccc { get; set; }
            public string nombre { get; set; }
        }
    }
}
