using System.Collections.Generic;

namespace Nesto.Infrastructure.Shared
{
    public class Constantes
    {
        public class Agencias
        {
            // Nesto#443 (17/08/26): Sending eliminada por desuso; ASM/GLS pasa a ser la agencia
            // por defecto y la del cambio automático en pedidos internacionales (decisión Carlos).
            public const string AGENCIA_DEFECTO = "ASM";
            public const string AGENCIA_INTERNACIONAL = "ASM"; // String.Empty para no usar ninguna
            public const string AGENCIA_REEMBOLSOS = "Correos Express"; // String.Empty para no usar ninguna
            // Nesto#571 (NestoAPI#739): el reparto con nuestras furgonetas como una agencia más (AgenciasTransporte 14,
            // empresa 1). Complementa a la ruta propia (AT, 16): Facturar rutas no cambia. Espejo de
            // Constantes.Agencias.AGENCIA_REPARTO_PROPIO de NestoAPI.
            public const int AGENCIA_REPARTO_PROPIO = 14;
            public const string NOMBRE_AGENCIA_REPARTO_PROPIO = "Reparto propio";
            public const int ESTADO_INICIAL_ENVIO = 0;
            public const int ESTADO_TRAMITADO_ENVIO = 1;
            // Estados de seguimiento (#247/#248), los rellena el poll del servidor por agencia.
            public const int ESTADO_ENTREGADO_ENVIO = 2;
            public const int ESTADO_INCIDENTADO_ENVIO = 3;
            public const int ESTADO_DEVUELTO_ENVIO = 4; // devuelto a origen (fallo terminal de entrega)
            // NestoAPI#676: un incidentado que la agencia nunca cerró y que una persona dio por cerrado. Terminado sin
            // confirmación de la agencia (no es Entregado). Espejo de Constantes.Agencias.ESTADO_CERRADO_A_MANO.
            public const int ESTADO_CERRADO_A_MANO_ENVIO = 5;
            public const short ESTADO_PENDIENTE_ENVIO = -1; // Etiqueta pendiente de envío
            public const decimal REEMBOLSO_NO_COBRAR = -1M; // Sentinel: no cobrar reembolso
        }
        public class Almacenes
        {
            public const string ALMACEN_CENTRAL = "ALG";
            public const string ALMACEN_ALGETE = "ALG";
            public const string ALMACEN_REINA = "REI";
            public const string ALMACEN_ALCOBENDAS = "ALC";
            public static readonly List<string> ALMACENES_STOCK = new List<string> { ALMACEN_ALGETE, ALMACEN_REINA, ALMACEN_ALCOBENDAS };
        }
        public class Clientes
        {
            public const int ESTADO_NORMAL = 0;
            public const int ESTADO_DISTRIBUIDOR = 6;
            public const int ESTADO_DISTRIBUIDOR_NO_VISITABLE = 67;
            public const int ESTADO_NO_VISITABLE = 7;
            public const int ESTADO_TELEFONO = 9;
            public class Especiales
            {
                public const string TIENDA_ONLINE = "31517";
                public const string AMAZON = "32624";
                public const string EL_EDEN = "15191";
            }
        }

        public class CorreosEmpresa
        {
            public const string COMPRAS = "compras@nuevavision.es";
        }
        public class Cuentas
        {
            public const string GASTOS_DATAFONO = "62600008";
            public const string CAJA_PENDIENTE_RECIBIR_TIENDAS = "57000016";
            /// <summary>Banco donde entran las transferencias: la cuenta por defecto de un prepago nuevo.</summary>
            public const string CAIXABANK = "57200013";
        }

