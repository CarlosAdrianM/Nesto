using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Nesto#523 (NestoAPI#623): el origen del cliente para las campañas (primer origen META-HARMONIA-0926, leads de Meta de
    /// Harmonia). Con la API vieja (sin los endpoints) el catálogo sale vacío y el origen, null: nada de errores.
    /// </summary>
    public interface IOrigenesClienteService
    {
        /// <summary>Los orígenes activos. Lista vacía si la API todavía no los tiene (404).</summary>
        Task<List<OrigenClienteModel>> LeerOrigenes();
        /// <summary>El origen del cliente, o null si no tiene (204/404).</summary>
        Task<OrigenDeClienteModel> LeerOrigen(string empresa, string cliente);
        /// <summary><paramref name="origen"/> null lo quita. Lanza con el texto de la API (400/403).</summary>
        Task<OrigenDeClienteModel> GuardarOrigen(string empresa, string cliente, string origen);
    }

    /// <summary>Nesto#523: por HTTP contra NestoAPI (patrón DiaRepartoClienteService del mismo módulo).</summary>
    public class OrigenesClienteService : IOrigenesClienteService
    {
        public const string MENSAJE_SIN_PERMISO = "No tiene permiso para cambiar el origen del cliente.";
        public const string MENSAJE_NO_DISPONIBLE = "La API todavía no permite cambiar el origen del cliente.";

        private static readonly TimeSpan DURACION_CACHE = TimeSpan.FromMinutes(10);
        private static List<OrigenClienteModel> _cache;
        private static DateTime _cacheHasta;

        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public OrigenesClienteService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        private async Task<HttpClient> Cliente()
        {
            var client = new HttpClient { BaseAddress = new Uri(configuracion.servidorAPI) };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }

        internal static string UrlOrigen(string empresa, string cliente)
            => $"Clientes/{Uri.EscapeDataString(string.IsNullOrWhiteSpace(empresa) ? "1" : empresa.Trim())}/{Uri.EscapeDataString(cliente?.Trim() ?? string.Empty)}/Origen";

        public async Task<List<OrigenClienteModel>> LeerOrigenes()
        {
            List<OrigenClienteModel> cache = _cache;
            if (cache != null && DateTime.Now < _cacheHasta)
            {
                return cache.ToList();
            }
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.GetAsync("Clientes/Origenes");
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                return new List<OrigenClienteModel>();
            }
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"No se han podido leer los orígenes de cliente: {DiaRepartoClienteService.ExtraerMensaje(body)}");
            }
            List<OrigenClienteModel> origenes = (string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject<List<OrigenClienteModel>>(body))
                ?? new List<OrigenClienteModel>();
            origenes = origenes.Where(o => o != null && !string.IsNullOrWhiteSpace(o.Codigo)).ToList();
            if (origenes.Count > 0)
            {
                _cache = origenes;
                _cacheHasta = DateTime.Now.Add(DURACION_CACHE);
            }
            return origenes.ToList();
        }

        public async Task<OrigenDeClienteModel> LeerOrigen(string empresa, string cliente)
        {
            if (string.IsNullOrWhiteSpace(cliente))
            {
                return null;
            }
            using HttpClient client = await Cliente();
            HttpResponseMessage response = await client.GetAsync(UrlOrigen(empresa, cliente));
            if (response.StatusCode == HttpStatusCode.NotFound || response.StatusCode == HttpStatusCode.NoContent)
            {
                return null;
            }
            string body = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"No se ha podido leer el origen del cliente: {DiaRepartoClienteService.ExtraerMensaje(body)}");
            }
            OrigenDeClienteModel origen = string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject<OrigenDeClienteModel>(body);
            return string.IsNullOrWhiteSpace(origen?.Origen) ? null : origen;
        }

        public async Task<OrigenDeClienteModel> GuardarOrigen(string empresa, string cliente, string origen)
        {
            using HttpClient client = await Cliente();
            var cuerpo = new GuardarOrigenClienteModel { Origen = string.IsNullOrWhiteSpace(origen) ? null : origen.Trim() };
            HttpResponseMessage response = await client.PutAsync(UrlOrigen(empresa, cliente),
                new StringContent(JsonConvert.SerializeObject(cuerpo), Encoding.UTF8, "application/json"));
            string body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                string mensaje = DiaRepartoClienteService.ExtraerMensaje(body);
                throw new Exception(string.IsNullOrWhiteSpace(mensaje) ? MENSAJE_NO_DISPONIBLE : mensaje);
            }
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                string mensaje = DiaRepartoClienteService.ExtraerMensaje(body);
                throw new UnauthorizedAccessException(string.IsNullOrWhiteSpace(mensaje) ? MENSAJE_SIN_PERMISO : mensaje);
            }
            if (!response.IsSuccessStatusCode)
            {
                throw new Exception(DiaRepartoClienteService.ExtraerMensaje(body));
            }
            OrigenDeClienteModel guardado = null;
            try
            {
                guardado = string.IsNullOrWhiteSpace(body) ? null : JsonConvert.DeserializeObject<OrigenDeClienteModel>(body);
            }
            catch (JsonException)
            {
                // El 200 puede venir sin el origen en el cuerpo: se da por guardado lo que se mandó.
            }
            guardado ??= new OrigenDeClienteModel();
            if (string.IsNullOrWhiteSpace(guardado.Origen))
            {
                guardado.Origen = cuerpo.Origen;
            }
            return guardado;
        }
    }
}
