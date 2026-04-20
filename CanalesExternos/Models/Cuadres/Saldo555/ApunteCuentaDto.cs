using System;

namespace Nesto.Modulos.CanalesExternos.Models.Cuadres.Saldo555
{
    /// <summary>
    /// Deserialización de un apunte contable dentro de la respuesta de
    /// <c>GET api/Informes/SaldoCuenta555</c>.
    /// </summary>
    public class ApunteCuentaDto
    {
        public long NumeroOrden { get; set; }
        public DateTime Fecha { get; set; }
        public string Concepto { get; set; }
        public decimal Debe { get; set; }
        public decimal Haber { get; set; }
        public string NumeroDocumento { get; set; }
        public string Diario { get; set; }
        public int TipoApunte { get; set; }

        public decimal ImporteNeto => Debe - Haber;
    }
}
