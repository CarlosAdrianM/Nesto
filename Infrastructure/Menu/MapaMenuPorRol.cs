using System;
using System.Collections.Generic;
using System.Linq;

namespace Nesto.Infrastructure.Menu
{
    /// <summary>Una sección del menú lateral: título, icono (SymbolRegular de WPF-UI, como texto) y orden.</summary>
    public sealed class DefinicionSeccion
    {
        public DefinicionSeccion(string titulo, string icono, int orden)
        {
            Titulo = titulo;
            Icono = icono;
            Orden = orden;
        }

        public string Titulo { get; }
        public string Icono { get; }
        public int Orden { get; }
    }

    /// <summary>
    /// Dónde va un elemento de la cinta en el menú lateral: sección, grupo dentro de ella, orden dentro de la sección y,
    /// si hace falta, otro título (los que en la cinta se repiten: «Ficha», «Pedidos»). Sin título, el de la cinta.
    /// </summary>
    public sealed class DestinoMenu
    {
        public DestinoMenu(string seccion, string grupo, int orden, string titulo = null)
        {
            Seccion = seccion;
            Grupo = grupo;
            Orden = orden;
            Titulo = titulo;
        }

        public string Seccion { get; }
        public string Grupo { get; }
        public int Orden { get; }
        public string Titulo { get; }
    }

    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1). Carlos 27/10/26 (decisión 1): las secciones del menú lateral van POR ROL desde
    /// el primer día, no calcan las 7 pestañas de la cinta. Esta es la tabla, explícita para poder validarla y probarla:
    /// cada elemento de la cinta, por su clave «Pestaña › Grupo › Botón», va a una sección, a un grupo y con un orden.
    /// <list type="bullet">
    /// <item>El botón se nombra por su texto en la cinta; si el texto es un Binding (cambia), por el comando entre
    /// corchetes (<c>[AbrirDiferenciasReposicionCommand]</c>); lo que no es un botón ni tiene texto, por su tipo
    /// (<c>[StackPanel]</c>).</item>
    /// <item>Lo que no esté en la tabla va a la sección <see cref="OTROS"/>, que se ve: nunca se pierde un botón. Aun así,
    /// <c>MapaMenuPorRolTests</c> falla si la cinta tiene algo que no está aquí.</item>
    /// <item>El permiso no se decide aquí: cada elemento se ve o no según su visibilidad y su CanExecute en la cinta.</item>
    /// </list>
    /// </summary>
    public static class MapaMenuPorRol
    {
        public const string VENTAS = "Ventas";
        public const string CLIENTES = "Clientes";
        public const string ALMACEN = "Almacén y envíos";
        public const string TIENDAS = "Tiendas y reposición";
        public const string COMPRAS = "Compras y proveedores";
        public const string ADMINISTRACION = "Administración y contabilidad";
        public const string CANALES = "Tienda online y marketplaces";
        public const string INFORMES = "Informes y dirección";
        /// <summary>Lo que no está en la tabla (no debería haber nada: lo vigila una prueba).</summary>
        public const string OTROS = "Otros";

        /// <summary>Separador de la clave, como en la ruta que enseñará la búsqueda (tramo 2).</summary>
        public const string SEPARADOR = " › ";

        public static string Clave(string pestana, string grupo, string elemento)
            => (pestana ?? string.Empty) + SEPARADOR + (grupo ?? string.Empty) + SEPARADOR + (elemento ?? string.Empty);

        public static IReadOnlyList<DefinicionSeccion> Secciones { get; } = new List<DefinicionSeccion>
        {
            new DefinicionSeccion(VENTAS, "ShoppingBag24", 10),
            new DefinicionSeccion(CLIENTES, "People24", 20),
            new DefinicionSeccion(ALMACEN, "Box24", 30),
            new DefinicionSeccion(TIENDAS, "BuildingShop24", 40),
            new DefinicionSeccion(COMPRAS, "Cart24", 50),
            new DefinicionSeccion(ADMINISTRACION, "Calculator24", 60),
            new DefinicionSeccion(CANALES, "Globe24", 70),
            new DefinicionSeccion(INFORMES, "DataBarVertical24", 80),
            new DefinicionSeccion(OTROS, "Apps24", 99),
        };

        /// <summary>La tabla: clave en la cinta → destino en el menú lateral.</summary>
        public static IReadOnlyDictionary<string, DestinoMenu> Destinos { get; } = Crear(
            // ── Ventas: vendedores, televenta y tiendas (meter y seguir pedidos, su cartera de clientes y comisiones)
            (Clave("Clientes", "Ventas", "Plantilla"), new DestinoMenu(VENTAS, "Pedidos", 10)),
            (Clave("Clientes", "Ventas", "Pedidos"), new DestinoMenu(VENTAS, "Pedidos", 20, "Pedidos de venta")),
            (Clave("Clientes", "Ventas", "Pendientes de entregar"), new DestinoMenu(VENTAS, "Pedidos", 30)),
            (Clave("Clientes", "Rapports", "Rapports"), new DestinoMenu(VENTAS, "Vendedores", 40)),
            (Clave("Vendedores", "Clientes", "Ficha"), new DestinoMenu(VENTAS, "Vendedores", 50, "Clientes del vendedor")),
            (Clave("Vendedores", "Clientes", "Planes de Ventajas"), new DestinoMenu(VENTAS, "Vendedores", 60)),
            (Clave("Vendedores", "Comisiones", "Comisiones"), new DestinoMenu(VENTAS, "Vendedores", 70)),

            // ── Clientes: datos maestros del cliente (fichas, altas, NIF, códigos postales, alquileres) y eventos
            (Clave("Clientes", "Mantenimiento", "Ficha"), new DestinoMenu(CLIENTES, "Fichas", 10, "Ficha de cliente")),
            (Clave("Clientes", "Mantenimiento", "Crear Cliente"), new DestinoMenu(CLIENTES, "Fichas", 20)),
            (Clave("Clientes", "Mantenimiento", "NIF incorrectos"), new DestinoMenu(CLIENTES, "Fichas", 30)),
            (Clave("Clientes", "Mantenimiento", "Códigos Postales"), new DestinoMenu(CLIENTES, "Fichas", 40)),
            (Clave("Clientes", "Mantenimiento", "Alquileres"), new DestinoMenu(CLIENTES, "Fichas", 50)),
            (Clave("Clientes", "Eventos", "Eventos"), new DestinoMenu(CLIENTES, "Eventos", 60)),
            (Clave("Clientes", "Eventos", "Señales de eventos"), new DestinoMenu(CLIENTES, "Eventos", 70)),

            // ── Almacén y envíos: producto, preparación (picking/packing), inventarios, huecos y agencias
            (Clave("Productos", "Mantenimiento", "Ficha"), new DestinoMenu(ALMACEN, "Almacén", 10, "Ficha de producto")),
            (Clave("Informes", "Almacén", "Picking"), new DestinoMenu(ALMACEN, "Almacén", 20)),
            (Clave("Informes", "Almacén", "Packing"), new DestinoMenu(ALMACEN, "Almacén", 30)),
            (Clave("Informes", "Inventario", "Inventario"), new DestinoMenu(ALMACEN, "Almacén", 40, "Inventario de ubicaciones")),
            (Clave("Productos", "Inventarios", "Tienda"), new DestinoMenu(ALMACEN, "Almacén", 50, "Inventario de tienda")),
            (Clave("Productos", "Almacén", "Etiquetas de hueco"), new DestinoMenu(ALMACEN, "Almacén", 60)),
            (Clave("Clientes", "Mantenimiento", "Agencias"), new DestinoMenu(ALMACEN, "Envíos", 70)),
            (Clave("Clientes", "Mantenimiento", "Mant. agencias"), new DestinoMenu(ALMACEN, "Envíos", 80, "Mantenimiento de agencias")),

            // ── Tiendas y reposición: lo que la tienda pide, recibe y manda a Algete, y lo facturado en ella
            (Clave("Productos", "Reposición", "Diarios"), new DestinoMenu(TIENDAS, "Reposición", 10, "Diarios de reposición")),
            (Clave("Productos", "Reposición", "Recibir"), new DestinoMenu(TIENDAS, "Reposición", 20, "Recibir reposición")),
            (Clave("Productos", "Reposición", "Enviar"), new DestinoMenu(TIENDAS, "Reposición", 30, "Enviar reposición")),
            (Clave("Productos", "Reposición", "Calendario"), new DestinoMenu(TIENDAS, "Reposición", 40, "Calendario de reposiciones")),
            // El texto es un Binding («Diferencias (3)»): se nombra por el comando y conserva su texto
            (Clave("Productos", "Reposición", "[AbrirDiferenciasReposicionCommand]"), new DestinoMenu(TIENDAS, "Reposición", 50)),
            (Clave("Productos", "Tienda", "Facturado en tienda"), new DestinoMenu(TIENDAS, "Tienda", 60)),

            // ── Compras y proveedores: pedidos y propuestas de compra, y lo que mantiene Compras (ofertas, familias)
            (Clave("Proveedores", "Pedidos", "Pedidos"), new DestinoMenu(COMPRAS, "Compras", 10, "Pedidos de compra")),
            (Clave("Productos", "Compras", "Propuestas de stock"), new DestinoMenu(COMPRAS, "Compras", 20)),
            (Clave("Proveedores", "Mantenimientos", "Ganavisiones"), new DestinoMenu(COMPRAS, "Mantenimientos", 30)),
            (Clave("Proveedores", "Mantenimientos", "Ofertas y descuentos"), new DestinoMenu(COMPRAS, "Mantenimientos", 40)),
            (Clave("Clientes", "Mantenimiento", "Mant. familias"), new DestinoMenu(COMPRAS, "Mantenimientos", 50, "Mantenimiento de familias")),

            // ── Administración y contabilidad: cobros, pagos, cajas y bancos, Verifactu y el 347
            (Clave("Clientes", "Mantenimiento", "Remesas"), new DestinoMenu(ADMINISTRACION, "Cobros", 10, "Remesas de clientes")),
            (Clave("Clientes", "Cartera", "Extracto Cliente"), new DestinoMenu(ADMINISTRACION, "Cobros", 20, "Extracto de cliente")),
            (Clave("Contabilidad", "Cobros", "Enlaces de pago"), new DestinoMenu(ADMINISTRACION, "Cobros", 30)),
            (Clave("Proveedores", "Cartera Pagos", "Remesa"), new DestinoMenu(ADMINISTRACION, "Pagos", 40, "Remesa de pagos")),
            (Clave("Contabilidad", "Cajas", "Cajas"), new DestinoMenu(ADMINISTRACION, "Contabilidad", 50)),
            (Clave("Contabilidad", "Cajas", "Bancos"), new DestinoMenu(ADMINISTRACION, "Contabilidad", 60)),
            (Clave("Contabilidad", "Cajas", "Mayor Cuenta"), new DestinoMenu(ADMINISTRACION, "Contabilidad", 70, "Mayor de una cuenta")),
            (Clave("Contabilidad", "Verifactu", "Facturas Verifactu"), new DestinoMenu(ADMINISTRACION, "Contabilidad", 80)),
            (Clave("Clientes", "Documentos", "Modelo 347"), new DestinoMenu(ADMINISTRACION, "Contabilidad", 90)),

            // ── Tienda online y marketplaces: canales externos (Amazon, PrestaShop…) y los vídeos de la tienda
            (Clave("Herramientas", "Canales Externos", "Pedidos"), new DestinoMenu(CANALES, "Canales externos", 10, "Pedidos de canales")),
            (Clave("Herramientas", "Canales Externos", "Pagos"), new DestinoMenu(CANALES, "Canales externos", 20, "Pagos de canales")),
            (Clave("Herramientas", "Canales Externos", "Facturas"), new DestinoMenu(CANALES, "Canales externos", 30, "Facturas de canales")),
            (Clave("Herramientas", "Canales Externos", "Cuadre facturas"), new DestinoMenu(CANALES, "Canales externos", 40, "Cuadre de facturas")),
            (Clave("Herramientas", "Canales Externos", "Productos"), new DestinoMenu(CANALES, "Canales externos", 50, "Productos en canales")),
            (Clave("Herramientas", "Canales Externos", "Poison Pills"), new DestinoMenu(CANALES, "Canales externos", 60)),
            (Clave("Herramientas", "Contenido", "Videos"), new DestinoMenu(CANALES, "Contenido", 70, "Vídeos")),

            // ── Informes y dirección: los informes con fechas (ventas del grupo, balances, rapports) y el control de pedidos
            (Clave("Informes", "Grupo de Empresas", "Ventas"), new DestinoMenu(INFORMES, "Informes", 10, "Ventas del grupo")),
            (Clave("Informes", "Contabilidad", "Balance Pymes"), new DestinoMenu(INFORMES, "Informes", 20)),
            (Clave("Informes", "Contabilidad", "PyG Pymes"), new DestinoMenu(INFORMES, "Informes", 30)),
            (Clave("Informes", "Vendedores Teléfono", "Rapport"), new DestinoMenu(INFORMES, "Informes", 40, "Informe de rapports")),
            (Clave("Informes", "Informes de Control", "Pedidos"), new DestinoMenu(INFORMES, "Informes", 50, "Control de pedidos")),
            // No son botones: el estado que usan los informes de arriba (el panel los pinta en «Opciones de los informes»)
            (Clave("Informes", "Contabilidad", "Incluir Global"), new DestinoMenu(INFORMES, "Opciones", 90)),
            (Clave("Informes", "Fechas", "[StackPanel]"), new DestinoMenu(INFORMES, "Opciones", 91, "Fechas de los informes"))
        );

        private static IReadOnlyDictionary<string, DestinoMenu> Crear(params (string Clave, DestinoMenu Destino)[] entradas)
        {
            var destinos = new Dictionary<string, DestinoMenu>(StringComparer.Ordinal);
            foreach ((string clave, DestinoMenu destino) in entradas)
            {
                // Una clave repetida es un error de la tabla: que salte al cargarla (lo prueba MapaMenuPorRolTests)
                destinos.Add(clave, destino);
            }
            return destinos;
        }

        /// <summary>La sección por su título (para el mapa y para «Otros»).</summary>
        public static DefinicionSeccion Seccion(string titulo) => Secciones.FirstOrDefault(s => s.Titulo == titulo);
    }
}
