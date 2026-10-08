using System;
using System.Collections.Generic;

namespace ControlesUsuario.Models
{
    /// <summary>
    /// NestoAPI#609: respuesta de <c>POST api/Pagos/RevisarConcepto</c>: el concepto de un enlace de pago tal
    /// cual (<see cref="Original"/>) y la corrección que propone la API (ortografía, tildes, nombres propios).
    /// </summary>
    public class RevisionConcepto
    {
        public string Original { get; set; }
        public string Propuesto { get; set; }
        public bool HayCambios { get; set; }
        public List<CambioConcepto> Cambios { get; set; } = new List<CambioConcepto>();

        /// <summary>
        /// Trocea <paramref name="propuesto"/> marcando lo que cambió (cada <see cref="CambioConcepto.A"/>, en
        /// orden y sin solaparse) para pintarlo resaltado. Lo que no se encuentra no se marca.
        /// </summary>
        public static List<TramoConcepto> Trocear(string propuesto, IEnumerable<CambioConcepto> cambios)
        {
            var tramos = new List<TramoConcepto>();
            if (string.IsNullOrEmpty(propuesto))
            {
                return tramos;
            }
            int posicion = 0;
            foreach (CambioConcepto cambio in cambios ?? new List<CambioConcepto>())
            {
                if (string.IsNullOrEmpty(cambio?.A))
                {
                    continue;
                }
                int indice = propuesto.IndexOf(cambio.A, posicion, StringComparison.Ordinal);
                if (indice < 0)
                {
                    continue;
                }
                if (indice > posicion)
                {
                    tramos.Add(new TramoConcepto(propuesto.Substring(posicion, indice - posicion), false));
                }
                tramos.Add(new TramoConcepto(cambio.A, true));
                posicion = indice + cambio.A.Length;
            }
            if (posicion < propuesto.Length)
            {
                tramos.Add(new TramoConcepto(propuesto.Substring(posicion), false));
            }
            return tramos;
        }
    }

    /// <summary>NestoAPI#609: una corrección del concepto («micronileng» → «Microneedling»).</summary>
    public class CambioConcepto
    {
        public string De { get; set; }
        public string A { get; set; }
    }

    /// <summary>NestoAPI#609: un trozo del concepto propuesto; <see cref="EsCambio"/> si es una corrección.</summary>
    public class TramoConcepto
    {
        public TramoConcepto(string texto, bool esCambio)
        {
            Texto = texto;
            EsCambio = esCambio;
        }

        public string Texto { get; }
        public bool EsCambio { get; }
    }
}
