using Nesto.Informes;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Services
{
    /// Cliente de los endpoints api/Informes/* de NestoAPI. Todos los informes se consumen
    /// a través de esta clase para centralizar autenticación, URL base y serialización JSON.
    public class InformesService : IInformesService
    {
        private readonly IConfiguracion _configuracion;
        private readonly IServicioAutenticacion _servicioAutenticacion;

        public InformesService(IConfiguracion configuracion, IServicioAutenticacion servicioAutenticacion)
        {
            _configuracion = configuracion;
            _servicioAutenticacion = servicioAutenticacion;
        }

        public async Task<List<ResumenVentasModel>> LeerResumenVentas(DateTime fechaDesde, DateTime fechaHasta, bool soloFacturas)
            => await GetAsync<List<ResumenVentasModel>>(
                $"Informes/ResumenVentas?fechaDesde={fechaDesde:yyyy-MM-dd}&fechaHasta={fechaHasta:yyyy-MM-dd}&soloFacturas={soloFacturas.ToString().ToLower()}",
                "el resumen de ventas").ConfigureAwait(false);

        public async Task<List<ControlPedidosModel>> LeerControlPedidos()
            => await GetAsync<List<ControlPedidosModel>>("Informes/ControlPedidos", "el control de pedidos").ConfigureAwait(false);

        public async Task<List<DetalleRapportsModel>> LeerDetalleRapports(DateTime fechaDesde, DateTime fechaHasta, string listaVendedores)
            => await GetAsync<List<DetalleRapportsModel>>(
                $"Informes/DetalleRapports?fechaDesde={fechaDesde:yyyy-MM-dd}&fechaHasta={fechaHasta:yyyy-MM-dd}&listaVendedores={Uri.EscapeDataString(listaVendedores ?? string.Empty)}",
                "el detalle de rapports").ConfigureAwait(false);

        public async Task<List<ExtractoContableModel>> LeerExtractoContable(string empresa, string cuenta, DateTime fechaDesde, DateTime fechaHasta)
            => await GetAsync<List<ExtractoContableModel>>(
                $"Informes/ExtractoContable?empresa={Uri.EscapeDataString(empresa)}&cuenta={Uri.EscapeDataString(cuenta)}&fechaDesde={fechaDesde:yyyy-MM-dd}&fechaHasta={fechaHasta:yyyy-MM-dd}",
                "el extracto contable").ConfigureAwait(false);

        public async Task<List<UbicacionesInventarioModel>> LeerUbicacionesInventario(string empresa = "1")
            => await GetAsync<List<UbicacionesInventarioModel>>(
                $"Informes/UbicacionesInventario?empresa={Uri.EscapeDataString(empresa)}",
                "las ubicaciones de inventario").ConfigureAwait(false);

        public async Task<List<KitsQueSePuedenMontarModel>> LeerKitsQueSePuedenMontar(string empresa, string fecha, string almacen, string filtroRutas)
            => await GetAsync<List<KitsQueSePuedenMontarModel>>(
                $"Informes/KitsQueSePuedenMontar?empresa={Uri.EscapeDataString(empresa)}&fecha={Uri.EscapeDataString(fecha)}&almacen={Uri.EscapeDataString(almacen)}&filtroRutas={Uri.EscapeDataString(filtroRutas)}",
                "los kits que se pueden montar").ConfigureAwait(false);

        public async Task<List<MontarKitProductosModel>> LeerMontarKitProductos(int traspaso)
            => await GetAsync<List<MontarKitProductosModel>>(
                $"Informes/MontarKitProductos?traspaso={traspaso}",
                "los productos del kit a montar").ConfigureAwait(false);

        public async Task<List<ManifiestoAgenciaModel>> LeerManifiestoAgencia(string empresa, int agencia, DateTime fecha)
            => await GetAsync<List<ManifiestoAgenciaModel>>(
                $"Informes/ManifiestoAgencia?empresa={Uri.EscapeDataString(empresa)}&agencia={agencia}&fecha={fecha:yyyy-MM-dd}",
                "el manifiesto de la agencia").ConfigureAwait(false);

        public async Task<List<PickingModel>> LeerPicking(int picking, string empresa = "1", int personas = 1)
            => await GetAsync<List<PickingModel>>(
                $"Informes/Picking?picking={picking}&empresa={Uri.EscapeDataString(empresa)}&personas={personas}",
                "el picking").ConfigureAwait(false);

        public async Task<int> LeerUltimoPicking()
            => await GetAsync<int>("Informes/UltimoPicking", "el último picking").ConfigureAwait(false);

        public async Task<List<PackingModel>> LeerPacking(int picking, int personas = 1)
            => await GetAsync<List<PackingModel>>(
                $"Informes/Packing?picking={picking}&personas={personas}",
                "el packing").ConfigureAwait(false);

        public async Task<PedidoCompraModel> LeerPedidoCompra(string empresa, int pedido)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client).ConfigureAwait(false))
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");

                string url = $"Informes/PedidoCompra?empresa={Uri.EscapeDataString(empresa)}&pedido={pedido}";
                var response = await client.GetAsync(url).ConfigureAwait(false);
                if (response.StatusCode == HttpStatusCode.NotFound) return null;
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Error al obtener el pedido de compra: {response.StatusCode}");

                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<PedidoCompraModel>(body);
            }
        }

        private async Task<T> GetAsync<T>(string urlRelativa, string descripcion)
        {
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(_configuracion.servidorAPI);
                if (!await _servicioAutenticacion.ConfigurarAutorizacion(client).ConfigureAwait(false))
                    throw new UnauthorizedAccessException("No se pudo configurar la autorización");

                var response = await client.GetAsync(urlRelativa).ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"Error al obtener {descripcion}: {response.StatusCode}");

                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                return JsonConvert.DeserializeObject<T>(body);
            }
        }
    }
}
