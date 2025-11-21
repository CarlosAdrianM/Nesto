using CanalesExternos.Models;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Services
{
    /// <summary>
    /// Servicio para gestionar poison pills de mensajes de sincronización
    /// </summary>
    public class PoisonPillsService : IPoisonPillsService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public PoisonPillsService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        /// <summary>
        /// Obtiene la lista de poison pills con filtros opcionales
        /// </summary>
        public async Task<List<PoisonPillModel>> ObtenerPoisonPillsAsync(string status = null, string tabla = null, int limit = 100)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                try
                {
                    // Construir URL con parámetros de filtro
                    var urlConsulta = "sync/poisonpills?";
                    var parametros = new List<string>();

                    if (!string.IsNullOrEmpty(status))
                    {
                        parametros.Add($"status={status}");
                    }

                    if (!string.IsNullOrEmpty(tabla))
                    {
                        parametros.Add($"tabla={tabla}");
                    }

                    if (limit > 0)
                    {
                        parametros.Add($"limit={limit}");
                    }

                    urlConsulta += string.Join("&", parametros);

                    var response = await client.GetAsync(urlConsulta);

                    if (!response.IsSuccessStatusCode)
                    {
                        var errorContent = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Error al obtener poison pills. Código: {response.StatusCode}. Detalle: {errorContent}");
                    }

                    var respuesta = await response.Content.ReadAsStringAsync();

                    // La API retorna un objeto con estructura { total, filters, poisonPills, timestamp }
                    var resultado = JsonConvert.DeserializeObject<PoisonPillsResponse>(respuesta);

                    return resultado?.PoisonPills ?? new List<PoisonPillModel>();
                }
                catch (Exception ex)
                {
                    throw new Exception($"No se ha podido recuperar la lista de poison pills: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Cambia el estado de un poison pill
        /// </summary>
        public async Task<bool> CambiarEstadoAsync(string messageId, string newStatus)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);

                // Carlos 21/11/24: Agregar autenticación
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client))
                {
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");
                }

                try
                {
                    var request = new ChangeStatusRequestModel
                    {
                        MessageId = messageId,
                        NewStatus = newStatus
                    };

                    var json = JsonConvert.SerializeObject(request);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var response = await client.PostAsync("sync/poisonpills/changestatus", content);

                    if (!response.IsSuccessStatusCode)
                    {
                        var error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Error al cambiar estado del mensaje {messageId}: {error}");
                    }

                    var respuesta = await response.Content.ReadAsStringAsync();
                    var resultado = JsonConvert.DeserializeObject<ChangeStatusResponse>(respuesta);

                    return resultado?.Success ?? false;
                }
                catch (Exception ex)
                {
                    throw new Exception($"No se ha podido cambiar el estado del mensaje: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Clase interna para deserializar la respuesta del endpoint GET /api/sync/poisonpills
        /// </summary>
        private class PoisonPillsResponse
        {
            public int Total { get; set; }
            public object Filters { get; set; }
            public List<PoisonPillModel> PoisonPills { get; set; }
            public DateTime Timestamp { get; set; }
        }

        /// <summary>
        /// Clase interna para deserializar la respuesta del endpoint POST /api/sync/poisonpills/changestatus
        /// </summary>
        private class ChangeStatusResponse
        {
            public bool Success { get; set; }
            public string MessageId { get; set; }
            public string NewStatus { get; set; }
            public DateTime Timestamp { get; set; }
        }
    }
}
