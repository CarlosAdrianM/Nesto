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
        private IDialogService dialogos;
        private IDialogParameters parametros;
        private AbridorNovedades sut;
        private readonly List<NovedadUsuario> novedades = new List<NovedadUsuario> { new NovedadUsuario { Id = 1, Version = "1.10.32.0" } };

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<INovedadesService>();
            dialogos = A.Fake<IDialogService>();
            A.CallTo(() => servicio.ObtenerNovedades(null)).Returns(Task.FromResult(novedades));
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes((string nombre, IDialogParameters p, Action<IDialogResult> cb) => parametros = p);
            sut = new AbridorNovedades(servicio, dialogos);
        }

        [TestMethod]
        public async Task Abrir_SinVersion_AbreConTodasLasNovedades()
        {
            await sut.Abrir();

            A.CallTo(() => dialogos.ShowDialog("NovedadesDialog", A<IDialogParameters>._, A<Action<IDialogResult>>._)).MustHaveHappenedOnceExactly();
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
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Throws(new InvalidOperationException("sin ventana"));

            await sut.Abrir();
        }
    }
}
