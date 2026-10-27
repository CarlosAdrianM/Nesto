using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Menu;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 2): «Buscar pantalla…» busca en todo el catálogo por el título y por la ruta
    /// «Sección › Título», sin mayúsculas ni tildes, solo entre lo que el usuario puede usar, y con los que coinciden en el
    /// título delante de los que coinciden en la ruta.
    /// </summary>
    [TestClass]
    public class FiltroCatalogoMenuTests
    {
        // Elementos con las claves de verdad de la cinta (las de MapaMenuPorRol), en el orden en que estaban en ella
        private static readonly string[] _claves =
        {
            MapaMenuPorRol.Clave("Clientes", "Ventas", "Plantilla"),
            MapaMenuPorRol.Clave("Clientes", "Ventas", "Pedidos"),
            MapaMenuPorRol.Clave("Clientes", "Mantenimiento", "Ficha"),
            MapaMenuPorRol.Clave("Clientes", "Mantenimiento", "Remesas"),
            MapaMenuPorRol.Clave("Clientes", "Mantenimiento", "Mant. familias"),
            MapaMenuPorRol.Clave("Productos", "Mantenimiento", "Ficha"),
            MapaMenuPorRol.Clave("Proveedores", "Pedidos", "Pedidos"),
            MapaMenuPorRol.Clave("Proveedores", "Cartera Pagos", "Remesa"),
            MapaMenuPorRol.Clave("Contabilidad", "Verifactu", "Facturas Verifactu"),
            MapaMenuPorRol.Clave("Herramientas", "Contenido", "Videos"),
            MapaMenuPorRol.Clave("Herramientas", "Canales Externos", "Pedidos"),
            MapaMenuPorRol.Clave("Informes", "Fechas", "[StackPanel]"),
        };

        private sealed class Montaje
        {
            public CatalogoMenu Catalogo;
            public Dictionary<string, ElementoMenuBase> PorClave = new Dictionary<string, ElementoMenuBase>();
            public ElementoMenu Parametros;
            public ElementoMenu Novedades;
            public ElementoMenu this[string pestana, string grupo, string boton] => (ElementoMenu)PorClave[MapaMenuPorRol.Clave(pestana, grupo, boton)];
        }

        private static Montaje Montar()
        {
            var m = new Montaje();
            var elementos = new List<ElementoMenuBase>();
            foreach (string clave in _claves)
            {
                string titulo = clave.Split(new[] { MapaMenuPorRol.SEPARADOR }, System.StringSplitOptions.None)[2];
                ElementoMenuBase elemento = titulo.StartsWith("[")
                    ? new ContenidoMenu(clave) { Titulo = "Fechas" }
                    : new ElementoMenu(clave) { Titulo = titulo, Comando = new MapaMenuPorRolTests.ComandoDePrueba() };
                m.PorClave[clave] = elemento;
                elementos.Add(elemento);
            }
            m.Parametros = new ElementoMenu(MapaMenuPorRol.Clave("Menú de aplicación", "", "Parámetros")) { Titulo = "Parámetros", Comando = new MapaMenuPorRolTests.ComandoDePrueba() };
            m.Novedades = new ElementoMenu(MapaMenuPorRol.Clave("Menú de aplicación", "", "Novedades")) { Titulo = "Novedades", Comando = new MapaMenuPorRolTests.ComandoDePrueba() };
            m.Catalogo = CatalogoMenu.Construir(elementos, new[] { m.Parametros, m.Novedades });
            return m;
        }

        private static string[] Rutas(IEnumerable<ResultadoBusquedaMenu> resultados) => resultados.Select(r => r.Ruta).ToArray();

        [DataTestMethod]
        [DataRow("verifactu")]
        [DataRow("VERIFACTU")]
        [DataRow("Verifactu")]
        [DataRow("factu")]
        public void SinDistinguirMayusculas(string texto)
        {
            CollectionAssert.AreEqual(new[] { "Administración y contabilidad › Facturas Verifactu" },
                Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, texto)));
        }

        [DataTestMethod]
        [DataRow("videos")]
        [DataRow("vídeos")]
        [DataRow("VÍDEOS")]
        public void SinDistinguirTildes(string texto)
        {
            // En el mapa el título lleva tilde («Vídeos»); en la cinta, no
            CollectionAssert.AreEqual(new[] { "Tienda online y marketplaces › Vídeos" },
                Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, texto)));
        }

        [TestMethod]
        public void Familias_DaElMantenimientoDeFamilias()
        {
            CollectionAssert.AreEqual(new[] { "Compras y proveedores › Mantenimiento de familias" },
                Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "familias")));
        }

        [TestMethod]
        public void Remesa_DaLasDosRemesas_ConSuRutaParaDistinguirlas()
        {
            CollectionAssert.AreEqual(new[]
            {
                "Administración y contabilidad › Remesas de clientes",
                "Administración y contabilidad › Remesa de pagos",
            }, Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "remesa")));
        }

        [TestMethod]
        public void PorLaSeccion_SaleTodoLoDeLaSeccion()
        {
            // «contabilidad» no está en ningún título: coincide por la ruta «Administración y contabilidad › …»
            CollectionAssert.AreEqual(new[]
            {
                "Administración y contabilidad › Remesas de clientes",
                "Administración y contabilidad › Remesa de pagos",
                "Administración y contabilidad › Facturas Verifactu",
            }, Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "contabilidad")));
        }

        [TestMethod]
        public void VariasPalabras_TienenQueEstarTodas_EnCualquierOrden_YPuedenMezclarSeccionYTitulo()
        {
            CollectionAssert.AreEqual(new[] { "Ventas › Plantilla" },
                Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "plantilla ventas")));
            CollectionAssert.AreEqual(new[] { "Compras y proveedores › Pedidos de compra" },
                Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "compra pedidos")));
            // Escrito como la ruta que se enseña
            CollectionAssert.AreEqual(new[] { "Almacén y envíos › Ficha de producto" },
                Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "almacen › ficha")));
        }

        [TestMethod]
        public void Orden_PrimeroElInicioDelTitulo_DespuesElTitulo_DespuesLaRuta_YAIgualdadElOrdenDelPanel()
        {
            // «Plantilla» está antes en el panel, pero solo coincide por la sección: detrás del que lo tiene en el título
            IReadOnlyList<ResultadoBusquedaMenu> resultados = FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "venta");
            CollectionAssert.AreEqual(new[]
            {
                "Ventas › Pedidos de venta",   // en el título
                "Ventas › Plantilla",          // solo en la ruta
            }, Rutas(resultados));
            Assert.AreEqual(CoincidenciaBusquedaMenu.Titulo, resultados[0].Coincidencia);
            Assert.AreEqual(CoincidenciaBusquedaMenu.Ruta, resultados[1].Coincidencia);

            // El que empieza por lo buscado, delante aunque en el panel vaya después
            resultados = FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "pedidos");
            CollectionAssert.AreEqual(new[]
            {
                "Ventas › Pedidos de venta",
                "Compras y proveedores › Pedidos de compra",
                "Tienda online y marketplaces › Pedidos de canales",
            }, Rutas(resultados));
            Assert.IsTrue(resultados.All(r => r.Coincidencia == CoincidenciaBusquedaMenu.InicioDelTitulo));

            resultados = FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "ficha");
            CollectionAssert.AreEqual(new[] { "Clientes › Ficha de cliente", "Almacén y envíos › Ficha de producto" }, Rutas(resultados));
        }

        [TestMethod]
        public void ConLaSeccionYElTitulo_ElTituloVaDelante()
        {
            // Con «compra», el título («Pedidos de compra») va delante de lo que solo lo tiene en la sección
            IReadOnlyList<ResultadoBusquedaMenu> resultados = FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "compra");
            Assert.AreEqual("Compras y proveedores › Pedidos de compra", resultados[0].Ruta);
            Assert.AreEqual(CoincidenciaBusquedaMenu.Titulo, resultados[0].Coincidencia);
            Assert.AreEqual("Compras y proveedores › Mantenimiento de familias", resultados[1].Ruta);
            Assert.AreEqual(CoincidenciaBusquedaMenu.Ruta, resultados[1].Coincidencia);
        }

        [TestMethod]
        public void LoQueNoSePuedeUsar_NoSale()
        {
            Montaje m = Montar();
            ((MapaMenuPorRolTests.ComandoDePrueba)m["Clientes", "Mantenimiento", "Remesas"].Comando).Cambiar(false);
            m["Proveedores", "Cartera Pagos", "Remesa"].Visibilidad = Visibility.Collapsed;

            Assert.AreEqual(0, FiltroCatalogoMenu.Filtrar(m.Catalogo, "remesa").Count, "Sin CanExecute o sin ver, no sale");

            ((MapaMenuPorRolTests.ComandoDePrueba)m["Clientes", "Mantenimiento", "Remesas"].Comando).Cambiar(true);
            CollectionAssert.AreEqual(new[] { "Administración y contabilidad › Remesas de clientes" },
                Rutas(FiltroCatalogoMenu.Filtrar(m.Catalogo, "remesa")));
        }

        [TestMethod]
        public void LoQueNoEsUnaPantalla_NoSale()
        {
            Assert.AreEqual(0, FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "fechas").Count,
                "«Fechas de los informes» no se abre: está en «Opciones de los informes»");
        }

        [TestMethod]
        public void ElPie_TambienSeBusca()
        {
            CollectionAssert.AreEqual(new[] { "Nesto › Parámetros" }, Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "parametros")));
            CollectionAssert.AreEqual(new[] { "Nesto › Novedades" }, Rutas(FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "noved")));
        }

        [TestMethod]
        public void PorDondeEstabaEnLaCinta_TambienSale_PeroAlFinal()
        {
            // Quien buscaba en la pestaña «Herramientas» de la cinta
            IReadOnlyList<ResultadoBusquedaMenu> resultados = FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "herramientas");
            CollectionAssert.AreEqual(new[]
            {
                "Tienda online y marketplaces › Pedidos de canales",
                "Tienda online y marketplaces › Vídeos",
            }, Rutas(resultados));
            Assert.IsTrue(resultados.All(r => r.Coincidencia == CoincidenciaBusquedaMenu.Cinta));
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("   ")]
        [DataRow(" › ")]
        public void SinTexto_NoSaleNada(string texto)
        {
            Assert.AreEqual(0, FiltroCatalogoMenu.Filtrar(Montar().Catalogo, texto).Count);
        }

        [TestMethod]
        public void SinCoincidencias_NoSaleNada()
        {
            Assert.AreEqual(0, FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "zzz").Count);
        }

        [DataTestMethod]
        [DataRow("Códigos Postales", "codigos postales")]
        [DataRow("Señales de eventos", "senales de eventos")]
        [DataRow("_Parámetros", "parametros")]
        [DataRow("Almacén y envíos", "almacen y envios")]
        [DataRow("PINGÜINO", "pinguino")]
        public void Normalizar_QuitaTildesMayusculasYAceleradores(string texto, string esperado)
        {
            Assert.AreEqual(esperado, FiltroCatalogoMenu.Normalizar(texto));
        }

        [TestMethod]
        public void CadaElementoSaleUnaSolaVez()
        {
            IReadOnlyList<ResultadoBusquedaMenu> resultados = FiltroCatalogoMenu.Filtrar(Montar().Catalogo, "a");
            Assert.AreEqual(resultados.Count, resultados.Select(r => r.Elemento).Distinct().Count());
        }
    }
}
