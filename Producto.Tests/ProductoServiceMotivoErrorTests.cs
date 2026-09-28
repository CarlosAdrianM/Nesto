using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modules.Producto;
using Newtonsoft.Json.Linq;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#479: el motivo de un error de NestoAPI llega en "ExceptionMessage" (500) o en "Message"
    /// (BadRequest con texto), en PascalCase. Antes se buscaba "exceptionMessage" y no casaba nunca.
    /// </summary>
    [TestClass]
    public class ProductoServiceMotivoErrorTests
    {
        [TestMethod]
        public void MotivoDelError_BadRequestConTexto_DevuelveElMessage()
        {
            JObject cuerpo = JObject.Parse("{\"Message\":\"No hay cantidad suficiente para montar el kit\"}");
            Assert.AreEqual("No hay cantidad suficiente para montar el kit", ProductoService.MotivoDelError(cuerpo));
        }

        [TestMethod]
        public void MotivoDelError_Error500_PrefiereElExceptionMessageAlGenerico()
        {
            JObject cuerpo = JObject.Parse("{\"Message\":\"An error has occurred.\",\"ExceptionMessage\":\"La ubicación está descuadrada\"}");
            Assert.AreEqual("La ubicación está descuadrada", ProductoService.MotivoDelError(cuerpo));
        }

        [TestMethod]
        public void MotivoDelError_CamelCaseOSinCuerpo()
        {
            Assert.AreEqual("x", ProductoService.MotivoDelError(JObject.Parse("{\"exceptionMessage\":\"x\"}")));
            Assert.IsNull(ProductoService.MotivoDelError(null));
            Assert.IsNull(ProductoService.MotivoDelError(JObject.Parse("{}")));
        }

        [TestMethod]
        public void MotivoDelErrorDeTexto_BadRequestO403ConCadena()
        {
            // NestoAPI#545: 400 llega como {"Message": "..."}; 403 con Content(...) como cadena JSON suelta.
            Assert.AreEqual("Usa la baja", ProductoService.MotivoDelErrorDeTexto("{\"Message\":\"Usa la baja\"}"));
            Assert.AreEqual("Solo Dirección", ProductoService.MotivoDelErrorDeTexto("\"Solo Dirección\""));
            Assert.IsNull(ProductoService.MotivoDelErrorDeTexto(""));
        }
    }
}
