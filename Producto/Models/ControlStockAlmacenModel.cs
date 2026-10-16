using System.Security.Policy;

namespace Nesto.Modules.Producto.Models
{
    public class ControlStockAlmacenModel
    {
        public string Almacen {  get; set; }
        public string Categoria { get; set; }
        public string Estacionalidad { get; set; }
        public int Multiplos { get; set; }
        public int StockMaximoActual { get; set; }
        public int StockMaximoCalculado { get; set; }
        public int StockMaximoInicial { get; set; }
        public bool YaExiste { get; set; }

        // NestoAPI#628 (ficha = Excel): cómo ha calculado la API lo sugerido. Una API vieja no los manda (null/false): sin tooltip.
        /// <summary>La frase que explica el máximo sugerido (en ALG, también el mínimo).</summary>
        public string Explicacion { get; set; }
        /// <summary>La fórmula con la que sale (la columna «Fórmula» del Excel en ALG; la media de 24 meses en las tiendas).</summary>
        public string Formula { get; set; }
        /// <summary>En ALG, la acción que propone el Excel de propuestas de stock; null en las tiendas.</summary>
        public string Accion { get; set; }
        /// <summary>True si lo sugerido es lo mismo que el Excel de propuestas de stock (motor de la API).</summary>
        public bool CalculadoComoExcel { get; set; }

        /// <summary>
        /// NestoAPI#628: el tooltip de lo sugerido (máximo y, en ALG, mínimo): de dónde sale, la fórmula, la acción y la
        /// explicación. Null si la API no manda explicación (API vieja): la vista no enseña tooltip.
        /// </summary>
        public string TooltipSugerido
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Explicacion))
                {
                    return null;
                }
                var lineas = new System.Collections.Generic.List<string>();
                if (CalculadoComoExcel)
                {
                    lineas.Add("Calculado como en el Excel de propuestas de stock.");
                }
                if (!string.IsNullOrWhiteSpace(Formula))
                {
                    lineas.Add("Fórmula: " + Formula.Trim());
                }
                if (!string.IsNullOrWhiteSpace(Accion))
                {
                    lineas.Add("Acción propuesta: " + Accion.Trim());
                }
                lineas.Add(Explicacion.Trim());
                return string.Join("\n", lineas);
            }
        }
    }
}
