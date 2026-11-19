using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Sugerencia 1356: el paso de decidir un devuelto (<see cref="DecisorEnviosDevueltos"/>), común a Agencias › Devueltos y a la
    /// pestaña «Entrega» del detalle del pedido, y la regla de qué devueltos son de un pedido y esperan decisión.
    /// </summary>
    [TestClass]
    public class EnviosDevueltosTests
    {
        private static EnvioDevueltoModel Devuelto(int numero = 251300, int? pedido = 925137, string decision = EnvioDevueltoModel.SIN_DECIDIR, string empresa = "1")
            => new EnvioDevueltoModel
            {
                Numero = numero,
                Pedido = pedido,
                Empresa = empresa,
                Decision = decision,
                Estado = EnvioDevueltoModel.ESTADO_DEVUELTO,
                NombreAgencia = "GLS",
                Fecha = new DateTime(2026, 11, 18),
                DetalleEstado = "AUSENTE",
                Nombre = "PELUQUERÍA LA RUBIA",
                Direccion = "Calle Mayor 1",
                CodPostal = "28001",
                Poblacion = "Madrid"
            };

        [TestMethod]
        public void PendientesDeDecidirDelPedido_SoloLosDeEsePedidoSinDecidir()
        {
            var lista = new List<EnvioDevueltoModel>
            {
                Devuelto(1, 925137),
                Devuelto(2, 925137, EnvioDevueltoModel.REENVIAR),
                Devuelto(3, 999999),
                Devuelto(4, null),
                Devuelto(5, 925137, decision: null),
                Devuelto(6, 925137, empresa: "3"),
                null
            };

            List<EnvioDevueltoModel> pendientes = EnvioDevueltoModel.PendientesDeDecidirDelPedido(lista, "1  ", 925137);

            CollectionAssert.AreEqual(new[] { 1, 5 }, pendientes.ConvertAll(d => d.Numero));
        }

        [TestMethod]
        public void PendientesDeDecidirDelPedido_SinLista_Vacia()
        {
            Assert.AreEqual(0, EnvioDevueltoModel.PendientesDeDecidirDelPedido(null, "1", 925137).Count);
        }

        [TestMethod]
        public void Resumen_Y_Destino()
        {
            EnvioDevueltoModel d = Devuelto();

            Assert.AreEqual("Envío 251300 (GLS) · Devuelto el 18/11/2026: AUSENTE", d.Resumen);
            Assert.AreEqual("PELUQUERÍA LA RUBIA · Calle Mayor 1, 28001 Madrid", d.Destino);
            Assert.AreEqual(string.Empty, new EnvioDevueltoModel().Destino);
        }

        [TestMethod]
        public void ExcepcionDeRespuesta_403_MensajeDeUsted()
        {
            Exception ex = ServicioEnviosDevueltos.ExcepcionDeRespuesta(HttpStatusCode.Forbidden, "{\"Message\":\"Authorization has been denied\"}");

            Assert.IsInstanceOfType(ex, typeof(UnauthorizedAccessException));
            Assert.AreEqual(ServicioEnviosDevueltos.MENSAJE_SIN_PERMISO, ex.Message);
        }

        [TestMethod]
        public void ExcepcionDeRespuesta_409_ElMotivoDeLaApi()
        {
            Exception ex = ServicioEnviosDevueltos.ExcepcionDeRespuesta(HttpStatusCode.Conflict, "\"El envío 251300 ya se volvió a enviar.\"");

            Assert.AreEqual("El envío 251300 ya se volvió a enviar.", ex.Message);
        }

        [TestMethod]
        public void Rutas()
        {
            Assert.AreEqual("EnviosAgencias/Devueltos?empresa=1", ServicioEnviosDevueltos.RutaDevueltos("1  "));
            Assert.AreEqual("EnviosAgencias/251300/Reenviar", ServicioEnviosDevueltos.RutaReenviar(251300));
            Assert.AreEqual("EnviosAgencias/251300/Abonar", ServicioEnviosDevueltos.RutaAbonar(251300));
            // Sugerencia 1356 (fleco): desde el pedido, solo los suyos.
            Assert.AreEqual("EnviosAgencias/Devueltos?empresa=1&pedido=925137", ServicioEnviosDevueltos.RutaDevueltosDelPedido("1  ", 925137));
        }

        private static (DecisorEnviosDevueltos decisor, IServicioEnviosDevueltos servicio, IServicioDialogos dialogos, List<Exception> registrados, List<bool> ocupado) Decisor()
        {
            var servicio = A.Fake<IServicioEnviosDevueltos>();
            var dialogos = A.Fake<IServicioDialogos>();
            var registrados = new List<Exception>();
            var ocupado = new List<bool>();
            return (new DecisorEnviosDevueltos(servicio, dialogos, registrados.Add, ocupado.Add), servicio, dialogos, registrados, ocupado);
        }

        [TestMethod]
        public async Task Reenviar_Cancelado_NoLlamaALaApi()
        {
            var (decisor, servicio, dialogos, _, _) = Decisor();
            A.CallTo(() => dialogos.ShowDialogAsync(ReenviarEnvioDialogViewModel.NOMBRE, A<ParametrosDialogo>._))
                .Returns(Task.FromResult(new ResultadoDialogo(ResultadoBoton.Cancel)));

            ResultadoDecisionDevuelto resultado = await decisor.Reenviar(Devuelto());

            Assert.AreEqual(ResultadoDecisionDevuelto.SinCambios, resultado);
            A.CallTo(() => servicio.Reenviar(A<int>._, A<ReenvioEnvioPeticion>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Reenviar_Aceptado_ManndaLaDireccionCorregidaYAvisa()
        {
            var (decisor, servicio, dialogos, _, ocupado) = Decisor();
            var parametros = new ParametrosDialogo
            {
                { ReenviarEnvioDialogViewModel.PARAMETRO_DIRECCION, "Calle Nueva 7" },
                { ReenviarEnvioDialogViewModel.PARAMETRO_INCLUIR_PORTES, true }
            };
            A.CallTo(() => dialogos.ShowDialogAsync(ReenviarEnvioDialogViewModel.NOMBRE,
                    A<ParametrosDialogo>.That.Matches(p => p.GetValue<int>(ReenviarEnvioDialogViewModel.PARAMETRO_ENVIO) == 251300
                                                         && p.GetValue<string>(ReenviarEnvioDialogViewModel.PARAMETRO_PEDIDO) == "925137")))
                .Returns(Task.FromResult(new ResultadoDialogo(ResultadoBoton.OK, parametros)));
            A.CallTo(() => servicio.Reenviar(251300, A<ReenvioEnvioPeticion>._))
                .Returns(Task.FromResult(new DecisionReenvioModel { EnvioNuevo = 300000, Avisos = new List<string> { "Ojo con el reembolso." } }));

            ResultadoDecisionDevuelto resultado = await decisor.Reenviar(Devuelto());

            Assert.AreEqual(ResultadoDecisionDevuelto.Decidido, resultado);
            A.CallTo(() => servicio.Reenviar(251300, A<ReenvioEnvioPeticion>.That.Matches(p => p.Direccion == "Calle Nueva 7" && p.IncluirPortes)))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowNotification(DecisorEnviosDevueltos.TITULO_REENVIAR,
                "Envío nuevo 300000 creado: imprima la etiqueta cuando llegue la caja.\r\rOjo con el reembolso.")).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { true, false }, ocupado);
        }

        [TestMethod]
        public async Task YaDecidido_NoHaceNada()
        {
            var (decisor, servicio, dialogos, _, _) = Decisor();

            Assert.AreEqual(ResultadoDecisionDevuelto.SinCambios, await decisor.Reenviar(Devuelto(decision: EnvioDevueltoModel.ABONAR)));
            Assert.AreEqual(ResultadoDecisionDevuelto.SinCambios, await decisor.Abonar(Devuelto(decision: EnvioDevueltoModel.REENVIAR)));
            Assert.AreEqual(ResultadoDecisionDevuelto.SinCambios, await decisor.Abonar(null));

            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.Abonar(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Abonar_Confirmado_LoApunta()
        {
            var (decisor, servicio, dialogos, _, _) = Decisor();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(DecisorEnviosDevueltos.TITULO_ABONAR, A<string>._)).Returns(true);

            ResultadoDecisionDevuelto resultado = await decisor.Abonar(Devuelto());

            Assert.AreEqual(ResultadoDecisionDevuelto.Decidido, resultado);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(DecisorEnviosDevueltos.TITULO_ABONAR,
                "¿Confirma que el cliente no quiere el pedido 925137 y hay que abonarlo?")).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Abonar(251300)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowNotification(DecisorEnviosDevueltos.TITULO_ABONAR, A<string>.That.Contains("Copiar factura"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Abonar_SinConfirmar_NoLlamaALaApi()
        {
            var (decisor, servicio, dialogos, _, _) = Decisor();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(false);

            Assert.AreEqual(ResultadoDecisionDevuelto.SinCambios, await decisor.Abonar(Devuelto()));
            A.CallTo(() => servicio.Abonar(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SinPermiso_MensajeDeUstedYSinElmah()
        {
            var (decisor, servicio, dialogos, registrados, ocupado) = Decisor();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => servicio.Abonar(251300)).ThrowsAsync(new UnauthorizedAccessException(ServicioEnviosDevueltos.MENSAJE_SIN_PERMISO));

            ResultadoDecisionDevuelto resultado = await decisor.Abonar(Devuelto());

            Assert.AreEqual(ResultadoDecisionDevuelto.Error, resultado);
            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains(ServicioEnviosDevueltos.MENSAJE_SIN_PERMISO))).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, registrados.Count);
            CollectionAssert.AreEqual(new[] { true, false }, ocupado);
        }

        [TestMethod]
        public async Task ErrorDeLaApi_SeEnsenaYSeRegistra()
        {
            var (decisor, servicio, dialogos, registrados, _) = Decisor();
            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._))
                .Returns(Task.FromResult(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo())));
            A.CallTo(() => servicio.Reenviar(A<int>._, A<ReenvioEnvioPeticion>._)).ThrowsAsync(new InvalidOperationException("El envío 251300 ya se volvió a enviar."));

            Assert.AreEqual(ResultadoDecisionDevuelto.Error, await decisor.Reenviar(Devuelto()));

            A.CallTo(() => dialogos.ShowError("No se ha podido volver a enviar el envío 251300:\rEl envío 251300 ya se volvió a enviar.")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, registrados.Count);
        }
    }
}
