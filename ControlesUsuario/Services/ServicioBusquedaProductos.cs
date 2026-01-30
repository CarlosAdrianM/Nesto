using ControlesUsuario.Models;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Services
{
    /// <summary>
    /// Servicio de búsqueda de productos para autocomplete.
    /// Usa el endpoint /api/buscador con Lucene para búsqueda full-text.
    /// Issue #263 - Carlos 30/01/26
    /// </summary>
    public class ServicioBusquedaProductos : IServicioBusquedaAutocomplete
    {
        private readonly IConfiguracion _configuracion;

        public ServicioBusquedaProductos(IConfiguracion configuracion)
        {
            _configuracion = configuracion ?? throw new ArgumentNullException(nameof(configuracion));
        }

        public async Task<IList<AutocompleteItem>> BuscarSugerenciasAsync(
            string texto,
            string empresa,
            int maxResultados,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(texto) || texto.Length < 2)
            {
                return new List<AutocompleteItem>();
            }

            try
            {
                using (var client = new HttpClient())
                {
                    client.BaseAddress = new Uri(_configuracion.servidorAPI);
                    client.DefaultRequestHeaders.Accept.Clear();
                    client.DefaultRequestHeaders.Accept.Add(
                        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                    // Usar el buscador Lucene con filtro de tipo=producto
                    var urlConsulta = $"buscador?q={Uri.EscapeDataString(texto)}&tipo=producto";

                    var response = await client.GetAsync(urlConsulta, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        return new List<AutocompleteItem>();
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var resultados = JsonConvert.DeserializeObject<List<BuscadorResultado>>(json);

                    if (resultados == null)
                    {
                        return new List<AutocompleteItem>();
                    }

                    return resultados
                        .Take(maxResultados)
                        .Select(r => new AutocompleteItem
                        {
                            Id = r.Id,
                            Texto = r.Nombre,
                            TextoSecundario = null
                        })
                        .ToList();
                }
            }
            catch (OperationCanceledException)
            {
                // Búsqueda cancelada, devolver lista vacía
                return new List<AutocompleteItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServicioBusquedaProductos] Error: {ex.Message}");
                return new List<AutocompleteItem>();
            }
        }

        /// <summary>
        /// Clase para deserializar la respuesta del buscador Lucene.
        /// </summary>
        private class BuscadorResultado
        {
            public string Tipo { get; set; }
            public string Id { get; set; }
            public string Nombre { get; set; }
        }
    }
}
