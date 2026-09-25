using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#486: qué cuenta se carga con recibo bancario y cuándo se avisa de que el recibo no
    /// llegará al banco. Misma regla que la remesa (ficha no de baja + IBAN correcto) y mismos
    /// textos que NestoApp#189.
    /// </summary>
    [TestClass]
    public class ReglaCCCReciboTests
    {
        private static CCCItem Valida(string numero = "1") => new CCCItem
        {
            numero = numero,
            estado = 1,
            ibanFormateado = "ES91 2100 0418 4502 0005 4321",
            entidad = "2100",
            nombreEntidad = "CaixaBank"
        };

        private static CCCItem DeBaja(string numero = "2") => new CCCItem
        {
            numero = numero,
            estado = -1,
            ibanFormateado = "ES91 2100 0418 4502 0005 1332",
            entidad = "2100"
        };

        private static CCCItem IbanMal(string numero = "3") => new CCCItem
        {
            numero = numero,
            estado = 1,
            ibanFormateado = null, // la API lo deja a null cuando el IBAN no pasa el mod-97
            entidad = "0049"
        };

        [TestMethod]
        public void Aviso_SinReciboBancario_NoAvisaNunca()
        {
            Assert.IsNull(ReglaCCCRecibo.Aviso("EFC", new List<CCCItem>(), null));
            Assert.IsNull(ReglaCCCRecibo.Aviso("TRN", new List<CCCItem> { DeBaja() }, "2"));
            Assert.IsNull(ReglaCCCRecibo.Aviso(null, null, null));
        }

        [TestMethod]
        public void Aviso_ReciboSinNingunaCuenta_AvisaQueNoIraAlBanco()
        {
            Assert.AreEqual(ReglaCCCRecibo.AVISO_SIN_CUENTA_VALIDA,
                ReglaCCCRecibo.Aviso("RCB", new List<CCCItem>(), null));
            Assert.AreEqual(
                "Este cliente no tiene cuenta bancaria válida: el recibo NO se podrá mandar al banco. Pide la cuenta o elige otra forma de pago.",
                ReglaCCCRecibo.AVISO_SIN_CUENTA_VALIDA);
        }

        [TestMethod]
        public void Aviso_ReciboConTodasDeBajaORechazadas_AvisaQueNoTieneCuentaValida()
        {
            // NestoAPI#502: la remesa retiene los efectos de una ficha de baja o con IBAN mal.
            var cccs = new List<CCCItem> { DeBaja("2"), IbanMal("3") };

            Assert.AreEqual(ReglaCCCRecibo.AVISO_SIN_CUENTA_VALIDA, ReglaCCCRecibo.Aviso("RCB ", cccs, "2"));
        }

        [TestMethod]
        public void Aviso_ReciboConCuentaValidaElegida_NoAvisa()
        {
            var cccs = new List<CCCItem> { DeBaja("2"), Valida("1") };

            // El ccc del pedido llega de un char relleno con espacios (#254)
            Assert.IsNull(ReglaCCCRecibo.Aviso("RCB", cccs, "1  "));
        }

        [TestMethod]
        public void Aviso_ReciboConCuentaDeBajaElegidaPeroOtraValida_PideElegirOtra()
        {
            var cccs = new List<CCCItem> { DeBaja("2"), Valida("1") };

            Assert.AreEqual(ReglaCCCRecibo.AVISO_CUENTA_ELEGIDA_NO_VALIDA, ReglaCCCRecibo.Aviso("RCB", cccs, "2"));
        }

        [TestMethod]
        public void Aviso_ReciboSinCuentaElegidaPeroConValidas_PideElegirUna()
        {
            var cccs = new List<CCCItem> { Valida("1") };

            Assert.AreEqual(ReglaCCCRecibo.AVISO_SIN_CUENTA_ELEGIDA, ReglaCCCRecibo.Aviso("RCB", cccs, null));
        }

        [TestMethod]
        public void Resumen_EnseñaPaisUltimosDigitosYEntidad()
        {
            Assert.AreEqual("ES91 …… 4321 — CaixaBank", ReglaCCCRecibo.Resumen(Valida()));
        }

        [TestMethod]
        public void Resumen_SinNombreDeEntidad_UsaElCodigo()
        {
            var ccc = Valida();
            ccc.nombreEntidad = null;

            Assert.AreEqual("ES91 …… 4321 — 2100", ReglaCCCRecibo.Resumen(ccc));
        }

        [TestMethod]
        public void TextoCuentaACargar_SoloConReciboYCuentaValida()
        {
            var cccs = new List<CCCItem> { Valida("1"), DeBaja("2") };

            Assert.AreEqual("Se cargará en: ES91 …… 4321 — CaixaBank", ReglaCCCRecibo.TextoCuentaACargar("RCB", cccs, "1"));
            Assert.IsNull(ReglaCCCRecibo.TextoCuentaACargar("RCB", cccs, "2"));
            Assert.IsNull(ReglaCCCRecibo.TextoCuentaACargar("EFC", cccs, "1"));
        }

        private static (string ccc, string aviso, bool hayAviso, string texto) CargarSelector(IEnumerable<CCCItem> cccs, string formaPago, string cccPrevio)
        {
            var servicioCCC = A.Fake<IServicioCCC>();
            A.CallTo(() => servicioCCC.ObtenerCCCs("1", "10", "0"))
                .Returns(Task.FromResult(cccs));

            (string, string, bool, string) resultado = default;
            var thread = new Thread(() =>
            {
                var sut = new SelectorCCC(servicioCCC);
                sut.CCCSeleccionado = cccPrevio;
                sut.FormaPago = formaPago;
                sut.MostrarAviso = true;
                sut.Contacto = "0";
                sut.Cliente = "10";
                sut.Empresa = "1";
                // Los DependencyProperty solo se leen desde el hilo que creó el control
                resultado = (sut.CCCSeleccionado, sut.AvisoRecibo, sut.HayAvisoRecibo, sut.TextoCuentaACargar);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return resultado;
        }

        [TestMethod]
        public void SelectorCCC_ReciboSinCuentasValidas_ExponeElAviso()
        {
            var sut = CargarSelector(new List<CCCItem> { DeBaja("2") }, "RCB", null);

            Assert.AreEqual(ReglaCCCRecibo.AVISO_SIN_CUENTA_VALIDA, sut.aviso);
            Assert.IsTrue(sut.hayAviso);
            Assert.IsNull(sut.texto);
        }

        [TestMethod]
        public void SelectorCCC_ReciboConLaCuentaDeLaFicha_EnseñaEsaYNoAvisa()
        {
            // La ficha del contacto dice "3" aunque "1" salga antes en la lista: se respeta.
            var sut = CargarSelector(new List<CCCItem> { Valida("1"), Valida("3") }, "RCB", "3  ");

            Assert.AreEqual("3", sut.ccc, "Nesto#494: la cuenta del pedido, normalizada al número de la lista para que el combo la enseñe");
            Assert.IsNull(sut.aviso);
            Assert.AreEqual("Se cargará en: ES91 …… 4321 — CaixaBank", sut.texto);
        }

        [TestMethod]
        public void SelectorCCC_ReciboSinCuentaEnLaFicha_EligeUnaQueValgaParaElBanco()
        {
            // Antes elegía la primera no de baja aunque su IBAN no pasara el mod-97.
            var sut = CargarSelector(new List<CCCItem> { IbanMal("3"), Valida("1") }, "RCB", null);

            Assert.AreEqual("1", sut.ccc);
            Assert.IsNull(sut.aviso);
        }

        [TestMethod]
        public void SelectorCCC_SinRecibo_NoAvisa()
        {
            var sut = CargarSelector(new List<CCCItem>(), "EFC", null);

            Assert.IsNull(sut.aviso);
            Assert.IsFalse(sut.hayAviso);
        }
    }
}
