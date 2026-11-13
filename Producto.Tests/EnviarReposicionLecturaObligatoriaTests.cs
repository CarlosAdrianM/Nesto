using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    internal static class EnviarReposicionLecturas
    {
        /// <summary>Novedades 1264: deja leídas todas las unidades de la reposición (lo que haría el lector, código a código).</summary>
        internal static void LeerTodo(this EnviarReposicionViewModel vm)
        {
            foreach (LineaEnviarReposicion linea in vm.Lineas)
            {
                linea.Leidas = linea.Cantidad;
            }
        }
    }

    /// <summary>
    /// Novedades 1264 (Carlos 13/11/26): al enviar la reposición pasa lo mismo que al recibirla en Algete: cada lectura del
    /// código de barras cuenta una unidad y solo sale de la tienda lo leído. El 13/11/26 salieron sin leer una laca que no
    /// era (29803 por 29804) y un producto cuya etiqueta se pidió pero no se pegó.
    /// </summary>
    [TestClass]
    public class EnviarReposicionLecturaObligatoriaTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private ISonidosLector _sonidos = null!;
        private EnviarReposicionViewModel _vm = null!;
        private int _focosEnLector;
        private int _focosEnCantidad;

        private static ReposicionEnPreparacion Reposicion(int cera = 3, int banda = 2, bool conMuestra = false)
        {
            var reposicion = new ReposicionEnPreparacion
            {
                Empresa = "1",
                Origen = "ALC",
                Destino = "ALG",
                Lineas = new List<LineaReposicionEnPreparacion>
                {
                    new() { NumeroOrden = 101, Producto = "17404", Nombre = "Cera", CodigoBarras = "8411", Cantidad = cera, StockOrigen = 7 },
                    new() { NumeroOrden = 102, Producto = "40057", Nombre = "Banda", CodigoBarras = "8422", Cantidad = banda, StockOrigen = 4 }
                }
            };
            if (conMuestra)
            {
                reposicion.Lineas.Add(new LineaReposicionEnPreparacion
                {
                    NumeroOrden = 103, Producto = "M001", Nombre = "Muestra", Cantidad = 4, StockOrigen = 9,
                    CodigosBarras = new List<string>(), SinCodigoAdmitido = true
                });
            }
            return reposicion;
        }

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALC");
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Reposicion());
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", A<int>._, A<int>._))
                .ReturnsLazily(c => Task.FromResult(Reposicion(cera: c.GetArgument<int>(2) == 101 ? c.GetArgument<int>(3) : 3,
                                               banda: c.GetArgument<int>(2) == 102 ? c.GetArgument<int>(3) : 2)));
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).Returns(new ResultadoTerminarReposicion { NumTraspaso = 81500 });
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            _sonidos = A.Fake<ISonidosLector>();
            _vm = new EnviarReposicionViewModel(_servicio, _dialogos, configuracion, _sonidos, A.Fake<IServicioNavegacion>(), _ => { });
            _focosEnLector = 0;
            _focosEnCantidad = 0;
            _vm.PedirFocoEnLector += () => _focosEnLector++;
            _vm.PedirFocoEnCantidad += _ => _focosEnCantidad++;
        }

        [TestMethod]
        public async Task Cargar_NadaLeido_CadaLineaEmpiezaEnCero()
        {
            await _vm.CargarAsync();

            Assert.AreEqual(0, _vm.Lineas[0].Leidas);
            Assert.AreEqual("0 de 3", _vm.Lineas[0].TextoLeidas);
            Assert.AreEqual(5, _vm.UnidadesPorLeer);
            Assert.AreEqual(0, _vm.UnidadesQueSeMandan);
            Assert.AreEqual("Faltan 5 unidades por leer", _vm.TextoPorLeer);
        }

        [TestMethod]
        public async Task Leer_CadaLecturaSumaUnaUnidad_YElCursorSigueEnElLector()
        {
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("8411");
            _vm.LeerCommand.Execute("8411");

            Assert.AreEqual(2, _vm.Lineas[0].Leidas);
            Assert.AreEqual("17404: 2 de 3 leídas.", _vm.Mensaje);
            Assert.AreSame(_vm.Lineas[0], _vm.Seleccionada);
            Assert.AreEqual(2, _focosEnLector);
            Assert.AreEqual(0, _focosEnCantidad);
            A.CallTo(() => _sonidos.Correcto()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Leer_MasDeLasQuePideLaReposicion_NoSumaYSuenaElError()
        {
            await _vm.CargarAsync();
            _vm.LeerCommand.Execute("8422");
            _vm.LeerCommand.Execute("8422");

            _vm.LeerCommand.Execute("8422");

            Assert.AreEqual(2, _vm.Lineas[1].Leidas);
            StringAssert.Contains(_vm.Mensaje, "ya ha leído las 2");
            A.CallTo(() => _sonidos.Error()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Leer_LaReferenciaTecleada_NoCuentaComoLectura()
        {
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("17404");

            Assert.AreEqual(0, _vm.Lineas[0].Leidas);
            StringAssert.Contains(_vm.Mensaje, "la referencia tecleada no cuenta");
            A.CallTo(() => _sonidos.Error()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Leer_ElCodigoDeOtroProducto_NoEntra()
        {
            // El caso de la laca: en la caja va la 29803 y la reposición pide la 29804
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("8021853008694");

            Assert.AreEqual(0, _vm.UnidadesQueSeMandan);
            StringAssert.Contains(_vm.Mensaje, "no está en esta reposición");
        }

        [TestMethod]
        public async Task Terminar_SinLeerNada_NoPreguntaNiMandaNada()
        {
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.AreEqual(EnviarReposicionViewModel.TEXTO_NADA_LEIDO, _vm.Mensaje);
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Terminar_ConParteSinLeer_LoDiceYBajaEnElServidorALoLeidoAntesDeTerminar()
        {
            await _vm.CargarAsync();
            _vm.LeerCommand.Execute("8411");
            _vm.LeerCommand.Execute("8411");

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._,
                    "Faltan 3 unidades por leer (17404: 1, 40057: 2): no se mandan, se quedan en la tienda."
                    + Environment.NewLine + "Se mandan 2 unidades de 1 productos a Algete. ¿Terminar?"))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", 101, 2)).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", 102, 0)).MustHaveHappenedOnceExactly())
                .Then(A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).MustHaveHappenedOnceExactly());
        }

        [TestMethod]
        public async Task Terminar_SiNoSePuedeBajarLoNoLeido_NoTermina()
        {
            await _vm.CargarAsync();
            _vm.LeerCommand.Execute("8411");
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", A<int>._, A<int>._))
                .Throws(new EnvioReposicionException("No se ha podido guardar."));

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.IsTrue(_vm.HayReposicion);
        }

        [TestMethod]
        public async Task Terminar_TodoLeido_NoTocaLasCantidades()
        {
            await _vm.CargarAsync();
            _vm.LeerTodo();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, "Se mandan 5 unidades de 2 productos a Algete. ¿Terminar?"))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, A<string>._, A<int>._, A<int>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task MuestraSinCodigo_NoSeLee_SaleConSuCantidadYElFocoVaALaCantidad()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Reposicion(conMuestra: true));
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("M001");

            Assert.IsFalse(_vm.Lineas[2].SeLee);
            Assert.AreEqual("no se lee", _vm.Lineas[2].TextoLeidas);
            Assert.AreEqual(4, _vm.Lineas[2].SeMandan);
            Assert.AreEqual(4, _vm.UnidadesQueSeMandan);
            Assert.AreEqual(5, _vm.UnidadesPorLeer);
            Assert.AreEqual(1, _focosEnCantidad);
        }

        [TestMethod]
        public async Task BajarLaCantidad_PorDebajoDeLoLeido_LoLeidoBajaConElla()
        {
            await _vm.CargarAsync();
            _vm.LeerTodo();

            _vm.Lineas[0].Cantidad = 1;
            if (_vm.CambiarCantidadCommand.ExecutionTask != null)
            {
                await _vm.CambiarCantidadCommand.ExecutionTask;
            }

            Assert.AreEqual(1, _vm.Lineas[0].Leidas);
            Assert.AreEqual(0, _vm.Lineas[0].PorLeer);
        }

        [TestMethod]
        public async Task Recargar_LoYaLeidoSigueLeido()
        {
            // Al volver a la pestaña la pantalla se rehace con lo del servidor: las lecturas no se pueden perder
            await _vm.CargarAsync();
            _vm.LeerCommand.Execute("8411");
            _vm.LeerCommand.Execute("8422");

            await _vm.CargarAsync();

            Assert.AreEqual(1, _vm.Lineas[0].Leidas);
            Assert.AreEqual(1, _vm.Lineas[1].Leidas);
            Assert.AreEqual(2, _vm.UnidadesQueSeMandan);
        }

        [TestMethod]
        public async Task Terminar_LaSiguienteReposicionEmpiezaSinNadaLeido()
        {
            await _vm.CargarAsync();
            _vm.LeerTodo();
            await _vm.TerminarCommand.ExecuteAsync(null);

            await _vm.CargarAsync();

            Assert.AreEqual(0, _vm.UnidadesQueSeMandan);
        }
    }
}
