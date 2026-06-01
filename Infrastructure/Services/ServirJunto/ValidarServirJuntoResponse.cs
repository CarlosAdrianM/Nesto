using System.Collections.Generic;

namespace Nesto.Infrastructure.Services.ServirJunto
{
    public class ValidarServirJuntoResponse
    {
        public bool PuedeDesmarcar { get; set; }
        public List<ProductoSinStockDTO> ProductosProblematicos { get; set; }
        public string Mensaje { get; set; }

        // NestoAPI#187: aviso no-bloqueante (p. ej. comisión contra reembolso por cada
        // envío al desmarcar servirJunto). Null si no hay nada que avisar.
        public string Aviso { get; set; }

        // NestoAPI#211 / Nesto#365: base de portes que quedaría al desmarcar "servir junto"
        // (excluidas las líneas sobre pedido). El cliente la compara con el umbral cacheado.
        public decimal? BaseImponibleSinServirJunto { get; set; }
    }
}
