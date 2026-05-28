using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using System;
using System.Threading;

namespace ControlesUsuario.Tests
{
    [TestClass]
    public class BarraFiltroTests
    {
        // Regresión: al cerrar la ventana, el RegionBehavior limpia el DataContext y el binding
        // pone BarraFiltro.ListaItems a null. OnListaItemsChanged debe ser null-safe; antes lanzaba
        // NullReferenceException al acceder a ListaItems.ElementoSeleccionado con ListaItems = null.
        [TestMethod]
        public void BarraFiltro_AlPonerListaItemsANull_NoLanzaExcepcion()
        {
            EjecutarEnSTA(() =>
            {
                var barra = new BarraFiltro
                {
                    ListaItems = new ColeccionFiltrable()
                };

                // Act: simula el teardown (DataContext -> null deja el binding ListaItems = null)
                barra.ListaItems = null;

                // Assert: si llega aquí sin excepción, el callback es null-safe
                Assert.IsNull(barra.ListaItems);
            });
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
