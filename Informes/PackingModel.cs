namespace Nesto.Informes
{
    public class PackingModel
    {
        public int Número { get; set; }
        public string Ampliacion { get; set; }
        public string Aviso { get; set; }
        public string NºCliente { get; set; }
        public string Contacto { get; set; }
        public string Direccion { get; set; }
        public string CodPostal { get; set; }
        public string Poblacion { get; set; }
        public string Telefono { get; set; }
        public string ComentarioPicking { get; set; }
        public string Usuario { get; set; }
        public string Ruta { get; set; }

        // Líneas
        public string ProveedorProducto { get; set; }
        public string NºProducto { get; set; }
        public string CodBarras { get; set; }
        public string NombreSubGrupo { get; set; }
        public string Descripcion { get; set; }
        public short? Tamaño { get; set; }
        public string UnidadMedida { get; set; }
        public int Cantidad { get; set; }
        public int CantidadCajas { get; set; }
        public short? Estado { get; set; }
        public string Pasillo { get; set; }
        public string Fila { get; set; }
        public string Columna { get; set; }
        public string Tipo { get; set; }
    }
}
