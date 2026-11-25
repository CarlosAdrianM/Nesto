using Newtonsoft.Json.Linq;
using System;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#908: la API no guarda una lista de asistencia sin nadie marcado (con inscritas pagadas, que perderían el
    /// cupón) sin un «confirmo» expreso. 409 con código LISTA_SIN_ASISTENTES; el mensaje ya dice qué pasa y qué hacer
    /// («No ha marcado a nadie: las 3 inscritas pagadas pasarán a «No asistió»… vuelva a guardar la lista confirmándolo.»).
    /// Si el usuario dice que sí, se repite el POST con Confirmar = true. Patrón <see cref="DiasEnServirConPickingException"/>.
    /// </summary>
    public class ListaSinAsistentesException : Exception
    {
        public const string CODIGO_ERROR = "LISTA_SIN_ASISTENTES";
        /// <summary>El 400 de un evento online (se enseña con su mensaje, como cualquier otro error).</summary>
        public const string CODIGO_EVENTO_ONLINE = "LISTA_EVENTO_ONLINE";

        /// <summary>details.perderanElCupon (null si no viene).</summary>
        public int? PerderanElCupon { get; }

        public ListaSinAsistentesException(string mensaje, int? perderanElCupon) : base(mensaje)
        {
            PerderanElCupon = perderanElCupon;
        }

        /// <summary>Si el error de la API es LISTA_SIN_ASISTENTES, la excepción con su mensaje; si no (u otro cuerpo), null.</summary>
        public static ListaSinAsistentesException DesdeRespuesta(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return null;
            }
            JObject json;
            try
            {
                json = JObject.Parse(body);
            }
            catch
            {
                return null;
            }
            JObject errorObj = json["error"] as JObject;
            string codigo = errorObj?["code"]?.ToString() ?? json["Codigo"]?.ToString();
            if (codigo != CODIGO_ERROR)
            {
                return null;
            }
            string mensaje = errorObj?["message"]?.ToString() ?? json["Message"]?.ToString();
            int? perderan = null;
            JToken valor = errorObj?["details"]?["perderanElCupon"];
            if (valor != null && int.TryParse(valor.ToString(), out int n))
            {
                perderan = n;
            }
            return new ListaSinAsistentesException(
                string.IsNullOrWhiteSpace(mensaje)
                    ? "No ha marcado a nadie: las inscritas pagadas pasarán a «No asistió» y perderán el cupón. ¿Desea guardar la lista?"
                    : mensaje.Trim(),
                perderan);
        }
    }
}
