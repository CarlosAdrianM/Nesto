using Nesto.Modulos.CanalesExternos.Models;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos
{
    public interface ICanalExternoPedidos
    {
        Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos);
        PedidoCanalExterno GetPedido(int Id);
        Task<bool> EjecutarTrasCrearPedido(PedidoCanalExterno pedido);
        Task<string> ConfirmarPedido(PedidoCanalExterno pedido);
    }
}
