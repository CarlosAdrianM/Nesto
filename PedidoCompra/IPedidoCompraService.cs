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
        /// <summary>Nesto#647: los productos de un código de barras (la llamada del núcleo común); null si la API no contesta bien.</summary>
        Task<List<ControlesUsuario.Models.ProductoCodigoBarrasDuplicado>> BuscarProductosPorCodigoBarras(string empresa, string codigo);
        Task<PedidoCompraDTO> AmpliarHastaStockMaximo(PedidoCompraDTO model);
        Task<int> CrearPedido(PedidoCompraDTO pedido);
        Task ModificarPedido(PedidoCompraDTO pedido);
        Task<CrearFacturaCmpResponse> CrearAlbaranYFactura(CrearFacturaCmpRequest request);
    }
}
