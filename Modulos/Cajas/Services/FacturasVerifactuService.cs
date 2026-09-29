using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.Cajas.Services
{
    /// <summary>NestoAPI#522: facturas pendientes de Verifactu por API (patrón NifIncorrectosService).</summary>
    public class FacturasVerifactuService : IFacturasVerifactuService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public FacturasVerifactuService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<FacturaPendienteVerifactuModel>> LeerFacturasPendientes()
        {
            using (HttpClient client = await CrearCliente())
            {
                HttpResponseMessage response = await client.GetAsync("Verifactu/FacturasPendientes");
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"No se pudieron cargar las facturas pendientes de Verifactu: {ExtraerMensaje(response, body)}");
                }
                return JsonConvert.DeserializeObject<List<FacturaPendienteVerifactuModel>>(body)
                    ?? new List<FacturaPendienteVerifactuModel>();
            }
        }

        public async Task<ResultadoReintentoVerifactuModel> ReintentarFactura(string empresa, string numero)
        {
            using (HttpClient client = await CrearCliente())
            {
                HttpContent contenido = new StringContent(
                    JsonConvert.SerializeObject(new { Empresa = empresa?.Trim(), Numero = numero?.Trim() }),
                    Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("Verifactu/ReintentarFactura", contenido);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"No se pudo reintentar la factura {numero?.Trim()}: {ExtraerMensaje(response, body)}");
                }
                return JsonConvert.DeserializeObject<ResultadoReintentoVerifactuModel>(body)
                    ?? new ResultadoReintentoVerifactuModel { Exitoso = false, Mensaje = "La API no ha devuelto respuesta" };
            }
        }

        /// <summary>
        /// NestoAPI#392: declara como simplificada (F2; sus rectificativas R5) una factura cuyo NIF no se puede
        /// conseguir. Si la API no lo permite (p. ej. supera el límite de la simplificada) lanza con su mensaje.
        /// </summary>
        public async Task<ResultadoReintentoVerifactuModel> DeclararSimplificada(string empresa, string numero, string motivo)
        {
            using (HttpClient client = await CrearCliente())
            {
                HttpContent contenido = new StringContent(
                    JsonConvert.SerializeObject(new { Empresa = empresa?.Trim(), Numero = numero?.Trim(), Motivo = motivo?.Trim() }),
                    Encoding.UTF8, "application/json");
                HttpResponseMessage response = await client.PostAsync("Verifactu/DeclararSimplificada", contenido);
                string body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                {
                    string mensaje = response.StatusCode == HttpStatusCode.Forbidden
                        ? "solo pueden hacerlo Administración y Dirección"
                        : ExtraerMensaje(response, body);
                    throw new Exception($"No se pudo declarar como simplificada la factura {numero?.Trim()}: {mensaje}");
                }
                return JsonConvert.DeserializeObject<ResultadoReintentoVerifactuModel>(body)
                    ?? new ResultadoReintentoVerifactuModel { Exitoso = false, Mensaje = "La API no ha devuelto respuesta" };
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
                return "solo pueden verlas Administración y Dirección";
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
