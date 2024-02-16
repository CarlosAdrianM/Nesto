using Nesto.Modulos.PedidoCompra.Models;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace Nesto.Modulos.PedidoCompra
{
    public interface IPedidoCompraService
    {
        Task<PedidoCompraDTO> CargarPedido(string empresa, int pedido);
        Task<ObservableCollection<PedidoCompraLookup>> CargarPedidos();
        Task<List<PedidoCompraDTO>> CargarPedidosAutomaticos(string empresa);
        Task<LineaPedidoCompraDTO> LeerProducto(string empresa, string producto, string proveedor, string ivaCabecera);
        Task<PedidoCompraDTO> AmpliarHastaStockMaximo(PedidoCompraDTO model);
        Task<int> CrearPedido(PedidoCompraDTO pedido);
        Task ModificarPedido(PedidoCompraDTO pedido);
        Task<CrearFacturaCmpResponse> CrearAlbaranYFactura(CrearFacturaCmpRequest request);
    }
}
