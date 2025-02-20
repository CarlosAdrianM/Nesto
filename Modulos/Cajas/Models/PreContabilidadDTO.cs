using Newtonsoft.Json;
using System;

namespace Nesto.Modulos.Cajas.Models
{
    public class PreContabilidadDTO
    {
        public string Empresa { get; set; }
        public string TipoApunte { get;set; }
        public string TipoCuenta { get; set; }
        [JsonProperty("Nº_Cuenta")]
        public string Cuenta { get; set; }
        public string Contacto { get; set; }
        public string Concepto { get;set; }
        public decimal Debe { get; set; }
        public decimal Haber { get; set; }
        public decimal Importe => string.IsNullOrEmpty(Contrapartida) ? Debe - Haber : 0;
        public DateOnly Fecha { get; set; }
        public DateOnly FechaVto { get; set; }
        [JsonProperty("Nº_Documento")]
        public string Documento { get;set; }
        public string Efecto { get;set; }
        public int Asiento { get;set; }
        public string Diario { get;set; }
        [JsonProperty("Delegación")]
        public string Delegacion { get;set; }
        public string FormaVenta { get;set; }
        public string Contrapartida { get;set; }
        public string CentroCoste { get; set; }
        public string Origen { get;set; }
        public string Departamento { get; set; }
        [JsonProperty("NºDocumentoProv")]
        public string FacturaProveedor { get; set; }
        public int Liquidado { get; set; }
        public string FormaPago { get; set; }
        public string Ruta { get; set; }
        public string Vendedor { get; set; }
        public string Usuario { get;set; }
        [JsonProperty("Fecha_Modificación")]
        public DateTime FechaModificacion { get;set; }
    }
}
