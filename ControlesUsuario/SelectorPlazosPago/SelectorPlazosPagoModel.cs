namespace ControlesUsuario.Models
{
    public class PlazosPago
    {
        public string plazoPago { get; set; }
        public string descripcion { get; set; }
        public short numeroPlazos { get; set; }
        public short diasPrimerPlazo { get; set; }
        public short diasEntrePlazos { get; set; }
        public short mesesPrimerPlazo { get; set; }
        public short mesesEntrePlazos { get; set; }
        public decimal descuentoPP { get; set; }
        public decimal? financiacion { get; set; }
    }

    public class PlazosPagoResponse
    {
        public System.Collections.Generic.List<PlazosPago> PlazosPago { get; set; }
        public InfoDeudaCliente InfoDeuda { get; set; }
    }

    public class InfoDeudaCliente
    {
        public bool TieneDeudaVencida { get; set; }
        public decimal? ImporteDeudaVencida { get; set; }
        public int? DiasVencimiento { get; set; }
        public bool TieneImpagados { get; set; }
        public decimal? ImporteImpagados { get; set; }
        public string MotivoRestriccion { get; set; }
    }
}
