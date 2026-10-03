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
    /// Nesto#509: los avisos de «dato mal en la ficha» que manda Ariadna (NestoAPI Ariadna#8) se cierran desde la
    /// campana con «Cambiado» o «Estaba bien» (POST api/Almacen/AvisosFicha/{avisoId}/Cerrar).
    /// </summary>
    [TestClass]
    public class CampanaAvisosFichaProductoTests
    {
        private static readonly DateTime Ahora = new DateTime(2026, 10, 3, 12, 0, 0);

        private IBuzonNotificacionesService buzon;
        private CampanaNotificacionesViewModel vm;

        [TestInitialize]
        public void Setup()
        {
            buzon = A.Fake<IBuzonNotificacionesService>();
            vm = new CampanaNotificacionesViewModel(buzon, A.Fake<IServicioDialogos>(), null, null, () => Ahora, new Random(1));
        }

        private static NotificacionBuzonItem Aviso(int id = 7, bool leida = false, string avisoId = "42", string tipo = NotificacionBuzon.TIPO_AVISO_FICHA_PRODUCTO)
        {
            return new NotificacionBuzonItem(new NotificacionBuzon
            {
                Id = id,
                Titulo = "Dato mal en la ficha del 22624",
                Cuerpo = "Foto: no es este producto",
                Leida = leida,
                FechaCreacion = Ahora,
                Datos = new Dictionary<string, string> { { "tipo", tipo }, { "avisoId", avisoId }, { "producto", "22624" } }
            }, Ahora);
        }

        [TestMethod]
        public void UnAvisoDeFicha_EnseñaLosBotones_YOtroNo()
        {
            Assert.IsTrue(Aviso().MostrarBotonesAvisoFicha);
            Assert.IsFalse(Aviso(tipo: NotificacionBuzon.TIPO_NOVEDAD_COMENTARIO).MostrarBotonesAvisoFicha);
            Assert.IsFalse(Aviso(avisoId: null).MostrarBotonesAvisoFicha, "Sin avisoId no hay qué cerrar");
        }

        [TestMethod]
        public async Task Cambiado_CierraElAvisoConEseResultado_YLoDaPorLeido()
        {
            NotificacionBuzonItem item = Aviso();
            A.CallTo(() => buzon.CerrarAvisoFicha(42, NotificacionBuzon.RESULTADO_AVISO_CAMBIADO)).Returns(Task.FromResult("Aviso cerrado: cambiado."));

            await vm.CerrarAvisoFichaCambiadoCommand.ExecuteAsync(item);

            A.CallTo(() => buzon.CerrarAvisoFicha(42, NotificacionBuzon.RESULTADO_AVISO_CAMBIADO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => buzon.MarcarLeida(7)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(item.AvisoCerrado);
            Assert.IsTrue(item.Leida);
            Assert.IsFalse(item.MostrarBotonesAvisoFicha);
            Assert.AreEqual("Aviso cerrado: cambiado.", vm.Mensaje);
        }

        [TestMethod]
        public async Task EstabaBien_MandaEseResultado()
        {
            NotificacionBuzonItem item = Aviso(leida: true);

            await vm.CerrarAvisoFichaEstabaBienCommand.ExecuteAsync(item);

            A.CallTo(() => buzon.CerrarAvisoFicha(42, NotificacionBuzon.RESULTADO_AVISO_ESTABA_BIEN)).MustHaveHappenedOnceExactly();
            A.CallTo(() => buzon.MarcarLeida(A<int>._)).MustNotHaveHappened();
            Assert.IsTrue(item.AvisoCerrado);
        }

        [TestMethod]
        public async Task FallaElCierre_SeQuedaAbiertoYEnseñaElMotivo()
        {
            NotificacionBuzonItem item = Aviso();
            A.CallTo(() => buzon.CerrarAvisoFicha(42, A<string>._)).ThrowsAsync(new InvalidOperationException("No eres del equipo de Compras ni de Tienda online."));

            await vm.CerrarAvisoFichaCambiadoCommand.ExecuteAsync(item);

            Assert.IsFalse(item.AvisoCerrado);
            Assert.IsTrue(item.MostrarBotonesAvisoFicha);
            Assert.AreEqual("No eres del equipo de Compras ni de Tienda online.", vm.Mensaje);
        }
    }
}
