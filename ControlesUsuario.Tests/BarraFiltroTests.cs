using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ControlesUsuario.Tests
{
    [TestClass]
    public class BarraFiltroTests
    {
        [TestMethod]
        public void BarraFiltro_AlCargarUnSoloElemento_NoBorraLaListaOriginal()
        {
            //// Arrange
            //var dispatcher = Application.Current.MainWindow.Dispatcher;
            //// Or use this.Dispatcher if this method is in a window class.

            //dispatcher.BeginInvoke(new Action(() =>
            //{
            //    var barra = new BarraFiltro();

            //    var lista = new ObservableCollection<FiltrableItemSample>
            //    {
            //        new FiltrableItemSample()
            //    };
            //    barra.ListaItems.ListaOriginal = new ObservableCollection<IFiltrableItem>(lista);

            //    // Act

            //    // Assert
            //    Assert.AreEqual(1, barra.ListaItems.ListaOriginal.Count);
            //}));            
        }
    }
}
