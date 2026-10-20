using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Nesto.Modulos.PedidoCompra.Models
{
    /// <summary>
    /// NestoAPI#668 / Nesto#536: CrearAlbaranYFactura ha devuelto 409 «FechaCompraNoPermitida»: las
    /// compras están cerradas en la fecha de la factura y la API propone la primera fecha permitida.
    /// No se ha creado nada; se puede reenviar con <see cref="CrearFacturaCmpRequest.FechaFactura"/>.
    /// </summary>
    public class FechaCompraNoPermitidaException : Exception
    {
        public const string CODIGO = "FechaCompraNoPermitida";
        private const int HTTP_CONFLICT = 409;

        public FechaCompraNoPermitidaException(DateTime fecha, DateTime fechaPropuesta, string mensaje)
            : base(string.IsNullOrWhiteSpace(mensaje)
                ? $"Las compras están cerradas en la fecha {fecha:dd/MM/yyyy}. La primera fecha permitida es el {fechaPropuesta:dd/MM/yyyy}."
                : mensaje)
        {
            Fecha = fecha;
            FechaPropuesta = fechaPropuesta;
        }

        /// <summary>Fecha con la que se quería contabilizar la factura.</summary>
        public DateTime Fecha { get; }
        /// <summary>Primera fecha de compras permitida para el usuario.</summary>
        public DateTime FechaPropuesta { get; }

        /// <summary>
        /// La excepción si la respuesta es el 409 de fecha de compras con propuesta; si no, null (y se
        /// trata como el error de siempre).
        /// </summary>
        public static FechaCompraNoPermitidaException DesdeRespuesta(int statusCode, string cuerpo)
        {
            if (statusCode != HTTP_CONFLICT || string.IsNullOrWhiteSpace(cuerpo))
            {
                return null;
            }
            try
            {
                JObject json = JsonConvert.DeserializeObject<JObject>(cuerpo);
                if (json == null)
                {
                    return null;
                }
                string codigo = json.GetValue("Codigo", StringComparison.OrdinalIgnoreCase)?.ToString();
                JToken fecha = json.GetValue("Fecha", StringComparison.OrdinalIgnoreCase);
                JToken fechaPropuesta = json.GetValue("FechaPropuesta", StringComparison.OrdinalIgnoreCase);
                if (codigo != CODIGO || fecha == null || fechaPropuesta == null
                    || fecha.Type == JTokenType.Null || fechaPropuesta.Type == JTokenType.Null)
                {
                    return null;
                }
                string mensaje = json.GetValue("Message", StringComparison.OrdinalIgnoreCase)?.ToString();
                return new FechaCompraNoPermitidaException(fecha.ToObject<DateTime>(), fechaPropuesta.ToObject<DateTime>(), mensaje);
            }
            catch (JsonException)
            {
                return null;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
