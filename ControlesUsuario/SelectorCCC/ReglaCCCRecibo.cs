using ControlesUsuario.Models;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario
{
    /// <summary>
    /// Nesto#486: qué cuenta se va a cargar cuando la forma de pago es recibo bancario y cuándo hay
    /// que avisar de que el recibo no llegará al banco. Pura y estática para testear sin WPF.
    /// La misma regla (y los mismos textos) están en NestoApp#189.
    ///
    /// Una cuenta vale para el recibo si la ficha NO está de baja (estado &gt;= 0) y su IBAN es
    /// correcto: la API solo rellena ibanFormateado cuando el IBAN compuesto pasa el mod-97.
    /// Es lo mismo que mira la remesa para no retener el efecto
    /// (NestoAPI SelectorEfectosCobrables.MotivoRetencionIban, #381 y #502).
    /// </summary>
    public static class ReglaCCCRecibo
    {
        public const string AVISO_SIN_CUENTA_VALIDA =
            "Este cliente no tiene cuenta bancaria válida: el recibo NO se podrá mandar al banco. " +
            "Pide la cuenta o elige otra forma de pago.";

        public const string AVISO_CUENTA_ELEGIDA_NO_VALIDA =
            "La cuenta elegida no es válida (de baja o con el IBAN mal): el recibo NO se podrá mandar al banco. " +
            "Elige otra cuenta o otra forma de pago.";

        public const string AVISO_SIN_CUENTA_ELEGIDA =
            "No hay ninguna cuenta elegida: el recibo NO se podrá mandar al banco. Elige una cuenta.";

        public static bool EsReciboBancario(string formaPago)
            => string.Equals(formaPago?.Trim(), Constantes.FormasPago.RECIBO, StringComparison.OrdinalIgnoreCase);

        public static bool EsValidaParaRecibo(CCCItem ccc)
            => ccc != null
               && !string.IsNullOrWhiteSpace(ccc.numero)
               && ccc.estado >= 0
               && !string.IsNullOrWhiteSpace(ccc.ibanFormateado);

        /// <summary>
        /// Busca en la lista el CCC con ese número (el del pedido llega de un char relleno con
        /// espacios, el de la API recortado: se compara con Trim, como en #254).
        /// </summary>
        public static CCCItem Buscar(IEnumerable<CCCItem> cccs, string numero)
        {
            if (cccs == null || string.IsNullOrWhiteSpace(numero))
            {
                return null;
            }
            return cccs.FirstOrDefault(c => string.Equals(c?.numero?.Trim(), numero.Trim(), StringComparison.Ordinal));
        }

        /// <summary>
        /// «ES12 …… 4321 — Banco X»: país y dígitos de control, los cuatro últimos y la entidad.
        /// </summary>
        public static string Resumen(CCCItem ccc)
        {
            if (ccc == null)
            {
                return null;
            }
            string iban = (ccc.ibanFormateado ?? string.Empty).Replace(" ", string.Empty);
            string cuenta = iban.Length >= 8
                ? $"{iban.Substring(0, 4)} …… {iban.Substring(iban.Length - 4)}"
                : ccc.numero?.Trim();
            string entidad = !string.IsNullOrWhiteSpace(ccc.nombreEntidad) ? ccc.nombreEntidad.Trim() : ccc.entidad?.Trim();
            return string.IsNullOrWhiteSpace(entidad) ? cuenta : $"{cuenta} — {entidad}";
        }

        /// <summary>
        /// Aviso para enseñar en naranja/rojo, o null si no hay nada que avisar. No bloquea
        /// guardar (decisión de Carlos, 24/09/26): solo avisa.
        /// </summary>
        public static string Aviso(string formaPago, IEnumerable<CCCItem> cccs, string cccSeleccionado)
        {
            if (!EsReciboBancario(formaPago))
            {
                return null;
            }
            List<CCCItem> lista = cccs?.Where(c => c != null && !string.IsNullOrWhiteSpace(c.numero)).ToList()
                ?? new List<CCCItem>();
            if (!lista.Any(EsValidaParaRecibo))
            {
                return AVISO_SIN_CUENTA_VALIDA;
            }
            if (string.IsNullOrWhiteSpace(cccSeleccionado))
            {
                return AVISO_SIN_CUENTA_ELEGIDA;
            }
            return EsValidaParaRecibo(Buscar(lista, cccSeleccionado)) ? null : AVISO_CUENTA_ELEGIDA_NO_VALIDA;
        }

        /// <summary>
        /// Texto «Se cargará en: …» de la cuenta elegida, solo con recibo y cuenta válida.
        /// </summary>
        public static string TextoCuentaACargar(string formaPago, IEnumerable<CCCItem> cccs, string cccSeleccionado)
        {
            if (!EsReciboBancario(formaPago))
            {
                return null;
            }
            CCCItem elegida = Buscar(cccs, cccSeleccionado);
            return EsValidaParaRecibo(elegida) ? $"Se cargará en: {Resumen(elegida)}" : null;
        }
    }
}
