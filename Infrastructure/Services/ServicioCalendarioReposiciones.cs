using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// NestoAPI#577 (09/10/26): el calendario de reposiciones (horas de cierre, de llegada y días de cada ruta), que antes
    /// se cambiaba con scripts. Nesto no valida nada de negocio: lo hace la API y aquí se enseña su mensaje.
    /// </summary>
    public interface IServicioCalendarioReposiciones
    {
        /// <exception cref="CalendarioReposicionesException">La API no lo ha devuelto.</exception>
        Task<List<FilaCalendarioReposicion>> LeerCalendario(string empresa);
        /// <summary>
        /// Si el usuario puede cambiarlo (GET api/Reposiciones/Calendario/PuedeEditar: los mismos que rellenan reposiciones a
        /// mano). Ante cualquier fallo, o una API anterior (404), false: la ventana queda en solo lectura.
        /// </summary>
        Task<bool> PuedeEditar();
        /// <summary>Guarda las filas y devuelve el calendario entero como queda.</summary>
        /// <exception cref="CalendarioReposicionesException">400 con el motivo, 403 sin permiso…</exception>
        Task<List<FilaCalendarioReposicion>> Guardar(GuardarCalendarioReposiciones peticion);
    }

    public class ServicioCalendarioReposiciones : IServicioCalendarioReposiciones
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioCalendarioReposiciones(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<FilaCalendarioReposicion>> LeerCalendario(string empresa)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Reposiciones/Calendario?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty))
                    .ConfigureAwait(false);
                return await Leer(response).ConfigureAwait(false);
            }
        }

        public async Task<bool> PuedeEditar()
        {
            try
            {
                using (HttpClient client = _clienteApiFactory.Crear())
                {
                    HttpResponseMessage response = await client.GetAsync("Reposiciones/Calendario/PuedeEditar").ConfigureAwait(false);
                    if (!response.IsSuccessStatusCode)
                    {
                        return false;
                    }
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    return JsonConvert.DeserializeObject<bool>(json);
                }
            }
            catch (HttpRequestException)
            {
                return false;
            }
        }

        public async Task<List<FilaCalendarioReposicion>> Guardar(GuardarCalendarioReposiciones peticion)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(peticion), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PutAsync("Reposiciones/Calendario", contenido).ConfigureAwait(false);
                return await Leer(response).ConfigureAwait(false);
            }
        }

        private static async Task<List<FilaCalendarioReposicion>> Leer(HttpResponseMessage response)
        {
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new CalendarioReposicionesException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode), (int)response.StatusCode);
            }
            return JsonConvert.DeserializeObject<List<FilaCalendarioReposicion>>(json) ?? new List<FilaCalendarioReposicion>();
        }
    }
}
