using System;

namespace Nesto.Modulos.Cajas.Models
{
    public class ExtractoClienteDTO
    {
        public int Id { get; set; }
        public string Empresa { get;set; }
        public string Cliente { get;set; }
        public string Contacto { get;set; }
        public string Tipo { get;set; }
        public DateTime Fecha { get;set; }
        public string Documento { get;set; }
        public string Efecto { get;set; }
        public string Concepto { get;set; }
        public decimal Importe { get;set; }
        public decimal ImportePendiente { get;set; }
        public DateTime Vencimiento { get;set; }
        public string Delegacion { get;set; }
        public string FormaVenta { get;set; }
        public string CCC { get;set; }
        public string Estado { get;set; }
        public string Vendedor { get; set; }
        public string Ruta { get; set; }
        public string Usuario { get;set; }
    }
}
