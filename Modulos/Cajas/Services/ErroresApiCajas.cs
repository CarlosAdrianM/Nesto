using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cajas.Services
{
    /// <summary>
    /// Error que ya lleva el mensaje de la API listo para enseñar al usuario. Los <c>catch</c> de los servicios de Cajas que
    /// envolvían cualquier excepción en un texto fijo lo dejan pasar tal cual, para no volver a esconderlo.
    /// </summary>
    public class ErrorApiCajasException : Exception
    {
        public ErrorApiCajasException(string mensaje) : base(mensaje)
        {
        }
    }

    /// <summary>
    /// El mensaje de error de los servicios de Cajas: el texto de contexto de cada operación («No se ha podido contabilizar»…)
    /// seguido de lo que diga la API.
    /// <para>
    /// Caso real del 13/11/26: la contabilidad estuvo bloqueada 10 minutos por la operación abierta de otro usuario. La API lo
    /// detecta y lo dice en su mensaje («Puede estar bloqueado por: … Si persiste, avisa a ese usuario…»), pero Cajas montaba el
    /// error a mano leyendo solo el formato antiguo, o tiraba un texto fijo, y quien contabilizaba una transferencia no vio quién
    /// la tenía parada.
    /// </para>
    /// </summary>
    public static class ErroresApiCajas
    {
        /// <summary>El texto de contexto y, debajo, el detalle de la API. Sin detalle legible, solo el contexto (como antes).</summary>
        public static string Mensaje(string contexto, string cuerpoRespuesta)
        {
            contexto = (contexto ?? string.Empty).Trim();
            string detalle = Detalle(cuerpoRespuesta);
            if (string.IsNullOrWhiteSpace(detalle))
            {
                return contexto;
            }
            return contexto.Length == 0 ? detalle : contexto + Environment.NewLine + detalle;
        }

        /// <summary>
        /// Lo que la API dice del error, o null si la respuesta viene vacía, no es JSON o no trae ningún mensaje conocido.
        /// Entiende el formato actual (<c>{ "error": { "message" } }</c>, vía <see cref="HttpErrorHelper"/>), los antiguos
        /// (<c>ExceptionMessage</c> / <c>exceptionMessage</c> con sus <c>InnerException</c>, <c>Message</c>) y <c>ModelState</c>.
        /// </summary>
        internal static string Detalle(string cuerpoRespuesta)
        {
            if (string.IsNullOrWhiteSpace(cuerpoRespuesta))
            {
                return null;
            }
            JObject json;
            try
            {
                json = JsonConvert.DeserializeObject<JObject>(cuerpoRespuesta);
            }
            catch (Exception)
            {
                return null; // No es un objeto JSON (HTML de un 502, texto suelto…): no se enseña
            }
            if (json == null)
            {
                return null;
            }

            var partes = new List<string>();
            bool formatoConocido = json["error"] is JObject || json["ExceptionMessage"] != null
                || json["exceptionMessage"] != null || json["Message"] != null;
            if (formatoConocido)
            {
                partes.Add(HttpErrorHelper.ParsearErrorHttp(json));
            }
            else if (json["message"] != null)
            {
                partes.Add(json["message"].ToString());
            }

            // Errores de validación del modelo: el último, como hacía Cajas hasta ahora
            if (json["ModelState"] is JObject modelState)
            {
                JToken ultimo = modelState.Properties().LastOrDefault()?.Value;
                string texto = ultimo is JArray lista ? lista.FirstOrDefault()?.ToString() : ultimo?.ToString();
                partes.Add(texto);
            }

            List<string> conTexto = partes.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()).Distinct().ToList();
            return conTexto.Count == 0 ? null : string.Join(Environment.NewLine, conTexto);
        }
    }
}
