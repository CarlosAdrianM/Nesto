using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto.Models;
using Nesto.Modulos.CanalesExternos.Models;
using System.Collections.Generic;

namespace CanalesExternosTests
{
    /// <summary>
    /// Nesto#453: Herramientas → Productos deja de pedir el -1 "de memoria". El modo del precio
    /// público (fijo / descuento por defecto / mismo que el profesional) se elige en un selector y el
    /// modelo escribe en PvpIvaIncluido el valor que viaja en el PUT (importe / null / -1).
    /// </summary>
    [TestClass]
    public class ProductoCanalExternoModoPrecioTests
    {
        [TestMethod]
        public void ModoPrecio_SinValor_EsDescuentoPorDefecto()
        {
            var producto = new ProductoCanalExterno { PvpIvaIncluido = null };

            Assert.AreEqual(ModoPrecioPublico.DescuentoPorDefecto, producto.ModoPrecio);
            Assert.IsFalse(producto.EsPrecioFijo);
            Assert.AreEqual(ProductoCanalExterno.TEXTO_DESCUENTO_POR_DEFECTO, producto.PrecioPublicoTexto);
        }

        [TestMethod]
        public void ModoPrecio_ConMenosUno_EsMismoQueProfesional_YNoEnsenaMenosUnEuro()
        {
            var producto = new ProductoCanalExterno { PvpIvaIncluido = -1M };

            Assert.AreEqual(ModoPrecioPublico.MismoQueProfesional, producto.ModoPrecio);
            Assert.IsFalse(producto.EsPrecioFijo);
            Assert.AreEqual(ProductoCanalExterno.TEXTO_MISMO_QUE_PROFESIONAL, producto.PrecioPublicoTexto);
            Assert.IsFalse(producto.PrecioPublicoTexto.Contains("-1"), "La columna Revisar no debe parecer un dato corrupto");
        }

        [TestMethod]
        public void ModoPrecio_ConImportePositivo_EsPrecioFijo_YEnsenaElImporte()
        {
            var producto = new ProductoCanalExterno { PvpIvaIncluido = 12.5M };

            Assert.AreEqual(ModoPrecioPublico.PrecioFijo, producto.ModoPrecio);
            Assert.IsTrue(producto.EsPrecioFijo);
            Assert.AreEqual(12.5M.ToString("C"), producto.PrecioPublicoTexto);
        }

        [TestMethod]
        public void CambiarAMismoQueProfesional_EscribeElSentinelYMarcaDirty()
        {
            var producto = new ProductoCanalExterno { PvpIvaIncluido = 12.5M, IsDirty = false };

            producto.ModoPrecio = ModoPrecioPublico.MismoQueProfesional;

            Assert.AreEqual(Constantes.Productos.PVP_IVA_MISMO_QUE_PROFESIONAL, producto.PvpIvaIncluido);
            Assert.AreEqual(-1M, producto.PvpIvaIncluido, "Es lo que espera NestoAPI en el PUT");
            Assert.IsTrue(producto.IsDirty);
        }

        [TestMethod]
        public void CambiarADescuentoPorDefecto_EscribeNullYMarcaDirty()
        {
            var producto = new ProductoCanalExterno { PvpIvaIncluido = -1M, IsDirty = false };

            producto.ModoPrecio = ModoPrecioPublico.DescuentoPorDefecto;

            Assert.IsNull(producto.PvpIvaIncluido);
            Assert.IsTrue(producto.IsDirty);
        }

        [TestMethod]
        public void CambiarAPrecioFijo_ProponeElPublicoQueCalculaLaApi()
        {
            var producto = new ProductoCanalExterno
            {
                PvpIvaIncluido = -1M,
                ProductoCompleto = new ProductoModel { PrecioPublicoFinal = 19.95M },
                IsDirty = false
            };

            producto.ModoPrecio = ModoPrecioPublico.PrecioFijo;

            Assert.AreEqual(19.95M, producto.PvpIvaIncluido);
            Assert.IsTrue(producto.EsPrecioFijo);
            Assert.IsTrue(producto.IsDirty);
        }

        [TestMethod]
        public void CambiarAPrecioFijo_SinFichaCompleta_DejaCeroParaQueSeTeclee()
        {
            var producto = new ProductoCanalExterno { PvpIvaIncluido = null, ProductoCompleto = null };

            producto.ModoPrecio = ModoPrecioPublico.PrecioFijo;

            Assert.AreEqual(0M, producto.PvpIvaIncluido);
            Assert.IsTrue(producto.EsPrecioFijo, "Con 0 la caja del importe tiene que estar visible para teclear el precio");
        }

        [TestMethod]
        public void CambiarAlMismoModo_NoTocaElImporteNiMarcaDirty()
        {
            var producto = new ProductoCanalExterno { PvpIvaIncluido = 12.5M, IsDirty = false };

            producto.ModoPrecio = ModoPrecioPublico.PrecioFijo;

            Assert.AreEqual(12.5M, producto.PvpIvaIncluido);
            Assert.IsFalse(producto.IsDirty);
        }

        [TestMethod]
        public void CambiarPvpIvaIncluido_AvisaDeQueCambianModoVisibilidadYTexto()
        {
            var producto = new ProductoCanalExterno { PvpIvaIncluido = 12.5M };
            var avisadas = new List<string>();
            producto.PropertyChanged += (s, e) => avisadas.Add(e.PropertyName);

            producto.PvpIvaIncluido = -1M;

            CollectionAssert.Contains(avisadas, nameof(ProductoCanalExterno.ModoPrecio));
            CollectionAssert.Contains(avisadas, nameof(ProductoCanalExterno.EsPrecioFijo));
            CollectionAssert.Contains(avisadas, nameof(ProductoCanalExterno.PrecioPublicoTexto));
        }
    }
}
