using ControlesUsuario.Dialogs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>Nesto#571 (NestoAPI#739): el diálogo de la prueba de entrega del reparto propio.</summary>
    [TestClass]
    public class PruebaEntregaDialogViewModelTests
    {
        private static PruebaEntregaDialogViewModel Abrir(string cobro, decimal? importe, bool tieneFirma, byte[] firma)
        {
            var vm = new PruebaEntregaDialogViewModel();
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { PruebaEntregaDialogViewModel.PARAMETRO_ENVIO, 251500 },
                { PruebaEntregaDialogViewModel.PARAMETRO_PEDIDO, "930001" },
                { PruebaEntregaDialogViewModel.PARAMETRO_FECHA_HORA, new DateTime(2026, 10, 29, 12, 34, 0) },
                { PruebaEntregaDialogViewModel.PARAMETRO_RECEPTOR, " Ana López " },
                { PruebaEntregaDialogViewModel.PARAMETRO_DNI, null },
                { PruebaEntregaDialogViewModel.PARAMETRO_COBRO_REEMBOLSO, cobro },
                { PruebaEntregaDialogViewModel.PARAMETRO_IMPORTE_COBRADO, importe },
                { PruebaEntregaDialogViewModel.PARAMETRO_USUARIO, "Pedro" },
                { PruebaEntregaDialogViewModel.PARAMETRO_TIENE_FIRMA, tieneFirma },
                { PruebaEntregaDialogViewModel.PARAMETRO_FIRMA, firma }
            });
            return vm;
        }

        [TestMethod]
        public void Abrir_PintaLosDatos()
        {
            var vm = Abrir("Efectivo", 123.45M, tieneFirma: true, firma: new byte[] { 1, 2, 3 });

            Assert.AreEqual("Envío 251500 (pedido 930001)", vm.Cabecera);
            Assert.AreEqual("Prueba de entrega del envío 251500", vm.Title);
            Assert.AreEqual("Ana López", vm.Receptor);
            Assert.AreEqual("-", vm.Dni, "Sin DNI (entregó con firma), un guion");
            Assert.AreEqual("29/10/2026 a las 12:34", vm.FechaHora);
            Assert.AreEqual("Pedro", vm.Usuario);
            Assert.IsTrue(vm.HayFirma);
        }

        [TestMethod]
        public void TextoCobro_CadaFormaDeCobro()
        {
            StringAssert.StartsWith(PruebaEntregaDialogViewModel.TextoCobro("Efectivo", 123.45M), "Cobrado en efectivo: 123,45");
            StringAssert.StartsWith(PruebaEntregaDialogViewModel.TextoCobro("Tarjeta", 80M), "Cobrado con tarjeta: 80,00");
            Assert.AreEqual("No se cobró el reembolso", PruebaEntregaDialogViewModel.TextoCobro("NoCobrado", 0M));
            Assert.AreEqual("Sin reembolso", PruebaEntregaDialogViewModel.TextoCobro(null, null));
            Assert.AreEqual("Sin reembolso", PruebaEntregaDialogViewModel.TextoCobro("", null));
        }

        [TestMethod]
        public void SinFirma_DiceQueSeEntregoConDni()
        {
            var vm = Abrir(null, null, tieneFirma: false, firma: null);

            Assert.IsFalse(vm.HayFirma);
            Assert.AreEqual("Sin firma (se entregó con DNI).", vm.TextoSinFirma);
            Assert.AreEqual("Sin reembolso", vm.Cobro);
        }

        [TestMethod]
        public void FirmaQueNoSePudoLeer_LoDiceDeUsted()
        {
            var vm = Abrir(null, null, tieneFirma: true, firma: null);

            Assert.IsFalse(vm.HayFirma);
            StringAssert.Contains(vm.TextoSinFirma, "Inténtelo de nuevo");
        }

        // ---- Desde el 10/11/26, de cualquier agencia ----

        private static PruebaEntregaDialogViewModel AbrirAgencia(byte[] documento, string tipo, string url, out string abierto)
        {
            string ruta = null;
            var vm = new PruebaEntregaDialogViewModel
            {
                AbrirConElSistema = r => ruta = r,
                GuardarTemporal = (nombre, bytes) => @"C:\temporal\" + nombre
            };
            bool esImagen = documento != null && tipo != null && tipo.StartsWith("image/");
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { PruebaEntregaDialogViewModel.PARAMETRO_ENVIO, 251501 },
                { PruebaEntregaDialogViewModel.PARAMETRO_AGENCIA, "CTT" },
                { PruebaEntregaDialogViewModel.PARAMETRO_RECEPTOR, "Receptor" },
                { PruebaEntregaDialogViewModel.PARAMETRO_FIRMA, esImagen ? documento : null },
                { PruebaEntregaDialogViewModel.PARAMETRO_DOCUMENTO, documento },
                { PruebaEntregaDialogViewModel.PARAMETRO_TIPO_DOCUMENTO, tipo },
                { PruebaEntregaDialogViewModel.PARAMETRO_URL_DOCUMENTO, url }
            });
            if (vm.AbrirDocumentoCommand.CanExecute(null))
            {
                vm.AbrirDocumentoCommand.Execute(null);
            }
            abierto = ruta;
            return vm;
        }

        [TestMethod]
        public void Pdf_NoSePintaYSeAbreFuera()
        {
            var vm = AbrirAgencia(new byte[] { 0x25, 0x50 }, "application/pdf", null, out string abierto);

            Assert.IsFalse(vm.HayFirma);
            Assert.IsTrue(vm.HayDocumentoParaAbrir);
            Assert.AreEqual("Abrir el justificante (PDF)", vm.TextoAbrirDocumento);
            Assert.AreEqual(@"C:\temporal\PruebaEntrega_251501.pdf", abierto);
            Assert.IsFalse(vm.HayDatosReparto, "Sin reembolso ni usuario: no es del reparto propio");
            Assert.AreEqual("CTT", vm.Agencia);
        }

        [TestMethod]
        public void ImagenDeGls_SePintaYSePuedeAbrir()
        {
            var vm = AbrirAgencia(new byte[] { 0xFF, 0xD8 }, "image/jpeg", null, out string abierto);

            Assert.IsTrue(vm.HayFirma);
            Assert.AreEqual(@"C:\temporal\PruebaEntrega_251501.jpg", abierto);
        }

        [TestMethod]
        public void SoloEnlace_SeAbreEnElNavegador()
        {
            var vm = AbrirAgencia(null, null, "https://agencia.invalid/pod", out string abierto);

            Assert.IsFalse(vm.HayFirma);
            Assert.AreEqual("Abrir en el navegador", vm.TextoAbrirDocumento);
            Assert.AreEqual("https://agencia.invalid/pod", abierto);
        }

        [TestMethod]
        public void SinDocumento_ElBotonNoSeOfrece()
        {
            var vm = AbrirAgencia(null, null, null, out string abierto);

            Assert.IsFalse(vm.HayDocumentoParaAbrir);
            Assert.IsNull(abierto);
        }

        [TestMethod]
        public void ElAvisoDeLaApi_VaEnElHuecoDeLaImagen()
        {
            var vm = new PruebaEntregaDialogViewModel();
            vm.OnDialogOpened(new ParametrosDialogo
            {
                { PruebaEntregaDialogViewModel.PARAMETRO_ENVIO, 251501 },
                { PruebaEntregaDialogViewModel.PARAMETRO_AVISO, "GLS no da el albarán firmado de este envío." }
            });

            Assert.AreEqual("GLS no da el albarán firmado de este envío.", vm.TextoSinFirma);
        }

        [TestMethod]
        public void RepartoPropio_EnsenaReembolsoYUsuario()
        {
            var vm = Abrir("Efectivo", 10M, tieneFirma: false, firma: null);

            Assert.IsTrue(vm.HayDatosReparto);
        }

        [TestMethod]
        public void Extension_PorTipo()
        {
            Assert.AreEqual(".pdf", PruebaEntregaDialogViewModel.Extension("application/pdf"));
            Assert.AreEqual(".png", PruebaEntregaDialogViewModel.Extension("image/png"));
            Assert.AreEqual(".jpg", PruebaEntregaDialogViewModel.Extension("IMAGE/JPEG"));
            Assert.AreEqual(".pdf", PruebaEntregaDialogViewModel.Extension(null));
        }

        [TestMethod]
        public void Cerrar_CierraConOk()
        {
            var vm = Abrir(null, null, tieneFirma: false, firma: null);
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.CerrarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }
    }
}
