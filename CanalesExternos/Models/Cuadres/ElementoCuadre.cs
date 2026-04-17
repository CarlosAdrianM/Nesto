using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.Models.Cuadres
{
    /// <summary>
    /// Representa un elemento a conciliar entre Nesto y un canal externo (Amazon, etc.).
    /// Cada lado puede existir o no; si ambos existen se comparan sus importes.
    /// </summary>
    /// <typeparam name="TClave">Tipo de la clave que identifica el elemento en ambos lados
    /// (InvoiceId, OrderId, fecha+referencia de liquidación…).</typeparam>
    public class ElementoCuadre<TClave>
    {
        public TClave Clave { get; set; }

        /// <summary>Descripción humana opcional (p. ej. fecha, concepto).</summary>
        public string Descripcion { get; set; }

        /// <summary><c>true</c> si el elemento existe en Nesto. Puede ser true con ImporteNesto null
        /// cuando el endpoint devuelve presencia pero no importes.</summary>
        public bool ExisteEnNesto { get; set; }

        /// <summary><c>true</c> si el elemento existe en el canal externo.</summary>
        public bool ExisteEnAmazon { get; set; }

        /// <summary>Importe en Nesto. <c>null</c> si no se conoce (ni siquiera aunque exista en Nesto).</summary>
        public decimal? ImporteNesto { get; set; }

        /// <summary>Importe en el canal externo. <c>null</c> si no se conoce.</summary>
        public decimal? ImporteAmazon { get; set; }

        /// <summary>
        /// <c>true</c> si el elemento existe en ambos lados y, si hay importes conocidos, coinciden.
        /// Si algún importe es desconocido, se considera cuadrado por presencia.
        /// </summary>
        public bool Cuadrado => ExisteEnNesto && ExisteEnAmazon
            && (!ImporteNesto.HasValue || !ImporteAmazon.HasValue || ImporteNesto == ImporteAmazon);

        /// <summary>
        /// Diferencia Nesto − Amazon cuando ambos importes se conocen; en otro caso 0.
        /// </summary>
        public decimal Diferencia => (ImporteNesto ?? 0M) - (ImporteAmazon ?? 0M);
    }

    /// <summary>
    /// Resultado de conciliar un tipo de elemento entre Nesto y un canal externo.
    /// Cada elemento cae en exactamente una de las cuatro listas: cuadrados, solo Nesto,
    /// solo Amazon, o con importes distintos.
    /// </summary>
    public class ResultadoCuadre<TClave>
    {
        /// <summary>Nombre del cuadre (p. ej. "Facturas Amazon febrero 2026").</summary>
        public string Nombre { get; set; }

        /// <summary>Existen en ambos lados e importes coinciden.</summary>
        public List<ElementoCuadre<TClave>> Cuadrados { get; } = new List<ElementoCuadre<TClave>>();

        /// <summary>Existe en Nesto pero Amazon no lo reconoce (o viceversa: sobra en Nesto).</summary>
        public List<ElementoCuadre<TClave>> SoloEnNesto { get; } = new List<ElementoCuadre<TClave>>();

        /// <summary>Existe en Amazon pero no lo hemos contabilizado (falta en Nesto).</summary>
        public List<ElementoCuadre<TClave>> SoloEnAmazon { get; } = new List<ElementoCuadre<TClave>>();

        /// <summary>Existen en ambos lados pero los importes no coinciden.</summary>
        public List<ElementoCuadre<TClave>> ImportesDistintos { get; } = new List<ElementoCuadre<TClave>>();

        public int TotalElementos => Cuadrados.Count + SoloEnNesto.Count + SoloEnAmazon.Count + ImportesDistintos.Count;

        /// <summary>Suma de importes en Nesto sobre todos los elementos que aparecen allí.</summary>
        public decimal TotalImporteNesto
        {
            get
            {
                decimal total = 0M;
                foreach (var e in Cuadrados) total += e.ImporteNesto ?? 0M;
                foreach (var e in SoloEnNesto) total += e.ImporteNesto ?? 0M;
                foreach (var e in ImportesDistintos) total += e.ImporteNesto ?? 0M;
                return total;
            }
        }

        /// <summary>Suma de importes en Amazon sobre todos los elementos que aparecen allí.</summary>
        public decimal TotalImporteAmazon
        {
            get
            {
                decimal total = 0M;
                foreach (var e in Cuadrados) total += e.ImporteAmazon ?? 0M;
                foreach (var e in SoloEnAmazon) total += e.ImporteAmazon ?? 0M;
                foreach (var e in ImportesDistintos) total += e.ImporteAmazon ?? 0M;
                return total;
            }
        }

        public bool EstaCuadrado => SoloEnNesto.Count == 0 && SoloEnAmazon.Count == 0 && ImportesDistintos.Count == 0;
    }
}
