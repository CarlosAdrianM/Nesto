namespace Nesto.Modules.Producto.Models
{
    /// <summary>
    /// Nesto#456: un subgrupo del catálogo, con su grupo. Es lo que se elige al añadir una
    /// categoría secundaria, y por eso se ve con el código delante: "COS/OFE — Ofertas Estética".
    /// </summary>
    public class SubgrupoProductoModel
    {
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public string Nombre { get; set; }

        public string Descripcion => $"{Grupo}/{Subgrupo} — {Nombre}";
    }
}
