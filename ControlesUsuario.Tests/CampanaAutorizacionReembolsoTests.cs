using ControlesUsuario.Dialogs;
using ControlesUsuario.Notificaciones;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#591 (NestoAPI#773): el repartidor pide entregar un reembolso sin cobrar; Administración o Dirección lo autoriza o lo deniega
    /// desde la campana (POST api/RepartoPropio/Autorizaciones/{id}/Responder).
    /// </summary>
    [TestClass]
    public class CampanaAutorizacionReembolsoTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 31, 10, 0, 0);

        private IBuzonNotificacionesService buzon;
        private CampanaNotificacionesViewModel vm;

        [TestInitialize]
        public void Setup()
        {
            buzon = A.Fake<IBuzonNotificacionesService>();
            vm = new CampanaNotificacionesViewModel(buzon, A.Fake<IServicioDialogos>(), null, null, () => Ahora, new Random(1));
        }

        private static NotificacionBuzonItem Aviso(string tipo = NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_REPARTO, string id = "12", bool leida = false)
            => new NotificacionBuzonItem(new NotificacionBuzon
            {
                Id = 7,
                Titulo = "Autorización: entregar sin cobrar 45,30 € (envío 251300)",
                Cuerpo = "Miguel pide entregar sin cobrar…",
                Leida = leida,
                FechaCreacion = Ahora,
                Datos = new Dictionary<string, string> { { "tipo", tipo }, { "autorizacionId", id }, { "envio", "251300" } }
            }, Ahora);

        [TestMethod]
        public void SoloLaPeticionDeAutorizacion_LlevaLosBotones()
        {
            Assert.IsTrue(Aviso().MostrarBotonesAutorizacionReembolso);
            Assert.IsFalse(Aviso(NotificacionBuzon.TIPO_AUTORIZACION_REEMBOLSO_TELEFONICA).MostrarBotonesAutorizacionReembolso, "La telefónica es solo aviso");
            Assert.IsFalse(Aviso(id: null).MostrarBotonesAutorizacionReembolso);
            Assert.IsFalse(Aviso().MostrarBotonesAvisoFicha);
        }

        [TestMethod]
        public async Task Autorizar_LlamaALaApi_CierraElAviso_YLoDaPorLeido()
        {
            NotificacionBuzonItem item = Aviso();
            A.CallTo(() => buzon.ResponderAutorizacionReembolso(12, true, null)).Returns(Task.FromResult("Autorizada"));

            await vm.AutorizarReembolsoCommand.ExecuteAsync(item);

            A.CallTo(() => buzon.ResponderAutorizacionReembolso(12, true, null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => buzon.MarcarLeida(7)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(item.AvisoCerrado);
            Assert.IsFalse(item.MostrarBotonesAutorizacionReembolso);
            StringAssert.Contains(vm.Mensaje, "Autorizado");
        }

        [TestMethod]
        public async Task Denegar_MandaFalse()
        {
            NotificacionBuzonItem item = Aviso(leida: true);

            await vm.DenegarReembolsoCommand.ExecuteAsync(item);

            A.CallTo(() => buzon.ResponderAutorizacionReembolso(12, false, null)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(vm.Mensaje, "Reembolso no cobrado");
        }

        [TestMethod]
        public async Task YaContestadaPorOtro_EnseñaElMotivo_YCierra_SinPermiso_SigueAbierta()
        {
            NotificacionBuzonItem contestada = Aviso();
            A.CallTo(() => buzon.ResponderAutorizacionReembolso(12, true, null))
                .Throws(new InvalidOperationException("Esta autorización ya está contestada (denegada por Laura)."));

            await vm.AutorizarReembolsoCommand.ExecuteAsync(contestada);

            Assert.IsTrue(contestada.AvisoCerrado);
            StringAssert.Contains(vm.Mensaje, "Laura");

            NotificacionBuzonItem sinPermiso = Aviso(id: "13");
            A.CallTo(() => buzon.ResponderAutorizacionReembolso(13, true, null))
                .Throws(new InvalidOperationException("Solo Administración o Dirección pueden autorizar que un reembolso se quede sin cobrar."));

            await vm.AutorizarReembolsoCommand.ExecuteAsync(sinPermiso);

            Assert.IsFalse(sinPermiso.AvisoCerrado);
            A.CallTo(() => buzon.MarcarLeida(A<int>._)).MustHaveHappenedOnceExactly();
        }
    }
}
