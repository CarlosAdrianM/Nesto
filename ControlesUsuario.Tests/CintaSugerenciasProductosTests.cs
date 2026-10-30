using ControlesUsuario;
using ControlesUsuario.SugerenciasProductos;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
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

        // NestoAPI#751: lo que gana el vendedor marcando, en la cabecera

        private static IServicioSugerenciasProductos ServicioConResumen(params string[] frases)
        {
            var servicio = A.Fake<IServicioSugerenciasProductos>();
            var respuestas = frases.Select(f => new ResumenSugerenciasVendedorDTO { Frase = f, Ayuda = "Detalle de " + f }).ToArray();
            A.CallTo(() => servicio.MiResumen()).ReturnsNextFromSequence(respuestas);
            A.CallTo(() => servicio.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO()));
            return servicio;
        }

        [TestMethod]
        public async Task Resumen_SePideUnaVezAlLlegarLasFichasYSeEnsenaConSuAyuda()
        {
            IServicioSugerenciasProductos servicio = ServicioConResumen("Cuanto más marque ✓ o ✗, más acierta con sus clientes.");
            CintaSugerenciasProductosViewModel cinta = Cinta(null);
            cinta.Servicio = servicio;
            Assert.IsFalse(cinta.HayResumen);

            cinta.Aplicar(new[] { Ficha(10, "38000", 1) });
            cinta.Aplicar(new[] { Ficha(11, "38001", 1) });
            await Task.Yield();

            A.CallTo(() => servicio.MiResumen()).MustHaveHappenedOnceExactly();
            Assert.IsTrue(cinta.HayResumen);
            Assert.AreEqual("Cuanto más marque ✓ o ✗, más acierta con sus clientes.", cinta.FraseResumen);
            Assert.AreEqual("Detalle de Cuanto más marque ✓ o ✗, más acierta con sus clientes.", cinta.AyudaResumen);
        }

        [TestMethod]
        public async Task Resumen_SeRefrescaAlContestarParaQueVeaQueCuenta()
        {
            IServicioSugerenciasProductos servicio = ServicioConResumen("Antes", "Esta semana ha contestado 1 sugerencia: gracias.");
            CintaSugerenciasProductosViewModel cinta = Cinta(null);
            cinta.Servicio = servicio;
            SugerenciaProductoDTO ficha = Ficha(10, "38000", 1);
            cinta.Aplicar(new[] { ficha });
            await Task.Yield();
            Assert.AreEqual("Antes", cinta.FraseResumen);

            await cinta.RechazarAsync(ficha);
            await Task.Yield();

            A.CallTo(() => servicio.MiResumen()).MustHaveHappenedTwiceExactly();
            StringAssert.StartsWith(cinta.FraseResumen, "Esta semana ha contestado 1 sugerencia");
        }

        [TestMethod]
        public async Task Resumen_SiLaApiFallaNoSaleNada()
        {
            var servicio = A.Fake<IServicioSugerenciasProductos>();
            A.CallTo(() => servicio.MiResumen()).Returns(Task.FromResult<ResumenSugerenciasVendedorDTO>(null));
            CintaSugerenciasProductosViewModel cinta = Cinta(null);
            cinta.Servicio = servicio;

            cinta.Aplicar(new[] { Ficha(10, "38000", 1) });
            await cinta.CargarResumenAsync();

            Assert.IsFalse(cinta.HayResumen);
            Assert.IsNull(cinta.FraseResumen);
        }

        [TestMethod]
        public void Resumen_LaVistaEnsenaLaFraseEnLaCabecera()
        {
            EjecutarEnSTA(() =>
            {
                IServicioSugerenciasProductos servicio = ServicioConResumen("Esta semana: 2 productos añadidos desde las sugerencias.");
                CintaSugerenciasProductosViewModel cinta = Cinta(null);
                cinta.Servicio = servicio;
                cinta.Aplicar(new[] { Ficha(10, "38000", 1) });
                cinta.CargarResumenAsync().GetAwaiter().GetResult();

                var vista = new CintaSugerenciasProductosView { DataContext = cinta };
                vista.Measure(new Size(900, 300));
                vista.Arrange(new Rect(0, 0, 900, 300));
                vista.UpdateLayout();

                TextBlock frase = Descendientes<TextBlock>(vista).Single(t => t.Name == "FraseResumen");
                Assert.AreEqual(Visibility.Visible, frase.Visibility);
                StringAssert.StartsWith(frase.Text, "Esta semana: 2 productos");
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
