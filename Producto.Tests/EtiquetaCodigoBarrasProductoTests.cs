using FakeItEasy;
using CommunityToolkit.Mvvm.Messaging;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#534 (NestoAPI#654, sugerencia 546 de Paloma): la etiqueta de código de barras de producto se imprime desde la ficha
    /// del producto y desde cada línea de Recibir reposición. La monta e imprime la API: aquí solo se le pide (fake).
    /// </summary>
    [TestClass]
    public class EtiquetaCodigoBarrasProductoTests
    {
        private IServicioEtiquetasProducto _etiquetas = null!;
        private IServicioDialogos _dialogos = null!;
        private IProductoService _productos = null!;

        [TestInitialize]
        public void Preparar()
        {
            _etiquetas = A.Fake<IServicioEtiquetasProducto>();
            _dialogos = A.Fake<IServicioDialogos>();
            _productos = A.Fake<IProductoService>();
            A.CallTo(() => _etiquetas.Imprimir(A<string>._, A<string>._, A<int>._, A<bool>._))
                .ReturnsLazily((string _, string producto, int copias, bool _) => Task.FromResult(new ResultadoEtiquetasProducto
                {
                    Producto = producto,
                    Impresas = copias,
                    Mensaje = $"{copias} etiqueta de {producto} mandada a \\\\RDS2016\\etiquetasalcobendas."
                }));
        }

        // ---- Ficha del producto ----

        private ProductoViewModel Ficha(params string[] grupos)
        {
            var configuracion = A.Fake<IConfiguracion>();
            foreach (string grupo in grupos)
            {
                A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            }
            var vm = new ProductoViewModel(A.Fake<IServicioNavegacion>(), configuracion, _productos,
                new WeakReferenceMessenger(), _dialogos, A.Fake<IServicioAutenticacion>())
            {
                ServicioEtiquetasProducto = _etiquetas
            };
            return vm;
        }

        [TestMethod]
        public async Task Ficha_ImprimeLaEtiquetaDelProductoConLasCopiasYEnseñaElMensajeDeLaApi()
        {
            ProductoViewModel vm = Ficha(Constantes.GruposSeguridad.TIENDAS);
            vm.ProductoActual = new ProductoModel { Producto = "17404     ", Nombre = "ROLLO PAPEL CAMILLA" };
            vm.CopiasEtiquetaCodigoBarras = 3;

            await vm.ImprimirEtiquetaCodigoBarrasCommand.ExecuteAsync(null);

            A.CallTo(() => _etiquetas.Imprimir(Constantes.Empresas.EMPRESA_DEFECTO, "17404", 3, false)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogos.ShowNotification(@"3 etiqueta de 17404 mandada a \\RDS2016\etiquetasalcobendas.")).MustHaveHappenedOnceExactly();
        }

        private void ApiDevuelve(string codigo, bool asignado, string mensaje)
            => A.CallTo(() => _etiquetas.Imprimir(A<string>._, A<string>._, A<int>._, A<bool>._))
                .Returns(new ResultadoEtiquetasProducto
                {
                    Producto = "17404",
                    Impresas = 1,
                    Mensaje = mensaje,
                    CodigoBarras = codigo,
                    CodigoInterno = codigo.StartsWith("29"),
                    CodigoAsignado = asignado
                });

        [TestMethod]
        public async Task Ficha_ConCodigoAsignado_LoDiceYRecargaLosCodigosDeLaFicha()
        {
            // Nesto#598 (NestoAPI#791): el producto no tenía código y la API le ha dado el interno al imprimir
            ApiDevuelve("2900000174047", true, "1 etiqueta de 17404 (código interno 2900000174047, recién dado) mandada a etiquetas4.");
            A.CallTo(() => _productos.LeerCodigosBarras("17404")).Returns(new List<CodigoBarrasProductoModel>
            {
                new() { Codigo = "2900000174047", Cantidad = 1, Activo = true }
            });
            ProductoViewModel vm = Ficha(Constantes.GruposSeguridad.TIENDAS);
            vm.ProductoActual = new ProductoModel { Producto = "17404" };
            Fake.ClearRecordedCalls(_productos);

            await vm.ImprimirEtiquetaCodigoBarrasCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowNotification(A<string>.That.Contains("se le ha dado el código interno 2900000174047")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _productos.LeerCodigosBarras("17404")).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.CodigosBarras.Any(c => c.Codigo == "2900000174047"));
        }

        [TestMethod]
        public async Task Ficha_SinCodigoAsignado_NoRecargaLosCodigosYEnseñaElCodigoImpreso()
        {
            ApiDevuelve("8411111111110", false, "1 etiqueta de 17404 mandada a etiquetas4.");
            ProductoViewModel vm = Ficha(Constantes.GruposSeguridad.TIENDAS);
            vm.ProductoActual = new ProductoModel { Producto = "17404" };
            Fake.ClearRecordedCalls(_productos);

            await vm.ImprimirEtiquetaCodigoBarrasCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowNotification("1 etiqueta de 17404 mandada a etiquetas4.\nCódigo impreso: 8411111111110."))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _productos.LeerCodigosBarras(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Ficha_ElMensajeDeLaApiYaLlevaElCodigo_NoSeRepite()
        {
            string texto = ProductoViewModel.TextoEtiquetaImpresa("17404", new ResultadoEtiquetasProducto
            {
                Mensaje = "1 etiqueta de 17404 (8411111111110) mandada a etiquetas4.",
                CodigoBarras = "8411111111110"
            });

            Assert.AreEqual("1 etiqueta de 17404 (8411111111110) mandada a etiquetas4.", texto);
        }

        [TestMethod]
        public void Ficha_AlCambiarDeReferencia_LasCopiasVuelvenAUno()
        {
            // Sugerencia 734 (Reina): imprimió 5 de una referencia y, al pasar a otra, seguían puestas 5
            ProductoViewModel vm = Ficha(Constantes.GruposSeguridad.TIENDAS);
            vm.ProductoActual = new ProductoModel { Producto = "17404" };
            vm.CopiasEtiquetaCodigoBarras = 5;

            vm.ProductoActual = new ProductoModel { Producto = "24883" };

            Assert.AreEqual(1, vm.CopiasEtiquetaCodigoBarras);
        }

        [TestMethod]
        public void Ficha_AlRecargarLaMismaReferencia_LasCopiasNoCambian()
        {
            ProductoViewModel vm = Ficha(Constantes.GruposSeguridad.TIENDAS);
            vm.ProductoActual = new ProductoModel { Producto = "17404" };
            vm.CopiasEtiquetaCodigoBarras = 5;

            vm.ProductoActual = new ProductoModel { Producto = "17404     " };

            Assert.AreEqual(5, vm.CopiasEtiquetaCodigoBarras);
        }

        [TestMethod]
        public void Ficha_SinProducto_ElBotonNoSePuedePulsar()
        {
            ProductoViewModel vm = Ficha(Constantes.GruposSeguridad.TIENDAS);

            Assert.IsFalse(vm.ImprimirEtiquetaCodigoBarrasCommand.CanExecute(null));
            vm.ProductoActual = new ProductoModel { Producto = "17404" };
            Assert.IsTrue(vm.ImprimirEtiquetaCodigoBarrasCommand.CanExecute(null));
        }

        [TestMethod]
        public void Ficha_ElBotonLoVenLosMismosGruposQueDejaLaApi()
        {
            Assert.IsTrue(Ficha(Constantes.GruposSeguridad.TIENDAS).PuedeImprimirEtiquetaCodigoBarras);
            Assert.IsTrue(Ficha(Constantes.GruposSeguridad.COMPRAS).PuedeImprimirEtiquetaCodigoBarras);
            Assert.IsTrue(Ficha(Constantes.GruposSeguridad.ALMACEN).PuedeImprimirEtiquetaCodigoBarras);
            Assert.IsTrue(Ficha(Constantes.GruposSeguridad.DIRECCION).PuedeImprimirEtiquetaCodigoBarras);
            Assert.IsTrue(Ficha(Constantes.GruposSeguridad.INFORMATICA).PuedeImprimirEtiquetaCodigoBarras);
            Assert.IsFalse(Ficha().PuedeImprimirEtiquetaCodigoBarras);
        }

        [TestMethod]
        public async Task Ficha_LaApiNoLaImprime_SeEnseñaSuMotivo()
        {
            A.CallTo(() => _etiquetas.Imprimir(A<string>._, A<string>._, A<int>._, A<bool>._))
                .ThrowsAsync(new EtiquetasProductoException("El usuario Paloma no tiene impresora de etiquetas."));
            ProductoViewModel vm = Ficha(Constantes.GruposSeguridad.TIENDAS);
            vm.ProductoActual = new ProductoModel { Producto = "17404" };

            await vm.ImprimirEtiquetaCodigoBarrasCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowError(A<string>.That.Contains("no tiene impresora de etiquetas"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Ficha_LasCopiasVanDeUnoACien()
        {
            ProductoViewModel vm = Ficha();

            vm.CopiasEtiquetaCodigoBarras = 0;
            Assert.AreEqual(1, vm.CopiasEtiquetaCodigoBarras);
            vm.CopiasEtiquetaCodigoBarras = 500;
            Assert.AreEqual(100, vm.CopiasEtiquetaCodigoBarras);
        }

        // ---- Recibir reposición ----

        private RecibirReposicionViewModel Recibir(IServicioEtiquetasProducto? etiquetas)
            => new RecibirReposicionViewModel(A.Fake<IServicioRecepcionReposiciones>(), _dialogos, A.Fake<IConfiguracion>(),
                A.Fake<ISonidosLector>(), A.Fake<IServicioNavegacion>(), _ => { }, etiquetas);

        [TestMethod]
        public async Task Recibir_ElBotonDeLaLinea_ImprimeLaEtiquetaDeSuProductoYDiceDonde()
        {
            RecibirReposicionViewModel vm = Recibir(_etiquetas);
            vm.CopiasEtiqueta = 2;
            var linea = new LineaRecibirReposicion { Producto = "17404", Descripcion = "ROLLO PAPEL CAMILLA", Enviado = 2 };

            await vm.ImprimirEtiquetaCommand.ExecuteAsync(linea);

            A.CallTo(() => _etiquetas.Imprimir(Constantes.Empresas.EMPRESA_DEFECTO, "17404", 2, false)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(@"2 etiqueta de 17404 mandada a \\RDS2016\etiquetasalcobendas.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Recibir_ConCodigoAsignado_LaLineaApuntaElCodigoYLaLecturaCasa()
        {
            // Nesto#598 (NestoAPI#791): la lista abierta no tiene el interno recién dado; sin apuntarlo, la lectura iría a la API
            ApiDevuelve("2900000174047", true, "1 etiqueta de 17404 (código interno 2900000174047, recién dado) mandada a etiquetas4.");
            RecibirReposicionViewModel vm = Recibir(_etiquetas);
            var linea = new LineaRecibirReposicion { Producto = "17404", SinCodigo = true, Enviado = 2 };

            await vm.ImprimirEtiquetaCommand.ExecuteAsync(linea);

            CollectionAssert.AreEqual(new[] { "2900000174047" }, linea.CodigosBarras);
            Assert.IsTrue(linea.TieneCodigo("2900000174047"));
        }

        [TestMethod]
        public async Task Recibir_SinCodigoAsignado_LaLineaNoCambia()
        {
            ApiDevuelve("8411111111110", false, "1 etiqueta de 17404 (8411111111110) mandada a etiquetas4.");
            RecibirReposicionViewModel vm = Recibir(_etiquetas);
            var linea = new LineaRecibirReposicion
            {
                Producto = "17404",
                CodigoBarras = "8411111111110",
                CodigosBarras = new List<string> { "8411111111110" },
                Enviado = 2
            };

            await vm.ImprimirEtiquetaCommand.ExecuteAsync(linea);

            CollectionAssert.AreEqual(new[] { "8411111111110" }, linea.CodigosBarras);
        }

        [TestMethod]
        public async Task Recibir_LaApiNoLaImprime_ElMotivoEnElMensaje()
        {
            A.CallTo(() => _etiquetas.Imprimir(A<string>._, A<string>._, A<int>._, A<bool>._))
                .ThrowsAsync(new EtiquetasProductoException("No existe el producto 8437000000001."));
            RecibirReposicionViewModel vm = Recibir(_etiquetas);

            await vm.ImprimirEtiquetaCommand.ExecuteAsync(new LineaRecibirReposicion { Producto = "8437000000001", NoVenia = true });

            Assert.AreEqual("No existe el producto 8437000000001.", vm.Mensaje);
        }

        [TestMethod]
        public void Recibir_UnaLineaSinProducto_NoSePuedePulsar()
        {
            RecibirReposicionViewModel vm = Recibir(_etiquetas);

            Assert.IsFalse(vm.ImprimirEtiquetaCommand.CanExecute(new LineaRecibirReposicion()));
            Assert.IsFalse(vm.ImprimirEtiquetaCommand.CanExecute(null));
            Assert.IsTrue(vm.ImprimirEtiquetaCommand.CanExecute(new LineaRecibirReposicion { Producto = "17404" }));
        }

        [TestMethod]
        public async Task Recibir_SinServicioDeEtiquetas_NoHaceNada()
        {
            RecibirReposicionViewModel vm = Recibir(null);

            await vm.ImprimirEtiquetaCommand.ExecuteAsync(new LineaRecibirReposicion { Producto = "17404" });

            Assert.IsNull(vm.Mensaje);
        }
    }
}
