using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    /// <summary>Inmovilizados (Carlos 25/11/26): consulta y etiquetas por API (patrón AuditoriaEnlacesPagoService).</summary>
    public class InmovilizadosService : IInmovilizadosService
    {
        internal const string MENSAJE_SIN_PERMISO = "No tiene permiso para imprimir etiquetas de inmovilizado.";
        internal const string MENSAJE_NO_EXISTE = "No existe ninguno de esos inmovilizados.";
        internal const string MENSAJE_SERVIDOR_ANTIGUO = "Esta opción necesita la versión nueva del servidor.";

        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public InmovilizadosService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<InmovilizadoModel>> Buscar(string empresa, string? filtro, string? delegacion, bool soloVivos)
        {
            using (HttpClient client = await CrearCliente())
            {
                HttpResponseMessage response = await client.GetAsync(ConstruirUrl(empresa, filtro, delegacion, soloVivos));
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new ErrorApiCajasException(Mensaje(response.StatusCode, body, "No se han podido consultar los inmovilizados."));
                }
                return JsonConvert.DeserializeObject<List<InmovilizadoModel>>(body) ?? new List<InmovilizadoModel>();
            }
        }

        public async Task<RespuestaImprimirEtiquetasInmovilizado> ImprimirEtiquetas(PeticionImprimirEtiquetasInmovilizado peticion)
        {
            using (HttpClient client = await CrearCliente())
            {
                string json = JsonConvert.SerializeObject(peticion);
                using (var contenido = new StringContent(json, Encoding.UTF8, "application/json"))
                {
                    HttpResponseMessage response = await client.PostAsync("Inmovilizados/Etiquetas/Imprimir", contenido);
                    string body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                    {
                        throw new ErrorApiCajasException(Mensaje(response.StatusCode, body, "No se han podido imprimir las etiquetas."));
                    }
                    return JsonConvert.DeserializeObject<RespuestaImprimirEtiquetasInmovilizado>(body)
                        ?? new RespuestaImprimirEtiquetasInmovilizado();
                }
            }
        }

        internal static string ConstruirUrl(string empresa, string? filtro, string? delegacion, bool soloVivos)
        {
            var url = $"Inmovilizados?empresa={Uri.EscapeDataString(empresa ?? string.Empty)}";
            if (!string.IsNullOrWhiteSpace(filtro))
            {
                url += "&filtro=" + Uri.EscapeDataString(filtro.Trim());
            }
            if (!string.IsNullOrWhiteSpace(delegacion))
            {
                url += "&delegacion=" + Uri.EscapeDataString(delegacion.Trim());
            }
            return url + "&soloVivos=" + (soloVivos ? "true" : "false");
        }

        /// <summary>
        /// Lo que dice la API; si no dice nada legible: 403 «no tiene permiso», 404 «no existe» (o, si es el 404 de una ruta que el
        /// servidor aún no tiene, «necesita la versión nueva») y, si no, el texto de contexto.
        /// </summary>
        internal static string Mensaje(HttpStatusCode estado, string body, string contexto)
        {
            string? detalle = ErroresApiCajas.Detalle(body);
            bool esRutaQueNoExiste = estado == HttpStatusCode.NotFound
                && detalle != null && detalle.IndexOf("No HTTP resource", StringComparison.OrdinalIgnoreCase) >= 0;
            if (esRutaQueNoExiste)
            {
                return MENSAJE_SERVIDOR_ANTIGUO;
            }
            if (!string.IsNullOrWhiteSpace(detalle))
            {
                return detalle!;
            }
            return estado switch
            {
                HttpStatusCode.Forbidden => MENSAJE_SIN_PERMISO,
                HttpStatusCode.NotFound => MENSAJE_NO_EXISTE,
                _ => contexto
            };
        }

        private async Task<HttpClient> CrearCliente()
        {
            var client = new HttpClient { BaseAddress = new Uri(_configuracion.servidorAPI) };
            if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
            {
                client.Dispose();
                throw new UnauthorizedAccessException("No se pudo configurar la autorización");
            }
            return client;
        }
    }
}
