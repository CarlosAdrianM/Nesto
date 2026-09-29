using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    /// <summary>Nesto#261: auditoría de enlaces de pago por API (patrón FacturasVerifactuService).</summary>
    public class AuditoriaEnlacesPagoService : IAuditoriaEnlacesPagoService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public AuditoriaEnlacesPagoService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<EnlacePagoAuditoriaModel>> Buscar(FiltroAuditoriaEnlacesPago filtro)
        {
            using (HttpClient client = await CrearCliente())
            {
                HttpResponseMessage response = await client.GetAsync(ConstruirUrl(filtro));
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"No se pudieron consultar los enlaces de pago: {ExtraerMensaje(response, body)}");
                }
                return JsonConvert.DeserializeObject<List<EnlacePagoAuditoriaModel>>(body)
                    ?? new List<EnlacePagoAuditoriaModel>();
            }
        }

        /// <summary>La URL relativa con los filtros que vengan informados.</summary>
        internal static string ConstruirUrl(FiltroAuditoriaEnlacesPago? filtro)
        {
            var parametros = new List<string>();
            if (filtro != null)
            {
                Anadir(parametros, "numeroOrden", filtro.NumeroOrden);
                if (filtro.FechaDesde.HasValue)
                {
                    Anadir(parametros, "fechaDesde", filtro.FechaDesde.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
                if (filtro.FechaHasta.HasValue)
                {
                    Anadir(parametros, "fechaHasta", filtro.FechaHasta.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
                }
                Anadir(parametros, "cliente", filtro.Cliente);
                Anadir(parametros, "usuario", filtro.Usuario);
                Anadir(parametros, "estado", filtro.Estado);
            }
            return parametros.Count == 0 ? "Pagos/Auditoria" : "Pagos/Auditoria?" + string.Join("&", parametros);
        }

        private static void Anadir(List<string> parametros, string nombre, string? valor)
        {
            if (!string.IsNullOrWhiteSpace(valor))
            {
                parametros.Add($"{nombre}={Uri.EscapeDataString(valor.Trim())}");
            }
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

        // Los errores de Web API llegan como {"Message":"..."}: extraer el texto legible.
        private static string ExtraerMensaje(HttpResponseMessage response, string body)
        {
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return "solo pueden consultarlos Administración y Dirección";
            }
            try
            {
                string? mensaje = JsonConvert.DeserializeObject<JObject>(body)?["Message"]?.ToString();
                if (!string.IsNullOrWhiteSpace(mensaje))
                {
                    return mensaje;
                }
            }
            catch
            {
                // No era JSON: se devuelve tal cual
            }
            return string.IsNullOrWhiteSpace(body) ? response.ReasonPhrase ?? response.StatusCode.ToString() : body;
        }
    }
}
