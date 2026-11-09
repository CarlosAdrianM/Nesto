using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// Nesto#616 (NestoAPI#779): el calendario de festivos, que antes iba codificado en la API y ahora está en la tabla
    /// <c>dbo.Festivos</c>. Nesto no valida nada de negocio: lo hace la API y aquí se enseña su mensaje.
    /// </summary>
    public interface IServicioFestivos
    {
        /// <summary>GET api/Festivos?anio=: los del año, por fecha.</summary>
        /// <exception cref="FestivosException">La API no los ha devuelto.</exception>
        Task<List<Festivo>> Leer(int anio);
        /// <summary>POST api/Festivos: alta (o cambio de la descripción si ya está esa fecha con ese tipo).</summary>
        /// <exception cref="FestivosException">400 con el motivo, 403 sin permiso, 409 sin tabla…</exception>
        Task<Festivo> Guardar(PeticionFestivo peticion);
        /// <summary>DELETE api/Festivos/{fecha}/{tipo}.</summary>
        /// <exception cref="FestivosException">403 sin permiso, 404 si no existe, 409 sin tabla…</exception>
        Task Borrar(DateTime fecha, string tipo);
        /// <summary>
        /// NestoAPI#841: ese día no sale la furgoneta (hay un festivo RepartoPropio en esa fecha). Lee los del año de la
        /// fecha en cada llamada (se llama al abrir la pantalla, sin temporizadores).
        /// </summary>
        /// <exception cref="FestivosException">La API no los ha devuelto.</exception>
        Task<bool> EsDiaSinRepartoPropio(DateTime fecha);
    }

    public class ServicioFestivos : IServicioFestivos
    {
        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioFestivos(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<Festivo>> Leer(int anio)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync("Festivos?anio=" + anio.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
                string json = await LeerRespuesta(response).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<List<Festivo>>(json) ?? new List<Festivo>();
            }
        }

        public async Task<Festivo> Guardar(PeticionFestivo peticion)
        {
            if (peticion == null)
            {
                throw new ArgumentNullException(nameof(peticion));
            }
            var cuerpo = new PeticionFestivo
            {
                Fecha = peticion.Fecha.Date,
                Tipo = peticion.Tipo,
                Descripcion = peticion.Descripcion?.Trim()
            };
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                var contenido = new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("Festivos", contenido).ConfigureAwait(false);
                string json = await LeerRespuesta(response).ConfigureAwait(false);
                return JsonConvert.DeserializeObject<Festivo>(json);
            }
        }

        public async Task Borrar(DateTime fecha, string tipo)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.DeleteAsync(UrlBorrar(fecha, tipo)).ConfigureAwait(false);
                _ = await LeerRespuesta(response).ConfigureAwait(false);
            }
        }

        public async Task<bool> EsDiaSinRepartoPropio(DateTime fecha)
        {
            List<Festivo> festivos = await Leer(fecha.Year).ConfigureAwait(false);
            return EsDiaSinRepartoPropio(festivos, fecha);
        }

        /// <summary>NestoAPI#841: la regla, sin la API: hay un festivo RepartoPropio en esa fecha (sin mirar la hora).</summary>
        public static bool EsDiaSinRepartoPropio(IEnumerable<Festivo> festivos, DateTime fecha)
            => festivos != null && festivos.Any(f => f != null && f.EsSinRepartoPropio && f.Fecha.Date == fecha.Date);

        /// <summary>«Festivos/2027-03-19/Auton%C3%B3mica»: la fecha sin hora y el tipo escapado (lleva tilde).</summary>
        internal static string UrlBorrar(DateTime fecha, string tipo)
            => "Festivos/" + fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) + "/" + Uri.EscapeDataString(tipo?.Trim() ?? string.Empty);

        private static async Task<string> LeerRespuesta(HttpResponseMessage response)
        {
            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                int codigo = (int)response.StatusCode;
                string motivo = codigo == 404 && string.IsNullOrWhiteSpace(json)
                    ? "Ese festivo ya no está en la tabla."
                    : ServicioEtiquetasHueco.Motivo(json, codigo);
                throw new FestivosException(motivo, codigo);
            }
            return json;
        }
    }
}
