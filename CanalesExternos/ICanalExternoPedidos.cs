using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Nesto.Models.PedidoVenta;

namespace Nesto.Modulos.CanalesExternos
{
    public interface ICanalExternoPedidos
    {
        Task<ObservableCollection<PedidoCanalExterno>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos);
        PedidoCanalExterno GetPedido(int Id);
    }
}
