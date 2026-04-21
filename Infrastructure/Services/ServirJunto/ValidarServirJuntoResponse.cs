using System.Collections.Generic;

namespace Nesto.Infrastructure.Services.ServirJunto
{
    public class ValidarServirJuntoResponse
    {
        public bool PuedeDesmarcar { get; set; }
        public List<ProductoSinStockDTO> ProductosProblematicos { get; set; }
        public string Mensaje { get; set; }
    }
}
