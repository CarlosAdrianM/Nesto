namespace Nesto.Modules.Producto.Models
{
    /// <summary>
    /// NestoAPI#414 / Nesto#456: categoría comercial SECUNDARIA de un producto en la tienda
    /// online. La principal sigue siendo el Grupo/Subgrupo de la ficha y no se toca desde aquí.
    ///
    /// El orden de la lista es la posición: viaja a la web tal cual, así que reordenar importa.
    /// </summary>
    public class CategoriaSecundariaModel
    {
        public string Grupo { get; set; }
        public string DescripcionGrupo { get; set; }
        public string Subgrupo { get; set; }
        public string DescripcionSubgrupo { get; set; }

        /// <summary>
        /// Cómo se ve en la pantalla: "COS/OFE — Ofertas Estética". El código va SIEMPRE delante
        /// porque quien mantiene esto necesita saber de qué grupo cuelga cada categoría, no solo
        /// su descripción (requisito de Nesto#456).
        /// </summary>
        public string Descripcion => $"{Grupo}/{Subgrupo} — {DescripcionSubgrupo}";

        public bool EsLaMisma(string grupo, string subgrupo)
        {
            return string.Equals(Grupo?.Trim(), grupo?.Trim(), System.StringComparison.OrdinalIgnoreCase)
                && string.Equals(Subgrupo?.Trim(), subgrupo?.Trim(), System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
