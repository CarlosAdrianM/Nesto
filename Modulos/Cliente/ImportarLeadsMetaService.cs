using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cliente
{
    /// <summary>NestoAPI#623 corte 4: importar el fichero de leads que exporta Meta Ads (Excel XML o CSV).</summary>
    public interface IImportarLeadsMetaService
    {
        /// <summary>Con <paramref name="ensayo"/> la API no escribe nada y dice qué haría. Lanza con el texto de la API (400/403).</summary>
        Task<ResultadoImportacionLeadsModel> Importar(string origen, bool ensayo, byte[] fichero, string nombreFichero);
    }

    public class ImportarLeadsMetaService : IImportarLeadsMetaService
    {
        public const string MENSAJE_NO_DISPONIBLE = "La API todavía no permite importar leads.";
        public const string MENSAJE_SIN_PERMISO = "No tiene permiso para importar leads.";

        private readonly IConfiguracion configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public ImportarLeadsMetaService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            this.configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        internal static string Url(string origen, bool ensayo)
            => $"Clientes/Origenes/ImportarLeads?origen={Uri.EscapeDataString(origen?.Trim() ?? string.Empty)}&ensayo={(ensayo ? "true" : "false")}";

        public async Task<ResultadoImportacionLeadsModel> Importar(string origen, bool ensayo, byte[] fichero, string nombreFichero)
        {
            using var client = new HttpClient { BaseAddress = new Uri(configuracion.servidorAPI), Timeout = TimeSpan.FromMinutes(5) };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            using var contenido = new MultipartFormDataContent();
            var parte = new ByteArrayContent(fichero ?? Array.Empty<byte>());
            parte.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            contenido.Add(parte, "fichero", string.IsNullOrWhiteSpace(nombreFichero) ? "leads.xml" : Path.GetFileName(nombreFichero));

            HttpResponseMessage response = await client.PostAsync(Url(origen, ensayo), contenido);
            string body = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                throw new Exception(MENSAJE_NO_DISPONIBLE);
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
            return JsonConvert.DeserializeObject<ResultadoImportacionLeadsModel>(body) ?? new ResultadoImportacionLeadsModel();
        }
    }
}
