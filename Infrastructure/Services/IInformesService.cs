using Nesto.Informes;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// Cliente de los endpoints api/Informes/* de NestoAPI. Se extrae en interfaz
    /// para que los ViewModels puedan ser testeados con mocks (FakeItEasy).
    public interface IInformesService
    {
        Task<List<ResumenVentasModel>> LeerResumenVentas(DateTime fechaDesde, DateTime fechaHasta, bool soloFacturas);
        Task<List<ControlPedidosModel>> LeerControlPedidos();
        Task<List<DetalleRapportsModel>> LeerDetalleRapports(DateTime fechaDesde, DateTime fechaHasta, string listaVendedores);
        Task<List<ExtractoContableModel>> LeerExtractoContable(string empresa, string cuenta, DateTime fechaDesde, DateTime fechaHasta);
        Task<List<UbicacionesInventarioModel>> LeerUbicacionesInventario(string empresa = "1");
        Task<List<KitsQueSePuedenMontarModel>> LeerKitsQueSePuedenMontar(string empresa, string fecha, string almacen, string filtroRutas);
        Task<List<MontarKitProductosModel>> LeerMontarKitProductos(int traspaso);
        Task<List<ManifiestoAgenciaModel>> LeerManifiestoAgencia(string empresa, int agencia, DateTime fecha);
        Task<List<PickingModel>> LeerPicking(int picking, string empresa = "1", int personas = 1);
        Task<int> LeerUltimoPicking();
        Task<List<PackingModel>> LeerPacking(int picking, int personas = 1);
        Task<PedidoCompraModel> LeerPedidoCompra(string empresa, int pedido);
        Task<List<FilaEtiquetasModel>> LeerEtiquetasTienda(List<string> productos, int etiquetaPrimera);
    }
}
