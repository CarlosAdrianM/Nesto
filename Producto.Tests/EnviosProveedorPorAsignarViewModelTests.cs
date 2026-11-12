using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.ViewModels;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#636 (API en NestoAPI#857, fase 3): la ventana «Envíos de proveedor por asignar». Carga, el sugerido preseleccionado, el
    /// resto de pedidos en el aire, asignar (con y sin recordar el remitente), el 409 que pregunta si reasignar, descartar con
    /// confirmación, los remitentes recordados, los errores de la API tal cual (503 → aviso y todo vacío) y la llegada desde la campana.
    /// </summary>
    [TestClass]
    public class EnviosProveedorPorAsignarViewModelTests
    {
        private IServicioEnviosProveedorPorAsignar _servicio = null!;
        private IServicioDialogos _dialogos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioEnviosProveedorPorAsignar>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).ReturnsLazily(() => Task.FromResult(Leido()));
            A.CallTo(() => _servicio.LeerRemitentesPaquete()).ReturnsLazily(() => Task.FromResult(new List<RemitentePaqueteRecordado>
            {
                new RemitentePaqueteRecordado { Id = 5, RemitentePaquete = "mbe store c/o multiboxes", Empresa = "1", Proveedor = "1234", Usuario = "compras1" }
            }));
            A.CallTo(() => _servicio.Asignar(A<int>._, A<string>._, A<int>._, A<bool>._, A<bool>._))
                .Returns(Task.FromResult(new RespuestaAsignacionEnvioProveedor { Mensaje = "Puesta la fecha 14/11/26 en 3 líneas del pedido 912001." }));
            A.CallTo(() => _servicio.Descartar(A<int>._))
                .Returns(Task.FromResult(new RespuestaDescarteEnvioProveedor { Mensaje = "Descartados 3 correos del envío." }));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(true);
        }

        private EnviosProveedorPorAsignarViewModel Crear() => new EnviosProveedorPorAsignarViewModel(_servicio, _dialogos);

        private static PedidoCompraPorAsignar Pedido(int pedido, string proveedor = "1234", bool sugerido = false, int recibidas = 0) => new PedidoCompraPorAsignar
        {
            Empresa = "1",
            Pedido = pedido,
            Proveedor = proveedor,
            NombreProveedor = "COSMÉTICOS EJEMPLO",
            FechaEnvio = new DateTime(2026, 11, 10, 9, 30, 0),
            LineasPendientes = 3,
            LineasYaRecibidas = recibidas,
            Sugerido = sugerido,
            Motivo = "Dominio del proveedor"
        };

        private static EnviosProveedorPorAsignar Leido() => new EnviosProveedorPorAsignar
        {
            Envios = new List<EnvioProveedorPorAsignar>
            {
                new EnvioProveedorPorAsignar
                {
                    Clave = 101,
                    PrimerCorreo = new DateTime(2026, 11, 12, 8, 0, 0),
                    Agencia = "UPS",
                    Codigos = new List<string> { "1Z0001", "1Z0002" },
                    RemitentePaquete = "mbe store c/o multiboxes",
                    ProveedorPropuesto = "1234",
                    EntregaEstimadaDia = new DateTime(2026, 11, 14),
                    FechaRecepcionPropuesta = new DateTime(2026, 11, 14),
                    Correos = new List<CorreoEnvioProveedor>
                    {
                        new CorreoEnvioProveedor { Id = 101, Asunto = "Tu paquete está de camino", RemitenteDireccion = "pkginfo@ups.com" },
                        new CorreoEnvioProveedor { Id = 103, Asunto = "Tu paquete está de camino", RemitenteDireccion = "pkginfo@ups.com" }
                    },
                    Candidatos = new List<PedidoCompraPorAsignar> { Pedido(912000), Pedido(912001, sugerido: true, recibidas: 2) }
                },
                new EnvioProveedorPorAsignar
                {
                    Clave = 200,
                    PrimerCorreo = new DateTime(2026, 11, 11, 8, 0, 0),
                    Agencia = "SEUR",
                    Codigos = new List<string> { "SEUR77" },
                    FechaRecepcionPropuesta = new DateTime(2026, 11, 13),
                    Correos = new List<CorreoEnvioProveedor> { new CorreoEnvioProveedor { Id = 200, Asunto = "Envío en reparto" } }
                }
            },
            PedidosEnAire = new List<PedidoCompraPorAsignar>
            {
                Pedido(912000), Pedido(912001, recibidas: 2), Pedido(913500, "5555")
            }
        };

        private static async Task Llegar(EnviosProveedorPorAsignarViewModel vm, ParametrosNavegacion? parametros = null)
        {
            vm.AlLlegar(parametros ?? new ParametrosNavegacion());
            await vm.LlegadaEnCurso;
        }

        // ------------------------------------------------------------------ cargar

        [TestMethod]
        public async Task AlLlegar_LeeLosEnviosDeLos7Dias_EligeElPrimeroYLeeLosRemitentes()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();

            await Llegar(vm);

            A.CallTo(() => _servicio.LeerPorAsignar(7)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerRemitentesPaquete()).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, vm.Envios.Count);
            Assert.AreEqual(101, vm.EnvioSeleccionado!.Clave);
            Assert.AreEqual("2 envíos por asignar.", vm.TextoEnvios);
            Assert.AreEqual(1, vm.Remitentes.Count);
            Assert.AreEqual("1 remitente recordado.", vm.TextoRemitentes);
            Assert.IsFalse(vm.HayAviso);
        }

        [TestMethod]
        public async Task ElSugerido_SaleElegido_YElRestoDePedidosEnElAireVaPlegadoSinLosCandidatos()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();

            await Llegar(vm);

            Assert.AreEqual(2, vm.Candidatos.Count);
            Assert.AreEqual(912001, vm.CandidatoSeleccionado!.Pedido);
            Assert.AreSame(vm.CandidatoSeleccionado, vm.PedidoElegido);
            Assert.AreEqual("Sugerido", vm.CandidatoSeleccionado.SugeridoTexto);
            Assert.AreEqual(string.Empty, vm.Candidatos[0].SugeridoTexto);
            Assert.AreEqual(913500, vm.OtrosPedidos.Single().Pedido, "Los candidatos no se repiten en el resto");
            Assert.IsFalse(vm.OtrosDesplegados);
            Assert.IsTrue(vm.AsignarCommand.CanExecute(null));
        }

        [TestMethod]
        public void UnPedidoConLineasYaRecibidas_SeDiceQueEsUnResto()
        {
            PedidoCompraPorAsignar resto = Pedido(1, recibidas: 2);
            PedidoCompraPorAsignar entero = Pedido(2);

            Assert.IsTrue(resto.EsResto);
            Assert.AreEqual("3 pendientes y 2 ya recibidas: resto de un pedido ya recibido en parte", resto.LineasTexto);
            Assert.IsFalse(entero.EsResto);
            Assert.AreEqual("3 pendientes", entero.LineasTexto);
            Assert.AreEqual("COSMÉTICOS EJEMPLO (1234)", entero.ProveedorTexto);
            Assert.AreEqual("10/11/26 09:30", entero.FechaEnvioTexto);
        }

        [TestMethod]
        public async Task UnEnvioSinCandidatos_DespliegaElRestoYNoHayNadaElegido()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            vm.EnvioSeleccionado = vm.Envios[1];

            Assert.AreEqual(0, vm.Candidatos.Count);
            Assert.IsFalse(vm.HayCandidatos);
            Assert.AreEqual(3, vm.OtrosPedidos.Count);
            Assert.IsTrue(vm.OtrosDesplegados);
            Assert.IsNull(vm.PedidoElegido);
            Assert.IsFalse(vm.AsignarCommand.CanExecute(null));
            Assert.IsTrue(vm.DescartarCommand.CanExecute(null));
            Assert.IsFalse(vm.HayRemitentePaquete, "Sin remitente del paquete no se ofrece recordarlo");
        }

        [TestMethod]
        public async Task ElegirOtroPedido_QuitaElCandidato_YAlReves()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            vm.OtroPedidoSeleccionado = vm.OtrosPedidos[0];

            Assert.IsNull(vm.CandidatoSeleccionado);
            Assert.AreEqual(913500, vm.PedidoElegido!.Pedido);

            vm.CandidatoSeleccionado = vm.Candidatos[0];

            Assert.IsNull(vm.OtroPedidoSeleccionado);
            Assert.AreEqual(912000, vm.PedidoElegido!.Pedido);
        }

        [TestMethod]
        public async Task Con503_AvisaConElMensajeDeLaApi_YTodoQuedaVacio()
        {
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._))
                .Throws(new EnvioProveedorPorAsignarException("Todavía no se puede: hay que lanzar Scripts/Issue857d_ComprasCorreosAplicarFecha.sql.", 503));
            EnviosProveedorPorAsignarViewModel vm = Crear();

            await Llegar(vm);

            Assert.IsFalse(vm.Disponible);
            Assert.AreEqual("Todavía no se puede: hay que lanzar Scripts/Issue857d_ComprasCorreosAplicarFecha.sql.", vm.Aviso);
            Assert.AreEqual(0, vm.Envios.Count);
            Assert.IsNull(vm.EnvioSeleccionado);
            Assert.AreEqual(0, vm.Candidatos.Count);
            Assert.IsFalse(vm.AsignarCommand.CanExecute(null));
            Assert.IsFalse(vm.DescartarCommand.CanExecute(null));
            A.CallTo(() => _servicio.LeerRemitentesPaquete()).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Con403_SeExplicaYLaListaQuedaVacia()
        {
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._))
                .Throws(new EnvioProveedorPorAsignarException("Solo Compras, Dirección o Informática pueden ver y marcar los correos de seguimiento de compras.", 403));
            EnviosProveedorPorAsignarViewModel vm = Crear();

            await Llegar(vm);

            Assert.AreEqual("Solo Compras, Dirección o Informática pueden ver y marcar los correos de seguimiento de compras.", vm.Aviso);
            Assert.AreEqual(0, vm.Envios.Count);
        }

        [TestMethod]
        public async Task CambiarLosDias_VuelveALeerConEsosDias()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            vm.Dias = 30;
            await Task.Yield();

            A.CallTo(() => _servicio.LeerPorAsignar(30)).MustHaveHappened();
        }

        // ------------------------------------------------------------------ asignar

        [TestMethod]
        public async Task Asignar_MandaLaClaveYElPedidoElegidoRecordandoElRemitente_EnsenaElMensajeYRelee()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.AsignarAsync();

            A.CallTo(() => _servicio.Asignar(101, "1", 912001, true, false)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Puesta la fecha 14/11/26 en 3 líneas del pedido 912001.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Asignar_SinMarcarRecordar_NoLoPide()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);
            vm.RecordarRemitente = false;

            await vm.AsignarAsync();

            A.CallTo(() => _servicio.Asignar(101, "1", 912001, false, false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Asignar_UnEnvioSinRemitenteDelPaquete_NuncaPideRecordar()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);
            vm.EnvioSeleccionado = vm.Envios[1];
            vm.OtroPedidoSeleccionado = vm.OtrosPedidos.First(p => p.Pedido == 913500);
            Assert.IsTrue(vm.RecordarRemitente, "La casilla vuelve a estar marcada por defecto, aunque no se vea");

            await vm.AsignarAsync();

            A.CallTo(() => _servicio.Asignar(200, "1", 913500, false, false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Asignar_Con409_PreguntaSiReasignar_YSiDiceQueSiRepiteConReasignar()
        {
            A.CallTo(() => _servicio.Asignar(A<int>._, A<string>._, A<int>._, A<bool>._, false))
                .Throws(new EnvioProveedorPorAsignarException("El correo ya está aplicado al pedido 911000.", 409));
            A.CallTo(() => _servicio.Asignar(A<int>._, A<string>._, A<int>._, A<bool>._, true))
                .Returns(Task.FromResult(new RespuestaAsignacionEnvioProveedor { Mensaje = "Reasignado al pedido 912001." }));
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.AsignarAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Reasignar el envío",
                A<string>.That.StartsWith("El correo ya está aplicado al pedido 911000."), true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Asignar(101, "1", 912001, true, true)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Reasignado al pedido 912001.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Asignar_Con409_SiDiceQueNo_NoReasignaYRelee()
        {
            A.CallTo(() => _servicio.Asignar(A<int>._, A<string>._, A<int>._, A<bool>._, false))
                .Throws(new EnvioProveedorPorAsignarException("El correo ya está aplicado al pedido 911000.", 409));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(false);
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.AsignarAsync();

            A.CallTo(() => _servicio.Asignar(A<int>._, A<string>._, A<int>._, A<bool>._, true)).MustNotHaveHappened();
            Assert.AreEqual("El correo ya está aplicado al pedido 911000. No se ha cambiado nada.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Asignar_Con400_EnsenaElMensajeTalCual_YNoRelee()
        {
            A.CallTo(() => _servicio.Asignar(A<int>._, A<string>._, A<int>._, A<bool>._, A<bool>._))
                .Throws(new EnvioProveedorPorAsignarException("El pedido 912001 no tiene líneas enviadas y sin recibir.", 400));
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.AsignarAsync();

            Assert.AreEqual("El pedido 912001 no tiene líneas enviadas y sin recibir.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("El pedido 912001 no tiene líneas enviadas y sin recibir.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public async Task Asignar_Con503_SeQuedaSinServicio()
        {
            A.CallTo(() => _servicio.Asignar(A<int>._, A<string>._, A<int>._, A<bool>._, A<bool>._))
                .Throws(new EnvioProveedorPorAsignarException("Todavía no se puede.", 503));
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.AsignarAsync();

            Assert.IsFalse(vm.Disponible);
            Assert.AreEqual("Todavía no se puede.", vm.Aviso);
            Assert.AreEqual(0, vm.Envios.Count);
        }

        [TestMethod]
        public async Task Asignar_UnErrorDeRed_NoRompe_YSeDice()
        {
            A.CallTo(() => _servicio.Asignar(A<int>._, A<string>._, A<int>._, A<bool>._, A<bool>._))
                .Throws(new HttpRequestException("Sin conexión"));
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.AsignarAsync();

            Assert.AreEqual("No se ha podido asignar: Sin conexión", vm.Mensaje);
            Assert.IsFalse(vm.EstaOcupado);
        }

        // ------------------------------------------------------------------ descartar

        [TestMethod]
        public async Task Descartar_PideConfirmacion_MandaLaClave_EnsenaElMensajeYRelee()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.DescartarAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAnswer("No es una compra", A<string>.That.Contains("sus 2 correos"), true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Descartar(101)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Descartados 3 correos del envío.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Descartar_SiNoLoConfirma_NoLlamaALaApi()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(false);
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.DescartarAsync();

            A.CallTo(() => _servicio.Descartar(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Descartar_Con409_EnsenaElMensaje_YRelee()
        {
            A.CallTo(() => _servicio.Descartar(A<int>._))
                .Throws(new EnvioProveedorPorAsignarException("El correo ya está aplicado a un pedido: no se puede descartar.", 409));
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);

            await vm.DescartarAsync();

            Assert.AreEqual("El correo ya está aplicado a un pedido: no se puede descartar.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerPorAsignar(A<int>._)).MustHaveHappenedTwiceExactly();
        }

        // ------------------------------------------------------------------ remitentes recordados

        [TestMethod]
        public async Task BorrarRemitente_ConConfirmacion_LoBorraYRelee()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);
            vm.RemitenteSeleccionado = vm.Remitentes[0];

            await vm.BorrarRemitenteAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Olvidar el remitente", A<string>.That.Contains("mbe store c/o multiboxes"), true)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.BorrarRemitentePaquete(5)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerRemitentesPaquete()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task BorrarRemitente_SinConfirmar_NoBorra()
        {
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._)).Returns(false);
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);
            vm.RemitenteSeleccionado = vm.Remitentes[0];

            await vm.BorrarRemitenteAsync();

            A.CallTo(() => _servicio.BorrarRemitentePaquete(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task BorrarRemitente_Con404_EnsenaElMensajeYRelee()
        {
            A.CallTo(() => _servicio.BorrarRemitentePaquete(A<int>._)).Throws(new EnvioProveedorPorAsignarException("Ya no existe.", 404));
            EnviosProveedorPorAsignarViewModel vm = Crear();
            await Llegar(vm);
            vm.RemitenteSeleccionado = vm.Remitentes[0];

            await vm.BorrarRemitenteAsync();

            Assert.AreEqual("Ya no existe.", vm.Mensaje);
            A.CallTo(() => _servicio.LeerRemitentesPaquete()).MustHaveHappenedTwiceExactly();
        }

        // ------------------------------------------------------------------ desde la campana

        [TestMethod]
        public async Task DesdeLaCampana_EligeElEnvioQueContieneEseCorreo()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();

            // 103 no es la Clave, pero es un correo del envío 101
            await Llegar(vm, new ParametrosNavegacion { { NotificacionBuzon.PARAMETRO_CORREO_COMPRA, 103 } });

            Assert.AreEqual(101, vm.EnvioSeleccionado!.Clave);
            Assert.AreEqual(EnviosProveedorPorAsignarViewModel.PESTANA_POR_ASIGNAR, vm.PestanaSeleccionada);

            await Llegar(vm, new ParametrosNavegacion { { NotificacionBuzon.PARAMETRO_CORREO_COMPRA, "200" } });

            Assert.AreEqual(200, vm.EnvioSeleccionado!.Clave, "La pestaña reutilizada vuelve a leer y elige el nuevo");
        }

        [TestMethod]
        public async Task DesdeLaCampana_UnCorreoQueYaNoEstaPorAsignar_SeDiceYSeEligeElPrimero()
        {
            EnviosProveedorPorAsignarViewModel vm = Crear();

            await Llegar(vm, new ParametrosNavegacion { { NotificacionBuzon.PARAMETRO_CORREO_COMPRA, 999 } });

            Assert.AreEqual(101, vm.EnvioSeleccionado!.Clave);
            StringAssert.StartsWith(vm.Mensaje, "El correo 999 ya no está por asignar");
        }

        [TestMethod]
        public void ElMenu_SoloParaComprasDireccionEInformatica_ConElGrupoVisible()
        {
            var configuracion = A.Fake<IConfiguracion>();
            var menu = new ProductoMenuBarViewModel(A.Fake<IServicioNavegacion>(), configuracion, A.Fake<Nesto.Infrastructure.Services.IServicioDiferenciasReposicion>());
            Assert.IsFalse(menu.CanAbrirEnviosProveedorPorAsignar());
            Assert.IsFalse(menu.PuedeVerGrupoCompras);

            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA)).Returns(true);

            Assert.IsTrue(menu.CanAbrirEnviosProveedorPorAsignar());
            Assert.IsTrue(menu.PuedeVerGrupoCompras);
            Assert.IsFalse(menu.PuedeVerPropuestasStock, "Las propuestas siguen siendo solo de Compras y Dirección");
        }

        [TestMethod]
        public void ElBotonDelMenu_AbreLaPantalla()
        {
            var navegacion = A.Fake<IServicioNavegacion>();
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)).Returns(true);
            var menu = new ProductoMenuBarViewModel(navegacion, configuracion, A.Fake<Nesto.Infrastructure.Services.IServicioDiferenciasReposicion>());

            menu.AbrirEnviosProveedorPorAsignarCommand.Execute(null);

            A.CallTo(() => navegacion.RequestNavigate("MainRegion", "EnviosProveedorPorAsignarView")).MustHaveHappenedOnceExactly();
        }
    }
}
