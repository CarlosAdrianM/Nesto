using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// <summary>
    /// NestoAPI#553: recibir en la tienda las reposiciones que salen de Algete, con la misma API que Ariadna
    /// (api/Almacen/Recepciones, tipo REPO). Nesto no decide nada: entra lo leído y el servidor informa de las
    /// diferencias a quien creó el traspaso.
    /// </summary>
    public interface IServicioRecepcionReposiciones
    {
        /// <summary>Solo las reposiciones (las compras de proveedor se reciben en Ariadna).</summary>
        Task<List<RecepcionPendiente>> LeerPendientes(string empresa, string almacen);
        Task<RecepcionReposicion> LeerRecepcion(string empresa, string almacen, string traspaso);
        /// <exception cref="RecepcionReposicionException">La API no la ha terminado (sin permiso, ya terminada…).</exception>
        Task<ResultadoRecepcionReposicion> Terminar(string empresa, string almacen, string traspaso, TerminarRecepcionReposicion terminar);
        /// <summary>
        /// Sugerencia 564: el PDF de lo que llega con la reposición, para comprobarlo a mano
        /// (GET api/Reposiciones/Recepcion/{traspaso}/Pdf).
        /// </summary>
        /// <exception cref="RecepcionReposicionException">Ya no está pendiente (404) o la API no lo ha generado.</exception>
        Task<byte[]> DescargarListadoPdf(string empresa, string almacen, string traspaso);
    }

    public class ServicioRecepcionReposiciones : IServicioRecepcionReposiciones
    {
        public const string TIPO_REPOSICION = "REPO";

        private readonly IClienteApiFactory _clienteApiFactory;

        public ServicioRecepcionReposiciones(IClienteApiFactory clienteApiFactory)
        {
            _clienteApiFactory = clienteApiFactory;
        }

        public async Task<List<RecepcionPendiente>> LeerPendientes(string empresa, string almacen)
        {
            List<RecepcionPendiente> todas = await LeerAsync<List<RecepcionPendiente>>("Almacen/Recepciones" + Consulta(empresa, almacen))
                .ConfigureAwait(false);
            return (todas ?? new List<RecepcionPendiente>())
                .Where(r => string.Equals(r.Tipo?.Trim(), TIPO_REPOSICION, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        public Task<RecepcionReposicion> LeerRecepcion(string empresa, string almacen, string traspaso)
            => LeerAsync<RecepcionReposicion>($"Almacen/Recepciones/{TIPO_REPOSICION}/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}" + Consulta(empresa, almacen));

        public async Task<ResultadoRecepcionReposicion> Terminar(string empresa, string almacen, string traspaso, TerminarRecepcionReposicion terminar)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = $"Almacen/Recepciones/{TIPO_REPOSICION}/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}/Terminar" + Consulta(empresa, almacen);
                var contenido = new StringContent(JsonConvert.SerializeObject(terminar), Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync(url, contenido).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return JsonConvert.DeserializeObject<ResultadoRecepcionReposicion>(json) ?? new ResultadoRecepcionReposicion();
            }
        }

        public async Task<byte[]> DescargarListadoPdf(string empresa, string almacen, string traspaso)
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                string url = $"Reposiciones/Recepcion/{Uri.EscapeDataString(traspaso?.Trim() ?? string.Empty)}/Pdf" + Consulta(empresa, almacen);
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
            }
        }

        private async Task<T> LeerAsync<T>(string url) where T : class
        {
            using (HttpClient client = _clienteApiFactory.Crear())
            {
                HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    throw new RecepcionReposicionException(ServicioEtiquetasHueco.Motivo(json, (int)response.StatusCode));
                }
                return JsonConvert.DeserializeObject<T>(json);
            }
        }

        private static string Consulta(string empresa, string almacen)
            => "?almacen=" + Uri.EscapeDataString(almacen?.Trim() ?? string.Empty)
               + "&empresa=" + Uri.EscapeDataString(empresa?.Trim() ?? string.Empty);
    }
}
