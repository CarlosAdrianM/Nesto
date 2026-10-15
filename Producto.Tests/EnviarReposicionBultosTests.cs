using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#530 (NestoAPI#645): al mandar la reposición a Algete se pregunta cuántos bultos lleva y la API imprime una
    /// etiqueta por bulto; si la impresora falla, los bultos quedan creados y se reimprimen.
    /// </summary>
    [TestClass]
    public class EnviarReposicionBultosTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private EnviarReposicionViewModel _vm = null!;

        private static ReposicionEnPreparacion Reposicion() => new()
        {
            Empresa = "1",
            Origen = "ALC",
            Destino = "ALG",
            Lineas = new List<LineaReposicionEnPreparacion>
            {
                new() { NumeroOrden = 101, Producto = "17404", Nombre = "Cera", CodigoBarras = "8411", Cantidad = 3, StockOrigen = 7 }
            }
        };

        private static BultosReposicion Impresos(int bultos) => new()
        {
            Traspaso = "81010",
            Origen = "ALC",
            Destino = "ALG",
            Impresora = "Zebra tienda",
            Bultos = Enumerable.Range(1, bultos)
                .Select(b => new BultoReposicion { Codigo = $"R81010-{b}", Bulto = b, Total = bultos, Texto = $"81010 · bulto {b}/{bultos} · ALC→ALG" })
                .ToList()
        };

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALC");
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Reposicion());
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(true);
            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._))
                .Returns(new EstadoReposicionPuntual { Situacion = EstadoReposicionPuntual.ABIERTA, SePuede = true });
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).Returns(new ResultadoTerminarReposicion
            {
                NumTraspaso = 81010,
                Lineas = new List<LineaTraspasoTerminado> { new() { Producto = "17404", Cantidad = 3 } }
            });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            _vm = new EnviarReposicionViewModel(_servicio, _dialogos, _configuracion, A.Fake<ISonidosLector>(), A.Fake<IServicioNavegacion>(), _ => { });
        }

        private async Task TerminarAsync()
        {
            await _vm.CargarAsync();
            await _vm.TerminarCommand.ExecuteAsync(null);
        }

        [TestMethod]
        public async Task Terminar_PreguntaLosBultosConUnoPorDefectoYLaApiImprimeUnaEtiquetaPorBulto()
        {
            A.CallTo(() => _dialogos.GetText(EnviarReposicionViewModel.TITULO_BULTOS, A<string>._, "1")).Returns("3");
            A.CallTo(() => _servicio.CrearBultos(A<string>._, "81010", 3)).Returns(Impresos(3));

            await TerminarAsync();

            A.CallTo(() => _dialogos.GetText(EnviarReposicionViewModel.TITULO_BULTOS, A<string>.That.Contains("81010"), "1")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CrearBultos("1", "81010", 3)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(_vm.Mensaje, "traspaso 81010");
            StringAssert.Contains(_vm.Mensaje, "Se han impreso 3 etiquetas en Zebra tienda.");
            Assert.IsFalse(_vm.PuedeReimprimirEtiquetas);
        }

        [TestMethod]
        public async Task Terminar_SiCancelaLaPreguntaNoSeCreanBultosYAvisaDeQueSeRecibiraSinEtiqueta()
        {
            A.CallTo(() => _dialogos.GetText(EnviarReposicionViewModel.TITULO_BULTOS, A<string>._, A<string>._)).Returns(null);

            await TerminarAsync();

            A.CallTo(() => _servicio.CrearBultos(A<string>._, A<string>._, A<int>._)).MustNotHaveHappened();
            StringAssert.Contains(_vm.Mensaje, "traspaso 81010");
            StringAssert.Contains(_vm.Mensaje, EnviarReposicionViewModel.TEXTO_SIN_BULTOS);
        }

        [TestMethod]
        public async Task Terminar_ConUnNumeroQueNoValeVuelveAPreguntar()
        {
            A.CallTo(() => _dialogos.GetText(EnviarReposicionViewModel.TITULO_BULTOS, A<string>._, "1")).ReturnsNextFromSequence("60", "2");
            A.CallTo(() => _servicio.CrearBultos(A<string>._, "81010", 2)).Returns(Impresos(2));

            await TerminarAsync();

            A.CallTo(() => _dialogos.GetText(EnviarReposicionViewModel.TITULO_BULTOS, A<string>.That.Contains("«60» no vale"), "1")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CrearBultos(A<string>._, "81010", 2)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(_vm.Mensaje, "Se han impreso 2 etiquetas");
        }

        [TestMethod]
        public async Task Terminar_SiFallaLaImpresion_OfreceReimprimirYReimprime()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns("2");
            A.CallTo(() => _servicio.CrearBultos(A<string>._, "81010", 2)).Throws(new EnvioReposicionException("La impresora Zebra tienda no responde.", 502));
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81010")).Returns(Impresos(2));

            await TerminarAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAsync(EnviarReposicionViewModel.TITULO_ETIQUETAS, A<string>.That.Contains("La impresora Zebra tienda no responde.")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.ReimprimirBultos("1", "81010")).MustHaveHappenedOnceExactly();
            StringAssert.Contains(_vm.Mensaje, "Se han impreso 2 etiquetas");
            Assert.IsFalse(_vm.PuedeReimprimirEtiquetas);
        }

        [TestMethod]
        public async Task Terminar_SiFallaLaImpresionYNoReimprime_QuedaElBotonReimprimir()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns("1");
            A.CallTo(() => _servicio.CrearBultos(A<string>._, "81010", 1)).Throws(new EnvioReposicionException("La impresora no responde.", 502));
            A.CallTo(() => _dialogos.ShowConfirmationAsync(EnviarReposicionViewModel.TITULO_ETIQUETAS, A<string>._)).Returns(false);

            await TerminarAsync();

            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.IsTrue(_vm.PuedeReimprimirEtiquetas);
            Assert.IsTrue(_vm.ReimprimirEtiquetasCommand.CanExecute(null));
            StringAssert.Contains(_vm.Mensaje, "Reimprimir etiquetas");

            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81010")).Returns(Impresos(1));
            await _vm.ReimprimirEtiquetasCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.ReimprimirBultos("1", "81010")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Se ha impreso 1 etiqueta en Zebra tienda.", _vm.Mensaje);
            Assert.IsFalse(_vm.PuedeReimprimirEtiquetas);
        }

        [TestMethod]
        public async Task Terminar_SiYaSeHaLeidoAlgunBulto_EnsenaElTextoDeLaApi()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns("2");
            A.CallTo(() => _servicio.CrearBultos(A<string>._, "81010", 2))
                .Throws(new EnvioReposicionException("Ya se ha leído algún bulto de la reposición 81010.", 409));

            await TerminarAsync();

            A.CallTo(() => _dialogos.ShowError("Ya se ha leído algún bulto de la reposición 81010.")).MustHaveHappenedOnceExactly();
            StringAssert.Contains(_vm.Mensaje, "Ya se ha leído algún bulto de la reposición 81010.");
            Assert.IsFalse(_vm.PuedeReimprimirEtiquetas);
        }

        [TestMethod]
        public async Task Puntual_AlMandarlaTambienPideLosBultos()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, "41980"))
                .Returns(new List<ProductoReposicionPuntual> { new() { Producto = "41980", Nombre = "Guantes", StockOrigen = 5 } });
            A.CallTo(() => _servicio.CrearPuntual(A<CrearReposicion>._)).Returns(new ResultadoTerminarReposicion
            {
                NumTraspaso = 81011,
                Lineas = new List<LineaTraspasoTerminado> { new() { Producto = "41980", Cantidad = 1 } }
            });
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns("1");
            A.CallTo(() => _servicio.CrearBultos(A<string>._, "81011", 1)).Returns(Impresos(1));
            await _vm.CargarAsync();
            _vm.EmpezarPuntualCommand.Execute(null);
            await _vm.LeerPuntualAsync("41980");

            await _vm.EnviarPuntualCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.CrearBultos("1", "81011", 1)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(_vm.Mensaje, "traspaso 81011");
            StringAssert.Contains(_vm.Mensaje, "Se ha impreso 1 etiqueta");
        }

        [TestMethod]
        public void LeerNumeroBultos_SoloDe1A50()
        {
            Assert.AreEqual(1, EnviarReposicionViewModel.LeerNumeroBultos(" 1 "));
            Assert.AreEqual(50, EnviarReposicionViewModel.LeerNumeroBultos("50"));
            Assert.IsNull(EnviarReposicionViewModel.LeerNumeroBultos("0"));
            Assert.IsNull(EnviarReposicionViewModel.LeerNumeroBultos("51"));
            Assert.IsNull(EnviarReposicionViewModel.LeerNumeroBultos("dos"));
        }
    }
}
