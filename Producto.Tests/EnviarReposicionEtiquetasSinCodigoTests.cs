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
    /// NestoAPI#762: el ORIGEN de la reposición etiqueta lo que no tiene código de barras, porque en el destino solo se recibe
    /// escaneando. Las líneas sin ningún código válido se marcan («Sin código de barras: imprima sus etiquetas antes de enviar»)
    /// y cada línea tiene su botón (por defecto, tantas etiquetas como unidades); otro botón las imprime todas a la vez.
    /// </summary>
    [TestClass]
    public class EnviarReposicionEtiquetasSinCodigoTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IServicioEtiquetasProducto _etiquetas = null!;
        private EnviarReposicionViewModel _vm = null!;
        private List<(string Producto, int Copias)> _impresas = null!;
        /// <summary>Nesto#605: la reposición lleva además una muestra (MMP) sin código, admitida sin etiqueta.</summary>
        private bool _conMuestra;
        /// <summary>Nesto#605: solo la muestra.</summary>
        private bool _soloMuestra;

        private ReposicionEnPreparacion Reposicion()
        {
            ReposicionEnPreparacion reposicion = ReposicionBase();
            var muestra = new LineaReposicionEnPreparacion
            {
                NumeroOrden = 104, Producto = "50001", Nombre = "Muestra crema", CodigosBarras = new List<string>(), SinCodigoAdmitido = true, Cantidad = 5, StockOrigen = 9
            };
            if (_soloMuestra)
            {
                reposicion.Lineas = new List<LineaReposicionEnPreparacion> { muestra };
            }
            else if (_conMuestra)
            {
                reposicion.Lineas.Add(muestra);
            }
            return reposicion;
        }

        private static ReposicionEnPreparacion ReposicionBase() => new()
        {
            Empresa = "1",
            Origen = "ALC",
            Destino = "ALG",
            Lineas = new List<LineaReposicionEnPreparacion>
            {
                // Sin ningún código: hay que etiquetarlo
                new() { NumeroOrden = 101, Producto = "17404", Nombre = "Rollo papel camilla", CodigosBarras = new List<string>(), Cantidad = 3, StockOrigen = 7 },
                // Con un EAN-13 bueno
                new() { NumeroOrden = 102, Producto = "40057", Nombre = "Banda", CodigoBarras = "8411111111110", CodigosBarras = new List<string> { "8411111111110" }, Cantidad = 2, StockOrigen = 4 },
                // Con basura en la ficha («1»): la API no la manda en la lista (no es un GTIN válido): cuenta como sin código
                new() { NumeroOrden = 103, Producto = "33333", Nombre = "Lima", CodigoBarras = "1", CodigosBarras = new List<string>(), Cantidad = 1, StockOrigen = 2 }
            }
        };

        [TestInitialize]
        public void Preparar()
        {
            _impresas = new List<(string, int)>();
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _etiquetas = A.Fake<IServicioEtiquetasProducto>();
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALC");
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).ReturnsLazily(() => Task.FromResult(Reposicion()));
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(true);
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => _etiquetas.Imprimir(A<string>._, A<string>._, A<int>._, A<bool>._))
                .ReturnsLazily((string _, string producto, int copias, bool _) =>
                {
                    _impresas.Add((producto, copias));
                    string codigo = producto == "17404" ? "2900000174047" : producto == "33333" ? "2900000333338" : "8411111111110";
                    return Task.FromResult(new ResultadoEtiquetasProducto
                    {
                        Producto = producto,
                        Copias = copias,
                        Impresas = copias,
                        Mensaje = $"{copias} etiquetas de {producto} ({codigo}) mandadas a la impresora.",
                        CodigoBarras = codigo,
                        CodigoInterno = codigo.StartsWith("290"),
                        CodigoAsignado = codigo.StartsWith("290")
                    });
                });
            _vm = new EnviarReposicionViewModel(_servicio, _dialogos, configuracion, A.Fake<ISonidosLector>(), A.Fake<IServicioNavegacion>(), _ => { }, _etiquetas);
        }

        private LineaEnviarReposicion Linea(string producto) => _vm.Lineas.Single(l => l.Producto == producto);

        [TestMethod]
        public async Task LasLineasSinCodigoValido_SeMarcanConElAviso()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(Linea("17404").FaltanEtiquetas);
            Assert.AreEqual("Sin código de barras: imprima sus etiquetas antes de enviar", Linea("17404").AvisoEtiquetas);
            Assert.IsTrue(Linea("33333").FaltanEtiquetas, "Manda la lista de la API, no el principal de la ficha");
            Assert.IsFalse(Linea("40057").FaltanEtiquetas);
            Assert.IsNull(Linea("40057").AvisoEtiquetas, "Con código, sin aviso");
            Assert.IsTrue(_vm.HayLineasSinEtiquetas);
            Assert.AreEqual("2 productos no tienen código de barras: imprima sus etiquetas antes de enviar.", _vm.AvisoSinCodigo);
        }

        [TestMethod]
        public void ConUnaApiAnterior_SinLista_ValeElPrincipal()
        {
            var conPrincipal = new LineaReposicionEnPreparacion { Producto = "1", CodigoBarras = "8411111111110" };
            var sinNada = new LineaReposicionEnPreparacion { Producto = "2" };

            CollectionAssert.AreEqual(new[] { "8411111111110" }, LineaEnviarReposicion.CodigosDe(conPrincipal));
            Assert.AreEqual(0, LineaEnviarReposicion.CodigosDe(sinNada).Count);
        }

        [TestMethod]
        public async Task ElBotonDeLaLinea_ImprimeEan13TantasComoUnidades()
        {
            await _vm.CargarAsync();

            await _vm.ImprimirEtiquetasLineaCommand.ExecuteAsync(Linea("17404"));

            Assert.AreEqual(("17404", 3), _impresas.Single());
            Assert.AreEqual("3 etiquetas de 17404 (2900000174047) mandadas a la impresora.", _vm.Mensaje);
            Assert.IsFalse(Linea("17404").FaltanEtiquetas);
            Assert.AreEqual("3 etiquetas impresas (2900000174047)", Linea("17404").AvisoEtiquetas);
        }

        [TestMethod]
        public async Task ElCodigoAsignado_SeAnadeALaLinea_YLaLecturaCasa()
        {
            await _vm.CargarAsync();

            await _vm.ImprimirEtiquetasLineaCommand.ExecuteAsync(Linea("17404"));

            CollectionAssert.Contains(Linea("17404").CodigosBarras, "2900000174047");
            _vm.LeerCommand.Execute("2900000174047");
            Assert.AreSame(Linea("17404"), _vm.Seleccionada, "La etiqueta recién impresa se lee sin preguntar a la API");
        }

        [TestMethod]
        public async Task ElBotonDeLaLinea_TambienEnLasQueTienenCodigo_YSeCambianLasCopias()
        {
            await _vm.CargarAsync();
            Linea("40057").CopiasEtiqueta = 1;

            await _vm.ImprimirEtiquetasLineaCommand.ExecuteAsync(Linea("40057"));

            Assert.AreEqual(("40057", 1), _impresas.Single());
        }

        [TestMethod]
        public async Task LasCopias_SiguenALaCantidadMientrasNoSeCambian()
        {
            await _vm.CargarAsync();
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, A<string>._, A<int>._, A<int>._)).Returns(new TaskCompletionSource<ReposicionEnPreparacion>().Task);

            Linea("17404").Cantidad = 2;

            Assert.AreEqual(2, Linea("17404").CopiasEtiqueta);
        }

        [TestMethod]
        public async Task MasDeCienEtiquetas_EnVariasPeticiones()
        {
            await _vm.CargarAsync();
            Linea("17404").CopiasEtiqueta = 150;

            await _vm.ImprimirEtiquetasLineaCommand.ExecuteAsync(Linea("17404"));

            CollectionAssert.AreEqual(new[] { 100, 50 }, _impresas.Select(i => i.Copias).ToList(), "La API admite de 1 a 100 por petición");
            Assert.AreEqual(150, Linea("17404").EtiquetasImpresas);
        }

        [TestMethod]
        public async Task LaApiNoLaImprime_SuTextoYLaLineaSigueMarcada()
        {
            A.CallTo(() => _etiquetas.Imprimir(A<string>._, A<string>._, A<int>._, A<bool>._))
                .ThrowsAsync(new EtiquetasProductoException("Su usuario no tiene impresora de etiquetas."));
            await _vm.CargarAsync();

            await _vm.ImprimirEtiquetasLineaCommand.ExecuteAsync(Linea("17404"));

            Assert.AreEqual("Su usuario no tiene impresora de etiquetas.", _vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("Su usuario no tiene impresora de etiquetas.")).MustHaveHappenedOnceExactly();
            Assert.IsTrue(Linea("17404").FaltanEtiquetas);
        }

        [TestMethod]
        public async Task ImprimirLoQueNoTieneCodigo_TodasLasPendientesTrasConfirmar()
        {
            await _vm.CargarAsync();

            await _vm.ImprimirEtiquetasSinCodigoCommand.ExecuteAsync(null);

            CollectionAssert.AreEquivalent(new[] { "17404", "33333" }, _impresas.Select(i => i.Producto).ToList());
            Assert.AreEqual(3, _impresas.Single(i => i.Producto == "17404").Copias);
            Assert.AreEqual(1, _impresas.Single(i => i.Producto == "33333").Copias);
            Assert.IsFalse(_vm.HayLineasSinEtiquetas);
            Assert.IsNull(_vm.AvisoSinCodigo);
            A.CallTo(() => _dialogos.ShowConfirmationAsync(EnviarReposicionViewModel.TITULO_ETIQUETAS_PRODUCTO,
                A<string>.That.Contains("4 etiquetas de 2 productos"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ImprimirLoQueNoTieneCodigo_SinConfirmar_NoImprimeNada()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            await _vm.CargarAsync();

            await _vm.ImprimirEtiquetasSinCodigoCommand.ExecuteAsync(null);

            Assert.AreEqual(0, _impresas.Count);
        }

        [TestMethod]
        public async Task ImprimirLoQueNoTieneCodigo_NoSeSaltaLoQueEstaACero()
        {
            await _vm.CargarAsync();
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, A<string>._, A<int>._, A<int>._)).Returns(new TaskCompletionSource<ReposicionEnPreparacion>().Task);
            Linea("33333").Cantidad = 0;

            Assert.AreEqual("1 producto no tiene código de barras: imprima sus etiquetas antes de enviar.", _vm.AvisoSinCodigo,
                "Lo que no se manda no hay que etiquetarlo");
        }

        [TestMethod]
        public async Task Terminar_ConLoSinEtiquetar_LoDiceEnLaConfirmacion()
        {
            await _vm.CargarAsync();

            string texto = _vm.TextoConfirmacion();

            StringAssert.StartsWith(texto, "2 productos no tienen código de barras: imprima sus etiquetas antes de enviar (17404, 33333).");
            StringAssert.EndsWith(texto, "¿Terminar?");
        }

        [TestMethod]
        public async Task Terminar_TodoEtiquetado_LaConfirmacionDeSiempre()
        {
            await _vm.CargarAsync();
            await _vm.ImprimirEtiquetasSinCodigoCommand.ExecuteAsync(null);
            _vm.LeerTodo();

            Assert.AreEqual("Se mandan 6 unidades de 3 productos a Algete. ¿Terminar?", _vm.TextoConfirmacion());
        }

        // ---- Nesto#605 (NestoAPI#797): las muestras (MMP) sin código se mandan sin etiqueta

        [TestMethod]
        public async Task MuestraSinCodigo_NoSeMarcaNiCuentaEnElAviso()
        {
            _conMuestra = true;

            await _vm.CargarAsync();

            Assert.IsFalse(Linea("50001").FaltanEtiquetas);
            Assert.IsTrue(Linea("50001").SeMandaSinEtiqueta);
            Assert.AreEqual("Muestra sin código de barras: se manda sin etiqueta", Linea("50001").AvisoEtiquetas);
            Assert.IsTrue(Linea("17404").FaltanEtiquetas, "Lo que no es muestra, como siempre");
            Assert.AreEqual("2 productos no tienen código de barras: imprima sus etiquetas antes de enviar.", _vm.AvisoSinCodigo,
                "La muestra no cuenta en el aviso");
        }

        [TestMethod]
        public async Task MuestraSinCodigo_NoEntraEnLaConfirmacionNiEnImprimirLoQueNoTieneCodigo()
        {
            _conMuestra = true;
            await _vm.CargarAsync();

            StringAssert.StartsWith(_vm.TextoConfirmacion(), "2 productos no tienen código de barras: imprima sus etiquetas antes de enviar (17404, 33333).");
            await _vm.ImprimirEtiquetasSinCodigoCommand.ExecuteAsync(null);

            CollectionAssert.AreEquivalent(new[] { "17404", "33333" }, _impresas.Select(i => i.Producto).ToList());
            _vm.LeerTodo();
            Assert.AreEqual("Se mandan 11 unidades de 4 productos a Algete. ¿Terminar?", _vm.TextoConfirmacion());
        }

        [TestMethod]
        public async Task SoloMuestrasSinCodigo_NoHayNadaQueEtiquetar()
        {
            _soloMuestra = true;

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.HayLineasSinEtiquetas);
            Assert.IsNull(_vm.AvisoSinCodigo);
            Assert.IsFalse(_vm.ImprimirEtiquetasSinCodigoCommand.CanExecute(null));
            Assert.AreEqual("Se mandan 5 unidades de 1 productos a Algete. ¿Terminar?", _vm.TextoConfirmacion());
        }

        [TestMethod]
        public async Task MuestraSinCodigo_SuReferenciaTecleada_EligeLaLinea()
        {
            _conMuestra = true;
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("50001");

            Assert.AreSame(Linea("50001"), _vm.Seleccionada);
            Assert.IsNull(_vm.Mensaje);
        }

        [TestMethod]
        public void SinCodigoAdmitido_SiNoViene_EsFalse()
        {
            var linea = Newtonsoft.Json.JsonConvert.DeserializeObject<LineaReposicionEnPreparacion>(
                "{\"NumeroOrden\":1,\"Producto\":\"50001\",\"CodigosBarras\":[],\"Cantidad\":1}")!;

            Assert.IsFalse(linea.SinCodigoAdmitido, "Una API anterior no lo manda: sin código, a etiquetar como siempre");
            Assert.IsTrue(Newtonsoft.Json.JsonConvert.DeserializeObject<LineaReposicionEnPreparacion>("{\"SinCodigoAdmitido\":true}")!.SinCodigoAdmitido);
        }
    }
}
