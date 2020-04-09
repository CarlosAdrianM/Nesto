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
        //Task<ObservableCollection<PedidoVentaDTO>> GetAllPedidosAsync();
        Task<ObservableCollection<PedidoVentaDTO>> GetAllPedidosAsync(DateTime fechaDesde, int numeroMaxPedidos);
        PedidoVentaDTO GetPedido(int Id);
    }
}
