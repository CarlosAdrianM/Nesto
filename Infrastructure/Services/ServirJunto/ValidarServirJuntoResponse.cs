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
    }
}
