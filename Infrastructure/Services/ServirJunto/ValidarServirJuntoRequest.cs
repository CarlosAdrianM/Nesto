using System.Collections.Generic;

namespace Nesto.Infrastructure.Services.ServirJunto
{
    public class ValidarServirJuntoRequest
    {
        public string Almacen { get; set; }

        // NestoAPI#262: número del pedido que se valida, para que el backend excluya sus propias líneas
        // del cálculo de stock disponible (si no, una línea del pedido cuenta su reserva contra sí misma
        // y se deniega aunque haya stock libre). 0/null en pedidos nuevos sin guardar.
        public int? Pedido { get; set; }

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

        // NestoAPI#211 / Nesto#365: líneas de producto para que el backend calcule la base de portes
        // que quedaría al desmarcar "servir junto".
        public List<LineaPortesServirJuntoDTO> LineasParaPortes { get; set; }
    }
}
