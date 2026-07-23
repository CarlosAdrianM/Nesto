namespace ControlesUsuario.Models
{
    /// <summary>
    /// Nesto#428 / NestoAPI#355: país del combo del SelectorPais (espejo del PaisDTO de
    /// GET api/Paises).
    /// </summary>
    public class PaisItem
    {
        public string Codigo { get; set; }
        public string Nombre { get; set; }
        public bool UnionEuropea { get; set; }
    }
}
