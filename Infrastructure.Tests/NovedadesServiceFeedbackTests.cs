using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Shared;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Infrastructure.Tests
{
    /// <summary>
    /// NestoAPI#520: los errores del feedback llegan al usuario con el motivo que da la API, no con un
    /// «error 400» sin más.
    /// </summary>
    [TestClass]
    public class NovedadesServiceFeedbackTests
    {
        private static HttpResponseMessage Respuesta(HttpStatusCode codigo, string cuerpo)
            => new HttpResponseMessage(codigo) { Content = new StringContent(cuerpo ?? string.Empty, Encoding.UTF8, "application/json") };

        [TestMethod]
        public async Task MensajeDeError_BadRequestDeWebApi_DevuelveElMotivo()
        {
            string mensaje = await NovedadesService.MensajeDeError(
                Respuesta(HttpStatusCode.BadRequest, "{\"Message\":\"La imagen supera los 2 MB.\"}"), "publicar el comentario");

            Assert.AreEqual("No se pudo publicar el comentario: La imagen supera los 2 MB.", mensaje);
        }

        [TestMethod]
        public async Task MensajeDeError_FormatoGlobalExceptionFilter_DevuelveElMotivo()
        {
            string mensaje = await NovedadesService.MensajeDeError(
                Respuesta(HttpStatusCode.Forbidden, "{\"error\":{\"code\":\"X\",\"message\":\"Solo puedes borrar tus comentarios.\"}}"), "borrar el comentario");

            StringAssert.Contains(mensaje, "Solo puedes borrar tus comentarios.");
        }

        [TestMethod]
        public async Task MensajeDeError_SinCuerpo_DiceElCodigo()
        {
            string mensaje = await NovedadesService.MensajeDeError(Respuesta(HttpStatusCode.InternalServerError, ""), "guardar el voto");

            Assert.AreEqual("No se pudo guardar el voto (error 500).", mensaje);
        }

        [TestMethod]
        public void VersionNesto_EmpiezaPorNesto()
        {
            StringAssert.StartsWith(NovedadesService.VersionNesto(), "Nesto");
        }
    }
}
