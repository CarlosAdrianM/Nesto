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
    /// Servicio de búsqueda de cuentas contables para autocomplete.
    /// Usa el endpoint /api/PlanCuentas con filtro por grupo (prefijo).
    /// Issue #263 - Carlos 30/01/26
    /// </summary>
    public class ServicioBusquedaCuentas : IServicioBusquedaAutocomplete
    {
        private readonly IConfiguracion _configuracion;

        public ServicioBusquedaCuentas(IConfiguracion configuracion)
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

                    // Si el texto es numérico, buscar por prefijo de cuenta
                    // Si contiene texto, cargar cuentas y filtrar por nombre
                    string urlConsulta;
                    bool filtrarPorNombre = false;

                    if (EsNumerico(texto))
                    {
                        // Buscar cuentas que empiecen por el prefijo
                        urlConsulta = $"PlanCuentas?empresa={Uri.EscapeDataString(empresa)}&grupo={Uri.EscapeDataString(texto)}";
                    }
                    else
                    {
                        // Cargar todas las cuentas activas y filtrar por nombre client-side
                        // Nota: Esto es menos eficiente, pero la API actual no soporta búsqueda por nombre
                        urlConsulta = $"PlanCuentas?empresa={Uri.EscapeDataString(empresa)}";
                        filtrarPorNombre = true;
                    }

                    var response = await client.GetAsync(urlConsulta, cancellationToken);

                    if (!response.IsSuccessStatusCode)
                    {
                        return new List<AutocompleteItem>();
                    }

                    var json = await response.Content.ReadAsStringAsync();
                    var cuentas = JsonConvert.DeserializeObject<List<CuentaContableApiResponse>>(json);

                    if (cuentas == null)
                    {
                        return new List<AutocompleteItem>();
                    }

                    IEnumerable<CuentaContableApiResponse> resultadosFiltrados = cuentas;

                    if (filtrarPorNombre)
                    {
                        // Filtrar cuentas cuyo nombre contenga el texto (case-insensitive)
                        var textoLower = texto.ToLowerInvariant();
                        resultadosFiltrados = cuentas
                            .Where(c => c.Nombre != null &&
                                       c.Nombre.ToLowerInvariant().Contains(textoLower));
                    }

                    return resultadosFiltrados
                        .Take(maxResultados)
                        .Select(c => new AutocompleteItem
                        {
                            Id = c.Cuenta,
                            Texto = c.Nombre,
                            TextoSecundario = null
                        })
                        .ToList();
                }
            }
            catch (OperationCanceledException)
            {
                return new List<AutocompleteItem>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ServicioBusquedaCuentas] Error: {ex.Message}");
                return new List<AutocompleteItem>();
            }
        }

        /// <summary>
        /// Determina si el texto es puramente numérico (para búsqueda por prefijo de cuenta).
        /// </summary>
        private bool EsNumerico(string texto)
        {
            return texto.All(char.IsDigit);
        }

        /// <summary>
        /// Clase para deserializar la respuesta del API de PlanCuentas.
        /// </summary>
        private class CuentaContableApiResponse
        {
            public string Cuenta { get; set; }
            public string Nombre { get; set; }
            public string Iva { get; set; }
        }
    }
}
