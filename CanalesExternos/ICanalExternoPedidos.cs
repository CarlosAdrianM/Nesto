using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nesto.Models.PedidoVenta;

namespace Nesto.Modulos.CanalesExternos
{
    public interface ICanalExternoPedidos
    {
        List<PedidoVentaDTO> GetAllPedidos();
        PedidoVentaDTO GetPedido(int Id);
    }
}