        public class DiariosContables
        {
            public const string DIARIO_CAJA = "_Caja";
            public const string DIARIO_PAGO_REEMBOLSOS = "_PagoReemb";
            public const string DIARIO_REEMBOLSOS = "_Reembolso";
            public const string DIARIO_TRASPASOS_CAJA = "_TraspCaja";
        }
        public class Empresas
        {
            public const string CENTRO_COSTE_DEFECTO = "CA";
            public const string DEPARTAMENTO_DEFECTO = "ADM";
            public const string EMPRESA_DEFECTO = "1";
            public const string EMPRESA_ESPEJO = "3  ";
            public const string DELEGACION_DEFECTO = "ALG";
            public const string FORMA_VENTA_DEFECTO = "VAR";
            public const string MONEDA_CONTABILIDAD = "EUR";
            public const string IVA_DEFECTO = "G21";

        }
        public class ExtractosCliente
        {
            public class Estados
            {
                public const string RETENIDO = "RTN";
            }
        }
        public class Familias
        {
            public const string EVA_VISNU_NOMBRE = "Eva Visnú";
            public const string UNION_LASER_NOMBRE = "Unión Láser";
        }
        public class FormasPago
        {
            public const string EFECTIVO = "EFC";
            public const string RECIBO = "RCB";
            public const string TARJETA = "TAR";
            public const string TRANSFERENCIA = "TRN";
        }
        public class FormasVenta
        {
            public static readonly List<string> FORMAS_ONLINE = ["QRU", "WEB", "STK", "BLT"];
            public const string APOYO_COMERCIAL = "APC";
        }
        public class Formatos
        {
            public const string HTML_CLIENTE_P_TAG = "<p style = \"color:DimGray; font-family:'Consolas'; font-size:10px; border-radius: 25px; border: 2px solid #73AD21; padding: 10px;\">";
            public const string HTML_BANCO_P_TAG = "<p style = \"color:DimGray; font-family:'Consolas'; font-size:12px; border-radius: 25px; border: 2px solid #778899; padding: 10px;\">";
        }
        public class GruposSeguridad
        {
            public const string ADMINISTRACION = "Administración";
            public const string ALMACEN = "Almacén";
            public const string COMPRAS = "Compras";
            public const string DIRECCION = "Dirección";
            public const string FACTURACION = "Facturación";
            public const string INFORMATICA = "Informática";
            public const string TIENDA_ON_LINE = "TiendaOnline";
            public const string TIENDAS = "Tiendas";
        }
        public class LineasPedido
        {
            public const int ESTADO_FACTURA = 4;
            public const int ESTADO_ALBARAN = 2;
            public const int ESTADO_SIN_FACTURAR = 1;
            public const int ESTADO_LINEA_EN_CURSO = 1; // Alias de ESTADO_SIN_FACTURAR
            public const int ESTADO_LINEA_PENDIENTE = -1;
            public const int ESTADO_LINEA_PRESUPUESTO = -3;
            public class TiposLinea
            {
                public const string LINEA_TEXTO = "0";
                public const string PRODUCTO = "1";
                public const string CUENTA_CONTABLE = "2";
                public const string INMOVILIZADO = "3";
            }
        }
        public class Pedidos
        {
            public const string RUTA_GLOVO = "GLV";
            /// <summary>
            /// Nesto#531: rutas de reparto que salen de Algete (espejo de RutaPropia + RutaAgencia de NestoAPI,
            /// NestoAPI#649). En ellas una muestra solo puede llevar las unidades que hay en Algete.
            /// </summary>
            public static readonly string[] RUTAS_QUE_SALEN_DE_ALGETE = { "AT", "16", "00", "FW" };
        }
        public class PeriodosFacturacion
        {
            public const string FIN_DE_MES = "FDM";
            public const string NORMAL = "NRM";
        }
        public class Picking
        {
            public const int HORA_MAXIMA_AMPLIAR_PEDIDOS = 11;
        }
        public class Planner
        {
            public class GestionCobro
            {
                public const string PLAN_ID = "uI4iq6Cw2EO7IvMNPmDXBJcAAmeB";
                public const string BUCKET_PENDIENTES = "mfv4eFpWok2SETNoMdyLnJcAG25s";
            }
        }
        public class PlazosPago
        {
            public const string CONTADO = "CONTADO";
            public const string PREPAGO = "PRE";
        }
        public class Productos
        {
            public class Estados
            {
                public const short EN_STOCK = 0;
            }
            public class Grupos
            {
                public const string MUESTRAS = "MMP";
                /// <summary>Nesto#531: la plantilla recibe la descripción del subgrupo, no el código MMP.</summary>
                public const string DESCRIPCION_MUESTRAS = "Muestras y material promocional";
            }
            // Nesto#453: sentinel de PrestashopProductos.PVP_IVA_Incluido (espejo de NestoAPI
            // Constantes.Productos.PVP_IVA_MISMO_QUE_PROFESIONAL): el público es el mismo que el profesional.
            public const decimal PVP_IVA_MISMO_QUE_PROFESIONAL = -1M;
        }
        public class Proveedores
        {
            public class Especiales
            {
                public const string PROVEEDOR_AMAZON = "869";
                public const string CONTACTO_PROVEEDOR_AMAZON = "0";
                // NestoAPI#390: Amazon EU S.à r.l. es el proveedor de las facturas de
                // comisiones (SubirFacturas); las liquidaciones le pagan a cuenta y la
                // compensación se hace en su cartera (ExtractoProveedor).
                public const string PROVEEDOR_AMAZON_EU = "999";
            }
        }
        public class Rapports
        {
            public class Estados
            {
                public const int GESTION_ADMINISTRATIVA = 2;
            }
            public class Tipos
            {
                public const string TIPO_VISITA_PRESENCIAL = "V";
                public const string TIPO_VISITA_TELEFONICA = "T";
            }
        }
        public class Sedes
        {
            public static List<Sede> ListaSedes =
            [
                new Sede { Nombre = "Algete", Codigo = "ALG" },
                new Sede { Nombre = "Reina", Codigo = "REI" },
                new Sede { Nombre = "Alcobendas", Codigo = "ALC" }
            ];
        }
        public class Series
        {
            public const string SERIE_CURSOS = "CV";
            public const string SERIE_DEFECTO = "NV";
            public const string UNION_LASER = "UL";
            public const string EVA_VISNU = "EV";
        }
        public class TiposApunte
        {
            public const string PASO_A_CARTERA = "0";
            public const string FACTURA = "1";
            public const string CARTERA = "2";
            public const string PAGO = "3";
            public const string IMPAGADO = "4";
        }
        public class TiposCuenta
        {
            public const string CUENTA_CONTABLE = "1";
            public const string CLIENTE = "2";
            public const string PROVEEDOR = "3";
        }

        public class Ubicaciones
        {
            public const int ESTADO_A_BORRAR = -100;
            public const int ESTADO_A_MODIFICAR = -101;
            public const int ESTADO_REGISTRO_MONTAR_KITS = -102;
        }

        public class Vendedores
        {
            public const string VENDEDOR_POR_DEFECTO = "NV";
        }
    }

}
