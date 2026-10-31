using System;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// NestoAPI#771: una foto del packing de Ariadna SIN caja (espejo de FotoPackingAlmacenDTO de NestoAPI,
    /// GET api/Almacen/Pedidos/{pedido}/FotosPacking). Desde el 31/10/26 las fotos ya no van ligadas a los bultos: mínimo
    /// una por entrega y las que quieran. Las de antes (una por bulto) siguen en <see cref="BultoAriadna"/>.
    /// </summary>
    public class FotoPackingAriadna
    {
        public int Id { get; set; }
        /// <summary>La foto en sí: la misma en las filas de los pedidos de una entrega.</summary>
        public Guid IdFoto { get; set; }
        public int Pedido { get; set; }
        public int Picking { get; set; }
        public string Usuario { get; set; }
        public DateTime? FechaFoto { get; set; }
        /// <summary>La ruta pública de la foto dentro de la API (api/Almacen/Fotos/p{id}-…), sin el servidor. Null si no hay clave.</summary>
        public string RutaFotoPublica { get; set; }
    }
}
