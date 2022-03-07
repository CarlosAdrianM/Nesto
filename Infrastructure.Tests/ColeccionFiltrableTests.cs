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
        public void ColeccionFiltrable_SiSeQuitaElFiltroActual_ElFiltroSeQuedaComoCadenaVacia()
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

        [TestMethod]
        public void ColeccionFiltrable_SiSePoneUnFiltro_NoDiferenciaMaysDeMins()
        {
            // Arrange
            ColeccionFiltrable coleccion = new();
            coleccion.TieneDatosIniciales = true;
            coleccion.ListaOriginal =new ObservableCollection<IFiltrableItem>
            {
                new MiClaseFiltrable
                {
                    Nombre = "Carlos"
                }
            };

            // Act
            coleccion.Filtro = "A";
            coleccion.FijarFiltroCommand.Execute(coleccion.Filtro);

            // Assert
            Assert.AreEqual(1, coleccion.Lista.Count);
        }

        [TestMethod]
        public void ColeccionFiltrable_SiNoTieneDatosInicialesYSeQuitaUnFiltro_ElPrimerFiltroNoSeVuelveAAplicar()
        {
            // El primer filtro es el que carga de la base de datos, por lo que en realidad se aplica en la API
            // Esto no es necesario si el Contains() de la clase IFiltrableItem coincide con el filtro de la API

            // Arrange
            ColeccionFiltrable coleccion = new();
            coleccion.TieneDatosIniciales = false;
            coleccion.HayQueCargarDatos += () =>
            {
                coleccion.ListaOriginal = new ObservableCollection<IFiltrableItem>
                {
                    new MiClaseFiltrable
                    {
                        Nombre = "Alejandro",
                        Apellido = "Dumas"
                    },
                    new MiClaseFiltrable
                    {
                        Nombre = "Dumas",
                        Apellido = "Otro"
                    }
                };
            };

            // Act
            coleccion.Filtro = "duma";
            coleccion.FijarFiltroCommand.Execute(coleccion.Filtro);
            coleccion.Filtro = "alej";
            coleccion.FijarFiltroCommand.Execute(coleccion.Filtro);
            coleccion.QuitarFiltroCommand.Execute(coleccion.Filtro);

            // Assert
            Assert.AreEqual(1, coleccion.FiltrosPuestos.Count);
            Assert.AreEqual(2, coleccion.Lista.Count);
        }

        [TestMethod]
        public void ColeccionFiltrable_SiSoloHayUnElementoEnListaOriginal_LoSeleccionamos()
        {
            // Arrange
            ColeccionFiltrable coleccion = new();
            coleccion.TieneDatosIniciales = false;
            MiClaseFiltrable alejandroDumas = new MiClaseFiltrable
            {
                Nombre = "Alejandro",
                Apellido = "Dumas"
            };
            coleccion.HayQueCargarDatos += () =>
            {
                coleccion.ListaOriginal = new ObservableCollection<IFiltrableItem>
                {
                    alejandroDumas
                };
            };

            // Act
            coleccion.Filtro = "alej";
            coleccion.FijarFiltroCommand.Execute(coleccion.Filtro);

            // Assert
            Assert.AreEqual(alejandroDumas, coleccion.ElementoSeleccionado);
            Assert.AreEqual(1, coleccion.ListaOriginal.Count);
        }

        [TestMethod]
        public void ColeccionFiltrable_SiNoTieneDatosInicialesYPonemosUnFiltro_SeleccionamosElPrimerElementoDevuelto()
        {
            // Arrange
            ColeccionFiltrable coleccion = new();
            coleccion.TieneDatosIniciales = true;
            MiClaseFiltrable alejandroDumas = new MiClaseFiltrable
            {
                Nombre = "Alejandro",
                Apellido = "Dumas"
            };
            
            coleccion.ListaOriginal = new ObservableCollection<IFiltrableItem>
            {
                new MiClaseFiltrable {Nombre = "Miguel", Apellido = "Cervantes"},
                alejandroDumas,
                new MiClaseFiltrable {Nombre = "Alejandro", Apellido = "Góngora"},
                new MiClaseFiltrable {Nombre = "Alejo", Apellido = "Quevedo"}
            };


            // Act
            coleccion.Filtro = "alej";
            coleccion.FijarFiltroCommand.Execute(coleccion.Filtro);

            // Assert
            Assert.AreEqual(alejandroDumas, coleccion.ElementoSeleccionado);
        }
    }
}
