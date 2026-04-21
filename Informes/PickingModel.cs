namespace Nesto.Informes
{
    public class PickingModel
    {
        public string Proveedor { get; set; }
        public string Producto { get; set; }
        public string CodigoBarras { get; set; }
        public string Descripcion { get; set; }
        public short? Tamanno { get; set; }
        public string UnidadMedida { get; set; }
        public string Subgrupo { get; set; }
        public int Cantidad { get; set; }
        public int CantidadCajas { get; set; }
        public string Pasillo { get; set; }
        public string Fila { get; set; }
        public string Columna { get; set; }
        public short? Estado { get; set; }
    }
}
