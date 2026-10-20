using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoCompra.Models;
using System;

namespace PedidoCompraTests
{
    /// <summary>
    /// Nesto#536 / NestoAPI#668: el 409 «FechaCompraNoPermitida» de CrearAlbaranYFactura se convierte en
    /// una excepción con las dos fechas; cualquier otro error sigue como siempre.
    /// </summary>
    [TestClass]
    public class FechaCompraNoPermitidaExceptionTests
    {
        [TestMethod]
        public void DesdeRespuesta_409ConCodigo_DevuelveLasFechasYElMensaje()
        {
            string cuerpo = "{\"Codigo\":\"FechaCompraNoPermitida\",\"Fecha\":\"2026-09-30T00:00:00\"," +
                "\"FechaPropuesta\":\"2026-10-01T00:00:00\",\"Message\":\"Las compras están cerradas hasta el 30/09/2026\"}";

            FechaCompraNoPermitidaException ex = FechaCompraNoPermitidaException.DesdeRespuesta(409, cuerpo);

            Assert.IsNotNull(ex);
            Assert.AreEqual(new DateTime(2026, 9, 30), ex.Fecha);
            Assert.AreEqual(new DateTime(2026, 10, 1), ex.FechaPropuesta);
            Assert.AreEqual("Las compras están cerradas hasta el 30/09/2026", ex.Message);
        }

        [TestMethod]
        public void DesdeRespuesta_CamelCase_TambienLaReconoce()
        {
            string cuerpo = "{\"codigo\":\"FechaCompraNoPermitida\",\"fecha\":\"2026-09-30T00:00:00\",\"fechaPropuesta\":\"2026-10-01T00:00:00\"}";

            Assert.IsNotNull(FechaCompraNoPermitidaException.DesdeRespuesta(409, cuerpo));
        }

        [TestMethod]
        public void DesdeRespuesta_OtrosErrores_DevuelveNull()
        {
            // Otro 409, el 500 de siempre o un cuerpo que no es JSON: se tratan como hasta ahora
            Assert.IsNull(FechaCompraNoPermitidaException.DesdeRespuesta(409, "{\"Message\":\"El envío ya está tramitado.\"}"));
            Assert.IsNull(FechaCompraNoPermitidaException.DesdeRespuesta(500,
                "{\"Codigo\":\"FechaCompraNoPermitida\",\"Fecha\":\"2026-09-30T00:00:00\",\"FechaPropuesta\":\"2026-10-01T00:00:00\"}"));
            Assert.IsNull(FechaCompraNoPermitidaException.DesdeRespuesta(409, "Fecha no permitida"));
            Assert.IsNull(FechaCompraNoPermitidaException.DesdeRespuesta(409, "{\"Codigo\":\"FechaCompraNoPermitida\",\"Fecha\":\"2026-09-30T00:00:00\",\"FechaPropuesta\":null}"));
        }
    }
}
