using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos.Interfaces
{
    /// <summary>
    /// Nesto#434 / NestoAPI#366: respuesta de POST api/CanalesExternos/Amazon/SubirFactura.
    /// </summary>
    public class SubirFacturaAmazonResponse
    {
        public string Empresa { get; set; }
        public int Pedido { get; set; }
        public string NumeroFactura { get; set; }
        public string AmazonOrderId { get; set; }
        public string MarketplaceId { get; set; }
        public string FeedId { get; set; }
        public string Estado { get; set; }
        public List<string> Avisos { get; set; } = new();
    }

    /// <summary>Estado de subida de un pedido (GET api/CanalesExternos/Amazon/FacturasSubidas).</summary>
    public class FacturaSubidaAmazon
    {
        public int Pedido { get; set; }
        public string NumeroFactura { get; set; }
        public string Estado { get; set; }
        public DateTime FechaEnvio { get; set; }
    }

    /// <summary>
    /// Nesto#434: facturar pedidos de Amazon y subir la factura (PDF) a Amazon. Todo el trabajo
    /// real lo hace NestoAPI (#366); Nesto solo llama a la API con JWT.
    /// </summary>
    public interface IFacturasAmazonService
    {
        /// <summary>Factura el pedido si no lo está y sube el PDF a Amazon. Idempotente
        /// (resubir reemplaza la factura en Amazon).</summary>
        Task<SubirFacturaAmazonResponse> FacturarYSubirAsync(string empresa, int pedido);

        /// <summary>Estado de subida de varios pedidos, indexado por número de pedido.</summary>
        Task<Dictionary<int, FacturaSubidaAmazon>> ConsultarSubidasAsync(string empresa, IEnumerable<int> pedidos);
    }
}
