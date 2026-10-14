using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Navegacion;
using System;
using System.Threading;
using System.Windows.Controls;

namespace Nesto.Infrastructure.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4, shell): la X de una pestaña (<c>CloseTabAction</c>) ya no toca Prism: pregunta al ViewModel si es
    /// un <see cref="ITabCloseConfirmation"/> y quita la vista con <see cref="IServicioNavegacion.CerrarVista"/>.
    /// </summary>
    [TestClass]
    public class CierrePestanasTests
    {
        private sealed class ConConfirmacion : ITabCloseConfirmation
        {
            public bool Respuesta { get; set; }
            public bool ConfirmTabClose() => Respuesta;
        }

        private static void EnSta(Action accion)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try { accion(); } catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }

        [TestMethod]
        public void Cerrar_SinConfirmacion_QuitaLaVistaDeLaRegion()
        {
            EnSta(() =>
            {
                var navegacion = A.Fake<IServicioNavegacion>();
                var vista = new Border { DataContext = new object() };

                Assert.IsTrue(CierrePestanas.Cerrar(navegacion, "MainRegion", vista));

                A.CallTo(() => navegacion.CerrarVista("MainRegion", vista)).MustHaveHappenedOnceExactly();
            });
        }

        [TestMethod]
        public void Cerrar_SiElViewModelDiceQueNo_NoLaQuita()
        {
            EnSta(() =>
            {
                var navegacion = A.Fake<IServicioNavegacion>();
                var vista = new Border { DataContext = new ConConfirmacion { Respuesta = false } };

                Assert.IsFalse(CierrePestanas.Cerrar(navegacion, "MainRegion", vista));

                A.CallTo(() => navegacion.CerrarVista(A<string>._, A<object>._)).MustNotHaveHappened();
            });
        }

        [TestMethod]
        public void Cerrar_SiElViewModelDiceQueSi_LaQuita()
        {
            EnSta(() =>
            {
                var navegacion = A.Fake<IServicioNavegacion>();
                var vista = new Border { DataContext = new ConConfirmacion { Respuesta = true } };

                Assert.IsTrue(CierrePestanas.Cerrar(navegacion, "MainRegion", vista));

                A.CallTo(() => navegacion.CerrarVista("MainRegion", vista)).MustHaveHappenedOnceExactly();
            });
        }

        [TestMethod]
        public void Cerrar_NoUsaNingunTipoDePrism()
        {
            foreach (var tipo in new[] { typeof(CierrePestanas) })
            {
                foreach (var metodo in tipo.GetMethods())
                {
                    foreach (var parametro in metodo.GetParameters())
                    {
                        Assert.IsFalse(parametro.ParameterType.Namespace?.StartsWith("Prism") ?? false, metodo.Name);
                    }
                }
            }
            Assert.AreEqual("MainRegion", CierrePestanas.REGION_PRINCIPAL);
        }
    }
}
