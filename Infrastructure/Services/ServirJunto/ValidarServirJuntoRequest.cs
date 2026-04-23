using System.Collections.Generic;

namespace Nesto.Infrastructure.Services.ServirJunto
{
    public class ValidarServirJuntoRequest
    {
        public string Almacen { get; set; }
        public List<string> ProductosBonificados { get; set; }
        public List<ProductoBonificadoConCantidadRequest> ProductosBonificadosConCantidad { get; set; }
        public List<ProductoBonificadoConCantidadRequest> LineasPedido { get; set; }

        // NestoAPI#187: datos del pedido para que el backend genere el aviso de
        // comisión por contra reembolso si procede al desmarcar Servir Junto.
        public string FormaPago { get; set; }
        public string PlazosPago { get; set; }
        public string CCC { get; set; }
        public string PeriodoFacturacion { get; set; }
        public bool? NotaEntrega { get; set; }
    }
}
