using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modulos.CanalesExternos
{
    public interface ICanalExternoPedidos
    {
        Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos);
        PedidoCanalExterno GetPedido(int Id);
        bool EjecutarTrasCrearPedido(PedidoCanalExterno pedido);
    }
}
