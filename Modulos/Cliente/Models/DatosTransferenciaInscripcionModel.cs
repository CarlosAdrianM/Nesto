using System.Collections.Generic;
using System.Globalization;

namespace Nesto.Modulos.Cliente.Models
{
    /// <summary>
    /// Nesto#649 (gemela de NestoAPI#887): GET api/Eventos/Inscripciones/{id}/DatosTransferencia, lo mismo que los datos de
    /// transferencia de un pedido de prepago (sugerencia 396): IBAN, beneficiario, concepto (número de cliente + INS{id}) e
    /// importe de la señal, y el texto listo para pegar en WhatsApp o en un correo. Solo para inscripciones reservadas de pago.
    /// </summary>
    public class DatosTransferenciaInscripcionModel
    {
        // Contrato real de NestoAPI#887 (DatosTransferenciaInscripcionDTO): InscripcionId (no «Inscripcion»), EventoId, Evento, Empresa,
        // Cliente, Contacto, Iban, Titular, Concepto, Importe, Texto y Recordatorio.
        public int InscripcionId { get; set; }
        public int EventoId { get; set; }
        public string Evento { get; set; }
        public string Empresa { get; set; }
        public string Cliente { get; set; }
        public string Contacto { get; set; }
        public string Iban { get; set; }
        public string Titular { get; set; }
        public string Concepto { get; set; }
        public decimal Importe { get; set; }
        /// <summary>Los datos listos para pegar, una línea por dato (lo decide la API).</summary>
        public string Texto { get; set; }
        /// <summary>
        /// Para quien copia, NO para el cliente («Cuando llegue la transferencia, Administración apunta el cobro en Cajas › Cobros
        /// (documento INS123).»): se enseña en el aviso y nunca se copia.
        /// </summary>
        public string Recordatorio { get; set; }

        public const string ERROR_COPIAR = "Windows no ha dejado copiar los datos de transferencia (puede que otro programa esté usando el portapapeles). Vuelva a intentarlo.";
        public const string SIN_DATOS = "No se han podido leer los datos de transferencia de la inscripción.";
        public const string API_SIN_DATOS = "El servidor todavía no da los datos de transferencia de las inscripciones. Avise a Informática.";

        /// <summary>
        /// Lo que se copia: el Texto de la API; si no lo trae, los datos uno por línea. Null si no hay nada que copiar (ni IBAN).
        /// </summary>
        public static string TextoParaCopiar(DatosTransferenciaInscripcionModel datos)
        {
            if (datos == null)
            {
                return null;
            }
            if (!string.IsNullOrWhiteSpace(datos.Texto))
            {
                return datos.Texto.Trim();
            }
            if (string.IsNullOrWhiteSpace(datos.Iban))
            {
                return null;
            }
            var lineas = new List<string>();
            if (!string.IsNullOrWhiteSpace(datos.Titular))
            {
                lineas.Add($"Beneficiario: {datos.Titular.Trim()}");
            }
            lineas.Add($"IBAN: {datos.Iban.Trim()}");
            if (!string.IsNullOrWhiteSpace(datos.Concepto))
            {
                lineas.Add($"Concepto: {datos.Concepto.Trim()}");
            }
            if (datos.Importe > 0)
            {
                lineas.Add($"Importe: {datos.Importe.ToString("N2", CultureInfo.GetCultureInfo("es-ES"))} €");
            }
            return string.Join("\n", lineas);
        }

        /// <summary>El aviso tras copiar y, detrás, el recordatorio de la API para quien copia (si lo trae).</summary>
        public static string TextoCopiados(string nombre, string recordatorio = null)
        {
            string texto = string.IsNullOrWhiteSpace(nombre)
                ? "Datos de transferencia copiados: péguelos en WhatsApp o en el correo."
                : $"Datos de transferencia de {nombre.Trim()} copiados: péguelos en WhatsApp o en el correo.";
            return string.IsNullOrWhiteSpace(recordatorio) ? texto : texto + " " + recordatorio.Trim();
        }
    }
}
