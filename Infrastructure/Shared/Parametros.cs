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
            // NestoAPI#536: "NO" = la campana no se conecta por SignalR para recibir los avisos al momento
            // (se queda con su sondeo de seguridad). Cualquier otro valor, o sin fila, = conectada.
            public const string AvisosTiempoReal = "AvisosTiempoReal";
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
            // NestoAPI#482: modo de servicio con el que nacen los pedidos (1..4; sin fila, 3). Contrato
            // común con NestoAPI y NestoApp; la API aplica el mismo valor a los clientes que no lo mandan.
            public const string ModoServicioPorDefecto = "ModoServicioPorDefecto";
            public const string ImpresoraAgenciaGLS = "ImpresoraAgenciaGLS";
            public const string ImpresoraBolsas = "ImpresoraBolsas";
            public const string ImpresoraCodBarras = "ImpresoraCodBarras";
            // NestoAPI#595 (slice 2): "API" = «Insertar» de Agencias crea el envío en el servidor (POST
            // api/EnviosAgencias/DesdePedido). Cualquier otro valor, sin fila o si no se puede leer = el camino de
            // siempre (InsertarRegistro). Script: NestoAPI Scripts/Nesto595_ParametroInsertarEnvioPorApi.sql.
            public const string InsertarEnvioPorApi = "InsertarEnvioPorApi";
            // Nesto#340 (retirada de flags 20/08 y 21/08/26): MotorPdfExtractoContable, MotorPdfPedidoCompra,
            // MotorPdfPicking y MotorPdfEtiquetasTienda se retiraron — llevaban semanas al 100% en QuestPDF (defecto
            // incluido) y el camino RDLC era código muerto.
            // Nesto#490 / Nesto#340 (13/10/26): motor del PDF del mandato SEPA (el último RDLC, en NestoAPI).
            // "QuestPDF" → api/Clientes/MandatoPDF?...&motor=QuestPDF; cualquier otro valor o sin fila → RDLC.
            // El mandato va al CLIENTE: se activa usuario a usuario (fila "(defecto)" = RDLC).
            public const string MotorPdfMandatoSepa = "MotorPdfMandatoSepa";
            // Nesto#490 (panel lateral): "1" = el menú lateral (NavigationView) en lugar de la cinta; cualquier otro
            // valor, sin fila o si no se puede leer = la cinta. Se lee una vez al crear la ventana principal
            // (ConmutadorNavegacionLateral): el cambio se nota al reiniciar Nesto. El usuario lo cambia desde Parámetros
            // (catálogo de editables de NestoAPI). Filas: Scripts/Nesto490_NavegacionLateral.sql (NestoAPI).
            public const string NavegacionLateral = "NavegacionLateral";
            // Nesto#490 (panel lateral): "0" = el menú lateral sale abierto; cualquier otro valor o sin fila = compacto
            // (solo iconos). Lo guarda el propio panel cuando el usuario lo abre o lo cierra con su botón.
            public const string NavegacionLateralCompacta = "NavegacionLateralCompacta";
            // Nesto#340 (Agencias A4.3/A4.4, 23/09/26): PagarReembolsosPorApi y ModificarEnvioPorApi se
            // retiraron: validados en vivo, el camino de Entity Framework se borró y ya siempre va por la API.
            public const string PathNorma19 = "PathNorma19";
            public const string PathNorma43 = "PathNorma43";
            public const string PathNormaFB500 = "PathNormaFB500";
            public const string PedidoVentaMostrarImagenes = "PedidoVentaMostrarImagenes";
            public const string PedidoVentaPapelMembrete = "PedidoVentaPapelMembrete";
            // NestoAPI#501: "1"/"SI"/"TRUE" permite crear o modificar un pedido que no pasa la
            // validacion del servidor (el "¿Desea crearlo de todos modos?"). Lo mira tambien
            // NestoAPI (PedidosVentaController.TieneParametroPermitirOmitirValidacion) y NestoApp.
            public const string PermitirCrearPedidoConErroresValidacion = "PermitirCrearPedidoConErroresValidacion";
            public const string PermitirCopiarSeguimientos = "PermitirCopiarSeguimientos";
            public const string PermitirVerClientesTodosLosVendedores = "PermitirVerClientesTodosLosVendedores";
            public const string RutaPedidosCmp = "RutaPedidosCmp";
            // Nesto#388: "1" = el panel de contactos/direcciones del SelectorCliente aparece
            // desplegado al seleccionar un cliente; cualquier otro valor (o ausencia) = colapsado.
            public const string SelectorClienteContactosExpandidos = "SelectorClienteContactosExpandidos";
            public const string SerieFacturacionDefecto = "SerieFacturaciónDefecto";
            // Nesto#537: "1" = la franja «Puede ofrecer» de la plantilla de venta sale plegada; cualquier otro valor, sin
            // fila o si no se puede leer = desplegada. La guarda la propia plantilla al plegarla o desplegarla.
            public const string SugerenciasPlantillaPlegadas = "SugerenciasPlantillaPlegadas";
            // Sugerencia 761: lo mismo para la cinta «Puede ofrecer» del rapport, que se recuerda aparte de la plantilla.
            // Fila «(defecto)»: Scripts/Sugerencia761_ParametroRapport.sql (NestoAPI). Sin ella, desplegada.
            public const string SugerenciasRapportPlegadas = "SugerenciasRapportPlegadas";
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
