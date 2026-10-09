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
    /// La tienda prepara la reposición que manda a Algete y la termina (NestoAPI#553), en lugar de hacerlo en Nesto
    /// viejo: el servidor propone, aquí solo se baja lo que no se manda y se termina.
    /// </summary>
    [TestClass]
    public class EnviarReposicionViewModelTests
    {
        private IServicioEnvioReposiciones _servicio = null!;
        private IServicioDialogos _dialogos = null!;
        private IConfiguracion _configuracion = null!;
        private EnviarReposicionViewModel _vm = null!;
        private ISonidosLector _sonidos = null!;
        private readonly List<string> _abiertos = new();
        private int _focosEnLector;

        private static ReposicionEnPreparacion Reposicion(int cantidadCera = 3) => new()
        {
            Empresa = "1",
            Origen = "ALC",
            Destino = "ALG",
            Lineas = new List<LineaReposicionEnPreparacion>
            {
                new() { NumeroOrden = 101, Producto = "17404", Nombre = "Cera", CodigoBarras = "8411", Cantidad = cantidadCera, StockOrigen = 7 },
                new() { NumeroOrden = 102, Producto = "40057", Nombre = "Banda", CodigoBarras = "8422", Cantidad = 2, StockOrigen = 4 }
            }
        };

        [TestInitialize]
        public void Preparar()
        {
            _servicio = A.Fake<IServicioEnvioReposiciones>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("alc ");
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Reposicion());
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(true);
            _sonidos = A.Fake<ISonidosLector>();
            _abiertos.Clear();
            _focosEnLector = 0;
            _vm = new EnviarReposicionViewModel(_servicio, _dialogos, _configuracion, _sonidos, ruta => _abiertos.Add(ruta));
            _vm.PedirFocoEnLector += () => _focosEnLector++;
        }

        private async Task CambiarCantidadAsync(int fila, int cantidad)
        {
            _vm.Lineas[fila].Cantidad = cantidad;
            if (_vm.CambiarCantidadCommand.ExecutionTask != null)
            {
                await _vm.CambiarCantidadCommand.ExecutionTask;
            }
        }

        [TestMethod]
        public async Task Cargar_ConUnaEnPreparacion_LaEnsena()
        {
            await _vm.CargarAsync();

            Assert.AreEqual("ALC", _vm.Almacen);
            Assert.IsTrue(_vm.HayReposicion);
            Assert.IsFalse(_vm.PuedePreparar);
            Assert.AreEqual(2, _vm.Lineas.Count);
            Assert.AreEqual(5, _vm.Unidades);
            Assert.AreEqual(2, _vm.Productos);
            Assert.AreEqual(7, _vm.Lineas[0].StockOrigen);
            Assert.IsTrue(_vm.TerminarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cargar_SinNingunaEnPreparacion_DejaPreparar()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.HayReposicion);
            Assert.IsTrue(_vm.PuedePreparar);
            Assert.IsTrue(_vm.PrepararCommand.CanExecute(null));
            Assert.IsFalse(_vm.TerminarCommand.CanExecute(null));
            StringAssert.Contains(_vm.Mensaje, "Preparar reposición");
        }

        [TestMethod]
        public async Task Cargar_DesdeAlgete_RemiteAAriadnaYNoPideNada()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");

            await _vm.CargarAsync();

            Assert.AreEqual("Las reposiciones desde Algete se hacen en Ariadna.", _vm.Mensaje);
            Assert.IsFalse(_vm.PuedePreparar);
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_SinAlmacenDelUsuario_LoDiceYNoPideNada()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns(" ");

            await _vm.CargarAsync();

            StringAssert.Contains(_vm.Mensaje, "almacén");
            Assert.IsFalse(_vm.PuedePreparar);
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Preparar_PideLaPropuestaDeLaTiendaAAlgeteYLaEnsena()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            CrearReposicion? pedida = null;
            A.CallTo(() => _servicio.Crear(A<CrearReposicion>._))
                .ReturnsLazily((CrearReposicion c) => { pedida = c; return Task.FromResult(Reposicion()); });
            await _vm.CargarAsync();

            await _vm.PrepararCommand.ExecuteAsync(null);

            Assert.IsNotNull(pedida);
            Assert.AreEqual("ALC", pedida!.Origen);
            Assert.AreEqual("ALG", pedida.Destino);
            Assert.IsNull(pedida.Lineas, "Sin líneas: la propuesta la calcula el servidor");
            Assert.IsTrue(_vm.HayReposicion);
            Assert.AreEqual(2, _vm.Lineas.Count);
        }

        [TestMethod]
        public async Task Preparar_ConConflicto_EnsenaElTextoDelServidorEnElDialogo()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            const string motivo = "El almacén ALC tiene un inventario en curso: termínalo antes de crear una reposición.";
            A.CallTo(() => _servicio.Crear(A<CrearReposicion>._)).Throws(new EnvioReposicionException(motivo));
            await _vm.CargarAsync();

            await _vm.PrepararCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowError(motivo)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(motivo, _vm.Mensaje);
            Assert.IsFalse(_vm.HayReposicion);
            Assert.IsTrue(_vm.PuedePreparar);
        }

        [TestMethod]
        public async Task BajarCantidad_LaMandaAlServidorYPoneLoQueContesta()
        {
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", 101, 0)).Returns(Reposicion(cantidadCera: 0));
            await _vm.CargarAsync();

            await CambiarCantidadAsync(0, 0);

            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", 101, 0)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, _vm.Lineas[0].Preparada);
            Assert.IsTrue(_vm.Lineas[0].EstaACero, "Se queda en gris y se borra al terminar");
            Assert.AreEqual(2, _vm.Unidades);
            Assert.AreEqual(1, _vm.Productos);
        }

        [TestMethod]
        public async Task SubirCantidad_NoLlamaALaApiYVuelveALaPreparada()
        {
            await _vm.CargarAsync();

            await CambiarCantidadAsync(0, 5);

            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, A<string>._, A<int>._, A<int>._)).MustNotHaveHappened();
            Assert.AreEqual(3, _vm.Lineas[0].Cantidad);
            StringAssert.Contains(_vm.Mensaje, "Solo se puede bajar");
        }

        [TestMethod]
        public async Task BajarCantidad_SiElServidorNoLaAcepta_VuelveALaPreparadaYEnsenaElMotivo()
        {
            const string motivo = "La línea 101 ha cambiado mientras tanto: vuelve a cargar la reposición.";
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, A<string>._, A<int>._, A<int>._)).Throws(new EnvioReposicionException(motivo));
            await _vm.CargarAsync();

            await CambiarCantidadAsync(0, 1);

            Assert.AreEqual(3, _vm.Lineas[0].Cantidad);
            A.CallTo(() => _dialogos.ShowError(motivo)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Leer_UnCodigoDeLaLista_SeleccionaSuLineaYPideElFocoEnLaCantidad()
        {
            await _vm.CargarAsync();
            LineaEnviarReposicion? enfocada = null;
            _vm.PedirFocoEnCantidad += l => enfocada = l;

            _vm.Lectura = " 8422 ";
            _vm.LeerLecturaCommand.Execute(null);

            Assert.AreSame(_vm.Lineas[1], _vm.Seleccionada);
            Assert.AreSame(_vm.Lineas[1], enfocada);
            Assert.AreEqual(string.Empty, _vm.Lectura);
        }

        [TestMethod]
        public async Task Leer_UnCodigoQueNoEsta_LoDice()
        {
            await _vm.CargarAsync();
            var pedido = false;
            _vm.PedirFocoEnCantidad += _ => pedido = true;

            _vm.LeerCommand.Execute("55555");

            Assert.IsFalse(pedido);
            StringAssert.Contains(_vm.Mensaje, "no está");
        }

        [TestMethod]
        public async Task Leer_SinReposicionEnPreparacion_LoDiceYVaciaElCuadro()
        {
            // Incidencia 505: lo leído desaparecía sin decir nada
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            await _vm.CargarAsync();

            _vm.Lectura = "8411";
            _vm.LeerLecturaCommand.Execute(null);

            Assert.AreEqual("Primero prepara la reposición (botón «Preparar reposición») y después lee los códigos.", _vm.Mensaje);
            Assert.AreEqual(string.Empty, _vm.Lectura);
        }

        [TestMethod]
        public async Task Terminar_PideConfirmacionConElResumenYVuelveAPreparar()
        {
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).Returns(new ResultadoTerminarReposicion
            {
                NumTraspaso = 80999,
                Lineas = new List<LineaTraspasoTerminado> { new() { Producto = "17404", Cantidad = 3 }, new() { Producto = "40057", Cantidad = 2 } }
            });
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, "Se mandan 5 unidades de 2 productos a Algete. ¿Terminar?"))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>.That.Contains("80999"))).MustHaveHappenedOnceExactly();
            StringAssert.Contains(_vm.Mensaje, "80999");
            Assert.AreEqual(0, _vm.Lineas.Count);
            Assert.IsFalse(_vm.HayReposicion);
            Assert.IsTrue(_vm.PuedePreparar);
        }

        [TestMethod]
        public async Task Terminar_ConLineasACero_NoLasCuentaEnLaConfirmacion()
        {
            A.CallTo(() => _servicio.CambiarCantidad(A<string>._, "ALC", 101, 0)).Returns(Reposicion(cantidadCera: 0));
            await _vm.CargarAsync();
            await CambiarCantidadAsync(0, 0);

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, "Se mandan 2 unidades de 1 productos a Algete. ¿Terminar?"))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Terminar_SiNoSeConfirma_NoMandaNada()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(2, _vm.Lineas.Count);
        }

        [TestMethod]
        public async Task Terminar_SiFallaEnElServidor_EnsenaSuTextoYSigueLaReposicion()
        {
            const string motivo = "El producto 17404 quedaría con stock negativo en ALC.";
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).Throws(new EnvioReposicionException(motivo));
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowError(motivo)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(motivo, _vm.Mensaje);
            Assert.AreEqual(2, _vm.Lineas.Count);
            Assert.IsTrue(_vm.HayReposicion);
        }

        [TestMethod]
        public async Task Terminar_ConTodoACero_NoPreguntaNiLlamaALaApi()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(new ReposicionEnPreparacion
            {
                Lineas = new List<LineaReposicionEnPreparacion> { new() { NumeroOrden = 101, Producto = "17404", Cantidad = 0 } }
            });
            await _vm.CargarAsync();

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).MustNotHaveHappened();
            StringAssert.Contains(_vm.Mensaje, "a 0");
        }

        // NestoAPI#577: las reposiciones de la tienda a Algete las rellena la API a la hora de corte del calendario; a mano
        // solo pueden las personas autorizadas (GET api/Reposiciones/PuedeRellenarManual).

        private void SinPermisoYSinReposicion()
        {
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(false);
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            _vm.Ahora = () => new DateTime(2026, 10, 8, 17, 30, 0); // jueves
        }

        [TestMethod]
        public async Task Cargar_SinPermisoParaRellenar_NoDejaPreparar()
        {
            SinPermisoYSinReposicion();

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.PuedePreparar);
            Assert.IsFalse(_vm.PrepararCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Cargar_SinPermisoNiReposicion_DiceCuandoSeRellenaSola()
        {
            SinPermisoYSinReposicion();
            A.CallTo(() => _servicio.LeerProximaLlegada(A<string>._, "ALC", "ALG"))
                .Returns(new ProximaReposicion { Origen = "ALC", Destino = "ALG", CierraEl = new DateTime(2026, 10, 12, 10, 0, 0) });

            await _vm.CargarAsync();

            Assert.AreEqual("Todavía no hay reposición para Algete. Se rellena sola el lunes 12/10 a las 10:00.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Cargar_SinPermisoNiReposicion_SiSeRellenaMananaLoDiceAsi()
        {
            SinPermisoYSinReposicion();
            A.CallTo(() => _servicio.LeerProximaLlegada(A<string>._, "ALC", "ALG"))
                .Returns(new ProximaReposicion { CierraEl = new DateTime(2026, 10, 9, 10, 0, 0) });

            await _vm.CargarAsync();

            Assert.AreEqual("Todavía no hay reposición para Algete. Se rellena sola mañana a las 10:00.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Cargar_SinPermisoNiReposicion_SiHoyTodaviaNoHaCerradoDiceHoy()
        {
            SinPermisoYSinReposicion();
            _vm.Ahora = () => new DateTime(2026, 10, 9, 8, 15, 0);
            A.CallTo(() => _servicio.LeerProximaLlegada(A<string>._, "ALC", "ALG"))
                .Returns(new ProximaReposicion { CierraEl = new DateTime(2026, 10, 9, 10, 0, 0) });

            await _vm.CargarAsync();

            Assert.AreEqual("Todavía no hay reposición para Algete. Se rellena sola hoy a las 10:00.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Cargar_SinPermisoNiReposicion_SinCalendario_DiceQueSeRellenaASuHora()
        {
            SinPermisoYSinReposicion();
            A.CallTo(() => _servicio.LeerProximaLlegada(A<string>._, "ALC", "ALG")).Returns(Task.FromResult<ProximaReposicion>(null!));

            await _vm.CargarAsync();

            Assert.AreEqual("Todavía no hay reposición para Algete. Se rellena sola a su hora.", _vm.Mensaje);
        }

        [TestMethod]
        public async Task Cargar_SinPermisoNiReposicion_SiFallaElCalendario_DiceQueSeRellenaASuHoraSinAvisarDeError()
        {
            SinPermisoYSinReposicion();
            A.CallTo(() => _servicio.LeerProximaLlegada(A<string>._, A<string>._, A<string>._)).Throws(new HttpRequestException("caído"));

            await _vm.CargarAsync();

            Assert.AreEqual("Todavía no hay reposición para Algete. Se rellena sola a su hora.", _vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_SinPermisoPeroConReposicionRellena_LaEnsenaParaTerminarla()
        {
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(false);

            await _vm.CargarAsync();

            Assert.IsTrue(_vm.HayReposicion);
            Assert.IsTrue(_vm.TerminarCommand.CanExecute(null));
            A.CallTo(() => _servicio.LeerProximaLlegada(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_ConPermisoYSinReposicion_DejaPrepararConElTextoDeSiempre()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));

            await _vm.CargarAsync();

            Assert.IsTrue(_vm.PuedePreparar);
            Assert.AreEqual("ALC no tiene ninguna reposición en preparación. Pulsa «Preparar reposición» y se propone lo que hay que mandar a Algete.", _vm.Mensaje);
            A.CallTo(() => _servicio.LeerProximaLlegada(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Leer_SinPermisoNiReposicion_NoPideQuePrepare()
        {
            SinPermisoYSinReposicion();
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("8411");

            Assert.IsFalse(_vm.Mensaje!.Contains("Preparar reposición"), _vm.Mensaje);
            StringAssert.Contains(_vm.Mensaje, "Todavía no hay reposición para Algete");
        }

        [TestMethod]
        public async Task Preparar_MandaQueLaCreaNesto()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            CrearReposicion? pedida = null;
            A.CallTo(() => _servicio.Crear(A<CrearReposicion>._))
                .ReturnsLazily((CrearReposicion c) => { pedida = c; return Task.FromResult(Reposicion()); });
            await _vm.CargarAsync();

            await _vm.PrepararCommand.ExecuteAsync(null);

            Assert.AreEqual("Nesto", pedida!.Herramienta);
        }

        [TestMethod]
        public async Task Preparar_ConUn403_EnsenaElMotivoSinErrorYEscondeElBoton()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            const string motivo = "Solo el proceso automático y las personas autorizadas pueden rellenar reposiciones a mano.";
            A.CallTo(() => _servicio.Crear(A<CrearReposicion>._)).Throws(new EnvioReposicionException(motivo, 403));
            await _vm.CargarAsync();

            await _vm.PrepararCommand.ExecuteAsync(null);

            Assert.AreEqual(motivo, _vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, motivo)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(_vm.PuedePreparar);
        }

        // ---- Sonido al leer y cursor en el lector ----

        [TestMethod]
        public async Task Leer_QueEstaEnLaReposicion_SuenaElDeConfirmacionYVaASuCantidad()
        {
            await _vm.CargarAsync();
            LineaEnviarReposicion? enfocada = null;
            _vm.PedirFocoEnCantidad += l => enfocada = l;

            _vm.LeerCommand.Execute("8422");

            A.CallTo(() => _sonidos.Correcto()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _sonidos.Error()).MustNotHaveHappened();
            Assert.AreEqual("40057", enfocada?.Producto);
            Assert.AreEqual(0, _focosEnLector);
        }

        [TestMethod]
        public async Task Leer_QueNoEstaEnLaReposicion_SuenaElErrorYSeQuedaEnElLector()
        {
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("99999");

            A.CallTo(() => _sonidos.Error()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _sonidos.Correcto()).MustNotHaveHappened();
            Assert.AreEqual(1, _focosEnLector);
            StringAssert.Contains(_vm.Mensaje, "no está en esta reposición");
        }

        [TestMethod]
        public async Task Leer_UnCodigoDeVariosProductos_SuenaElError()
        {
            ReposicionEnPreparacion reposicion = Reposicion();
            reposicion.Lineas[1].CodigoBarras = "8411";
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(reposicion);
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("8411");

            A.CallTo(() => _sonidos.Error()).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, _focosEnLector);
            Assert.IsNull(_vm.Seleccionada);
        }

        [TestMethod]
        public async Task Leer_SinReposicion_SuenaElError()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            await _vm.CargarAsync();

            _vm.LeerCommand.Execute("8411");

            A.CallTo(() => _sonidos.Error()).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, _focosEnLector);
        }

        // ---- Sugerencia 564: imprimir la reposición para prepararla a mano ----

        [TestMethod]
        public async Task Imprimir_SinReposicionEnPreparacion_NoSePuede()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            await _vm.CargarAsync();

            Assert.IsFalse(_vm.ImprimirCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Imprimir_DescargaElPdfDeLaDeLaTiendaYLoAbre()
        {
            A.CallTo(() => _servicio.DescargarListadoPdf(A<string>._, A<string>._)).Returns(new byte[] { 37, 80, 68, 70 });
            await _vm.CargarAsync();
            Assert.IsTrue(_vm.ImprimirCommand.CanExecute(null));

            await _vm.ImprimirCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.DescargarListadoPdf("1", "ALC")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, _abiertos.Count);
            StringAssert.Contains(_abiertos[0], "Reposicion_ALC");
            CollectionAssert.AreEqual(new byte[] { 37, 80, 68, 70 }, File.ReadAllBytes(_abiertos[0]));
            File.Delete(_abiertos[0]);
        }

        [TestMethod]
        public async Task Imprimir_SiLaApiNoLoDa_DiceElMotivoYNoAbreNada()
        {
            A.CallTo(() => _servicio.DescargarListadoPdf(A<string>._, A<string>._))
                .ThrowsAsync(new EnvioReposicionException("ALC no tiene ninguna reposición en preparación.", 404));
            await _vm.CargarAsync();

            await _vm.ImprimirCommand.ExecuteAsync(null);

            Assert.AreEqual("ALC no tiene ninguna reposición en preparación.", _vm.Mensaje);
            Assert.AreEqual(0, _abiertos.Count);
        }
    }
}
