using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Menu;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1): la tabla del reparto por rol (decisión 1 de Carlos). Que todo lo de la cinta
    /// esté en la tabla lo comprueba la prueba sombra (Nesto.Tests, con el arranque de verdad); aquí, la tabla en sí y el
    /// reparto del catálogo, con «Otros» para lo que no esté.
    /// </summary>
    [TestClass]
    public class MapaMenuPorRolTests
    {
        [TestMethod]
        public void LaTabla_SeCargaSinClavesRepetidas()
        {
            // Crear() lanza con una clave repetida: si se carga, no las hay
            Assert.AreEqual(57, MapaMenuPorRol.Destinos.Count, "Los 55 botones de la cinta y sus 2 contenidos (Fechas e Incluir Global)");
        }

        [TestMethod]
        public void CadaDestino_VaAUnaSeccionQueExiste_YNoEsOtros()
        {
            var secciones = MapaMenuPorRol.Secciones.Select(s => s.Titulo).ToList();
            foreach (KeyValuePair<string, DestinoMenu> entrada in MapaMenuPorRol.Destinos)
            {
                CollectionAssert.Contains(secciones, entrada.Value.Seccion, entrada.Key);
                Assert.AreNotEqual(MapaMenuPorRol.OTROS, entrada.Value.Seccion, entrada.Key + ": «Otros» es para lo que no está en la tabla");
            }
        }

        [TestMethod]
        public void DentroDeCadaSeccion_NiOrdenesNiTitulosRepetidos()
        {
            foreach (IGrouping<string, KeyValuePair<string, DestinoMenu>> seccion in MapaMenuPorRol.Destinos.GroupBy(d => d.Value.Seccion))
            {
                var ordenes = seccion.Select(d => d.Value.Orden).ToList();
                Assert.AreEqual(ordenes.Count, ordenes.Distinct().Count(), $"Órdenes repetidos en «{seccion.Key}»");
                // El título que se verá: el del mapa o, si no lo trae, el del botón (lo último de la clave)
                var titulos = seccion.Select(d => d.Value.Titulo ?? d.Key.Split(new[] { MapaMenuPorRol.SEPARADOR }, System.StringSplitOptions.None).Last()).ToList();
                Assert.AreEqual(titulos.Count, titulos.Distinct().Count(), $"Títulos repetidos en «{seccion.Key}»: {string.Join(", ", titulos)}");
            }
        }

        [TestMethod]
        public void LosTitulosQueSeRepitenEnLaCinta_SeDistinguenEnElPanel()
        {
            // «Ficha» y «Pedidos» salen en varias pestañas de la cinta (trampa 7 del plan)
            Assert.AreEqual("Ficha de cliente", MapaMenuPorRol.Destinos[MapaMenuPorRol.Clave("Clientes", "Mantenimiento", "Ficha")].Titulo);
            Assert.AreEqual("Ficha de producto", MapaMenuPorRol.Destinos[MapaMenuPorRol.Clave("Productos", "Mantenimiento", "Ficha")].Titulo);
            Assert.AreEqual("Pedidos de venta", MapaMenuPorRol.Destinos[MapaMenuPorRol.Clave("Clientes", "Ventas", "Pedidos")].Titulo);
            Assert.AreEqual("Pedidos de compra", MapaMenuPorRol.Destinos[MapaMenuPorRol.Clave("Proveedores", "Pedidos", "Pedidos")].Titulo);
        }

        [TestMethod]
        public void ElCatalogo_RepartePorRol_EnElOrdenDeLaTabla_YLoQueNoEstaVaAOtros()
        {
            var elementos = new List<ElementoMenuBase>
            {
                Boton(MapaMenuPorRol.Clave("Proveedores", "Pedidos", "Pedidos")),
                Boton(MapaMenuPorRol.Clave("Clientes", "Ventas", "Pedidos")),
                Boton(MapaMenuPorRol.Clave("Pestaña nueva", "Grupo nuevo", "Botón nuevo")),
                Boton(MapaMenuPorRol.Clave("Clientes", "Ventas", "Plantilla")),
            };

            CatalogoMenu catalogo = CatalogoMenu.Construir(elementos, null);

            CollectionAssert.AreEqual(new[] { MapaMenuPorRol.VENTAS, MapaMenuPorRol.COMPRAS, MapaMenuPorRol.OTROS },
                catalogo.Secciones.Select(s => s.Titulo).ToArray(), "Solo las secciones con algo, en el orden del mapa");
            CollectionAssert.AreEqual(new[] { "Plantilla", "Pedidos de venta" },
                catalogo.Secciones[0].Elementos.Select(e => e.Titulo).ToArray(), "Dentro de la sección, por el orden de la tabla");
            ElementoMenuBase nuevo = catalogo.Secciones[2].Elementos.Single();
            Assert.AreEqual("Botón nuevo", nuevo.Titulo, "Lo que no está en la tabla no se pierde: va a «Otros» con su título");
            Assert.AreEqual("Pestaña nueva › Grupo nuevo", nuevo.Grupo);
            CollectionAssert.AreEqual(new[] { MapaMenuPorRol.Clave("Pestaña nueva", "Grupo nuevo", "Botón nuevo") }, catalogo.SinMapear.ToArray());
            Assert.AreEqual(4, catalogo.Elementos.Count);
        }

        [TestMethod]
        public void UnaSeccion_SeOcultaCuandoNoHayNadaQueEsteUsuarioPuedaUsar()
        {
            ElementoMenu plantilla = Boton(MapaMenuPorRol.Clave("Clientes", "Ventas", "Plantilla"));
            CatalogoMenu catalogo = CatalogoMenu.Construir(new[] { plantilla }, null);
            SeccionMenu ventas = catalogo.Secciones.Single();
            Assert.IsTrue(ventas.HayAlgoQueMostrar);

            plantilla.Visibilidad = System.Windows.Visibility.Collapsed;

            Assert.IsFalse(ventas.HayAlgoQueMostrar);
        }

        private static ElementoMenu Boton(string clave)
        {
            string titulo = clave.Split(new[] { MapaMenuPorRol.SEPARADOR }, System.StringSplitOptions.None).Last();
            return new ElementoMenu(clave) { Titulo = titulo, Comando = new ComandoDePrueba() };
        }

        internal sealed class ComandoDePrueba : ICommand
        {
            public bool Puede { get; set; } = true;
            public int Ejecuciones { get; private set; }
            public event System.EventHandler CanExecuteChanged;
            public bool CanExecute(object parameter) => Puede;
            public void Execute(object parameter) => Ejecuciones++;
            public void Cambiar(bool puede)
            {
                Puede = puede;
                CanExecuteChanged?.Invoke(this, System.EventArgs.Empty);
            }
        }
    }
}
