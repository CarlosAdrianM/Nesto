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
        /// <summary>NestoAPI#867: las excepciones por fecha de hoy en adelante.</summary>
        /// <exception cref="CalendarioReposicionesException">503 si falta el script en la BD, 404 con una API anterior…</exception>
        Task<List<ExcepcionCalendarioReposicion>> LeerExcepciones(string empresa);
        /// <summary>NestoAPI#867: crea o cambia la excepción de esa ruta y ese día; devuelve la guardada.</summary>
        /// <exception cref="CalendarioReposicionesException">400 con el motivo, 403, 409 si ese día ya ha cerrado…</exception>
        Task<ExcepcionCalendarioReposicion> GuardarExcepcion(ExcepcionCalendarioReposicion excepcion);
        /// <summary>NestoAPI#867: borra una excepción (ese día vuelve a ser como dice el calendario).</summary>
        /// <exception cref="CalendarioReposicionesException">403, 404, 409 si ese día ya ha cerrado…</exception>
        Task BorrarExcepcion(int id);
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

        public async Task<List<ExcepcionCalendarioReposicion>> LeerExcepciones(string empresa)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Reposiciones/Calendario/Excepciones?empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty))
                    .ConfigureAwait(false);
                return await LeerJson<List<ExcepcionCalendarioReposicion>>(response).ConfigureAwait(false) ?? new List<ExcepcionCalendarioReposicion>();
            }
        }

        public async Task<ExcepcionCalendarioReposicion> GuardarExcepcion(ExcepcionCalendarioReposicion excepcion)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(excepcion), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("Reposiciones/Calendario/Excepciones", contenido).ConfigureAwait(false);
                return await LeerJson<ExcepcionCalendarioReposicion>(response).ConfigureAwait(false);
            }
        }

        public async Task BorrarExcepcion(int id)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.DeleteAsync("Reposiciones/Calendario/Excepciones/" + id).ConfigureAwait(false);
                _ = await LeerJson<ExcepcionCalendarioReposicion>(response).ConfigureAwait(false);
            }
        }

        private static async Task<T> LeerJson<T>(HttpResponseMessage response)
        {
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new CalendarioReposicionesException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode), (int)response.StatusCode);
            }
            return string.IsNullOrWhiteSpace(json) ? default : JsonConvert.DeserializeObject<T>(json);
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
