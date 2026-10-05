using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Etiquetas de hueco (30×20 mm, «Pasillo / Fila / Columna» y el código PPPFFFCCC que lee Ariadna): se piden por un
    /// rango de un pasillo o por huecos sueltos, se ven antes de imprimir y salen por la impresora de etiquetas de
    /// producto del usuario. La API decide la impresora e imprime; Nesto solo pregunta y enseña lo que contesta.
    /// </summary>
    [TestClass]
    public class EtiquetasHuecoViewModelTests
    {
        private IServicioEtiquetasHueco _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private EtiquetasHuecoViewModel _vm = null!;
        private readonly List<(PeticionEtiquetasHueco Peticion, bool Ensayo)> _llamadas = new();

        private static ResultadoEtiquetasHueco Resultado(int n, bool ensayo) => new()
        {
            Impresas = ensayo ? 0 : n,
            Impresora = @"\\RDS2016\etiquetas2",
            Huecos = Enumerable.Range(1, n).Select(i => $"002004{i:000}").ToList(),
            Mensaje = ensayo ? $"Saldrían {n} etiquetas" : $"Impresas {n} etiquetas"
        };

        private void ConHuecos(int n)
        {
            A.CallTo(() => _servicio.Imprimir(A<string>._, A<string>._, A<PeticionEtiquetasHueco>._, A<bool>._))
                .ReturnsLazily((string _, string _, PeticionEtiquetasHueco p, bool ensayo) =>
                {
                    _llamadas.Add((p, ensayo));
                    return Task.FromResult(Resultado(p.Huecos?.Count > 0 ? p.Huecos.Count : n, ensayo));
                });
        }

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioEtiquetasHueco>();
            _dialogos = A.Fake<IServicioDialogos>();
            _vm = new EtiquetasHuecoViewModel(_servicio, _dialogos);
            ConHuecos(3);
        }

        [TestMethod]
        public async Task Ver_UnRango_PideElEnsayoYEnsenaLosHuecosYLaImpresora()
        {
            _vm.Pasillo = "2";
            _vm.FilaDesde = "4";
            _vm.FilaHasta = "4";
            _vm.ColumnaDesde = "1";
            _vm.ColumnaHasta = "3";
            _vm.SoloEnUso = true;

            await _vm.VerCommand.ExecuteAsync(null);

            (PeticionEtiquetasHueco peticion, bool ensayo) = _llamadas.Single();
            Assert.IsTrue(ensayo);
            Assert.AreEqual("002", peticion.Pasillo, "Se rellena a 3 cifras");
            Assert.AreEqual("004", peticion.FilaDesde);
            Assert.AreEqual("003", peticion.ColumnaHasta);
            Assert.IsTrue(peticion.SoloEnUso);
            Assert.IsNull(peticion.Huecos);
            Assert.AreEqual(3, _vm.Huecos.Count);
            Assert.AreEqual("002/004/001", _vm.Huecos[0], "Se enseñan como en la etiqueta");
            StringAssert.Contains(_vm.Mensaje, "Saldrían 3");
            StringAssert.Contains(_vm.Mensaje, @"\\RDS2016\etiquetas2");
        }

        [TestMethod]
        public async Task Ver_HuecosSueltos_UnoPorLinea_SinLineasVacias()
        {
            _vm.ModoSueltos = true;
            _vm.HuecosSueltos = "002004001\r\n\r\n 003/001/002 \r\n";

            await _vm.VerCommand.ExecuteAsync(null);

            CollectionAssert.AreEqual(new[] { "002004001", "003/001/002" }, _llamadas.Single().Peticion.Huecos);
            Assert.IsNull(_llamadas.Single().Peticion.Pasillo);
        }

        [TestMethod]
        public async Task Ver_SinPasillo_LoDiceSinLlamarALaApi()
        {
            await _vm.VerCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _llamadas.Count);
            StringAssert.Contains(_vm.Mensaje, "pasillo");
        }

        [TestMethod]
        public async Task Ver_SinHuecosSueltos_LoDiceSinLlamarALaApi()
        {
            _vm.ModoSueltos = true;

            await _vm.VerCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _llamadas.Count);
            StringAssert.Contains(_vm.Mensaje, "hueco");
        }

        [TestMethod]
        public async Task Imprimir_PocasEtiquetas_ImprimeSinPreguntar()
        {
            _vm.Pasillo = "002";

            await _vm.ImprimirCommand.ExecuteAsync(null);

            Assert.AreEqual(2, _llamadas.Count, "Primero el ensayo (para contar) y luego la de verdad");
            Assert.IsFalse(_llamadas[1].Ensayo);
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            StringAssert.Contains(_vm.Mensaje, "Impresas 3");
        }

        [TestMethod]
        public async Task Imprimir_MasDe20_PideConfirmacion_YSiNoSeConfirmaNoImprime()
        {
            ConHuecos(25);
            _vm.Pasillo = "002";
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);

            await _vm.ImprimirCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>.That.Contains("25 etiquetas"))).MustHaveHappenedOnceExactly();
            Assert.IsTrue(_llamadas.All(l => l.Ensayo), "Sin confirmar no se imprime nada");
        }

        [TestMethod]
        public async Task Imprimir_MasDe20_Confirmado_Imprime()
        {
            ConHuecos(25);
            _vm.Pasillo = "002";
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);

            await _vm.ImprimirCommand.ExecuteAsync(null);

            Assert.IsFalse(_llamadas.Last().Ensayo);
        }

        [TestMethod]
        public async Task Imprimir_SinNingunHueco_NoImprime()
        {
            ConHuecos(0);
            _vm.Pasillo = "002";
            _vm.SoloEnUso = true;

            await _vm.ImprimirCommand.ExecuteAsync(null);

            Assert.IsTrue(_llamadas.All(l => l.Ensayo));
            StringAssert.Contains(_vm.Mensaje, "Saldrían 0");
        }

        [TestMethod]
        public async Task EtiquetaDePrueba_ImprimeSolo002004001()
        {
            await _vm.PruebaCommand.ExecuteAsync(null);

            (PeticionEtiquetasHueco peticion, bool ensayo) = _llamadas.Single();
            Assert.IsFalse(ensayo);
            CollectionAssert.AreEqual(new[] { "002004001" }, peticion.Huecos);
        }

        [TestMethod]
        public async Task ErrorDeLaApi_SeEnsenaTalCual()
        {
            A.CallTo(() => _servicio.Imprimir(A<string>._, A<string>._, A<PeticionEtiquetasHueco>._, A<bool>._))
                .ThrowsAsync(new EtiquetasHuecoException("Para imprimir etiquetas de hueco hay que ser de Almacén."));
            _vm.Pasillo = "002";

            await _vm.ImprimirCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowError("Para imprimir etiquetas de hueco hay que ser de Almacén.")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(_vm.EstaOcupado);
        }

        [TestMethod]
        public async Task SinConexion_DiceQueNoSePuedeConectar()
        {
            A.CallTo(() => _servicio.Imprimir(A<string>._, A<string>._, A<PeticionEtiquetasHueco>._, A<bool>._))
                .ThrowsAsync(new HttpRequestException("No such host"));
            _vm.Pasillo = "002";

            await _vm.VerCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowError(A<string>.That.Contains("servidor"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void MenuBar_AbreLaVentanaDeEtiquetasDeHueco()
        {
            var navegacion = A.Fake<IServicioNavegacion>();
            var menu = new ProductoMenuBarViewModel(navegacion, A.Fake<IConfiguracion>());

            menu.AbrirEtiquetasHuecoCommand.Execute(null);

            A.CallTo(() => navegacion.RequestNavigate("MainRegion", "EtiquetasHuecoView")).MustHaveHappenedOnceExactly();
        }
    }
}
