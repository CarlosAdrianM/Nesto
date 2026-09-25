using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#541: la API no guarda la ficha porque el cambio de días cierra un día y el contacto tiene pedidos
    /// con picking. Código DIAS_CON_PICKING; el mensaje ya pregunta «¿Avisamos a almacén?». Si el usuario acepta,
    /// se repite el PUT con ConfirmarDiasEnServirConPicking; si no, el cambio no se guarda.
    /// </summary>
    public class DiasEnServirConPickingException : Exception
    {
        public const string CODIGO_ERROR = "DIAS_CON_PICKING";
        public const string TITULO = "Avisar a almacén";

        public IReadOnlyList<int> Pedidos { get; }

        public DiasEnServirConPickingException(string mensaje, IReadOnlyList<int> pedidos) : base(mensaje)
        {
            Pedidos = pedidos ?? Array.Empty<int>();
        }

        /// <summary>Si el error de la API es DIAS_CON_PICKING, la excepción con su mensaje legible; si no, null.</summary>
        public static DiasEnServirConPickingException DesdeRespuesta(JObject detallesError, string mensajeLegible)
        {
            JObject errorObj = detallesError?["error"] as JObject;
            if (errorObj?["code"]?.ToString() != CODIGO_ERROR)
            {
                return null;
            }
            string mensaje = errorObj["message"]?.ToString();
            List<int> pedidos = (errorObj["details"]?["pedidos"] as JArray)?.Select(p => (int)p).ToList() ?? new List<int>();
            return new DiasEnServirConPickingException(string.IsNullOrWhiteSpace(mensaje) ? mensajeLegible : mensaje, pedidos);
        }
    }
}
