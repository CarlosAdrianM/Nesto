using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#642 (NestoAPI#873, pedido 929555): Aida guardó el pedido con un prepago a las 15:14 y Alfredo, que lo tenía abierto
    /// desde antes, lo guardó a las 15:17 y el prepago desapareció. Ahora Nesto manda la versión del pedido al guardar, trata el 409
    /// PEDIDO_CAMBIADO (mensaje y «¿recargar?») y, con «pedidoCambiado» de otro, pone la banda y deshabilita Guardar.
    /// </summary>
    [TestClass]
    public class PedidoCambiadoDetalleTests
    {
        private const int NUMERO = 929555;
        private const string VERSION = "v1";
        private const string MENSAJE_409 = "El pedido 929555 lo ha modificado Aida a las 15:14 desde que usted lo abrió. Recárguelo y vuelva a hacer los cambios.";

        private static PedidoVentaDTO Pedido(string version = VERSION) => new PedidoVentaDTO
        {
            empresa = "1",
            numero = NUMERO,
            cliente = "15191",
            contacto = "0",
            Version = version,
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { id = 1, Producto = "38093", Cantidad = 2, almacen = "ALG", tipoLinea = 1, fechaEntrega = new DateTime(2026, 11, 17) }
            }
        };

        private static DetallePedidoViewModel Vm(IPedidoVentaService servicio, IServicioDialogos dialogos, IGruposAlmacenTiempoReal grupos = null)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.GruposTiempoReal = grupos;
            vm.pedido = new PedidoVentaWrapper(Pedido());
            vm.MarcarComoGuardado();
            return vm;
        }

        private static PedidoCambiadoAviso Aviso(string version, string usuario = "Aida") =>
            new PedidoCambiadoAviso { Empresa = "1", Pedido = NUMERO, Usuario = usuario, Fecha = DateTime.Today.AddHours(15).AddMinutes(14), Version = version };

        // ---------- Lo que llega de la API ----------

        [TestMethod]
        public void InterpretarRespuestaError_PedidoCambiado_MensajeLimpioQuienYCuando()
        {
            string json = "{\"error\":{\"code\":\"PEDIDO_CAMBIADO\",\"message\":\"" + MENSAJE_409 + "\",\"details\":{\"Version\":\"v2\",\"Usuario\":\"Aida\",\"Fecha\":\"2026-11-16T15:14:00\"}},\"Message\":\"" + MENSAJE_409 + "\"}";

            var ex = PedidoVentaService.InterpretarRespuestaError(json) as PedidoCambiadoException;

            Assert.IsNotNull(ex);
            Assert.AreEqual(MENSAJE_409, ex.Message, "Sin el código delante");
            Assert.AreEqual("Aida", ex.Usuario);
            Assert.AreEqual(new DateTime(2026, 11, 16, 15, 14, 0), ex.Fecha);
        }

        [TestMethod]
        public void LeerVersion_DelCuerpoDelPost()
        {
            Assert.AreEqual("abc", PedidoVentaService.LeerVersion("{\"numero\":929555,\"Version\":\"abc\"}"));
            Assert.IsNull(PedidoVentaService.LeerVersion("{\"numero\":929555}"));
            Assert.IsNull(PedidoVentaService.LeerVersion("no es json"));
        }

        [TestMethod]
        public void TextoBanda_DeUsted_ConQuienYCuando()
        {
            DateTime hoy = new DateTime(2026, 11, 16);
            Assert.AreEqual("Aida ha modificado este pedido a las 15:14. Recárguelo antes de guardar.",
                DetallePedidoViewModel.TextoBandaPedidoCambiado("Aida", new DateTime(2026, 11, 16, 15, 14, 0), hoy));
            Assert.AreEqual("Aida ha modificado este pedido el 15/11/2026 a las 09:05. Recárguelo antes de guardar.",
                DetallePedidoViewModel.TextoBandaPedidoCambiado("Aida", new DateTime(2026, 11, 15, 9, 5, 0), hoy));
            Assert.AreEqual("Otra persona ha modificado este pedido. Recárguelo antes de guardar.",
                DetallePedidoViewModel.TextoBandaPedidoCambiado(null, null, hoy));
        }

        // ---------- El 409 al guardar ----------

        [TestMethod]
        public async Task Guardar_ConLaVersion_LaMandaEnElPut()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).Returns(new List<AvisoPedidoModel>());
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>());

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>.That.Matches(p => p.Version == VERSION))).MustHaveHappenedOnceExactly();
        }

        /// <summary>El diálogo de botones contesta lo que diga <paramref name="contestar"/> (por su mensaje) y apunta los mensajes.</summary>
        private static List<string> DialogoContesta(IServicioDialogos dialogos, Func<string, ResultadoBoton> contestar)
        {
            var mensajes = new List<string>();
            A.CallTo(() => dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo parametros, Action<ResultadoDialogo> callback) =>
                {
                    string mensaje = parametros.GetValue<string>("message");
                    mensajes.Add(mensaje);
                    callback?.Invoke(new ResultadoDialogo(contestar(mensaje)));
                });
            return mensajes;
        }

        [TestMethod]
        public async Task Guardar_409PedidoCambiado_EnsenaElMensajePoneLaBandaYOfreceRecargarConservando()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new PedidoCambiadoException(MENSAJE_409, "Aida", DateTime.Today.AddHours(15).AddMinutes(14)));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            List<string> mensajes = DialogoContesta(dialogos, m => ResultadoBoton.Cancel); // «Ahora no»
            DetallePedidoViewModel vm = Vm(servicio, dialogos);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>.That.Matches(p =>
                p.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_SI) == DetallePedidoViewModel.TEXTO_RECARGAR_CONSERVANDO &&
                p.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_TERCERO) == DetallePedidoViewModel.TEXTO_RECARGAR_SIN_CAMBIOS),
                A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(mensajes.Single(), MENSAJE_409);
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>.That.Contains("modificado correctamente"))).MustNotHaveHappened();
            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("Aida ha modificado este pedido a las 15:14. Recárguelo antes de guardar.", vm.TextoPedidoCambiadoPorOtro);
            Assert.IsFalse(vm.cmdModificarPedido.CanExecute(null), "Guardar deshabilitado hasta recargar");
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_409_RecargarConservando_ReleeYPoneEncimaLoDelUsuario()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new PedidoCambiadoException(MENSAJE_409, "Aida", null));
            PedidoVentaDTO delServidor = Pedido("v2");
            delServidor.formaPago = "TRN"; // lo de Aida
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).Returns(delServidor);
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            DialogoContesta(dialogos, m => ResultadoBoton.OK);
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.pedido.Model.Lineas.Add(new LineaPedidoVentaDTO { id = 0, Producto = "17404", Cantidad = 3, almacen = "ALG", tipoLinea = 1 }); // lo de Alfredo

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>._)).MustHaveHappened();
            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("v2", vm.pedido.Model.Version, "Con la versión nueva");
            Assert.AreEqual("TRN", vm.pedido.Model.formaPago, "Se queda lo de Aida");
            Assert.AreEqual(2, vm.pedido.Model.Lineas.Count, "Y la línea de Alfredo, sin guardar");
            Assert.IsTrue(vm.pedido.Model.Lineas.Any(l => l.Producto == "17404" && l.id == 0));
            Assert.IsTrue(vm.cmdModificarPedido.CanExecute(null));
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly(); // no vuelve a guardar solo
        }

        [TestMethod]
        public async Task Guardar_409_RecargarSinMisCambios_PideConfirmacion()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new PedidoCambiadoException(MENSAJE_409, "Aida", null));
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).Returns(Pedido("v2"));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            DialogoContesta(dialogos, m => ResultadoBoton.No); // el del medio: «Recargar sin mis cambios»
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(true));
            DetallePedidoViewModel vm = Vm(servicio, dialogos);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>.That.Contains("se pierde todo lo que no ha guardado"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("v2", vm.pedido.Model.Version);
        }

        [TestMethod]
        public async Task Guardar_ConLaBandaPuesta_NoLlamaALaApi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            DialogoContesta(dialogos, m => ResultadoBoton.Cancel);
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.ProcesarPedidoCambiado(Aviso("v2"));

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
        }

        // ---------- «pedidoCambiado» ----------

        [TestMethod]
        public void PedidoCambiado_DeOtro_BandaYGuardarDeshabilitado()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>());

            vm.ProcesarPedidoCambiado(Aviso("v2"));

            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("Aida ha modificado este pedido a las 15:14. Recárguelo antes de guardar.", vm.TextoPedidoCambiadoPorOtro);
            Assert.IsFalse(vm.cmdModificarPedido.CanExecute(null));
            Assert.IsTrue(vm.RecargarPedidoCambiadoCommand.CanExecute(null));
        }

        [TestMethod]
        public void PedidoCambiado_ConSuMismaVersion_EsElPropioGuardado_NadaQueHacer()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>());

            vm.ProcesarPedidoCambiado(Aviso(VERSION, "Alfredo"));

            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
            Assert.IsTrue(vm.cmdModificarPedido.CanExecute(null));
        }

        [TestMethod]
        public void PedidoCambiado_DeOtroPedido_NadaQueHacer()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>());
            PedidoCambiadoAviso otro = Aviso("v2");
            otro.Pedido = 929556;

            vm.ProcesarPedidoCambiado(otro);

            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task PedidoCambiado_DelPropioGuardado_LlegaAntesQueLaRespuesta_NoPoneLaBanda()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            DetallePedidoViewModel vm = null;
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                vm.ProcesarPedidoCambiado(Aviso("v2", "Alfredo")); // el aviso llega antes que la respuesta del PUT...
                p.Version = "v2"; // ...que trae la versión nueva
                return Task.FromResult(new List<AvisoPedidoModel>());
            });
            vm = Vm(servicio, A.Fake<IServicioDialogos>());

            await vm.ModificarPedidoAsync();

            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("v2", vm.pedido.Model.Version, "Sigue guardando sin recargar");
        }

        [TestMethod]
        public async Task PedidoCambiado_DeOtroMientrasSeGuarda_PoneLaBandaAlTerminar()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            DetallePedidoViewModel vm = null;
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ReturnsLazily((PedidoVentaDTO p) =>
            {
                vm.ProcesarPedidoCambiado(Aviso("v3")); // Aida guarda a la vez
                Assert.IsFalse(vm.PedidoCambiadoPorOtro, "Mientras se guarda no se decide");
                p.Version = "v2";
                return Task.FromResult(new List<AvisoPedidoModel>());
            });
            vm = Vm(servicio, A.Fake<IServicioDialogos>());

            await vm.ModificarPedidoAsync();

            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
        }

        [TestMethod]
        public void PedidoCambiado_PorSignalR_LlegaALaBanda()
        {
            IGruposAlmacenTiempoReal grupos = A.Fake<IGruposAlmacenTiempoReal>();
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), grupos);
            vm.ActivarAvisosPedido();

            grupos.PedidoCambiado += Raise.With<PedidoCambiadoAviso>(grupos, Aviso("v2"));

            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
        }

        [TestMethod]
        public void PedidoCambiado_ConLaVistaOculta_NoEscucha()
        {
            IGruposAlmacenTiempoReal grupos = A.Fake<IGruposAlmacenTiempoReal>();
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), grupos);
            vm.ActivarAvisosPedido();
            vm.DesactivarAvisosPedido();

            grupos.PedidoCambiado += Raise.With<PedidoCambiadoAviso>(grupos, Aviso("v2"));

            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
        }

        // ---------- Entrar y salir del grupo ----------

        [TestMethod]
        public void Activar_EntraEnElGrupoDelPedido_Desactivar_Sale()
        {
            IGruposAlmacenTiempoReal grupos = A.Fake<IGruposAlmacenTiempoReal>();
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), grupos);

            vm.ActivarAvisosPedido();
            vm.ActivarAvisosPedido(); // Loaded dos veces: una sola entrada

            A.CallTo(() => grupos.EntrarEnGrupo("Pedido:1:929555")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Pedido:1:929555", vm.GrupoPedidoActual);

            vm.DesactivarAvisosPedido();
            vm.DesactivarAvisosPedido();

            A.CallTo(() => grupos.SalirDeGrupo("Pedido:1:929555")).MustHaveHappenedOnceExactly();
            Assert.IsNull(vm.GrupoPedidoActual);
        }

        [TestMethod]
        public async Task CambiarDePedido_SaleDelGrupoDelAnteriorYEntraEnElDelNuevo()
        {
            IGruposAlmacenTiempoReal grupos = A.Fake<IGruposAlmacenTiempoReal>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            PedidoVentaDTO otro = Pedido("w1");
            otro.numero = 929600;
            A.CallTo(() => servicio.cargarPedido("1", 929600)).Returns(otro);
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>(), grupos);
            vm.ActivarAvisosPedido();
            vm.ProcesarPedidoCambiado(Aviso("v2")); // la banda del pedido anterior

            await vm.CargarPedidoAsync(new PedidoVentaModel.ResumenPedido { empresa = "1", numero = 929600 });

            A.CallTo(() => grupos.SalirDeGrupo("Pedido:1:929555")).MustHaveHappenedOnceExactly();
            A.CallTo(() => grupos.EntrarEnGrupo("Pedido:1:929600")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.PedidoCambiadoPorOtro, "La banda era del pedido anterior");
        }

        [TestMethod]
        public void PedidoNuevo_NoEntraEnNingunGrupo()
        {
            IGruposAlmacenTiempoReal grupos = A.Fake<IGruposAlmacenTiempoReal>();
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), grupos);
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = 0 });

            vm.ActivarAvisosPedido();

            A.CallTo(() => grupos.EntrarEnGrupo(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void SinTiempoReal_ActivarYDesactivarNoLanzan()
        {
            DetallePedidoViewModel vm = Vm(A.Fake<IPedidoVentaService>(), A.Fake<IServicioDialogos>(), null);

            vm.ActivarAvisosPedido();
            vm.DesactivarAvisosPedido();

            Assert.IsNull(vm.GrupoPedidoActual);
        }

        // ---------- Sin temporizadores: al volver a enseñarse, se pregunta la versión una vez ----------

        [TestMethod]
        public async Task VolverAEnsenarse_ConOtraVersion_PoneLaBanda()
        {
            IGruposAlmacenTiempoReal grupos = A.Fake<IGruposAlmacenTiempoReal>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerVersionPedido("1", NUMERO)).Returns(new VersionPedidoModel { Version = "v2", Usuario = "Aida", Fecha = DateTime.Today.AddHours(15).AddMinutes(14) });
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>(), grupos);
            vm.ActivarAvisosPedido();
            A.CallTo(() => servicio.LeerVersionPedido(A<string>._, A<int>._)).MustNotHaveHappened(); // la primera vez acaba de cargarse
            vm.DesactivarAvisosPedido();

            vm.ActivarAvisosPedido();
            await vm.ComprobacionVersionEnCurso;

            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("Aida ha modificado este pedido a las 15:14. Recárguelo antes de guardar.", vm.TextoPedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task VolverAEnsenarse_ConLaMismaVersion_NadaQueHacer()
        {
            IGruposAlmacenTiempoReal grupos = A.Fake<IGruposAlmacenTiempoReal>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerVersionPedido("1", NUMERO)).Returns(new VersionPedidoModel { Version = VERSION });
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>(), grupos);
            vm.ActivarAvisosPedido();
            vm.DesactivarAvisosPedido();

            vm.ActivarAvisosPedido();
            await vm.ComprobacionVersionEnCurso;

            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task TrasReconectar_HaCambiadoDelGrupo_PreguntaLaVersion()
        {
            IGruposAlmacenTiempoReal grupos = A.Fake<IGruposAlmacenTiempoReal>();
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerVersionPedido("1", NUMERO)).Returns(new VersionPedidoModel { Version = "v2", Usuario = "Aida" });
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>(), grupos);
            vm.ActivarAvisosPedido();

            grupos.GrupoHaCambiado += Raise.With<string>(grupos, "Pedido:1:929555");
            await vm.ComprobacionVersionEnCurso;

            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task ComprobarVersion_SiLaApiNoContesta_NadaQueHacer()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerVersionPedido(A<string>._, A<int>._)).Returns(Task.FromResult<VersionPedidoModel>(null));
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>());

            await vm.ComprobarVersionPedidoAsync();

            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
        }

        // ---------- Recargar ----------

        [TestMethod]
        public async Task RecargarSinMisCambios_SiempreConConfirmacion()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.ProcesarPedidoCambiado(Aviso("v2"));
            vm.pedido.Model.comentarios = "un cambio mío";

            await vm.RecargarPedidoCambiadoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>.That.Contains("se pierde todo lo que no ha guardado"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
        }

        // ---------- Acciones que cambian el pedido sin recargarlo: refrescan la versión (Carlos 17/11/26) ----------

        private static IPedidoVentaService ServicioConVersiones(params string[] versiones)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.LeerVersionPedido("1", NUMERO)).ReturnsNextFromSequence(
                versiones.Select(v => v == null ? null : new VersionPedidoModel { Version = v, Usuario = "Aida", Fecha = DateTime.Today.AddHours(15) }).ToArray());
            return servicio;
        }

        [TestMethod]
        public async Task AccionAlDia_AdoptaLaVersionDeDespues_YElSiguienteGuardarLaManda()
        {
            IPedidoVentaService servicio = ServicioConVersiones(VERSION, "v2");
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).Returns(new List<AvisoPedidoModel>());
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>());

            bool hecho = await vm.ConVersionAlDiaAsync(() => Task.FromResult(true)); // p. ej. Retener

            Assert.IsTrue(hecho);
            Assert.AreEqual("v2", vm.pedido.Model.Version);
            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
            await vm.ModificarPedidoAsync();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>.That.Matches(p => p.Version == "v2"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AccionAlDia_SuPropioPedidoCambiado_NoSacaLaBanda()
        {
            IPedidoVentaService servicio = ServicioConVersiones(VERSION, "v2");
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>());

            await vm.ConVersionAlDiaAsync(() =>
            {
                vm.ProcesarPedidoCambiado(Aviso("v2", "Alfredo")); // el aviso de la propia acción llega antes de terminar
                return Task.CompletedTask;
            });

            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("v2", vm.pedido.Model.Version);
        }

        [TestMethod]
        public async Task Accion_SiOtroLoHabiaCambiadoAntes_BandaYNoAdoptaLaVersion()
        {
            // Si se adoptara, el siguiente Guardar pisaría lo de Aida sin 409
            IPedidoVentaService servicio = ServicioConVersiones("vAida", "v3");
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>());

            await vm.ConVersionAlDiaAsync(() => Task.FromResult(1));

            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual(VERSION, vm.pedido.Model.Version);
        }

        [TestMethod]
        public async Task Accion_QueFalla_NoTocaLaVersionYSePropaga()
        {
            IPedidoVentaService servicio = ServicioConVersiones(VERSION, "v2");
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>());

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                vm.ConVersionAlDiaAsync<bool>(() => throw new InvalidOperationException("la API dice que no")));

            Assert.AreEqual(VERSION, vm.pedido.Model.Version);
            vm.ProcesarPedidoCambiado(Aviso("v9"));
            Assert.IsTrue(vm.PedidoCambiadoPorOtro, "Ya no se está «guardando»: los avisos vuelven a contar");
        }

        [TestMethod]
        public async Task Accion_SinVersionDeLaApi_SeQuedaComoEstaba()
        {
            IPedidoVentaService servicio = ServicioConVersiones(null, null);
            DetallePedidoViewModel vm = Vm(servicio, A.Fake<IServicioDialogos>());

            await vm.ConVersionAlDiaAsync(() => Task.FromResult(true));

            Assert.AreEqual(VERSION, vm.pedido.Model.Version);
            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
        }

        [TestMethod]
        public void AccionesDelDetalle_PasanPorConVersionAlDia()
        {
            // Las que cambian el pedido sin recargarlo (si se añade otra, que pase también por ConVersionAlDiaAsync)
            var dir = new System.IO.DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);
            while (dir != null && !System.IO.Directory.Exists(System.IO.Path.Combine(dir.FullName, "PedidoVenta", "ViewModels")))
            {
                dir = dir.Parent;
            }
            Assert.IsNotNull(dir, "No se encuentra el código del módulo");
            string carpeta = System.IO.Path.Combine(dir.FullName, "PedidoVenta", "ViewModels");
            string codigo = System.IO.File.ReadAllText(System.IO.Path.Combine(carpeta, "DetallePedidoViewModel.vb")) +
                System.IO.File.ReadAllText(System.IO.Path.Combine(carpeta, "DetallePedidoViewModel.NotaEntrega.vb"));
            foreach (string accion in new[] { "RetenerPedido", "LiberarPedido", "DecidirRetencionPorFalta", "CambiarCabeceraNotaEntrega",
                "CrearEtiquetaPendiente", "EliminarEtiquetaPendiente", "CambiarServirPendienteNota", "PonerSuPedidoFactura" })
            {
                int llamadas = System.Text.RegularExpressions.Regex.Matches(codigo, @"servicio\." + accion + @"\(").Count;
                int envueltas = System.Text.RegularExpressions.Regex.Matches(codigo, @"ConVersionAlDiaAsync\(Function\(\) servicio\." + accion + @"\(").Count;
                Assert.IsTrue(llamadas > 0, accion);
                Assert.AreEqual(llamadas, envueltas, $"{accion}: todas sus llamadas refrescan la versión");
            }
        }

        // ---------- «Recargar conservando mis cambios» desde la pantalla ----------

        [TestMethod]
        public async Task RecargarConservando_ConConflicto_PreguntaDeUstedYSeQuedaConLoElegido()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            PedidoVentaDTO delServidor = Pedido("v2");
            delServidor.Lineas.First().Cantidad = 5; // Alfredo
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).Returns(delServidor);
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            List<string> preguntas = DialogoContesta(dialogos, m => ResultadoBoton.OK); // «Con 8 (el mío)»
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.ProcesarPedidoCambiado(Aviso("v2", "Alfredo"));
            vm.pedido.Model.Lineas.First().Cantidad = 8;

            await vm.RecargarConservandoCambiosAsync();

            Assert.AreEqual("Alfredo cambió la cantidad de la línea 1 (38093) a 5 y usted a 8: ¿con cuál se queda?", preguntas.Single());
            Assert.AreEqual(8, vm.pedido.Model.Lineas.First().Cantidad);
            Assert.AreEqual("v2", vm.pedido.Model.Version);
            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
        }

        [TestMethod]
        public async Task RecargarConservando_CancelarEnUnaPregunta_LaPantallaSigueComoEstaba()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            PedidoVentaDTO delServidor = Pedido("v2");
            delServidor.Lineas.First().Cantidad = 5;
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).Returns(delServidor);
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            DialogoContesta(dialogos, m => ResultadoBoton.Cancel);
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.ProcesarPedidoCambiado(Aviso("v2", "Alfredo"));
            PedidoVentaDTO enPantalla = vm.pedido.Model;
            enPantalla.Lineas.First().Cantidad = 8;

            await vm.RecargarConservandoCambiosAsync();

            Assert.AreSame(enPantalla, vm.pedido.Model);
            Assert.AreEqual(VERSION, vm.pedido.Model.Version);
            Assert.IsTrue(vm.PedidoCambiadoPorOtro, "La banda sigue");
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>.That.Contains("No se ha recargado nada"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task RecargarConservando_SinFoto_OfreceRecargarSinCambios()
        {
            // Guardado con líneas nuevas: sus Nº de orden solo los sabe el servidor
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.pedido.Model.Lineas.Add(new LineaPedidoVentaDTO { id = 0, Producto = "17404", Cantidad = 1, tipoLinea = 1 });
            vm.MarcarComoGuardado();
            Assert.IsFalse(vm.TieneFotoAlCargar);

            await vm.RecargarConservandoCambiosAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>.That.Contains("no se pueden separar sus cambios"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
        }
    }
}
