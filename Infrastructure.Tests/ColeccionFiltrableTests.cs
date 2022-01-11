using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Infrastructure.Tests;
using System.Collections.ObjectModel;

namespace Infrastructure.Tests
{
    [TestClass]
    public class ColeccionFiltrableTests
    {
        [TestMethod]
        public void ColeccionFiltrable_SiElFiltroAunNoEstaFijado_EmiteIgualmenteElEventoHayQueCargarDatos()
        {
            // Arrange
            ColeccionFiltrable coleccion = new();
            coleccion.TieneDatosIniciales = false;
            bool haPasado = false;
            coleccion.HayQueCargarDatos += () => { haPasado = true; };

            // Act
            coleccion.FijarFiltroCommand.Execute("hola");

            // Assert
            Assert.IsTrue(haPasado);
        }

        [TestMethod]
        public void ColeccionFiltrable_SiSeQuitaElUltimoFiltro_ElFiltroSeQuedaComoCadenaVacia()
        {
            // Arrange
            ColeccionFiltrable coleccion = new();
            coleccion.TieneDatosIniciales = false;
            coleccion.HayQueCargarDatos += () =>
            {
                coleccion.ListaOriginal = new ObservableCollection<IFiltrableItem>
                {
                    new MiClaseFiltrable()
                };
            };

            // Act
            coleccion.Filtro = "duma";
            coleccion.FijarFiltroCommand.Execute(coleccion.Filtro);
            coleccion.QuitarFiltroCommand.Execute(coleccion.Filtro);

            // Assert
            Assert.AreEqual(0, coleccion.FiltrosPuestos.Count);
            Assert.AreEqual(string.Empty, coleccion.Filtro);
        }
    }
}
