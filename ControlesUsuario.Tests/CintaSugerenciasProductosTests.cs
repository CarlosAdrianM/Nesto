using ControlesUsuario;
using ControlesUsuario.SugerenciasProductos;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#544: la cinta «Puede ofrecer» común a la plantilla y al rapport. «✓ Añadir» solo sale donde la pantalla sabe meter
    /// el producto en un pedido (la plantilla); «✗ No le interesa» y «🚫 Quitar» salen siempre. Sin fichas, no se ve nada.
    /// </summary>
    [TestClass]
    public class CintaSugerenciasProductosTests
    {
        private static SugerenciaProductoDTO Ficha(int id, string producto, int posicion) =>
            new SugerenciaProductoDTO { IdSugerencia = id, Producto = producto, Nombre = "PRODUCTO " + producto, Posicion = posicion };

        private static CintaSugerenciasProductosViewModel Cinta(Func<SugerenciaProductoDTO, Task<bool>> anadir) =>
            new CintaSugerenciasProductosViewModel(() => new CalcularSugerenciasProductosDTO { Empresa = "1", Cliente = "15191" }, anadir: anadir);

        [TestMethod]
        public void SinAnadir_LaVistaNoEnsenaElBotonAnadir_PeroSiLosOtrosDos()
        {
            EjecutarEnSTA(() =>
            {
                CintaSugerenciasProductosViewModel cinta = Cinta(null);
                cinta.Aplicar(new[] { Ficha(10, "38000", 1) });

                List<Button> botones = BotonesDeLaPrimeraFicha(cinta);

                Assert.IsFalse(botones.Any(b => Equals(b.Content, "✓") && b.Visibility == Visibility.Visible), "«✓ Añadir» no sale en el rapport");
                Assert.IsTrue(botones.Any(b => Equals(b.Content, "✗") && b.Visibility == Visibility.Visible));
                Assert.AreEqual(2, botones.Count(b => b.Visibility == Visibility.Visible));
            });
        }

        [TestMethod]
        public void ConAnadir_LaVistaEnsenaLosTresBotones()
        {
            EjecutarEnSTA(() =>
            {
                CintaSugerenciasProductosViewModel cinta = Cinta(_ => Task.FromResult(true));
                cinta.Aplicar(new[] { Ficha(10, "38000", 1) });

                List<Button> botones = BotonesDeLaPrimeraFicha(cinta);

                Assert.IsTrue(botones.Any(b => Equals(b.Content, "✓") && b.Visibility == Visibility.Visible));
                Assert.AreEqual(3, botones.Count(b => b.Visibility == Visibility.Visible));
            });
        }

        [TestMethod]
        public void SinFichas_LaCintaNoSeVe()
        {
            EjecutarEnSTA(() =>
            {
                var vista = new CintaSugerenciasProductosView { DataContext = Cinta(null), EnColumna = true };
                vista.Measure(new Size(400, 600));
                vista.Arrange(new Rect(0, 0, 400, 600));
                vista.UpdateLayout();

                Assert.AreEqual(0, vista.DesiredSize.Height, "Sin sugerencias la cinta no ocupa nada");
            });
        }

        private static List<Button> BotonesDeLaPrimeraFicha(CintaSugerenciasProductosViewModel cinta)
        {
            var vista = new CintaSugerenciasProductosView { DataContext = cinta };
            vista.Measure(new Size(900, 300));
            vista.Arrange(new Rect(0, 0, 900, 300));
            vista.UpdateLayout();
            Assert.IsTrue(vista.DesiredSize.Height > 0, "Con fichas la cinta se ve");
            return Descendientes<Button>(vista).Where(b => b.Command != null && b.Command != cinta.PlegarCommand).ToList();
        }

        private static IEnumerable<T> Descendientes<T>(DependencyObject padre) where T : DependencyObject
        {
            int hijos = VisualTreeHelper.GetChildrenCount(padre);
            for (int i = 0; i < hijos; i++)
            {
                DependencyObject hijo = VisualTreeHelper.GetChild(padre, i);
                if (hijo is T encontrado)
                {
                    yield return encontrado;
                }
                foreach (T nieto in Descendientes<T>(hijo))
                {
                    yield return nieto;
                }
            }
        }

        private static void EjecutarEnSTA(Action action)
        {
            Exception capturada = null;
            var hilo = new Thread(() =>
            {
                try { action(); }
                catch (Exception ex) { capturada = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (capturada != null)
            {
                throw new AssertFailedException(capturada.Message, capturada);
            }
        }
    }
}
