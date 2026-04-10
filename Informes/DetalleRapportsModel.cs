using System;

namespace Nesto.Informes
{
    public class DetalleRapportsModel
    {
        public string Usuario { get; set; }
        public string Empresa { get; set; }
        public string NombreEmpresa { get; set; }
        public string Cliente { get; set; }
        public string Direccion { get; set; }
        public string Comentarios { get; set; }
        public DateTime? HoraLlamada { get; set; }
        public short? EstadoCliente { get; set; }
        public int? AcumuladoMes { get; set; }
        public string Tipo { get; set; }
        public bool? Pedido { get; set; }
        public string Vendedor { get; set; }
        public string CodigoPostal { get; set; }
        public string Poblacion { get; set; }
        public short? EstadoRapport { get; set; }
    }
}
