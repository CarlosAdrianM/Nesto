using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System.Windows;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (panel lateral, tramo 1). Carlos 27/10/26 (decisión 3): lo que no se puede usar no se ve. Un elemento del
    /// menú lateral se muestra si él, su grupo y su pestaña están visibles Y su comando se puede ejecutar; no hay
    /// elementos deshabilitados. El CanExecute se sigue con CanExecuteChanged.
    /// </summary>
    [TestClass]
    public class ElementoMenuTests
    {
        private static ElementoMenu Elemento(MapaMenuPorRolTests.ComandoDePrueba comando)
            => new ElementoMenu("Pestaña › Grupo › Botón") { Comando = comando };

        [TestMethod]
        public void VisibleYSePuedeEjecutar_SeMuestra()
        {
            ElementoMenu elemento = Elemento(new MapaMenuPorRolTests.ComandoDePrueba());

            Assert.IsTrue(elemento.Visible);
            Assert.IsTrue(elemento.PuedeEjecutar);
            Assert.IsTrue(elemento.Mostrar);
        }

        [TestMethod]
        public void SinComando_NoSeMuestra()
        {
            Assert.IsFalse(new ElementoMenu("a › b › c").Mostrar);
        }

        [TestMethod]
        public void ConElComandoQueNoSePuedeEjecutar_NoSeMuestra_YVuelveConCanExecuteChanged()
        {
            var comando = new MapaMenuPorRolTests.ComandoDePrueba { Puede = false };
            ElementoMenu elemento = Elemento(comando);
            int avisos = 0;
            elemento.MostrarCambiado += (_, _) => avisos++;
            Assert.IsTrue(elemento.Visible, "Visible, pero sin permiso");
            Assert.IsFalse(elemento.Mostrar);

            comando.Cambiar(true);
            Assert.IsTrue(elemento.Mostrar);

            comando.Cambiar(false);
            Assert.IsFalse(elemento.Mostrar);
            Assert.AreEqual(2, avisos);
        }

        [DataTestMethod]
        [DataRow(Visibility.Collapsed, Visibility.Visible, Visibility.Visible)]
        [DataRow(Visibility.Hidden, Visibility.Visible, Visibility.Visible)]
        [DataRow(Visibility.Visible, Visibility.Hidden, Visibility.Visible)]
        [DataRow(Visibility.Visible, Visibility.Visible, Visibility.Collapsed)]
        public void SiNoEstaVisibleElElementoSuGrupoOSuPestana_NoSeMuestra(Visibility elemento, Visibility grupo, Visibility pestana)
        {
            ElementoMenu boton = Elemento(new MapaMenuPorRolTests.ComandoDePrueba());

            boton.Visibilidad = elemento;
            boton.VisibilidadGrupo = grupo;
            boton.VisibilidadPestana = pestana;

            Assert.IsFalse(boton.Visible);
            Assert.IsFalse(boton.Mostrar);
        }

        [TestMethod]
        public void CambiarDeComando_DejaDeEscucharAlAnterior()
        {
            var anterior = new MapaMenuPorRolTests.ComandoDePrueba { Puede = true };
            ElementoMenu elemento = Elemento(anterior);
            elemento.Comando = new MapaMenuPorRolTests.ComandoDePrueba { Puede = false };
            Assert.IsFalse(elemento.Mostrar);

            anterior.Cambiar(true);

            Assert.IsFalse(elemento.Mostrar, "El comando de antes ya no cuenta");
        }

        [TestMethod]
        public void UnContenido_SeMuestraSiEstaVisible()
        {
            var contenido = new ContenidoMenu("Informes › Fechas › [StackPanel]");
            Assert.IsTrue(contenido.Mostrar);

            contenido.VisibilidadGrupo = Visibility.Collapsed;

            Assert.IsFalse(contenido.Mostrar);
        }
    }
}
