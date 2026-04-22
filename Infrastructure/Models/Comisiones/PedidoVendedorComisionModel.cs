using System;

namespace Nesto.Infrastructure.Models.Comisiones
{
    public class PedidoVendedorComisionModel
    {
        public int NumeroOrden { get; set; }
        public string Vendedor { get; set; }
        public string Ruta { get; set; }
        public string Nombre { get; set; }
        public string Direccion { get; set; }
        public string CodPostal { get; set; }
        public string Poblacion { get; set; }
        public int Numero { get; set; }
        public string Producto { get; set; }
        public string Texto { get; set; }
        public string Familia { get; set; }
        public DateTime FechaEntrega { get; set; }
        public short? Cantidad { get; set; }
        public short Estado { get; set; }
        public int? Picking { get; set; }
        public string Empresa { get; set; }
        public string NumeroCliente { get; set; }
        public string Contacto { get; set; }
        public decimal BaseImponible { get; set; }
    }
}
