using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#635 (API en NestoAPI#855): la ventana «Recogidas solicitadas». Lista de pendientes, confirmar (con o sin correcciones de
    /// bultos y texto), rechazar con motivo obligatorio, anular desde «Por cliente», errores de la API tal cual (409 → se relee, 503 →
    /// aviso y lista vacía), refresco por SignalR (grupo «RecogidasSolicitadas») y llegada desde la campana.
    /// </summary>
    [TestClass]
    public class RecogidasSolicitadasViewModelTests
    {
        private IServicioRecogidasSolicitadas _servicio = null!;
        private IConfiguracion _configuracion = null!;
        private IServicioDialogos _dialogos = null!;
        private IGruposAlmacenTiempoReal _grupos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioRecogidasSolicitadas>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            _grupos = A.Fake<IGruposAlmacenTiempoReal>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ALMACEN)).Returns(true);
            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).ReturnsLazily(() => Task.FromResult(new List<RecogidaSolicitada> { Pendiente(12), Pendiente(13, "Vendedor") }));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
        }

        private RecogidasSolicitadasViewModel Crear() => new RecogidasSolicitadasViewModel(_servicio, _configuracion, _dialogos, _grupos);

        private static RecogidaSolicitada Pendiente(int id, string quienRecoge = "Reparto") => new RecogidaSolicitada
        {
            Id = id,
            Empresa = "1",
            Cliente = "15191",
            Contacto = "0",
            NombreCliente = "CENTRO DE PRUEBA",
            Direccion = "C/ Mayor 1",
            CodPostal = "28001",
            Poblacion = "Madrid",
            Telefono = "910000000",
            Ruta = "AT",
            Texto = "Una caja de esmaltes",
            Bultos = 2,
            FacturaOriginal = "NV26/000123",
            QuienRecoge = quienRecoge,
            Vendedor = "CAM",
            UsuarioSolicita = "vendedor1",
            FechaSolicitud = new DateTime(2026, 11, 12, 10, 15, 0),
            Estado = RecogidaSolicitada.ESTADO_SOLICITADA
        };

        private static RecogidaSolicitada Confirmada(int id, string quienRecoge = "Reparto")
        {
            RecogidaSolicitada r = Pendiente(id, quienRecoge);
            r.Estado = RecogidaSolicitada.ESTADO_CONFIRMADA;
            r.PedidoNota = 930500;
            r.CodigoDocumento = "NR930500";
            return r;
        }

        private static async Task Llegar(RecogidasSolicitadasViewModel vm, ParametrosNavegacion? parametros = null)
        {
            vm.AlLlegar(parametros ?? new ParametrosNavegacion());
            await vm.LlegadaEnCurso;
        }

        // ------------------------------------------------------------------ cargar

        [TestMethod]
        public async Task AlLlegar_LeeLasPendientesDeLaEmpresa1_EligeLaPrimeraYEntraEnElGrupo()
        {
            RecogidasSolicitadasViewModel vm = Crear();

            await Llegar(vm);

            A.CallTo(() => _servicio.LeerPendientes("1")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, vm.Pendientes.Count);
            Assert.AreEqual(12, vm.Seleccionada!.Id);
            Assert.AreEqual("2", vm.BultosEditados);
            Assert.AreEqual("Una caja de esmaltes", vm.TextoEditado);
            Assert.AreEqual("2 recogidas pendientes de contestar.", vm.TextoPendientes);
            Assert.IsFalse(vm.HayAviso);
            A.CallTo(() => _grupos.EntrarEnGrupo("RecogidasSolicitadas")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task QuienRecoge_SeVeEnPalabras_YLaDireccionCompleta()
        {
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            Assert.AreEqual("Reparto propio", vm.Pendientes[0].QuienRecogeTexto);
            Assert.AreEqual("La recoge el vendedor", vm.Pendientes[1].QuienRecogeTexto);
            Assert.AreEqual("C/ Mayor 1, 28001 Madrid", vm.Pendientes[0].DireccionCompleta);
            Assert.AreEqual("15191/0", vm.Pendientes[0].ClienteContacto);
            Assert.AreEqual("vendedor1 (vendedor CAM) el 12/11/26 10:15", vm.Pendientes[0].Pedida);
        }

        [TestMethod]
        public void ElAvisoSinRespuesta_SeVeSoloSiLaApiLoHaMandado()
        {
            RecogidaSolicitada sinAviso = Pendiente(1);
            RecogidaSolicitada conAviso = Pendiente(2);
            conAviso.FechaAvisoSinRespuesta = new DateTime(2026, 11, 13, 9, 0, 0);

            Assert.IsFalse(sinAviso.SinRespuesta);
            Assert.AreEqual(string.Empty, sinAviso.AvisoSinRespuestaTexto);
            Assert.IsTrue(conAviso.SinRespuesta);
            Assert.AreEqual("Sin respuesta (avisado el 13/11/26 9:00)", conAviso.AvisoSinRespuestaTexto);
        }

        [TestMethod]
        public async Task Con503_AvisaQueFaltaElScript_YLaListaQuedaVacia()
        {
            A.CallTo(() => _servicio.LeerPendientes(A<string>._))
                .Throws(new RecogidaSolicitadaException("Las recogidas solicitadas aún no están disponibles: falta lanzar el script.", 503));
            RecogidasSolicitadasViewModel vm = Crear();

            await Llegar(vm);

            Assert.IsFalse(vm.Disponible);
            Assert.AreEqual("Las recogidas solicitadas aún no están disponibles: falta lanzar el script.", vm.Aviso);
            Assert.AreEqual(0, vm.Pendientes.Count);
            Assert.IsNull(vm.Seleccionada);
            Assert.IsFalse(vm.ConfirmarCommand.CanExecute(null));
            Assert.IsFalse(vm.BuscarClienteCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SinGrupo_NoPuedeConfirmarNiRechazar_YSeLeDice()
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            RecogidasSolicitadasViewModel vm = Crear();

            await Llegar(vm);

            Assert.IsFalse(vm.PuedeGestionar);
            Assert.IsNotNull(vm.Seleccionada);
            Assert.IsFalse(vm.ConfirmarCommand.CanExecute(null));
            Assert.IsFalse(vm.RechazarCommand.CanExecute(null));
            Assert.AreEqual(RecogidasSolicitadasViewModel.MENSAJE_SIN_GRUPO, vm.Aviso);
        }

        [TestMethod]
        public async Task Con403DeLaApi_ListaVaciaYElMotivoTalCual()
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).Throws(new RecogidaSolicitadaException("No tiene permiso.", 403));
            RecogidasSolicitadasViewModel vm = Crear();

            await Llegar(vm);

            Assert.AreEqual(0, vm.Pendientes.Count);
            Assert.IsTrue(vm.Disponible, "403 no es falta de servicio: «Por cliente» sigue valiendo");
            StringAssert.StartsWith(vm.Aviso, "No tiene permiso.");
            StringAssert.Contains(vm.Aviso, "Por cliente");
        }

        [DataTestMethod]
        [DataRow("Almacén")]
        [DataRow("Administración")]
        [DataRow("Dirección")]
        [DataRow("Informática")]
        public async Task LosCuatroGrupos_PuedenContestar(string grupo)
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            A.CallTo(() => _configuracion.UsuarioEnGrupo(grupo)).Returns(true);
            RecogidasSolicitadasViewModel vm = Crear();

            await Llegar(vm);

            Assert.IsTrue(vm.PuedeGestionar);
            Assert.IsTrue(vm.ConfirmarCommand.CanExecute(null));
            Assert.IsTrue(vm.RechazarCommand.CanExecute(null));
        }

        // ------------------------------------------------------------------ confirmar

        [TestMethod]
        public async Task ConfirmarSinCorregir_NoMandaNiBultosNiTexto_YEnsenaElCodigoDeLaNota()
        {
            A.CallTo(() => _servicio.Confirmar(12, A<short?>._, A<string>._)).Returns(Task.FromResult(Confirmada(12)));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            string? texto = null;
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>._)).Invokes((string _, string t) => texto = t);

            await vm.ConfirmarAsync();

            A.CallTo(() => _servicio.Confirmar(12, null, null)).MustHaveHappenedOnceExactly();
            Assert.IsNotNull(texto);
            StringAssert.Contains(texto, "NR930500");
            StringAssert.Contains(texto, "reparto propio");
            StringAssert.Contains(texto, "camino normal de las notas de recogida");
            StringAssert.Contains(vm.Mensaje, "NR930500");
            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task ConfirmarCorrigiendo_MandaLosBultosYElTextoNuevos()
        {
            A.CallTo(() => _servicio.Confirmar(12, A<short?>._, A<string>._)).Returns(Task.FromResult(Confirmada(12)));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.BultosEditados = " 3 ";
            vm.TextoEditado = " Dos cajas de esmaltes ";

            await vm.ConfirmarAsync();

            A.CallTo(() => _servicio.Confirmar(12, (short?)3, "Dos cajas de esmaltes")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ConfirmarLaDelVendedor_DiceQueSeCierraLeyendoElCodigoEnAlgete()
        {
            A.CallTo(() => _servicio.Confirmar(13, A<short?>._, A<string>._)).Returns(Task.FromResult(Confirmada(13, "Vendedor")));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.Seleccionada = vm.Pendientes[1];
            string? texto = null;
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>._)).Invokes((string _, string t) => texto = t);

            await vm.ConfirmarAsync();

            Assert.IsNotNull(texto);
            StringAssert.Contains(texto, "La recoge el vendedor");
            StringAssert.Contains(texto, "NR930500");
            StringAssert.Contains(texto, "al llegar a Algete");
        }

        [TestMethod]
        public async Task ConfirmarConBultosQueNoValen_NoLlamaALaApi()
        {
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.BultosEditados = "0";

            await vm.ConfirmarAsync();

            Assert.AreEqual(RecogidasSolicitadasViewModel.MENSAJE_BULTOS, vm.Mensaje);
            A.CallTo(() => _servicio.Confirmar(A<int>._, A<short?>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ConfirmarSinTexto_NoLlamaALaApi()
        {
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.TextoEditado = "  ";

            await vm.ConfirmarAsync();

            Assert.AreEqual(RecogidasSolicitadasViewModel.MENSAJE_FALTA_TEXTO, vm.Mensaje);
            A.CallTo(() => _servicio.Confirmar(A<int>._, A<short?>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ConfirmarYDecirQueNo_NoLlamaALaApi()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(false);
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.ConfirmarAsync();

            A.CallTo(() => _servicio.Confirmar(A<int>._, A<short?>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ConfirmarCon409PorqueOtroLaHaResuelto_EnsenaElMensajeYRelee()
        {
            A.CallTo(() => _servicio.Confirmar(12, A<short?>._, A<string>._))
                .Throws(new RecogidaSolicitadaException("La recogida 12 ya está rechazada: no es de ruta propia.", 409));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.ConfirmarAsync();

            Assert.AreEqual("La recogida 12 ya está rechazada: no es de ruta propia.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("La recogida 12 ya está rechazada: no es de ruta propia.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task ConfirmarCon400_EnsenaElMensajeTalCual_YNoRelee()
        {
            A.CallTo(() => _servicio.Confirmar(12, A<short?>._, A<string>._))
                .Throws(new RecogidaSolicitadaException("El contacto ya no es de ruta propia. Rechácela con ese motivo.", 400));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.ConfirmarAsync();

            Assert.AreEqual("El contacto ya no es de ruta propia. Rechácela con ese motivo.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(12, vm.Seleccionada!.Id, "Sigue en la misma para corregirla o rechazarla");
        }

        [TestMethod]
        public async Task ConfirmarCon503_SeQuedaSinServicio()
        {
            A.CallTo(() => _servicio.Confirmar(12, A<short?>._, A<string>._)).Throws(new RecogidaSolicitadaException("Falta el script.", 503));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.ConfirmarAsync();

            Assert.IsFalse(vm.Disponible);
            Assert.AreEqual("Falta el script.", vm.Aviso);
            Assert.AreEqual(0, vm.Pendientes.Count);
        }

        // ------------------------------------------------------------------ rechazar

        [TestMethod]
        public async Task RechazarSinMotivo_NoLlamaALaApi()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns("   ");
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.RechazarAsync();

            Assert.AreEqual(RecogidasSolicitadasViewModel.MENSAJE_FALTA_MOTIVO, vm.Mensaje);
            A.CallTo(() => _servicio.Rechazar(A<int>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task RechazarYCancelar_NoLlamaALaApi()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns(null!);
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.RechazarAsync();

            A.CallTo(() => _servicio.Rechazar(A<int>._, A<string>._)).MustNotHaveHappened();
            Assert.IsNull(vm.Mensaje);
        }

        [TestMethod]
        public async Task RechazarConMotivo_LoMandaYRelee()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns(" No es de ruta propia ");
            RecogidaSolicitada rechazada = Pendiente(12);
            rechazada.Estado = RecogidaSolicitada.ESTADO_RECHAZADA;
            A.CallTo(() => _servicio.Rechazar(12, A<string>._)).Returns(Task.FromResult(rechazada));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.RechazarAsync();

            A.CallTo(() => _servicio.Rechazar(12, "No es de ruta propia")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Recogida 12 rechazada.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).MustHaveHappenedTwiceExactly();
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task RechazarCon409_EnsenaElMensajeYRelee()
        {
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns("Motivo");
            A.CallTo(() => _servicio.Rechazar(12, A<string>._)).Throws(new RecogidaSolicitadaException("La recogida 12 ya está confirmada.", 409));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.RechazarAsync();

            Assert.AreEqual("La recogida 12 ya está confirmada.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).MustHaveHappenedTwiceExactly();
        }

        // ------------------------------------------------------------------ por cliente y anular

        [TestMethod]
        public async Task BuscarSinCliente_NoLlamaALaApi()
        {
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            await vm.BuscarClienteAsync();

            Assert.AreEqual(RecogidasSolicitadasViewModel.MENSAJE_FALTA_CLIENTE, vm.Mensaje);
            A.CallTo(() => _servicio.LeerDelCliente(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task BuscarCliente_LeeTodasLasSuyas_ConContactoSoloSiSeEscribe()
        {
            A.CallTo(() => _servicio.LeerDelCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new List<RecogidaSolicitada> { Confirmada(14), Pendiente(12) }));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.ClienteBusqueda = " 15191 ";
            vm.ContactoBusqueda = " ";

            await vm.BuscarClienteAsync();

            A.CallTo(() => _servicio.LeerDelCliente("1", "15191", null)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, vm.DelCliente.Count);
            Assert.AreEqual(14, vm.SeleccionadaDelCliente!.Id);
            Assert.IsTrue(vm.AnularCommand.CanExecute(null));
        }

        [DataTestMethod]
        [DataRow("Solicitada", true)]
        [DataRow("Confirmada", true)]
        [DataRow("EnReparto", true)]
        [DataRow("Recogida", false)]
        [DataRow("Rechazada", false)]
        [DataRow("Anulada", false)]
        public void SoloSeOfreceAnularLoQueLaApiDejaAnular(string estado, bool sePuede)
        {
            RecogidaSolicitada r = Pendiente(1);
            r.Estado = estado;

            Assert.AreEqual(sePuede, r.SePuedeAnular);
        }

        [TestMethod]
        public async Task AnularEnMarcha_EnsenaLoQueHayQueHacerAMano_YRelee()
        {
            A.CallTo(() => _servicio.LeerDelCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new List<RecogidaSolicitada> { Confirmada(14) }));
            RecogidaSolicitada anulada = Confirmada(14);
            anulada.Estado = RecogidaSolicitada.ESTADO_ANULADA;
            anulada.AvisoAnulacion = "La nota NR930500 sigue en el reparto: quítela de la ruta a mano.";
            A.CallTo(() => _servicio.Anular(14, A<string>._)).Returns(Task.FromResult(anulada));
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns(" El cliente ya no la quiere ");
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.ClienteBusqueda = "15191";
            await vm.BuscarClienteAsync();
            string? texto = null;
            A.CallTo(() => _dialogos.ShowNotification(A<string>._, A<string>._)).Invokes((string _, string t) => texto = t);

            await vm.AnularAsync();

            A.CallTo(() => _servicio.Anular(14, "El cliente ya no la quiere")).MustHaveHappenedOnceExactly();
            Assert.IsNotNull(texto);
            StringAssert.Contains(texto, "quítela de la ruta a mano");
            Assert.AreEqual("Recogida 14 anulada.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerDelCliente("1", "15191", null)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task AnularSinMotivo_LoMandaSinMotivo_YCancelarNoLlama()
        {
            A.CallTo(() => _servicio.LeerDelCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new List<RecogidaSolicitada> { Confirmada(14) }));
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).ReturnsNextFromSequence(null!, "");
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.ClienteBusqueda = "15191";
            await vm.BuscarClienteAsync();

            await vm.AnularAsync();
            A.CallTo(() => _servicio.Anular(A<int>._, A<string>._)).MustNotHaveHappened();

            await vm.AnularAsync();
            A.CallTo(() => _servicio.Anular(14, null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AnularCon403_EnsenaElMensajeTalCual()
        {
            A.CallTo(() => _servicio.LeerDelCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new List<RecogidaSolicitada> { Confirmada(14) }));
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns("x");
            A.CallTo(() => _servicio.Anular(14, A<string>._))
                .Throws(new RecogidaSolicitadaException("Ya está en reparto: solo Almacén o Administración pueden anularla.", 403));
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.ClienteBusqueda = "15191";
            await vm.BuscarClienteAsync();

            await vm.AnularAsync();

            Assert.AreEqual("Ya está en reparto: solo Almacén o Administración pueden anularla.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("Ya está en reparto: solo Almacén o Administración pueden anularla.")).MustHaveHappenedOnceExactly();
        }

        // ------------------------------------------------------------------ SignalR

        [TestMethod]
        public async Task ConUnAvisoDelGrupo_Relee_YConservaLoQueSeEstabaCorrigiendo()
        {
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            vm.BultosEditados = "5";

            _grupos.GrupoHaCambiado += Raise.FreeForm<EventHandler<string>>.With(_grupos, "recogidassolicitadas");
            await vm.RecargaEnCurso;

            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).MustHaveHappenedTwiceExactly();
            Assert.AreEqual(12, vm.Seleccionada!.Id);
            Assert.AreEqual("5", vm.BultosEditados);
        }

        [TestMethod]
        public async Task ConUnAvisoDeOtroGrupo_NoRelee()
        {
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            _grupos.GrupoHaCambiado += Raise.FreeForm<EventHandler<string>>.With(_grupos, "Reposiciones:REI");
            await vm.RecargaEnCurso;

            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlCerrar_SaleDelGrupo_YYaNoRelee()
        {
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);

            Assert.IsTrue(vm.ConfirmTabClose());
            _grupos.GrupoHaCambiado += Raise.FreeForm<EventHandler<string>>.With(_grupos, "RecogidasSolicitadas");
            await vm.RecargaEnCurso;

            A.CallTo(() => _grupos.SalirDeGrupo("RecogidasSolicitadas")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerPendientes(A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlVolverALlegar_NoEntraDosVecesEnElGrupo()
        {
            RecogidasSolicitadasViewModel vm = Crear();
            await Llegar(vm);
            await Llegar(vm);

            A.CallTo(() => _grupos.EntrarEnGrupo(A<string>._)).MustHaveHappenedOnceExactly();
        }

        // ------------------------------------------------------------------ desde la campana

        private static ParametrosNavegacion DeLaCampana(int id, bool anulada = false) => new ParametrosNavegacion
        {
            { NotificacionBuzon.PARAMETRO_RECOGIDA_ID, id },
            { NotificacionBuzon.PARAMETRO_RECOGIDA_EMPRESA, "1" },
            { NotificacionBuzon.PARAMETRO_RECOGIDA_CLIENTE, "15191" },
            { NotificacionBuzon.PARAMETRO_RECOGIDA_CONTACTO, "0" },
            { NotificacionBuzon.PARAMETRO_RECOGIDA_ANULADA, anulada }
        };

        [TestMethod]
        public async Task DesdeLaCampana_EligeEsaSolicitudEntreLasPendientes()
        {
            RecogidasSolicitadasViewModel vm = Crear();

            await Llegar(vm, DeLaCampana(13));

            Assert.AreEqual(13, vm.Seleccionada!.Id);
            Assert.AreEqual(RecogidasSolicitadasViewModel.PESTANA_PENDIENTES, vm.PestanaSeleccionada);
            A.CallTo(() => _servicio.LeerDelCliente(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task DesdeLaCampana_SiYaNoEstaPendiente_LaBuscaPorSuCliente()
        {
            A.CallTo(() => _servicio.LeerDelCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new List<RecogidaSolicitada> { Confirmada(20), Confirmada(99) }));
            RecogidasSolicitadasViewModel vm = Crear();

            await Llegar(vm, DeLaCampana(99));

            Assert.AreEqual(RecogidasSolicitadasViewModel.PESTANA_CLIENTE, vm.PestanaSeleccionada);
            A.CallTo(() => _servicio.LeerDelCliente("1", "15191", "0")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(99, vm.SeleccionadaDelCliente!.Id);
            StringAssert.Contains(vm.Mensaje, "ya no está pendiente");
        }

        [TestMethod]
        public async Task DesdeLaCampana_UnaAnulada_AbreLaPestanaDelCliente()
        {
            RecogidaSolicitada anulada = Confirmada(12);
            anulada.Estado = RecogidaSolicitada.ESTADO_ANULADA;
            A.CallTo(() => _servicio.LeerDelCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new List<RecogidaSolicitada> { anulada }));
            RecogidasSolicitadasViewModel vm = Crear();

            await Llegar(vm, DeLaCampana(12, anulada: true));

            Assert.AreEqual(RecogidasSolicitadasViewModel.PESTANA_CLIENTE, vm.PestanaSeleccionada);
            Assert.AreEqual("15191", vm.ClienteBusqueda);
            Assert.AreEqual(12, vm.SeleccionadaDelCliente!.Id);
            Assert.IsFalse(vm.AnularCommand.CanExecute(null), "Ya está anulada");
        }

        // ------------------------------------------------------------------ servicio

        [TestMethod]
        public void Servicio_Urls()
        {
            Assert.AreEqual("NotasEntrega/Recogidas/Solicitudes/Pendientes?empresa=1", ServicioRecogidasSolicitadas.UrlPendientes(" 1 "));
            Assert.AreEqual("NotasEntrega/Recogidas/Solicitudes?cliente=15191&empresa=1", ServicioRecogidasSolicitadas.UrlDelCliente("1", "15191", null));
            Assert.AreEqual("NotasEntrega/Recogidas/Solicitudes?cliente=15191&empresa=1&contacto=2", ServicioRecogidasSolicitadas.UrlDelCliente("1", "15191", "2"));
            Assert.AreEqual("NotasEntrega/Recogidas/Solicitudes/14", ServicioRecogidasSolicitadas.UrlAnular(14, " "));
            Assert.AreEqual("NotasEntrega/Recogidas/Solicitudes/14?motivo=ya%20no%20la%20quiere", ServicioRecogidasSolicitadas.UrlAnular(14, "ya no la quiere"));
        }
    }
}
