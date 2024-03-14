using Newtonsoft.Json;


namespace Nesto.Modulos.Cajas.Models
{
    public class ExtractoProveedorDTO
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public string Proveedor { get; set; }
        public string Contacto { get; set; }
        public string Documento { get; set; }
        public string DocumentoProveedor { get; set; }
        public string Delegacion { get; set; }
        public string FormaVenta { get; set; }
    }
}
