using System.Collections.Generic;

namespace Nesto.Modulos.CanalesExternos.Models
{
    public class CuadreLiquidacionCanalExterno
    {
        public int Año { get; set; }
        public int Mes { get; set; }
        public decimal TotalFacturasContabilizadas { get; set; }
        public decimal TotalComisionesLiquidaciones { get; set; }
        public decimal Diferencia => TotalFacturasContabilizadas - TotalComisionesLiquidaciones;
        public List<CuadreLiquidacionDetalle> Detalle { get; set; } = new List<CuadreLiquidacionDetalle>();
    }

    public class CuadreLiquidacionDetalle
    {
        public string MarketplaceId { get; set; }
        public string NombreMarket { get; set; }
        public decimal TotalFacturas { get; set; }
        public decimal TotalLiquidaciones { get; set; }
        public decimal Diferencia => TotalFacturas - TotalLiquidaciones;
    }
}
