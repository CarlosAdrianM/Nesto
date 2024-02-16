using Newtonsoft.Json;
using System;
using System.Security.Policy;

namespace Nesto.Modulos.Cajas.Models
{
    public class ContabilidadDTO
    {
        [JsonProperty("Nº_Orden")]
        public int Id { get; set; }
        public string Empresa { get;set; }
        [JsonProperty("Nº_Cuenta")]
        public string Cuenta { get; set; }
        public string Concepto { get; set; }
        public decimal Debe { get; set; }
        public decimal Haber { get; set; }
        public decimal Importe => Debe - Haber;
        public DateTime Fecha { get; set; }
        [JsonProperty("Nº_Documento")]
        public string Documento { get; set; }
        public int Asiento { get; set; }
        public string Diario { get; set; }
        public string Delegacion { get; set; }
        public string FormaVenta { get; set; }
        public string Departamento { get; set; }
        public string CentroCoste { get; set; }
        public EstadoPunteo EstadoPunteo { get; set; }

        public PreContabilidadDTO ToPreContabilidadDTO()
        {
            return new PreContabilidadDTO
            {
                Empresa = Empresa,
                Cuenta = Cuenta,
                Concepto = Concepto,
                Debe = Debe,
                Haber = Haber,
                Fecha = new DateOnly(Fecha.Year, Fecha.Month, Fecha.Day),
                Documento = Documento,
                Asiento = Asiento,
                Diario = Diario,
                Delegacion = Delegacion,
                FormaVenta = FormaVenta,
                Departamento = Departamento,
                CentroCoste = CentroCoste
            };
        }
    }
}
