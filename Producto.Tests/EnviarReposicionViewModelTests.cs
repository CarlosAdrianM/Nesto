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
            // Sugerencia 581: por defecto hoy hay viaje, la de hoy está rellena y enviada y el repartidor no ha pasado
            HoyEs(EstadoReposicionPuntual.ABIERTA, puedeMarcarRecogida: true);
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

        // ---------------------------------------------------------------- NestoAPI#622: lo que ha hecho hoy la API

        private const string AVISO_COMPLETADA = "La reposición a ALG (corte de hoy a las 09:00) se ha sumado a la que tenías sin enviar del 09/10. " +
            "Envíala desde «Enviar reposición».";

        private void LaApiAvisa(params string[] textos)
        {
            A.CallTo(() => _servicio.LeerAvisosAutomatica(A<string>._, "ALC")).Returns(textos
                .Select(t => new AvisoReposicionAutomatica { Origen = "ALC", Destino = "ALG", Tipo = AvisoReposicionAutomatica.COMPLETADA, Texto = t })
                .ToList());
        }

        [TestMethod]
        public async Task Cargar_ConAvisoDeLaApi_LoDiceArriba()
        {
            LaApiAvisa(AVISO_COMPLETADA);

            await _vm.CargarAsync();

            Assert.IsTrue(_vm.HayAvisoAutomatica);
            Assert.AreEqual(AVISO_COMPLETADA, _vm.AvisoAutomatica);
            Assert.IsTrue(_vm.HayReposicion, "El aviso no cambia nada más: la reposición se enseña igual");
        }

        [TestMethod]
        public async Task Cargar_SinAvisos_NoDiceNada()
        {
            await _vm.CargarAsync();

            Assert.IsFalse(_vm.HayAvisoAutomatica);
            Assert.IsNull(_vm.AvisoAutomatica);
        }

        [TestMethod]
        public async Task Cargar_SiNoSePuedenLeerLosAvisos_CargaIgualSinDecirNada()
        {
            A.CallTo(() => _servicio.LeerAvisosAutomatica(A<string>._, A<string>._)).ThrowsAsync(new HttpRequestException("Sin conexión"));

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.HayAvisoAutomatica);
            Assert.IsTrue(_vm.HayReposicion);
        }

        [TestMethod]
        public async Task Cargar_DesdeAlgete_NoPideAvisos()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");

            await _vm.CargarAsync();

            A.CallTo(() => _servicio.LeerAvisosAutomatica(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.IsFalse(_vm.HayAvisoAutomatica);
        }

        [TestMethod]
        public async Task Terminar_VuelveALeerLosAvisos_YElDeSinEnviarDesaparece()
        {
            LaApiAvisa(AVISO_COMPLETADA);
            A.CallTo(() => _servicio.Terminar(A<string>._, "ALC")).Returns(new ResultadoTerminarReposicion { NumTraspaso = 80999 });
            await _vm.CargarAsync();
            Assert.IsTrue(_vm.HayAvisoAutomatica);
            LaApiAvisa();

            await _vm.TerminarCommand.ExecuteAsync(null);

            Assert.IsFalse(_vm.HayAvisoAutomatica);
        }

        [TestMethod]
        public void TextoAvisos_UnoPorLinea_SinLosVacios()
        {
            string texto = EnviarReposicionViewModel.TextoAvisos(new[]
            {
                new AvisoReposicionAutomatica { Texto = " Uno. " },
                new AvisoReposicionAutomatica { Texto = "" },
                null!,
                new AvisoReposicionAutomatica { Texto = "Dos." }
            });

            Assert.AreEqual("Uno." + Environment.NewLine + "Dos.", texto);
            Assert.IsNull(EnviarReposicionViewModel.TextoAvisos(null!));
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

        // ---------------------------------------------------------------- Sugerencia 581: reposición puntual

        private static ProductoReposicionPuntual Guantes(int stock = 9, int porLectura = 1) => new()
        {
            Producto = "41980", Nombre = "Guantes M", CodigoBarras = "8437", UnidadesPorLectura = porLectura, StockOrigen = stock
        };

        private async Task EmpezarPuntualSinReposicionAsync(bool puedeRellenarManual = false)
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(puedeRellenarManual);
            await _vm.CargarAsync();
            _vm.EmpezarPuntualCommand.Execute(null);
        }

        [TestMethod]
        public async Task Puntual_SeVeAunqueNoPuedaRellenarAMano_SiNoHayLaDelCalendarioEnPreparacion()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            A.CallTo(() => _servicio.PuedeRellenarManual()).Returns(false);

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.PuedePreparar, "Rellenar la del calendario sigue siendo solo para los autorizados");
            Assert.IsTrue(_vm.PuedeEmpezarPuntual);
            Assert.IsTrue(_vm.EmpezarPuntualCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Puntual_ConLaDelCalendarioEnPreparacion_NoSePuedeEmpezar()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.HayReposicion);
            Assert.IsFalse(_vm.PuedeEmpezarPuntual, "Primero se manda la del calendario");
            Assert.IsFalse(_vm.EmpezarPuntualCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Puntual_DesdeAlgete_NoSePuedeEmpezar()
        {
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.AlmacenPedidoVta)).Returns("ALG");

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.PuedeEmpezarPuntual);
        }

        [TestMethod]
        public async Task Puntual_AlEmpezar_EnsenaElLectorYDiceQueVaAparte()
        {
            await EmpezarPuntualSinReposicionAsync();

            Assert.IsTrue(_vm.EnModoPuntual);
            Assert.IsTrue(_vm.MostrarLector);
            Assert.IsFalse(_vm.PuedeEmpezarPuntual);
            Assert.AreEqual(EnviarReposicionViewModel.TEXTO_PUNTUAL, _vm.Mensaje);
            Assert.IsFalse(_vm.EnviarPuntualCommand.CanExecute(null), "Sin nada puesto no se puede mandar");
            Assert.IsTrue(_focosEnLector > 0);
        }

        [TestMethod]
        public async Task Puntual_LeerUnCodigo_LoBuscaYLoAnadeConSuStock_YOtraLecturaSuma()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual("1", "ALC", "8437")).Returns(new List<ProductoReposicionPuntual> { Guantes() });

            _vm.Lectura = " 8437 ";
            _vm.LeerLecturaCommand.Execute(null);
            await _vm.LecturaPuntualEnCurso;
            await _vm.LeerPuntualAsync("8437");
            await _vm.LeerPuntualAsync("41980");

            Assert.AreEqual(string.Empty, _vm.Lectura);
            LineaPuntualReposicion linea = _vm.LineasPuntual.Single();
            Assert.AreEqual("41980", linea.Producto);
            Assert.AreEqual(9, linea.StockOrigen);
            Assert.AreEqual(3, linea.Cantidad, "Cada lectura (código o referencia) suma una");
            Assert.AreEqual(3, _vm.UnidadesPuntual);
            Assert.AreSame(linea, _vm.SeleccionadaPuntual);
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _sonidos.Correcto()).MustHaveHappened(3, Times.Exactly);
            Assert.IsTrue(_vm.EnviarPuntualCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Puntual_ElCodigoDeUnaCaja_SumaSusUnidades()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual("1", "ALC", "8437")).Returns(new List<ProductoReposicionPuntual> { Guantes(stock: 20, porLectura: 6) });

            await _vm.LeerPuntualAsync("8437");
            await _vm.LeerPuntualAsync("8437");

            Assert.AreEqual(12, _vm.LineasPuntual.Single().Cantidad);
        }

        [TestMethod]
        public async Task Puntual_CodigoQueNoExiste_LoDiceSuenaElErrorYNoAnadeNada()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual>());

            await _vm.LeerPuntualAsync("999");

            Assert.AreEqual(0, _vm.LineasPuntual.Count);
            StringAssert.Contains(_vm.Mensaje, "999");
            A.CallTo(() => _sonidos.Error()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Puntual_CodigoCompartido_PideLaReferencia()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._))
                .Returns(new List<ProductoReposicionPuntual> { Guantes(), new() { Producto = "41981", Nombre = "Guantes L", StockOrigen = 3 } });

            await _vm.LeerPuntualAsync("8437");

            Assert.AreEqual(0, _vm.LineasPuntual.Count);
            StringAssert.Contains(_vm.Mensaje, "41980, 41981");
            StringAssert.Contains(_vm.Mensaje, "referencia");
        }

        [TestMethod]
        public async Task Puntual_SinStockEnLaTienda_NoEntra()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual> { Guantes(stock: 0) });

            await _vm.LeerPuntualAsync("41980");

            Assert.AreEqual(0, _vm.LineasPuntual.Count);
            StringAssert.Contains(_vm.Mensaje, "no tiene stock en ALC");
        }

        [TestMethod]
        public async Task Puntual_MasDeLoQueHayEnLaTienda_AvisaYNoDejaMandar()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual> { Guantes(stock: 1) });
            await _vm.LeerPuntualAsync("41980");
            await _vm.LeerPuntualAsync("41980");

            Assert.IsTrue(_vm.LineasPuntual.Single().FaltaStock);
            StringAssert.Contains(_vm.Mensaje, "hay 1 en la tienda");
            await _vm.EnviarPuntualCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.CrearPuntual(A<CrearReposicion>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            StringAssert.Contains(_vm.Mensaje, "41980 (pones 2, hay 1)");
        }

        [TestMethod]
        public async Task Puntual_Mandar_PideConfirmacionYMandaSoloLoPuesto_YVuelveALaPantallaNormal()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, "41980")).Returns(new List<ProductoReposicionPuntual> { Guantes() });
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, "17404"))
                .Returns(new List<ProductoReposicionPuntual> { new() { Producto = "17404", Nombre = "Cera", StockOrigen = 5 } });
            await _vm.LeerPuntualAsync("41980");
            await _vm.LeerPuntualAsync("17404");
            _vm.LineasPuntual[0].Cantidad = 2;
            _vm.LineasPuntual[1].Cantidad = 0;
            CrearReposicion? mandada = null;
            A.CallTo(() => _servicio.CrearPuntual(A<CrearReposicion>._))
                .Invokes((CrearReposicion p) => mandada = p)
                .Returns(new ResultadoTerminarReposicion { NumTraspaso = 80951, Lineas = new List<LineaTraspasoTerminado> { new() { Producto = "41980", Cantidad = 2 } } });

            await _vm.EnviarPuntualCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync("Mandar a Algete",
                "Se mandan 2 unidades de 1 productos a Algete en una reposición aparte de la del calendario. ¿Mandar?")).MustHaveHappenedOnceExactly();
            Assert.IsNotNull(mandada);
            Assert.AreEqual("ALC", mandada!.Origen);
            Assert.AreEqual("ALG", mandada.Destino);
            Assert.AreEqual("41980", mandada.Lineas.Single().Producto, "La línea a 0 no se manda");
            Assert.AreEqual(2, mandada.Lineas.Single().Cantidad);
            Assert.IsFalse(_vm.EnModoPuntual);
            Assert.AreEqual(0, _vm.LineasPuntual.Count);
            StringAssert.Contains(_vm.Mensaje, "traspaso 80951");
            A.CallTo(() => _dialogos.ShowNotification("Reposición puntual enviada", A<string>._)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(_vm.PuedeEmpezarPuntual, "Se puede hacer otra si vuelven a pedir algo");
        }

        [TestMethod]
        public async Task Puntual_SiElServidorNoLaAcepta_EnsenaSuTextoYSeQuedaConLasLineas()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual> { Guantes() });
            await _vm.LeerPuntualAsync("41980");
            A.CallTo(() => _servicio.CrearPuntual(A<CrearReposicion>._))
                .ThrowsAsync(new EnvioReposicionException("ALC tiene una reposición a ALG en preparación (3 líneas) sin enviar: termínala antes de hacer una puntual.", 409));

            await _vm.EnviarPuntualCommand.ExecuteAsync(null);

            StringAssert.Contains(_vm.Mensaje, "termínala antes");
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(_vm.EnModoPuntual);
            Assert.AreEqual(1, _vm.LineasPuntual.Count);
        }

        [TestMethod]
        public async Task Puntual_Cancelar_ConLineasPreguntaYSaleSinMandar()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual> { Guantes() });
            await _vm.LeerPuntualAsync("41980");

            await _vm.CancelarPuntualCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync("Reposición puntual", A<string>._)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(_vm.EnModoPuntual);
            Assert.AreEqual(0, _vm.LineasPuntual.Count);
            A.CallTo(() => _servicio.CrearPuntual(A<CrearReposicion>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Puntual_QuitarLinea_LaSacaYRecalculaElTotal()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual> { Guantes() });
            await _vm.LeerPuntualAsync("41980");

            _vm.QuitarLineaPuntualCommand.Execute(_vm.LineasPuntual[0]);

            Assert.AreEqual(0, _vm.LineasPuntual.Count);
            Assert.AreEqual(0, _vm.UnidadesPuntual);
            Assert.IsFalse(_vm.EnviarPuntualCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Puntual_SiAlActualizarAparecelaDelCalendario_SaleDeLaPuntual()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Reposicion());

            await _vm.CargarAsync();

            Assert.IsTrue(_vm.HayReposicion);
            Assert.IsFalse(_vm.EnModoPuntual);
        }

        // ---------------------------------------------------------------- Sugerencia 581 (Carlos, 13/10/26): cuándo se puede y la recogida

        private static EstadoReposicionPuntual Estado(string situacion, string? motivo = null, bool puedeMarcarRecogida = false,
            DateTime? fechaRecogida = null, string? usuarioRecogida = null) => new()
        {
            Origen = "ALC",
            Destino = "ALG",
            Situacion = situacion,
            SePuede = situacion == EstadoReposicionPuntual.ABIERTA,
            Motivo = motivo,
            PuedeMarcarRecogida = puedeMarcarRecogida,
            FechaRecogida = fechaRecogida,
            UsuarioRecogida = usuarioRecogida
        };

        private void HoyEs(string situacion, string? motivo = null, bool puedeMarcarRecogida = false, DateTime? fechaRecogida = null, string? usuarioRecogida = null)
        {
            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._))
                .Returns(Estado(situacion, motivo, puedeMarcarRecogida, fechaRecogida, usuarioRecogida));
        }

        private const string RECOGIDA_1210 = "El repartidor ya se la ha llevado a las 12:10 (lo marcó Paloma): lo que quede va en la siguiente.";

        [DataTestMethod]
        [DataRow(EstadoReposicionPuntual.SIN_VIAJE, "Hoy no hay viaje de Alcobendas a Algete.")]
        [DataRow(EstadoReposicionPuntual.SIN_VIAJE, "Hoy no hay viaje de Alcobendas a Algete: es festivo.")]
        [DataRow(EstadoReposicionPuntual.SIN_RELLENAR, "Todavía no se ha rellenado la de hoy (se rellena sola a las 9:00).")]
        [DataRow(EstadoReposicionPuntual.SIN_ENVIAR, "Primero envía la de hoy.")]
        public async Task Puntual_CuandoHoyNoSePuede_ElBotonSeVeDesactivadoYDiceElMotivo(string situacion, string motivo)
        {
            HoyEs(situacion, motivo);

            await EmpezarPuntualSinReposicionAsync();

            Assert.IsFalse(_vm.EnModoPuntual, "No se empieza");
            Assert.IsTrue(_vm.MostrarBotonPuntual, "El botón se ve…");
            Assert.IsFalse(_vm.PuedeEmpezarPuntual, "…desactivado");
            Assert.IsFalse(_vm.EmpezarPuntualCommand.CanExecute(null));
            Assert.AreEqual(motivo, _vm.AvisoPuntual, "Al lado del botón");
            Assert.AreEqual(motivo, _vm.ToolTipPuntual, "Y en el tooltip");
            Assert.AreEqual(motivo, _vm.Mensaje);
            Assert.IsFalse(_vm.MostrarBotonRecogida);
        }

        [TestMethod]
        public async Task Puntual_ConLaDeHoyEnPreparacion_ElBotonDicePrimeroEnviaLaDeHoy()
        {
            await _vm.CargarAsync();

            Assert.IsTrue(_vm.HayReposicion);
            Assert.IsTrue(_vm.MostrarBotonPuntual);
            Assert.IsFalse(_vm.PuedeEmpezarPuntual);
            Assert.AreEqual("Primero envía la de hoy.", _vm.AvisoPuntual);
            Assert.IsFalse(_vm.MostrarBotonRecogida, "La recogida, cuando esté enviada");
        }

        [TestMethod]
        public async Task Puntual_YaRecogida_DiceAQueHoraYQuienLoMarco()
        {
            HoyEs(EstadoReposicionPuntual.RECOGIDA, "El repartidor ya se ha llevado la reposición de hoy (a las 12:10): lo que quede va en la siguiente.",
                fechaRecogida: new DateTime(2026, 10, 13, 12, 10, 0), usuarioRecogida: "NUEVAVISION\\Paloma");

            await EmpezarPuntualSinReposicionAsync();

            Assert.IsFalse(_vm.EnModoPuntual);
            Assert.IsFalse(_vm.PuedeEmpezarPuntual);
            Assert.AreEqual(RECOGIDA_1210, _vm.AvisoPuntual);
            Assert.AreEqual(RECOGIDA_1210, _vm.ToolTipPuntual);
            Assert.AreEqual(RECOGIDA_1210, _vm.Mensaje);
            Assert.IsFalse(_vm.MostrarBotonRecogida, "Ya está marcada");
        }

        [TestMethod]
        public async Task Puntual_CuandoSePuede_SinAvisoYConElTooltipNormal()
        {
            await EmpezarPuntualSinReposicionAsync();

            Assert.IsTrue(_vm.EnModoPuntual);
            Assert.IsNull(_vm.AvisoPuntual);
        }

        [TestMethod]
        public async Task Puntual_SiMarcanLaRecogidaConLaPantallaAbierta_NoSeManda()
        {
            await EmpezarPuntualSinReposicionAsync();
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual> { Guantes() });
            await _vm.LeerPuntualAsync("41980");
            HoyEs(EstadoReposicionPuntual.RECOGIDA, "El repartidor ya se ha llevado…",
                fechaRecogida: new DateTime(2026, 10, 13, 12, 10, 0), usuarioRecogida: "NUEVAVISION\\Paloma");

            await _vm.EnviarPuntualCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.CrearPuntual(A<CrearReposicion>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowConfirmationAsync("Mandar a Algete", A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(RECOGIDA_1210, _vm.Mensaje);
            Assert.IsTrue(_vm.EnModoPuntual, "Se queda con lo puesto: puede cancelar");
        }

        [TestMethod]
        public async Task Puntual_SiLaApiNoSabeElEstado_SinRestricciones()
        {
            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._)).ThrowsAsync(new EnvioReposicionException("no", 500));

            await EmpezarPuntualSinReposicionAsync();

            Assert.IsTrue(_vm.EnModoPuntual);
            Assert.IsNull(_vm.AvisoPuntual);
        }

        [TestMethod]
        public async Task Recogida_ConLaDeHoyEnviada_PideConfirmacion_ApuntaYYaNoDejaHacerPuntual()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            await _vm.CargarAsync();
            Assert.IsTrue(_vm.MostrarBotonRecogida);
            Assert.IsTrue(_vm.MarcarRecogidaCommand.CanExecute(null));
            MarcarRecogidaReposicion? marcada = null;
            A.CallTo(() => _servicio.MarcarRecogida(A<MarcarRecogidaReposicion>._))
                .Invokes((MarcarRecogidaReposicion p) => marcada = p)
                .Returns(Estado(EstadoReposicionPuntual.RECOGIDA, "El repartidor ya se ha llevado…",
                    fechaRecogida: new DateTime(2026, 10, 13, 12, 10, 0), usuarioRecogida: "NUEVAVISION\\Paloma"));

            await _vm.MarcarRecogidaCommand.ExecuteAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAsync("El repartidor ya se la ha llevado", EnviarReposicionViewModel.TEXTO_CONFIRMAR_RECOGIDA))
                .MustHaveHappenedOnceExactly();
            Assert.AreEqual("ALC", marcada!.Origen);
            Assert.AreEqual("ALG", marcada.Destino);
            Assert.AreEqual("1", marcada.Empresa);
            Assert.AreEqual(RECOGIDA_1210, _vm.Mensaje);
            Assert.AreEqual(RECOGIDA_1210, _vm.AvisoPuntual);
            Assert.IsFalse(_vm.MostrarBotonRecogida);
            Assert.IsFalse(_vm.PuedeEmpezarPuntual);
        }

        [TestMethod]
        public async Task Recogida_SinConfirmar_NoSeApunta()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            A.CallTo(() => _dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            await _vm.CargarAsync();

            await _vm.MarcarRecogidaCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.MarcarRecogida(A<MarcarRecogidaReposicion>._)).MustNotHaveHappened();
            Assert.IsTrue(_vm.MostrarBotonRecogida);
        }

        [TestMethod]
        public async Task Recogida_SiLaApiNoLaAcepta_DiceElMotivoYEnsenaLoQueHayAhora()
        {
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            await _vm.CargarAsync();
            A.CallTo(() => _servicio.MarcarRecogida(A<MarcarRecogidaReposicion>._))
                .ThrowsAsync(new EnvioReposicionException("El repartidor ya se ha llevado la reposición de hoy (a las 12:10): lo que quede va en la siguiente.", 409));
            HoyEs(EstadoReposicionPuntual.RECOGIDA, "El repartidor ya se ha llevado…",
                fechaRecogida: new DateTime(2026, 10, 13, 12, 10, 0), usuarioRecogida: "NUEVAVISION\\Paloma");

            await _vm.MarcarRecogidaCommand.ExecuteAsync(null);

            StringAssert.StartsWith(_vm.Mensaje, "El repartidor ya se ha llevado la reposición de hoy (a las 12:10)");
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(RECOGIDA_1210, _vm.AvisoPuntual, "Relee el estado: lo marcó otra persona");
            Assert.IsFalse(_vm.MostrarBotonRecogida);
        }

        [TestMethod]
        public async Task Recogida_SinElScriptEnLaApi_NoSeVeElBoton()
        {
            HoyEs(EstadoReposicionPuntual.ABIERTA, puedeMarcarRecogida: false);
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));

            await _vm.CargarAsync();

            Assert.IsFalse(_vm.MostrarBotonRecogida);
            Assert.IsTrue(_vm.PuedeEmpezarPuntual, "La puntual sigue como hasta ahora");
        }

        [TestMethod]
        public async Task Terminar_LaDeHoy_ReleeElEstado_YYaSeVeElBotonDeLaRecogida()
        {
            await _vm.CargarAsync();
            Assert.IsFalse(_vm.MostrarBotonRecogida);
            A.CallTo(() => _servicio.Terminar(A<string>._, A<string>._)).Returns(new ResultadoTerminarReposicion { NumTraspaso = 80947 });

            await _vm.TerminarCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.LeerEstadoPuntual("1", "ALC")).MustHaveHappenedTwiceExactly();
            Assert.IsFalse(_vm.HayReposicion);
            Assert.IsTrue(_vm.MostrarBotonRecogida);
            Assert.IsTrue(_vm.PuedeEmpezarPuntual);
        }

        [DataTestMethod]
        [DataRow("NUEVAVISION\\Paloma", "Paloma")]
        [DataRow("Andre", "Andre")]
        [DataRow(null, null)]
        public void SinDominio_QuitaElDominio(string? usuario, string? esperado)
        {
            Assert.AreEqual(esperado, EnviarReposicionViewModel.SinDominio(usuario));
        }

        // ---------------------------------------------------------------- Sugerencia 581 (Carlos, 13/10/26): lo de sus pedidos no se manda

        [TestMethod]
        public async Task Puntual_LoQueNecesitanSusPedidos_AvisaYNoDejaMandar()
        {
            await EmpezarPuntualSinReposicionAsync();
            ProductoReposicionPuntual guantes = Guantes(stock: 5);
            guantes.Comprometido = 4;
            guantes.Disponible = 1;
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual> { guantes });
            await _vm.LeerPuntualAsync("41980");
            await _vm.LeerPuntualAsync("41980");

            LineaPuntualReposicion linea = _vm.LineasPuntual.Single();
            Assert.AreEqual(1, linea.Disponible);
            Assert.AreEqual(4, linea.Comprometido);
            Assert.IsTrue(linea.FaltaStock);
            Assert.AreEqual("De 41980 puedes mandar 1 (hay 5 en la tienda y 4 son para sus pedidos) y pones 2: baja la cantidad.", _vm.Mensaje);
            await _vm.EnviarPuntualCommand.ExecuteAsync(null);

            A.CallTo(() => _servicio.CrearPuntual(A<CrearReposicion>._)).MustNotHaveHappened();
            StringAssert.Contains(_vm.Mensaje, "41980 (pones 2, puedes mandar 1: el resto es para sus pedidos)");
        }

        [TestMethod]
        public async Task Puntual_TodoLoQueHayEsParaSusPedidos_NoEntra()
        {
            await EmpezarPuntualSinReposicionAsync();
            ProductoReposicionPuntual guantes = Guantes(stock: 3);
            guantes.Comprometido = 3;
            guantes.Disponible = 0;
            A.CallTo(() => _servicio.BuscarProductoPuntual(A<string>._, A<string>._, A<string>._)).Returns(new List<ProductoReposicionPuntual> { guantes });

            await _vm.LeerPuntualAsync("41980");

            Assert.AreEqual(0, _vm.LineasPuntual.Count);
            Assert.AreEqual("41980 (Guantes M): las 3 que hay en ALC las necesitan sus pedidos pendientes: no se puede mandar.", _vm.Mensaje);
            A.CallTo(() => _sonidos.Error()).MustHaveHappenedOnceExactly();
        }

        // ---------------------------------------------------------------- Revisión 13/10/26: operaciones a la vez y respuestas tardías

        [TestMethod]
        public async Task Puntual_DosLecturasALaVez_SigueOcupadaHastaQueAcabaLaUltima()
        {
            await EmpezarPuntualSinReposicionAsync();
            var primera = new TaskCompletionSource<List<ProductoReposicionPuntual>>();
            var segunda = new TaskCompletionSource<List<ProductoReposicionPuntual>>();
            A.CallTo(() => _servicio.BuscarProductoPuntual("1", "ALC", "8437")).Returns(primera.Task);
            A.CallTo(() => _servicio.BuscarProductoPuntual("1", "ALC", "8422")).Returns(segunda.Task);

            Task lectura1 = _vm.LeerPuntualAsync("8437");
            Task lectura2 = _vm.LeerPuntualAsync("8422");
            primera.SetResult(new List<ProductoReposicionPuntual> { Guantes() });
            await lectura1;

            Assert.IsTrue(_vm.EstaOcupado, "La segunda lectura sigue buscando");
            Assert.IsFalse(_vm.EnviarPuntualCommand.CanExecute(null), "No se puede mandar con una lectura a medias");
            Assert.IsFalse(_vm.CancelarPuntualCommand.CanExecute(null));

            segunda.SetResult(new List<ProductoReposicionPuntual>
            {
                new() { Producto = "40057", Nombre = "Banda", CodigoBarras = "8422", UnidadesPorLectura = 1, StockOrigen = 4 }
            });
            await lectura2;

            Assert.IsFalse(_vm.EstaOcupado);
            CollectionAssert.AreEqual(new[] { "41980", "40057" }, _vm.LineasPuntual.Select(l => l.Producto).ToList());
            Assert.IsTrue(_vm.EnviarPuntualCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Puntual_UnaBusquedaQueContestaDespuesDeSalir_NoAnadeNada()
        {
            await EmpezarPuntualSinReposicionAsync();
            var tardia = new TaskCompletionSource<List<ProductoReposicionPuntual>>();
            A.CallTo(() => _servicio.BuscarProductoPuntual("1", "ALC", "8437")).Returns(tardia.Task);

            Task lectura = _vm.LeerPuntualAsync("8437");
            await _vm.CancelarPuntualCommand.ExecuteAsync(null);
            string mensajeAlSalir = _vm.Mensaje;
            tardia.SetResult(new List<ProductoReposicionPuntual> { Guantes() });
            await lectura;

            Assert.IsFalse(_vm.EnModoPuntual);
            Assert.AreEqual(0, _vm.LineasPuntual.Count, "Era de la puntual que ya no está");
            Assert.AreEqual(mensajeAlSalir, _vm.Mensaje);
            A.CallTo(() => _sonidos.Correcto()).MustNotHaveHappened();
            Assert.IsFalse(_vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Cargar_DosVecesSeguidas_ElEstadoViejoQueContestaTardeNoPisaElNuevo()
        {
            // Al volver a la pestaña se carga otra vez: la primera lectura del estado contesta después que la segunda
            A.CallTo(() => _servicio.LeerEnPreparacion(A<string>._, "ALC")).Returns(Task.FromResult<ReposicionEnPreparacion>(null!));
            var vieja = new TaskCompletionSource<EstadoReposicionPuntual>();
            var nueva = new TaskCompletionSource<EstadoReposicionPuntual>();
            A.CallTo(() => _servicio.LeerEstadoPuntual(A<string>._, A<string>._)).ReturnsNextFromSequence(vieja.Task, nueva.Task);

            Task primera = _vm.CargarAsync();
            Task segunda = _vm.CargarAsync();
            nueva.SetResult(Estado(EstadoReposicionPuntual.RECOGIDA, fechaRecogida: DateTime.Today.AddHours(12).AddMinutes(10), usuarioRecogida: "Paloma"));
            await segunda;

            Assert.IsTrue(_vm.EstaOcupado, "La primera carga sigue en marcha");

            vieja.SetResult(Estado(EstadoReposicionPuntual.ABIERTA, puedeMarcarRecogida: true));
            await primera;

            Assert.IsFalse(_vm.EstaOcupado);
            Assert.IsTrue(_vm.PuntualBloqueada, "Manda el estado más nuevo: el repartidor ya ha pasado");
            Assert.IsFalse(_vm.MostrarBotonRecogida);
            Assert.IsFalse(_vm.EmpezarPuntualCommand.CanExecute(null));
        }

        [TestMethod]
        public void LineaPuntual_SinDisponibleDeLaApi_UsaElStock()
        {
            var linea = new LineaPuntualReposicion { Producto = "41980", StockOrigen = 2, Cantidad = 2 };

            Assert.AreEqual(2, linea.Disponible);
            Assert.IsFalse(linea.FaltaStock);
        }
    }
}
