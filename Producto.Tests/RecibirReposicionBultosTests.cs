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
    /// Nesto#530 (NestoAPI#645): las reposiciones enviadas con bultos exigen leer la etiqueta de un bulto (R81010-2) antes de
    /// leer los productos; las etiquetas leídas van al terminar. Las de antes (sin bultos) se reciben como siempre.
    /// </summary>
    [TestClass]
    public class RecibirReposicionBultosTests
    {
        private IServicioRecepcionReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private ISonidosLector _sonidos = null!;
        private RecibirReposicionViewModel _vm = null!;
        private TerminarRecepcionReposicion? _terminada;

        private static RecepcionReposicion Reposicion(string documento, bool exigeEtiqueta, int bultos = 2) => new()
        {
            Tipo = "REPO",
            Documento = documento,
            Titulo = $"Reposición {documento}",
            PuedeTerminar = true,
            SeTerminaDesdeAqui = true,
            ExigeEtiqueta = exigeEtiqueta,
            Bultos = exigeEtiqueta ? bultos : 0,
            Lineas = new List<LineaRecepcionReposicion>
            {
                new() { Producto = "17404", Descripcion = "Cera", CodigoBarras = "8411", Cantidad = 2 }
            }
        };

        private static EtiquetaBultoReposicion Etiqueta(string traspaso, int bulto, int total = 2) => new()
        {
            Codigo = $"R{traspaso}-{bulto}",
            Traspaso = traspaso,
            Bulto = bulto,
            Total = total,
            Origen = "ALG",
            Destino = "ALC"
        };

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioRecepcionReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            _sonidos = A.Fake<ISonidosLector>();
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALC");
            A.CallTo(() => _servicio.LeerPendientes(A<string>._, "ALC")).Returns(new List<RecepcionPendiente>
            {
                new() { Tipo = "REPO", Documento = "81010", Titulo = "Reposición 81010", Lineas = 1, Unidades = 2 },
                new() { Tipo = "REPO", Documento = "81011", Titulo = "Reposición puntual 81011", Lineas = 1, Unidades = 2 }
            });
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "ALC", "81010")).Returns(Reposicion("81010", exigeEtiqueta: true));
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "ALC", "81011")).Returns(Reposicion("81011", exigeEtiqueta: true));
            A.CallTo(() => _servicio.LeerBulto(A<string>._, "R81010-1")).Returns(Etiqueta("81010", 1));
            A.CallTo(() => _servicio.LeerBulto(A<string>._, "R81010-2")).Returns(Etiqueta("81010", 2));
            A.CallTo(() => _servicio.LeerBulto(A<string>._, "R81011-1")).Returns(Etiqueta("81011", 1, 1));
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC", A<string>._, A<TerminarRecepcionReposicion>._))
                .ReturnsLazily((string _, string _, string documento, TerminarRecepcionReposicion t) =>
                {
                    _terminada = t;
                    return Task.FromResult(new ResultadoRecepcionReposicion { Documento = documento });
                });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            _terminada = null;
            _vm = new RecibirReposicionViewModel(_servicio, _dialogos, _configuracion, _sonidos, A.Fake<IServicioNavegacion>(), _ => { });
        }

        private async Task AbrirAsync(int indice = 0)
        {
            await _vm.CargarAsync();
            await _vm.ElegirCommand.ExecuteAsync(_vm.Pendientes[indice]);
        }

        private async Task LeerAsync(string codigo)
        {
            _vm.Lectura = codigo;
            _vm.LeerLecturaCommand.Execute(null);
            await _vm.LecturaEtiquetaEnCurso;
        }

        [TestMethod]
        public async Task Elegir_QueExigeEtiqueta_PideLeerlaYNoDejaLeerProductos()
        {
            await AbrirAsync();

            Assert.IsTrue(_vm.EsperandoEtiqueta);
            Assert.IsFalse(_vm.PuedeLeerProductos);
            Assert.AreEqual("Lea la etiqueta del bulto (R81010-n) antes de leer los productos.", _vm.Mensaje);

            await LeerAsync("8411");

            Assert.AreEqual(0, _vm.Lineas[0].Leido, "Hasta leer la etiqueta no entra ningún producto");
            Assert.AreEqual("Lea la etiqueta del bulto (R81010-n) antes de leer los productos.", _vm.Mensaje);
            A.CallTo(() => _sonidos.Error()).MustHaveHappened();
        }

        [TestMethod]
        public async Task LeerEtiqueta_DeEsaReposicion_DejaLeerProductosYVaAlTerminar()
        {
            await AbrirAsync();

            await LeerAsync("r81010-1");
            await LeerAsync("8411");
            await LeerAsync("R81010-2");
            await LeerAsync("8411");
            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.LeerBulto("1", "R81010-1")).MustHaveHappenedOnceExactly();
            Assert.IsNotNull(_terminada);
            CollectionAssert.AreEqual(new[] { "R81010-1", "R81010-2" }, _terminada!.CodigosBulto);
            Assert.AreEqual(2, _terminada.Lecturas.Single().Cantidad);
        }

        [TestMethod]
        public async Task LeerEtiqueta_EnsenaLosBultosLeidos()
        {
            await AbrirAsync();

            await LeerAsync("R81010-2");

            Assert.IsFalse(_vm.EsperandoEtiqueta);
            Assert.IsTrue(_vm.PuedeLeerProductos);
            Assert.AreEqual("Bultos leídos: 1 de 2 (R81010-2)", _vm.TextoBultos);
            Assert.AreEqual("Bulto 2 de 2 de la reposición 81010 leído. Lea los productos.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task LeerEtiqueta_DeOtraReposicion_NoValeYSigueEsperando()
        {
            await AbrirAsync();

            await LeerAsync("R81011-1");

            Assert.AreEqual("Esa etiqueta es de la reposición 81011, no de la 81010.", _vm.Mensaje);
            Assert.IsTrue(_vm.EsperandoEtiqueta);
            Assert.AreEqual(0, _vm.CodigosBulto.Count);
            Assert.AreEqual("81010", _vm.Seleccionada!.Documento);
        }

        [TestMethod]
        public async Task LeerEtiqueta_QueNoExiste_EnsenaElTextoDeLaApi()
        {
            A.CallTo(() => _servicio.LeerBulto(A<string>._, "R99999-1"))
                .Throws(new RecepcionReposicionException("No hay ningún bulto con la etiqueta R99999-1."));
            await AbrirAsync();

            await LeerAsync("R99999-1");

            Assert.AreEqual("No hay ningún bulto con la etiqueta R99999-1.", _vm.Mensaje);
            Assert.IsTrue(_vm.EsperandoEtiqueta);
        }

        [TestMethod]
        public async Task LeerEtiqueta_SinNingunaAbierta_AbreSuReposicion()
        {
            await _vm.CargarAsync();
            Assert.IsNull(_vm.Seleccionada, "Con dos pendientes no se abre ninguna sola");

            await LeerAsync("R81011-1");

            Assert.AreEqual("81011", _vm.Seleccionada!.Documento);
            Assert.IsFalse(_vm.EsperandoEtiqueta);
            CollectionAssert.AreEqual(new[] { "R81011-1" }, _vm.CodigosBulto);
        }

        [TestMethod]
        public async Task Terminar_SinLeerLaEtiqueta_NoLlamaALaApi()
        {
            await AbrirAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._, A<string>._, A<TerminarRecepcionReposicion>._)).MustNotHaveHappened();
            Assert.AreEqual("Lea la etiqueta del bulto (R81010-n) antes de leer los productos.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Terminar_SiLaApiLaRechaza_EnsenaSuTexto()
        {
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._, A<string>._, A<TerminarRecepcionReposicion>._))
                .Throws(new RecepcionReposicionException("Lea la etiqueta del bulto antes de recibir la reposición 81010"));
            await AbrirAsync();
            await LeerAsync("R81010-1");

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.AreEqual("Lea la etiqueta del bulto antes de recibir la reposición 81010", _vm.Mensaje);
        }

        [TestMethod]
        public async Task ReposicionSinEtiquetas_SeRecibeComoSiempre()
        {
            A.CallTo(() => _servicio.LeerRecepcion(A<string>._, "ALC", "81010")).Returns(Reposicion("81010", exigeEtiqueta: false));
            await AbrirAsync();

            Assert.IsFalse(_vm.EsperandoEtiqueta);
            Assert.IsTrue(_vm.PuedeLeerProductos);
            await LeerAsync("8411");
            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.AreEqual(1, _terminada!.Lecturas.Single().Cantidad);
            Assert.AreEqual(0, _terminada!.CodigosBulto.Count);
            A.CallTo(() => _servicio.LeerBulto(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CambiarDeReposicion_OlvidaLasEtiquetasLeidas()
        {
            await AbrirAsync();
            await LeerAsync("R81010-1");

            await _vm.ElegirCommand.ExecuteAsync(_vm.Pendientes[1]);

            Assert.AreEqual(0, _vm.CodigosBulto.Count);
            Assert.IsTrue(_vm.EsperandoEtiqueta);
        }

        [TestMethod]
        public void EsEtiquetaBulto_SoloElFormatoRTraspasoGuionBulto()
        {
            Assert.IsTrue(RecibirReposicionViewModel.EsEtiquetaBulto("R81010-2"));
            Assert.IsTrue(RecibirReposicionViewModel.EsEtiquetaBulto(" r81010-12 "));
            Assert.IsFalse(RecibirReposicionViewModel.EsEtiquetaBulto("8411"));
            Assert.IsFalse(RecibirReposicionViewModel.EsEtiquetaBulto("R81010"));
            Assert.IsFalse(RecibirReposicionViewModel.EsEtiquetaBulto("17404"));
        }

        // NestoAPI#675: la pistola con teclado americano en un Windows español manda «R81029'1»
        [TestMethod]
        [DataRow("R81029-1", "R81029-1")]
        [DataRow("R81029'1", "R81029-1")]
        [DataRow("r81029?1", "R81029-1")]
        [DataRow(" R81029-1 ", "R81029-1")]
        [DataRow("R81029_1", "R81029-1")]
        [DataRow("R81029/1", "R81029-1")]
        [DataRow("R081029'01", "R81029-1")]
        public void NormalizarEtiquetaBulto_CualquierSeparador_SiempreConGuion(string leido, string esperado)
        {
            Assert.IsTrue(RecibirReposicionViewModel.EsEtiquetaBulto(leido));
            Assert.AreEqual(esperado, RecibirReposicionViewModel.NormalizarEtiquetaBulto(leido));
        }

        [TestMethod]
        [DataRow("8437017509999")]
        [DataRow("17404")]
        [DataRow("8411")]
        [DataRow("R81029")]
        [DataRow("R810291")]
        [DataRow("R81029A1")]
        [DataRow("RE81029-1")]
        [DataRow("AB-1234")]
        [DataRow("R81029-")]
        [DataRow("R81029-0")]
        [DataRow("")]
        [DataRow(null)]
        public void NormalizarEtiquetaBulto_UnProductoOUnaReferencia_NoEsEtiqueta(string? leido)
        {
            Assert.IsFalse(RecibirReposicionViewModel.EsEtiquetaBulto(leido));
            Assert.IsNull(RecibirReposicionViewModel.NormalizarEtiquetaBulto(leido));
        }

        [TestMethod]
        public async Task LeerEtiqueta_ConApostrofo_LaPideALaApiConGuionYLaApunta()
        {
            await AbrirAsync();

            await LeerAsync("R81010'1");
            await LeerAsync("8411");
            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.LeerBulto("1", "R81010-1")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerBulto(A<string>._, "R81010'1")).MustNotHaveHappened();
            CollectionAssert.AreEqual(new[] { "R81010-1" }, _terminada!.CodigosBulto);
        }
    }
}
