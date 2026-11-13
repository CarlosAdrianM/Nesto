using Nesto.Modulos.Cajas.Services;

namespace CajasTests
{
    /// <summary>
    /// El mensaje de error de los servicios de Cajas: el texto de contexto seguido de lo que diga la API. Caso real del
    /// 13/11/26: la API decía quién tenía bloqueada la contabilidad y Cajas no lo enseñaba (leía solo el formato antiguo o
    /// tiraba un texto fijo).
    /// </summary>
    [TestClass]
    public class ErroresApiCajasTests
    {
        private const string BLOQUEO = "Tiempo de espera de la operación de espera agotado Puede estar bloqueado por: NUEVAVISION\\\\Santiago " +
            "(Nesto, transacción abierta desde 10:59, bloquea a 2 sesión(es)). Si persiste, avisa a ese usuario para que termine " +
            "o cierre la operación que tiene a medias.";

        [TestMethod]
        public void Mensaje_FormatoActualConBloqueo_EnsenaElContextoYQuienBloquea()
        {
            string cuerpo = "{\"error\":{\"code\":\"INTERNAL_ERROR\",\"message\":\"" + BLOQUEO + "\"}}";

            string mensaje = ErroresApiCajas.Mensaje("No se ha podido contabilizar\n", cuerpo);

            StringAssert.StartsWith(mensaje, "No se ha podido contabilizar" + Environment.NewLine);
            StringAssert.Contains(mensaje, "Puede estar bloqueado por: NUEVAVISION\\Santiago");
            StringAssert.Contains(mensaje, "avisa a ese usuario");
        }

        [TestMethod]
        public void Mensaje_FormatoActualConCodigoDeNegocio_LoConserva()
        {
            string cuerpo = "{\"error\":{\"code\":\"FECHA_NO_PERMITIDA\",\"message\":\"Fecha 01/10/2026 no permitida\"}}";

            string mensaje = ErroresApiCajas.Mensaje("No se ha podido contabilizar", cuerpo);

            StringAssert.Contains(mensaje, "[FECHA_NO_PERMITIDA] Fecha 01/10/2026 no permitida");
        }

        [TestMethod]
        public void Mensaje_FormatoAntiguoConInnerException_EnsenaTodaLaCadena()
        {
            // Lo que Cajas ya enseñaba: exceptionMessage y la cadena de InnerException
            string cuerpo = "{\"exceptionMessage\":\"Error al contabilizar\",\"innerException\":{\"exceptionMessage\":\"El asiento está descuadrado\"}}";
            string cuerpoMayusculas = "{\"ExceptionMessage\":\"Error al contabilizar\",\"InnerException\":{\"ExceptionMessage\":\"El asiento está descuadrado\"}}";

            foreach (string c in new[] { cuerpo, cuerpoMayusculas })
            {
                string mensaje = ErroresApiCajas.Mensaje("No se ha podido contabilizar", c);

                StringAssert.Contains(mensaje, "Error al contabilizar");
                StringAssert.Contains(mensaje, "El asiento está descuadrado");
            }
        }

        [TestMethod]
        public void Mensaje_BadRequestConMessageYModelState_EnsenaLosDos()
        {
            string cuerpo = "{\"Message\":\"La solicitud no es válida.\",\"ModelState\":{\"apunte.Fecha\":[\"La fecha es obligatoria\"]}}";

            string mensaje = ErroresApiCajas.Mensaje("No se ha podido contabilizar", cuerpo);

            StringAssert.Contains(mensaje, "La solicitud no es válida.");
            StringAssert.Contains(mensaje, "La fecha es obligatoria");
        }

        [TestMethod]
        public void Mensaje_CuerpoVacioONoJson_SoloElContexto()
        {
            foreach (string cuerpo in new[] { null, "", "   ", "<html><body>502 Bad Gateway</body></html>", "\"texto suelto\"", "[1,2]", "{}" })
            {
                Assert.AreEqual("No se han podido cargar los apuntes de la contabilidad",
                    ErroresApiCajas.Mensaje("No se han podido cargar los apuntes de la contabilidad", cuerpo), $"Cuerpo: «{cuerpo}»");
            }
        }

        [TestMethod]
        public void Mensaje_SinContexto_SoloElDetalle()
        {
            Assert.AreEqual("Algo ha ido mal", ErroresApiCajas.Mensaje(null, "{\"error\":{\"message\":\"Algo ha ido mal\"}}"));
        }
    }
}
