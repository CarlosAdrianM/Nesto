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
    /// NestoAPI#685: en «Enviar reposición» solo se podían reimprimir los bultos justo después de enviarla. Ahora se ven las
    /// enviadas desde la tienda que aún no se han recibido y de cada una se reimprimen los bultos y el impreso.
    /// </summary>
    [TestClass]
    public class EnviarReposicionEnviadasTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private EnviarReposicionViewModel _vm = null!;
        private readonly List<string> _abiertos = new();

        private static ReposicionEnviada Enviada(int traspaso, int bultos) => new()
        {
            Traspaso = traspaso, Origen = "ALC", Destino = "ALG", NombreDestino = "Algete", Lineas = 3, Unidades = 7, Bultos = bultos,
            Fecha = new DateTime(2026, 10, 21)
        };

        private static BultosReposicion Impresos(string traspaso, int bultos) => new()
        {
            Traspaso = traspaso,
            Impresora = "Zebra tienda",
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
                .Returns(new List<ReposicionEnviada> { Enviada(81093, 2), Enviada(81097, 1) });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            _vm = new EnviarReposicionViewModel(_servicio, _dialogos, _configuracion, A.Fake<ISonidosLector>(), A.Fake<IServicioNavegacion>(), _abiertos.Add);
        }

        [TestCleanup]
        public void Limpiar()
        {
            foreach (string fichero in _abiertos.Where(File.Exists))
            {
                File.Delete(fichero);
            }
        }

        [TestMethod]
        public async Task Cargar_EnsenaLasEnviadasSinRecibirLaMasRecientePrimero()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.HayEnviadas);
            CollectionAssert.AreEqual(new[] { 81097, 81093 }, _vm.Enviadas.Select(e => e.Traspaso).ToArray());
            A.CallTo(() => _servicio.LeerEnviadas("1", "ALC")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Cargar_SinNingunaOSiNoSePuedeLeer_NoSeVeLaLista()
        {
            A.CallTo(() => _servicio.LeerEnviadas(A<string>._, A<string>._)).ThrowsAsync(new HttpRequestException("sin red"));

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.HayEnviadas);
            Assert.AreEqual(0, _vm.Enviadas.Count);
        }

        [TestMethod]
        public async Task Cargar_DesdeAlgete_NiSeLee()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.HayEnviadas);
            A.CallTo(() => _servicio.LeerEnviadas(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ReimprimirBultos_ConBultos_LosReimprimeYDiceDonde()
        {
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81093")).Returns(Impresos("81093", 2));
            await _vm.CargarAsync();
            ReposicionEnviada enviada = _vm.Enviadas.Single(e => e.Traspaso == 81093);
            Assert.IsTrue(_vm.ReimprimirBultosEnviadaCommand.CanExecute(enviada));

            await _vm.ReimprimirBultosEnviadaCommand.ExecuteAsync(enviada);

            A.CallTo(() => _servicio.ReimprimirBultos("1", "81093")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CrearBultos(A<string>._, A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.AreEqual("Se han impreso 2 etiquetas en Zebra tienda.", _vm.Mensaje);
            A.CallTo(() => _servicio.LeerEnviadas("1", "ALC")).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task ReimprimirBultos_MandadaSinBultos_PreguntaCuantosYLosHace()
        {
            A.CallTo(() => _servicio.LeerEnviadas(A<string>._, "ALC")).Returns(new List<ReposicionEnviada> { Enviada(81097, 0) });
            A.CallTo(() => _dialogos.GetText(EnviarReposicionViewModel.TITULO_BULTOS, A<string>._, "1")).Returns("2");
            A.CallTo(() => _servicio.CrearBultos(A<string>._, "81097", 2)).Returns(Impresos("81097", 2));
            await _vm.CargarAsync();

            await _vm.ReimprimirBultosEnviadaCommand.ExecuteAsync(_vm.Enviadas[0]);

            A.CallTo(() => _servicio.CrearBultos("1", "81097", 2)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual("Se han impreso 2 etiquetas en Zebra tienda.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task ReimprimirBultos_SiFallaLaImpresora_OfreceReintentar()
        {
            A.CallTo(() => _servicio.ReimprimirBultos(A<string>._, "81093"))
                .Throws(new EnvioReposicionException("La impresora Zebra tienda no responde.", 502)).Once()
                .Then.Returns(Impresos("81093", 2));
            await _vm.CargarAsync();

            await _vm.ReimprimirBultosEnviadaCommand.ExecuteAsync(_vm.Enviadas.Single(e => e.Traspaso == 81093));

            A.CallTo(() => _dialogos.ShowConfirmationAsync(EnviarReposicionViewModel.TITULO_ETIQUETAS, A<string>.That.Contains("no responde")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.ReimprimirBultos("1", "81093")).MustHaveHappenedTwiceExactly();
            Assert.AreEqual("Se han impreso 2 etiquetas en Zebra tienda.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task ReimprimirImpreso_DescargaElPdfDeEsaReposicionYLoAbre()
        {
            A.CallTo(() => _servicio.DescargarImpresoEnviada(A<string>._, A<string>._, 81093)).Returns(new byte[] { 37, 80, 68, 70 });
            await _vm.CargarAsync();
            ReposicionEnviada enviada = _vm.Enviadas.Single(e => e.Traspaso == 81093);
            Assert.IsTrue(_vm.ReimprimirImpresoEnviadaCommand.CanExecute(enviada));

            await _vm.ReimprimirImpresoEnviadaCommand.ExecuteAsync(enviada);

            A.CallTo(() => _servicio.DescargarImpresoEnviada("1", "ALC", 81093)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, _abiertos.Count);
            StringAssert.Contains(_abiertos[0], "Reposicion_ALC_81093");
            CollectionAssert.AreEqual(new byte[] { 37, 80, 68, 70 }, File.ReadAllBytes(_abiertos[0]));
        }

        [TestMethod]
        public async Task ReimprimirImpreso_SiYaSeHaRecibido_DiceElMotivoYQuitaLaDeLaLista()
        {
            const string motivo = "La reposición 81093 no es una reposición enviada desde ALC pendiente de recibir.";
            A.CallTo(() => _servicio.DescargarImpresoEnviada(A<string>._, A<string>._, 81093)).ThrowsAsync(new EnvioReposicionException(motivo, 404));
            await _vm.CargarAsync();
            ReposicionEnviada enviada = _vm.Enviadas.Single(e => e.Traspaso == 81093);
            A.CallTo(() => _servicio.LeerEnviadas(A<string>._, "ALC")).Returns(new List<ReposicionEnviada> { Enviada(81097, 1) });

            await _vm.ReimprimirImpresoEnviadaCommand.ExecuteAsync(enviada);

            Assert.AreEqual(motivo, _vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError(motivo)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, _abiertos.Count);
            CollectionAssert.AreEqual(new[] { 81097 }, _vm.Enviadas.Select(e => e.Traspaso).ToArray());
        }

        [TestMethod]
        public async Task Terminar_LaRecienEnviadaSaleEnLaLista()
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
            A.CallTo(() => _servicio.LeerEnviadas(A<string>._, "ALC"))
                .Returns(new List<ReposicionEnviada> { Enviada(81093, 2), Enviada(81097, 1), Enviada(81100, 0) });

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.AreEqual(81100, _vm.Enviadas[0].Traspaso);
        }

        [TestMethod]
        public async Task EnLaPuntual_NoSeVeLaLista()
        {
            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._))
                .Returns(new EstadoReposicionPuntual { Situacion = EstadoReposicionPuntual.ABIERTA, SePuede = true });
            await _vm.CargarAsync();

            _vm.EmpezarPuntualCommand.Execute(null);

            Assert.IsTrue(_vm.EnModoPuntual);
            Assert.IsFalse(_vm.HayEnviadas);
        }
    }
}
