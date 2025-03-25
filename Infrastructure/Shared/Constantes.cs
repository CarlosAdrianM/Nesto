using System;
using System.Collections.Generic;

namespace Nesto.Infrastructure.Shared
{
    public class Constantes
    {
        public class Agencias
        {
            public const string AGENCIA_DEFECTO = "Sending"; 
            public const string AGENCIA_INTERNACIONAL = "Sending"; // String.Empty para no usar ninguna
            public const string AGENCIA_REEMBOLSOS = "Correos Express"; // String.Empty para no usar ninguna
            public const int ESTADO_INICIAL_ENVIO = 0;
            public const int ESTADO_TRAMITADO_ENVIO = 1;
            public const Int16 ESTADO_PENDIENTE_ENVIO = -1; // Etiqueta pendiente de envío
        }
        public class Almacenes
        {
            public const string ALMACEN_CENTRAL = "ALG";
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
            public const string CAJA_PENDIENTE_RECIBIR_TIENDAS = "57000016";
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
            public static readonly List<string> FORMAS_ONLINE = new List<string>() { "QRU", "WEB", "STK", "BLT" };
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
            public const string TIENDA_ON_LINE = "TiendaOnline";
            public const string TIENDAS = "Tiendas";
        }
        public class LineasPedido
        {
            public const int ESTADO_FACTURA = 4;
            public const int ESTADO_ALBARAN = 2;
            public const int ESTADO_SIN_FACTURAR = 1;
            public const int ESTADO_LINEA_PENDIENTE = -1;
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
            public static List<Sede> ListaSedes = new List<Sede>
            {
                new Sede { Nombre = "Algete", Codigo = "ALG" },
                new Sede { Nombre = "Reina", Codigo = "REI" },
                new Sede { Nombre = "Alcobendas", Codigo = "ALC" }
            };
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

        public class Ubicaciones {
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
