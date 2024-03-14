using Nesto.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos
{
    public interface ICanalExternoPedidos
    {
        Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos);
        Task<PedidoCanalExterno> GetPedido(string Id);
        Task<ICollection<LineaPedidoVentaDTO>> GetLineas(PedidoCanalExterno pedido);
        Task<bool> EjecutarTrasCrearPedido(PedidoCanalExterno pedido);
        Task<string> ConfirmarPedido(PedidoCanalExterno pedido);
    }
}
