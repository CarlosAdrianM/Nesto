using Newtonsoft.Json.Linq;
using System;

namespace Nesto.Infrastructure.Shared
{
    /// <summary>
    /// Helper para parsear errores HTTP del API
    /// </summary>
    /// <remarks>
    /// Este helper parsea el formato de error devuelto por GlobalExceptionFilter en NestoAPI.
    ///
    /// FORMATO NUEVO (desde 2025-01-19):
    /// {
    ///   "error": {
    ///     "code": "FACTURACION_IVA_FALTANTE",
    ///     "message": "El pedido 12345 no se puede facturar...",
    ///     "details": {...},
    ///     "timestamp": "2025-01-19T10:30:00Z"
    ///   }
    /// }
    ///
    /// FORMATO ANTIGUO (fallback):
    /// {
    ///   "ExceptionMessage": "...",
    ///   "InnerException": {...}
    /// }
    ///
    /// Uso desde VB:
    /// <code>
    /// Dim respuestaError = response.Content.ReadAsStringAsync().Result
    /// Dim detallesError As JObject = JsonConvert.DeserializeObject(Of Object)(respuestaError)
    /// Dim contenido As String = HttpErrorHelper.ParsearErrorHttp(detallesError)
    /// Throw New Exception(contenido)
    /// </code>
    /// </remarks>
    public static class HttpErrorHelper
    {
        /// <summary>
        /// Parsea un JObject de error HTTP y devuelve un mensaje legible.
        /// Soporta tanto el nuevo formato (GlobalExceptionFilter) como el antiguo (fallback).
        /// </summary>
        /// <param name="detallesError">JObject con el JSON de error deserializado</param>
        /// <returns>Mensaje de error formateado y legible</returns>
        public static string ParsearErrorHttp(JObject detallesError)
        {
            if (detallesError == null)
            {
                return "Error desconocido al comunicarse con el servidor";
            }

            string contenido = "";

            // Intentar leer el NUEVO formato de errores (desde GlobalExceptionFilter)
            if (detallesError["error"] != null)
            {
                // Nuevo formato: { "error": { "code": "...", "message": "..." } }
                var errorObj = detallesError["error"] as JObject;
                if (errorObj != null)
                {
                    contenido = errorObj["message"]?.ToString() ?? "";

                    // Opcionalmente agregar código de error si existe y no es genérico
                    var errorCode = errorObj["code"]?.ToString();
                    if (!string.IsNullOrEmpty(errorCode) && errorCode != "INTERNAL_ERROR")
                    {
                        contenido = $"[{errorCode}] {contenido}";
                    }
                }
            }
            // Fallback al FORMATO ANTIGUO: { "ExceptionMessage": "...", "InnerException": {...} }
            else if (detallesError["ExceptionMessage"] != null)
            {
                contenido = detallesError["ExceptionMessage"]?.ToString() ?? "";

                // Recorrer inner exceptions
                var currentError = detallesError;
                while (currentError["InnerException"] != null)
                {
                    currentError = currentError["InnerException"] as JObject;
                    if (currentError != null)
                    {
                        var contenido2 = currentError["ExceptionMessage"]?.ToString();
                        if (!string.IsNullOrEmpty(contenido2))
                        {
                            contenido = contenido + Environment.NewLine + contenido2;
                        }
                    }
                }
            }
            // Fallback final: intentar leer el formato con minúscula inicial (algunos endpoints legacy)
            else if (detallesError["exceptionMessage"] != null)
            {
                contenido = detallesError["exceptionMessage"]?.ToString() ?? "";

                var currentError = detallesError;
                while (currentError["innerException"] != null)
                {
                    currentError = currentError["innerException"] as JObject;
                    if (currentError != null)
                    {
                        var contenido2 = currentError["exceptionMessage"]?.ToString();
                        if (!string.IsNullOrEmpty(contenido2))
                        {
                            contenido = contenido + Environment.NewLine + contenido2;
                        }
                    }
                }
            }
            else
            {
                // Fallback: usar el JSON raw como string
                contenido = detallesError.ToString();
            }

            return contenido;
        }

        /// <summary>
        /// Parsea un string JSON de error HTTP directamente y devuelve un mensaje legible.
        /// </summary>
        /// <param name="respuestaError">String con el JSON de error</param>
        /// <returns>Mensaje de error formateado y legible</returns>
        public static string ParsearErrorHttp(string respuestaError)
        {
            if (string.IsNullOrEmpty(respuestaError))
            {
                return "Error desconocido al comunicarse con el servidor";
            }

            try
            {
                var detallesError = JObject.Parse(respuestaError);
                return ParsearErrorHttp(detallesError);
            }
            catch
            {
                // Si no se puede parsear como JSON, devolver el string tal cual
                return respuestaError;
            }
        }
    }
}
