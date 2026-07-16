namespace Nesto.Infrastructure.Shared
{
    public class Parametros
    {
        public struct Claves
        {
            // Colocar por orden alfabético, por favor
            public const string AgenciasEnCuarentena = "AgenciasEnCuarentena";
            // NestoAPI#256: almacenes de stock de la plantilla de venta (CSV: "ALG,ALC,REI" o "ALG").
            // Contrato común con NestoAPI y NestoApp; la API es la fuente de verdad al cargar stocks.
            public const string AlmacenesPlantillaVenta = "AlmacenesPlantillaVenta";
            public const string AlmacenInventario = "AlmacénInventario";
            public const string AlmacenPedidoVta = "AlmacénPedidoVta";
            public const string AlmacenReposición = "AlmacénReposición";
            public const string AlmacenRuta = "AlmacénRuta";
            public const string CajaDefecto = "CajaDefecto";
            public const string CuentaBancoTarjeta = "CuentaBancoTarjeta";
            public const string ConciliacionBancariaUltimoBanco = "ConciliacionBancariaUltimoBanco";
            public const string ConciliacionBancariaFechaDesde = "ConciliacionBancariaFechaDesde";
            public const string ConciliacionBancariaFechaHasta = "ConciliacionBancariaFechaHasta";
            public const string DelegacionDefecto = "DelegaciónDefecto";
            public const string DiarioCaja = "DiarioCaja";
            public const string EmpresaPorDefecto = "EmpresaPorDefecto";
            public const string FondoCaja = "FondoCaja";
            public const string FormaVentaDefecto = "FormaVentaDefecto";
            public const string ImpresoraAgencia = "ImpresoraAgencia";
            public const string ImpresoraAgenciaGLS = "ImpresoraAgenciaGLS";
            public const string ImpresoraBolsas = "ImpresoraBolsas";
            public const string ImpresoraCodBarras = "ImpresoraCodBarras";
            // Motor de generación del extracto contable (Cajas): "RDLC" (por defecto, render local)
            // o "QuestPDF" (descarga el PDF de NestoAPI). Permite migrar usuario a usuario con vuelta atrás.
            public const string MotorPdfExtractoContable = "MotorPdfExtractoContable";
            // Motor de generación del pedido de compra a proveedor: "RDLC" (por defecto, render local)
            // o "QuestPDF" (descarga PDF/Excel de NestoAPI). Permite migrar usuario a usuario con vuelta atrás.
            public const string MotorPdfPedidoCompra = "MotorPdfPedidoCompra";
            // Motor de los informes de picking Y packing: "RDLC" (por defecto, render local) o
            // "QuestPDF" (descarga de NestoAPI). Son informes delicados (el packing viaja dentro
            // de la caja del cliente): migración usuario a usuario con vuelta atrás (Nesto#340).
            public const string MotorPdfPicking = "MotorPdfPicking";
            public const string PathNorma19 = "PathNorma19";
            public const string PathNorma43 = "PathNorma43";
            public const string PathNormaFB500 = "PathNormaFB500";
            public const string PedidoVentaMostrarImagenes = "PedidoVentaMostrarImagenes";
            public const string PedidoVentaPapelMembrete = "PedidoVentaPapelMembrete";
            public const string PermitirCopiarSeguimientos = "PermitirCopiarSeguimientos";
            public const string PermitirVerClientesTodosLosVendedores = "PermitirVerClientesTodosLosVendedores";
            public const string RutaPedidosCmp = "RutaPedidosCmp";
            // Nesto#388: "1" = el panel de contactos/direcciones del SelectorCliente aparece
            // desplegado al seleccionar un cliente; cualquier otro valor (o ausencia) = colapsado.
            public const string SelectorClienteContactosExpandidos = "SelectorClienteContactosExpandidos";
            public const string SerieFacturacionDefecto = "SerieFacturaciónDefecto";
            public const string UltimaVersionNovedades = "UltimaVersionNovedades";
            public const string UltNumProducto = "UltNumProducto";
            public const string UltTipoSeguimientoCliente = "UltTipoSeguimientoCliente";
            public const string UsarBusquedaContextualAND = "UsarBusquedaContextualAND";
            public const string UsuarioAvisoImpagadoDefecto = "UsuarioAvisoImpagadoDefecto";
            public const string Vendedor = "Vendedor";
            public const string VistoBuenoVentas = "VistoBuenoVentas";
        }
    }
}
