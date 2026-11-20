using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Newtonsoft.Json.Linq;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Carlos 20/11/26: «Añadir nota» a un envío (POST api/EnviosAgencias/{id}/Nota) y cómo se leen los errores de los endpoints nuevos
    /// del reparto propio (un servidor anterior no los tiene: se dice con claridad y la pantalla sigue).
    /// </summary>
    [TestClass]
    public class NotasEnvioTests
    {
        [TestMethod]
        public void RutaYCuerpo_ContratoConElEndpoint()
        {
            Assert.AreEqual("EnviosAgencias/251104/Nota", ServicioNotasEnvio.RutaNota(251104));
            Assert.AreEqual("Llamar antes", (string)JObject.Parse(ServicioNotasEnvio.CuerpoNota("Llamar antes"))["Texto"]);
        }

        [TestMethod]
        public void Excepcion_RutaQueNoExiste_ServidorSinActualizar()
        {
            var ex = ErroresApiReparto.Excepcion(404, "{\"Message\":\"No HTTP resource was found that matches the request URI 'x'.\"}", null);

            Assert.IsTrue(ex.ServidorSinActualizar);
            Assert.AreEqual("Esto estará disponible cuando se actualice el servidor.", ex.Message);
        }

        [TestMethod]
        public void Excepcion_503Sin_Mensaje_Y_405_ServidorSinActualizar()
        {
            Assert.AreEqual(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR, ErroresApiReparto.Excepcion(503, "", null).Message);
            Assert.AreEqual(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR, ErroresApiReparto.Excepcion(405, "{\"Message\":\"The requested resource does not support http method 'POST'.\"}", null).Message);
        }

        [TestMethod]
        public void Excepcion_404ConMensajeDeLaApi_ElMensaje()
        {
            var ex = ErroresApiReparto.Excepcion(404, "{\"Message\":\"El envío 9 no existe.\"}", null);

            Assert.IsFalse(ex.ServidorSinActualizar);
            Assert.AreEqual("El envío 9 no existe.", ex.Message);
            Assert.AreEqual(404, ex.CodigoHttp);
        }

        [TestMethod]
        public void Excepcion_409ConCodigo_ElMensajeYElCodigo()
        {
            var ex = ErroresApiReparto.Excepcion(409, "{\"Message\":\"Ya está recogida.\",\"Codigo\":\"RECOGIDA_YA_RECOGIDA\"}", null);

            Assert.AreEqual("Ya está recogida.", ex.Message);
            Assert.AreEqual("RECOGIDA_YA_RECOGIDA", ex.CodigoApi);
        }

        [TestMethod]
        public void Excepcion_403SinMensaje_ElDeUsted_YElFormatoDeErrorDeLaApi()
        {
            Assert.AreEqual("No tiene permiso.", ErroresApiReparto.Excepcion(403, "", "No tiene permiso.").Message);
            Assert.AreEqual("Falta el motivo.", ErroresApiReparto.Excepcion(400, "{\"error\":{\"code\":\"X\",\"message\":\"Falta el motivo.\"}}", null).Message);
            Assert.AreEqual("Cadena suelta", ErroresApiReparto.Excepcion(409, "\"Cadena suelta\"", null).Message);
        }

        [TestMethod]
        public void MotivoTextoNoValido_UnoAQuinientos()
        {
            Assert.AreEqual(AnotadorEnvios.MENSAJE_VACIA, AnotadorEnvios.MotivoTextoNoValido("  "));
            Assert.AreEqual(AnotadorEnvios.MENSAJE_LARGA, AnotadorEnvios.MotivoTextoNoValido(new string('x', 501)));
            Assert.IsNull(AnotadorEnvios.MotivoTextoNoValido(new string('x', 500)));
            Assert.IsNull(AnotadorEnvios.MotivoTextoNoValido("  a  "));
        }

        [TestMethod]
        public async Task AnadirNota_Cancelado_NoLlama()
        {
            var servicio = A.Fake<IServicioNotasEnvio>();
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns(null);

            var nota = await new AnotadorEnvios(servicio, dialogos).AnadirNota(251104, 930553);

            Assert.IsNull(nota);
            A.CallTo(() => servicio.Anadir(A<int>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task AnadirNota_LaApiRechaza_SuMensajeTalCual()
        {
            var servicio = A.Fake<IServicioNotasEnvio>();
            var dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("hola");
            A.CallTo(() => servicio.Anadir(251104, "hola")).ThrowsAsync(ErroresApiReparto.Excepcion(403, "", ServicioNotasEnvio.MENSAJE_SIN_PERMISO));

            var nota = await new AnotadorEnvios(servicio, dialogos).AnadirNota(251104, null);

            Assert.IsNull(nota);
            A.CallTo(() => dialogos.ShowError(ServicioNotasEnvio.MENSAJE_SIN_PERMISO)).MustHaveHappenedOnceExactly();
        }
    }
}
