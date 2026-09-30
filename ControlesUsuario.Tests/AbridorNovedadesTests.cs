using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#501: la apertura de Novedades que comparten el menú de la cinta, el botón junto a la campana
    /// y el aviso de versión nueva.
    /// </summary>
    [TestClass]
    public class AbridorNovedadesTests
    {
        private INovedadesService servicio;
        private IServicioDialogos dialogos;
        private ParametrosDialogo parametros;
        private AbridorNovedades sut;
        private readonly List<NovedadUsuario> novedades = new List<NovedadUsuario> { new NovedadUsuario { Id = 1, Version = "1.10.32.0" } };

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => servicio.ObtenerNovedades(null)).Returns(Task.FromResult(novedades));
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo p, Action<ResultadoDialogo> cb) => parametros = p);
            sut = new AbridorNovedades(servicio, dialogos);
        }

        [TestMethod]
        public async Task Abrir_SinVersion_AbreConTodasLasNovedades()
        {
            await sut.Abrir();

            A.CallTo(() => dialogos.ShowDialog("NovedadesDialog", A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
            Assert.AreSame(novedades, parametros.GetValue<List<NovedadUsuario>>("novedades"));
            Assert.IsFalse(parametros.ContainsKey(NovedadesDialogViewModel.PARAMETRO_VERSION));
        }

        [TestMethod]
        public async Task Abrir_ConVersion_LaPasaAlDialogo()
        {
            await sut.Abrir(" 1.10.32.0 ");

            Assert.AreEqual("1.10.32.0", parametros.GetValue<string>(NovedadesDialogViewModel.PARAMETRO_VERSION));
        }

        [TestMethod]
        public async Task Abrir_SiFallaElDialogo_NoLanza()
        {
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Throws(new InvalidOperationException("sin ventana"));

            await sut.Abrir();
        }
    }
}
