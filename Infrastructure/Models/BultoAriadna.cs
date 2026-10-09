using System;

namespace Nesto.Infrastructure.Models
{
    /// <summary>
    /// Nesto#507: un bulto que el mozo ha hecho en el packing de Ariadna (espejo de BultoAlmacenDTO de NestoAPI,
    /// GET api/Almacen/Pedidos/{pedido}/Bultos). Un bulto compartido por varios pedidos de la misma entrega sale en
    /// todos con el mismo Id.
    /// </summary>
    public class BultoAriadna
    {
        public int Id { get; set; }
        public int Pedido { get; set; }
        public int Picking { get; set; }
        public int Bulto { get; set; }
        public decimal? Peso { get; set; }
        public bool TieneFoto { get; set; }
        public int? NumeroEnvio { get; set; }
        public string Usuario { get; set; }
        public DateTime? FechaFoto { get; set; }
        /// <summary>
        /// Nesto#522: la ruta pública de la foto dentro de la API, sin el servidor (api/Almacen/Fotos/{token}), para
        /// mandársela al cliente o a la agencia: no pide usuario. Null si no hay foto (o la API es anterior).
        /// </summary>
        public string RutaFotoPublica { get; set; }
    }
}
