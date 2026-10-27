using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Menu;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 2): el ViewModel de «Buscar pantalla…». Ctrl+K pide el foco; las flechas eligen;
    /// Intro abre (con el MISMO comando que la cinta) y Esc cancela, y las dos terminan la búsqueda para que el panel vuelva
    /// a compacto y devuelva el foco. Un error al abrir va a ELMAH con el contexto «NavegacionLateral» y no tumba nada.
    /// </summary>
    [TestClass]
    public class BuscadorMenuTests
    {
        private sealed class ComandoQueFalla : ICommand
        {
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => throw new InvalidOperationException("Sin servidor");
        }

        private sealed class Montaje
        {
            public BuscadorMenu Buscador;
            public ElementoMenu Plantilla;
            public ElementoMenu Pedidos;
            public ElementoMenu PedidosCompra;
            public MapaMenuPorRolTests.ComandoDePrueba ComandoPlantilla = new MapaMenuPorRolTests.ComandoDePrueba();
            public MapaMenuPorRolTests.ComandoDePrueba ComandoPedidos = new MapaMenuPorRolTests.ComandoDePrueba();
            public MapaMenuPorRolTests.ComandoDePrueba ComandoPedidosCompra = new MapaMenuPorRolTests.ComandoDePrueba();
            public List<string> Errores = new List<string>();
            public List<BusquedaTerminadaEventArgs> Terminadas = new List<BusquedaTerminadaEventArgs>();
            public int Activaciones;
        }

        private static Montaje Montar()
        {
            var m = new Montaje();
            m.Plantilla = new ElementoMenu(MapaMenuPorRol.Clave("Clientes", "Ventas", "Plantilla")) { Titulo = "Plantilla", Comando = m.ComandoPlantilla };
            m.Pedidos = new ElementoMenu(MapaMenuPorRol.Clave("Clientes", "Ventas", "Pedidos")) { Titulo = "Pedidos", Comando = m.ComandoPedidos };
            m.PedidosCompra = new ElementoMenu(MapaMenuPorRol.Clave("Proveedores", "Pedidos", "Pedidos")) { Titulo = "Pedidos", Comando = m.ComandoPedidosCompra };
            CatalogoMenu catalogo = CatalogoMenu.Construir(new ElementoMenuBase[] { m.Plantilla, m.Pedidos, m.PedidosCompra }, null);
            m.Buscador = new BuscadorMenu(catalogo, (ex, contexto) => m.Errores.Add(contexto + " | " + ex.Message));
            m.Buscador.Terminada += (_, e) => m.Terminadas.Add(e);
            m.Buscador.ActivacionPedida += (_, _) => m.Activaciones++;
            return m;
        }

        [TestMethod]
        public void CtrlK_PideElFocoAlPanel()
        {
            Montaje m = Montar();
            Assert.IsTrue(m.Buscador.ActivarCommand.CanExecute(null), "Ctrl+K siempre se puede pulsar");

            m.Buscador.ActivarCommand.Execute(null);

            Assert.AreEqual(1, m.Activaciones);
            Assert.AreEqual(0, m.Terminadas.Count);
        }

        [TestMethod]
        public void AlEscribir_FiltraYSeleccionaElPrimero()
        {
            Montaje m = Montar();
            Assert.IsFalse(m.Buscador.HayBusqueda);
            Assert.AreEqual(0, m.Buscador.Resultados.Count, "Sin escribir, no hay lista");

            m.Buscador.Texto = "pedidos";

            CollectionAssert.AreEqual(new[] { "Ventas › Pedidos de venta", "Compras y proveedores › Pedidos de compra" },
                m.Buscador.Resultados.Select(r => r.Ruta).ToArray());
            Assert.AreSame(m.Buscador.Resultados[0], m.Buscador.Seleccionado);
            Assert.IsTrue(m.Buscador.HayBusqueda);
            Assert.IsFalse(m.Buscador.SinResultados);
        }

        [TestMethod]
        public void SinResultados_LoDice()
        {
            Montaje m = Montar();
            m.Buscador.Texto = "zzz";
            Assert.IsTrue(m.Buscador.SinResultados);
            Assert.AreEqual("No hay ninguna pantalla con «zzz»", m.Buscador.TextoSinResultados);
            Assert.IsNull(m.Buscador.Seleccionado);
            Assert.IsFalse(m.Buscador.Ejecutar(), "Intro sin resultados no hace nada");
            Assert.AreEqual(0, m.Terminadas.Count);
        }

        [TestMethod]
        public void LasFlechas_RecorrenLosResultados_YDanLaVuelta()
        {
            Montaje m = Montar();
            m.Buscador.Texto = "pedidos";
            ResultadoBusquedaMenu primero = m.Buscador.Resultados[0];
            ResultadoBusquedaMenu segundo = m.Buscador.Resultados[1];

            m.Buscador.Mover(1);
            Assert.AreSame(segundo, m.Buscador.Seleccionado);
            m.Buscador.Mover(1);
            Assert.AreSame(primero, m.Buscador.Seleccionado, "Abajo desde el último: el primero");
            m.Buscador.Mover(-1);
            Assert.AreSame(segundo, m.Buscador.Seleccionado, "Arriba desde el primero: el último");
        }

        [TestMethod]
        public void Intro_AbreElSeleccionado_ConElComandoDeLaCinta_BorraYTermina()
        {
            Montaje m = Montar();
            m.Buscador.Texto = "pedidos";
            m.Buscador.Mover(1);

            Assert.IsTrue(m.Buscador.Ejecutar());

            Assert.AreEqual(0, m.ComandoPedidos.Ejecuciones);
            Assert.AreEqual(1, m.ComandoPedidosCompra.Ejecuciones, "El seleccionado con las flechas");
            Assert.AreEqual(string.Empty, m.Buscador.Texto, "La caja queda vacía para la próxima vez");
            Assert.AreEqual(0, m.Buscador.Resultados.Count);
            Assert.AreEqual(1, m.Terminadas.Count);
            Assert.AreSame(m.PedidosCompra, m.Terminadas[0].Ejecutado.Elemento);
        }

        [TestMethod]
        public void Intro_SinElegir_AbreElPrimero()
        {
            Montaje m = Montar();
            m.Buscador.Texto = "pla";
            m.Buscador.Seleccionado = null;

            Assert.IsTrue(m.Buscador.Ejecutar());

            Assert.AreEqual(1, m.ComandoPlantilla.Ejecuciones);
        }

        [TestMethod]
        public void LaBusquedaTermina_AntesDeAbrirLaPantalla()
        {
            // Para que el panel vuelva a compacto y suelte el foco antes de que la pantalla nueva lo quiera
            Montaje m = Montar();
            var orden = new List<string>();
            m.Buscador.Terminada += (_, _) => orden.Add("terminada");
            var comando = new ComandoQueApunta(orden);
            m.Plantilla.Comando = comando;
            m.Buscador.Texto = "plantilla";

            _ = m.Buscador.Ejecutar();

            CollectionAssert.AreEqual(new[] { "terminada", "ejecutado" }, orden);
        }

        private sealed class ComandoQueApunta : ICommand
        {
            private readonly List<string> _orden;
            public ComandoQueApunta(List<string> orden) => _orden = orden;
            public event EventHandler CanExecuteChanged { add { } remove { } }
            public bool CanExecute(object parameter) => true;
            public void Execute(object parameter) => _orden.Add("ejecutado");
        }

        [TestMethod]
        public void Esc_BorraYTermina_SinAbrirNada()
        {
            Montaje m = Montar();
            m.Buscador.Texto = "pedidos";

            m.Buscador.Cancelar();

            Assert.AreEqual(string.Empty, m.Buscador.Texto);
            Assert.AreEqual(0, m.Buscador.Resultados.Count);
            Assert.AreEqual(1, m.Terminadas.Count);
            Assert.IsNull(m.Terminadas[0].Ejecutado);
            Assert.AreEqual(0, m.ComandoPedidos.Ejecuciones + m.ComandoPedidosCompra.Ejecuciones + m.ComandoPlantilla.Ejecuciones);
        }

        [TestMethod]
        public void SiYaNoSePuedeUsar_NoSeAbre_YDejaDeSalir()
        {
            Montaje m = Montar();
            m.Buscador.Texto = "plantilla";
            ResultadoBusquedaMenu resultado = m.Buscador.Resultados.Single();
            m.ComandoPlantilla.Cambiar(false);

            Assert.IsFalse(m.Buscador.Ejecutar(resultado));

            Assert.AreEqual(0, m.ComandoPlantilla.Ejecuciones);
            Assert.AreEqual(0, m.Buscador.Resultados.Count);
            Assert.AreEqual(0, m.Terminadas.Count, "La búsqueda sigue abierta");
        }

        [TestMethod]
        public void UnErrorAlAbrir_VaAElmahConElContextoNavegacionLateral_SeAvisa_YNoSePropaga()
        {
            Montaje m = Montar();
            m.Plantilla.Comando = new ComandoQueFalla();
            var avisos = new List<string>();
            m.Buscador.ErrorAlEjecutar += (_, mensaje) => avisos.Add(mensaje);
            m.Buscador.Texto = "plantilla";

            Assert.IsFalse(m.Buscador.Ejecutar());

            Assert.AreEqual(1, m.Errores.Count);
            StringAssert.StartsWith(m.Errores[0], "NavegacionLateral: abrir «Plantilla» desde la búsqueda");
            StringAssert.Contains(m.Errores[0], "Sin servidor");
            Assert.AreEqual(1, avisos.Count);
            StringAssert.StartsWith(avisos[0], "No se ha podido abrir «Plantilla»");
            Assert.AreEqual(1, m.Terminadas.Count, "El panel ya ha vuelto a como estaba");
        }

        [TestMethod]
        public void UnFalloAlTerminar_TampocoSePropaga()
        {
            Montaje m = Montar();
            m.Buscador.Terminada += (_, _) => throw new InvalidOperationException("foco");
            m.Buscador.Texto = "plantilla";

            Assert.IsTrue(m.Buscador.Ejecutar());

            Assert.AreEqual(1, m.ComandoPlantilla.Ejecuciones, "La pantalla se abre igual");
            StringAssert.StartsWith(m.Errores.Single(), "NavegacionLateral: terminar la búsqueda");
        }
    }
}
