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
    /// Sugerencia 733 (Paloma y Santiago): las etiquetas de bulto solo se podían reimprimir justo después de un fallo de la
    /// impresora o desde las enviadas sin recibir. Si la impresora se atasca aunque la API las haya mandado bien, no había forma.
    /// Ahora se reimprimen las de cualquier reposición escribiendo su número.
    /// </summary>
    [TestClass]
    public class EnviarReposicionReimprimirPorNumeroTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private EnviarReposicionViewModel _vm = null!;

        private static ReposicionEnviada Enviada(int traspaso, int bultos) => new()
        {
            Traspaso = traspaso, Origen = "ALC", Destino = "ALG", NombreDestino = "Algete", Lineas = 3, Unidades = 7, Bultos = bultos
        };

        private static BultosReposicion Impresos(string traspaso, int bultos, string impresora = "Zebra tienda") => new()
        {
            Traspaso = traspaso,
            Impresora = impresora,
            Bultos = Enumerable.Range(1, bultos).Select(b => new BultoReposicion { Codigo = $"R{traspaso}-{b}", Bulto = b, Total = bultos }).ToList()
        };

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALC");
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, A<string>._)).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(false);
            A.CallTo(() => _servicio.LeerEnviadas(A<string>._, "ALC"))
                .Returns(new List<ReposicionEnviada> { Enviada(81093, 2), Enviada(81097, 0) });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            _vm = new EnviarReposicionViewModel(_servicio, _dialogos, _configuracion, A.Fake<ISonidosLector>(), A.Fake<IServicioNavegacion>(), _ => { });
        }

        [TestMethod]
        public async Task Cargar_EnUnaTienda_SeVeYProponeLaUltimaEnviadaConBultos()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.MostrarReimprimirPorNumero);
            Assert.AreEqual("81093", _vm.NumeroReimprimirBultos, "La 81097 se mandó sin bultos: se propone la anterior");
        }

        [TestMethod]
        public async Task Cargar_DesdeAlgete_NoSeVe()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.MostrarReimprimirPorNumero);
            Assert.IsFalse(_vm.ReimprimirBultosPorNumeroCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cargar_NoPisaElNumeroQueHayaEscrito()
        {
            _vm.NumeroReimprimirBultos = "81198";

            await _vm.CargarAsync();

            Assert.AreEqual("81198", _vm.NumeroReimprimirBultos);
        }

        [TestMethod]
        public async Task Reimprimir_CualquierReposicion_AunqueNoEsteEnLasEnviadas()
        {
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81198")).Returns(Impresos("81198", 2));
            await _vm.CargarAsync();
            _vm.NumeroReimprimirBultos = " 81198 ";
            Assert.IsTrue(_vm.ReimprimirBultosPorNumeroCommand.CanExecute(null));

            await _vm.ReimprimirBultosPorNumeroCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.ReimprimirBultos("1", "81198")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CrearBultos(A<string>._, A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.AreEqual("Etiquetas de la reposición 81198 reimpresas (2 bultos) en Zebra tienda.", _vm.Mensaje);
            Assert.AreEqual("81198", _vm.NumeroReimprimirBultos);
        }

        [TestMethod]
        public async Task Reimprimir_ConLaEtiquetaDeUnBulto_UsaSuReposicion()
        {
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81198")).Returns(Impresos("81198", 1));
            await _vm.CargarAsync();
            _vm.NumeroReimprimirBultos = "r81198";

            await _vm.ReimprimirBultosPorNumeroCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.ReimprimirBultos("1", "81198")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Etiquetas de la reposición 81198 reimpresas (1 bulto) en Zebra tienda.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Reimprimir_SinEtiquetas_DiceQueNoTiene()
        {
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81198"))
                .ThrowsAsync(new EnvioReposicionException("Not found", 404));
            await _vm.CargarAsync();
            _vm.NumeroReimprimirBultos = "81198";

            await _vm.ReimprimirBultosPorNumeroCommand.ExecuteAsync(null);

            Assert.AreEqual("La reposición 81198 no tiene etiquetas de bulto.", _vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("La reposición 81198 no tiene etiquetas de bulto.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CrearBultos(A<string>._, A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Reimprimir_SiFallaLaImpresora_OfreceReintentar()
        {
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81198"))
                .Throws(new EnvioReposicionException("La impresora Zebra tienda no responde.", 502)).Once()
                .Then.Returns(Impresos("81198", 2));
            await _vm.CargarAsync();
            _vm.NumeroReimprimirBultos = "81198";

            await _vm.ReimprimirBultosPorNumeroCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(EnviarReposicionViewModel.TITULO_ETIQUETAS, A<string>.That.Contains("no responde")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.ReimprimirBultos("1", "81198")).MustHaveHappenedTwiceExactly();
            Assert.AreEqual("Etiquetas de la reposición 81198 reimpresas (2 bultos) en Zebra tienda.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Reimprimir_SiFallaLaImpresoraYNoReintenta_QuedaReimprimirEtiquetas()
        {
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81198"))
                .ThrowsAsync(new EnvioReposicionException("La impresora Zebra tienda no responde.", 502));
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            await _vm.CargarAsync();
            _vm.NumeroReimprimirBultos = "81198";

            await _vm.ReimprimirBultosPorNumeroCommand.ExecuteAsync(null);

            StringAssert.Contains(_vm.Mensaje, "no responde");
            Assert.IsTrue(_vm.PuedeReimprimirEtiquetas);
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("  ")]
        [DataRow("abc")]
        [DataRow("0")]
        [DataRow("-5")]
        [DataRow("R81198-1")]
        public async Task NumeroNoValido_NoSePuedeReimprimir(string texto)
        {
            await _vm.CargarAsync();
            _vm.NumeroReimprimirBultos = texto;

            Assert.IsFalse(_vm.ReimprimirBultosPorNumeroCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Terminar_ProponeLaRecienEnviada()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(new ReposicionEnPreparacion
            {
                Empresa = "1", Origen = "ALC", Destino = "ALG",
                Lineas = new List<LineaReposicionEnPreparacion> { new() { NumeroOrden = 1, Producto = "17404", Cantidad = 3 } }
            });
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).Returns(new ResultadoTerminarReposicion
            {
                NumTraspaso = 81100,
                Lineas = new List<LineaTraspasoTerminado> { new() { Producto = "17404", Cantidad = 3 } }
            });
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._, A<string>._)).Returns(null);
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.AreEqual("81100", _vm.NumeroReimprimirBultos);
        }

        [TestMethod]
        public async Task EnLaPuntual_NoSeVe()
        {
            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._))
                .Returns(new EstadoReposicionPuntual { Situacion = EstadoReposicionPuntual.ABIERTA, SePuede = true });
            await _vm.CargarAsync();

            _vm.EmpezarPuntualCommand.Execute(null);

            Assert.IsFalse(_vm.MostrarReimprimirPorNumero);
        }

        [TestMethod]
        public void TextoEtiquetasReimpresas_SinImpresora_NoLaNombra()
        {
            Assert.AreEqual("Etiquetas de la reposición 81198 reimpresas (2 bultos).",
                EnviarReposicionViewModel.TextoEtiquetasReimpresas("81198", Impresos("81198", 2, null!)));
        }
    }
}
