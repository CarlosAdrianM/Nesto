using System;

namespace Nesto.Modulos.Cajas.Models
{
    public class MovimientoTPV
    {
        public string ModoCaptura { get; set; }
        public string TextoModoCaptura { get; set; }
        public string Sesion { get; set; }
        public string Terminal { get; set; }
        public DateTime FechaCaptura { get; set; }
        public DateTime FechaOperacion { get; set; }
        public decimal ImporteOperacion { get; set; }
        public decimal ImporteComision { get; set; }
        public decimal ImporteAbono { get; set; }
        public string CodigoMoneda { get; set; }
        public string Comentarios { get; set; }
        public string Usuario { get; set; }
        public DateTime FechaCreacion { get; set; }
    }
}
