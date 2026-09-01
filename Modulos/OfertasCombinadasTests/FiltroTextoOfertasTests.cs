using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;

namespace Nesto.Modulos.OfertasCombinadasTests
{
    /// <summary>
    /// Petición 01/09/26: filtro de texto por pestaña en la ventana de Ofertas y descuentos
    /// (buscar "apraise" o "level" y ver solo esas ofertas). Es un filtro LOCAL sobre la
    /// colección ya cargada (ICollectionView), sin volver a la API, e ignora mayúsculas y
    /// acentos.
    /// </summary>
    [TestClass]
    public class FiltroTextoOfertasTests
    {
        private static ObservableCollection<OfertaCombinadaWrapper> TresOfertas()
        {
            return new ObservableCollection<OfertaCombinadaWrapper>
            {
                new OfertaCombinadaWrapper { Nombre = "Apraise 6+2 primavera" },
                new OfertaCombinadaWrapper { Nombre = "Level lote lacas" },
                new OfertaCombinadaWrapper { Nombre = "Ganavisiones otoño" }
            };
        }

        private static int Visibles<T>(ObservableCollection<T> coleccion) =>
            CollectionViewSource.GetDefaultView(coleccion).Cast<object>().Count();

        [TestMethod]
        public void AplicarFiltro_PorNombre_DejaSoloLasQueCoinciden()
        {
            ObservableCollection<OfertaCombinadaWrapper> ofertas = TresOfertas();

            OfertasCombinadasViewModel.AplicarFiltro(ofertas, "apraise",
                OfertasCombinadasViewModel.CoincideOfertaCombinada);

            Assert.AreEqual(1, Visibles(ofertas));
        }

        [TestMethod]
        public void AplicarFiltro_IgnoraMayusculasYAcentos()
        {
            ObservableCollection<OfertaCombinadaWrapper> ofertas = TresOfertas();

            OfertasCombinadasViewModel.AplicarFiltro(ofertas, "LEVEL",
                OfertasCombinadasViewModel.CoincideOfertaCombinada);
            Assert.AreEqual(1, Visibles(ofertas), "Mayúsculas");

            OfertasCombinadasViewModel.AplicarFiltro(ofertas, "otono",
                OfertasCombinadasViewModel.CoincideOfertaCombinada);
            Assert.AreEqual(1, Visibles(ofertas), "'otono' encuentra 'otoño'");
        }

        [TestMethod]
        public void AplicarFiltro_VacioOEspacios_QuitaElFiltro()
        {
            ObservableCollection<OfertaCombinadaWrapper> ofertas = TresOfertas();
            OfertasCombinadasViewModel.AplicarFiltro(ofertas, "apraise",
                OfertasCombinadasViewModel.CoincideOfertaCombinada);

            OfertasCombinadasViewModel.AplicarFiltro(ofertas, "   ",
                OfertasCombinadasViewModel.CoincideOfertaCombinada);

            Assert.AreEqual(3, Visibles(ofertas));
        }

        [TestMethod]
        public void CoincideCampana_BuscaEnCampanaProductoFamiliaYGrupo()
        {
            CampanaWrapper campana = new CampanaWrapper
            {
                Campana = "Rebajas verano 2026",
                Producto = "44166",
                Familia = "Ufaes",
                Grupo = "COS"
            };

            Assert.IsTrue(OfertasCombinadasViewModel.CoincideCampana(campana, "rebajas"));
            Assert.IsTrue(OfertasCombinadasViewModel.CoincideCampana(campana, "44166"));
            Assert.IsTrue(OfertasCombinadasViewModel.CoincideCampana(campana, "ufaes"));
            Assert.IsFalse(OfertasCombinadasViewModel.CoincideCampana(campana, "level"));
        }

        [TestMethod]
        public void CoincideOfertaProducto_BuscaTambienPorNombreDeProducto()
        {
            OfertaProductoWrapper oferta = new OfertaProductoWrapper
            {
                Producto = "44724",
                ProductoNombre = "LACA APRAISE FIJACIÓN FUERTE"
            };

            Assert.IsTrue(OfertasCombinadasViewModel.CoincideOfertaProducto(oferta, "apraise"));
            Assert.IsTrue(OfertasCombinadasViewModel.CoincideOfertaProducto(oferta, "fijacion"), "Sin acento también");
            Assert.IsFalse(OfertasCombinadasViewModel.CoincideOfertaProducto(oferta, "level"));
        }
    }
}
