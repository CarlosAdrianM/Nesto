using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;

namespace Infrastructure.Tests
{
    /// <summary>
    /// NestoAPI#453: este helper es el que convierte la respuesta de error del servidor en algo
    /// que el usuario pueda leer. De él dependen la descarga del PDF de un pedido y la de las
    /// facturas del cliente, que hasta ahora se tragaban el motivo real: una decía
    /// "value cannot be null. (Parameter buffer)" y la otra no decía nada.
    ///
    /// <para>El caso importante es el de texto plano: FacturasController responde con
    /// <c>new StringContent(ex.Message)</c>, sin JSON, así que el mensaje tiene que salir tal
    /// cual y no como "Error desconocido".</para>
    /// </summary>
    [TestClass]
    public class HttpErrorHelperTests
    {
        private const string MENSAJE_REAL = "No cuadran los vencimientos con el total de la factura";

        [TestMethod]
        public void ParsearErrorHttp_TextoPlano_LoDevuelveTalCual()
        {
            // Lo que manda FacturasController en su BadRequest
            Assert.AreEqual(MENSAJE_REAL, HttpErrorHelper.ParsearErrorHttp(MENSAJE_REAL));
        }

        [TestMethod]
        public void ParsearErrorHttp_TextoPlanoConDetalle_NoLoRecorta()
        {
            const string conDetalle = "No cuadran los vencimientos con el total de la factura. " +
                "Total calculado: 594,80€, Suma vencimientos: 594,81€, Diferencia: -0,01€";

            Assert.AreEqual(conDetalle, HttpErrorHelper.ParsearErrorHttp(conDetalle));
        }

        [TestMethod]
        public void ParsearErrorHttp_BadRequestDeWebApi_SacaElMessage()
        {
            string json = "{\"Message\":\"" + MENSAJE_REAL + "\"}";

            Assert.AreEqual(MENSAJE_REAL, HttpErrorHelper.ParsearErrorHttp(json));
        }

        [TestMethod]
        public void ParsearErrorHttp_FormatoDelGlobalExceptionFilter_SacaElMensajeYElCodigo()
        {
            string json = "{\"error\":{\"code\":\"FACTURACION_DESCUADRE\",\"message\":\"" + MENSAJE_REAL + "\"}}";

            Assert.AreEqual("[FACTURACION_DESCUADRE] " + MENSAJE_REAL,
                HttpErrorHelper.ParsearErrorHttp(json));
        }

        [TestMethod]
        public void ParsearErrorHttp_FormatoAntiguo_EncadenaLasInnerException()
        {
            string json = "{\"ExceptionMessage\":\"Error al generar la factura\"," +
                          "\"InnerException\":{\"ExceptionMessage\":\"" + MENSAJE_REAL + "\"}}";

            string resultado = HttpErrorHelper.ParsearErrorHttp(json);

            StringAssert.Contains(resultado, "Error al generar la factura");
            StringAssert.Contains(resultado, MENSAJE_REAL);
        }

        [TestMethod]
        public void ParsearErrorHttp_CuerpoVacio_NoDevuelveCadenaVacia()
        {
            // Un mensaje vacío deja al usuario igual de perdido que el "parameter buffer"
            Assert.AreEqual("Error desconocido al comunicarse con el servidor",
                HttpErrorHelper.ParsearErrorHttp(string.Empty));
            Assert.AreEqual("Error desconocido al comunicarse con el servidor",
                HttpErrorHelper.ParsearErrorHttp((string)null));
        }
    }
}
