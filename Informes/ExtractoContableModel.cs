using System;

namespace Nesto.Informes
{
    public class ExtractoContableModel
    {
        public int Id { get; set; }
        public string Empresa { get; set; }
        public DateTime Fecha { get; set; }
        public string Documento { get; set; }
        public string Concepto { get; set; }
        public decimal Debe { get; set; }
        public decimal Haber { get; set; }
        public decimal Saldo { get; set; }
        public string Delegacion { get; set; }
        public string FormaVenta { get; set; }
    }
}
