namespace ControlesUsuario.Models
{
    /// <summary>
    /// Nesto#425: banco del combo del SelectorBanco (espejo del BancoSelectorDTO de
    /// GET api/Bancos/Selector).
    /// </summary>
    public class BancoItem
    {
        public string Numero { get; set; }
        public string Nombre { get; set; }
        public string Entidad { get; set; }
        public string Sucursal { get; set; }
    }
}
