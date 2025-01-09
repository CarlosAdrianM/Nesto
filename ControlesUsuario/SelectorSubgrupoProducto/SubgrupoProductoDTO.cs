namespace ControlesUsuario
{
    public class SubgrupoProductoDTO
    {
        public string Grupo { get; set; }
        public string Subgrupo { get; set; }
        public string Nombre { get; set; }
        public string GrupoSubgrupo => Grupo + Subgrupo;
    }
}
