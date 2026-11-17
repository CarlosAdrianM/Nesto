using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
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

        [TestMethod]
        public async Task Guardar_409PedidoCambiado_EnsenaElMensajePoneLaBandaYOfreceRecargar()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new PedidoCambiadoException(MENSAJE_409, "Aida", DateTime.Today.AddHours(15).AddMinutes(14)));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
            DetallePedidoViewModel vm = Vm(servicio, dialogos);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>.That.Contains(MENSAJE_409))).MustHaveHappenedOnceExactly();
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>.That.Contains("modificado correctamente"))).MustNotHaveHappened();
            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("Aida ha modificado este pedido a las 15:14. Recárguelo antes de guardar.", vm.TextoPedidoCambiadoPorOtro);
            Assert.IsFalse(vm.cmdModificarPedido.CanExecute(null), "Guardar deshabilitado hasta recargar");
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_409_SiDiceQueSi_RecargaYQuitaLaBanda()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new PedidoCambiadoException(MENSAJE_409, "Aida", null));
            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).Returns(Pedido("v2"));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(true));
            DetallePedidoViewModel vm = Vm(servicio, dialogos);

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.cargarPedido("1", NUMERO)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.PedidoCambiadoPorOtro);
            Assert.AreEqual("v2", vm.pedido.Model.Version);
            Assert.IsTrue(vm.cmdModificarPedido.CanExecute(null));
        }

        [TestMethod]
        public async Task Guardar_ConLaBandaPuesta_NoLlamaALaApi()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.ProcesarPedidoCambiado(Aviso("v2"));

            await vm.ModificarPedidoAsync();

            A.CallTo(() => servicio.modificarPedido(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
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
        public async Task Recargar_ConCambiosSinGuardar_PreguntaAntes()
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(Task.FromResult(false));
            DetallePedidoViewModel vm = Vm(servicio, dialogos);
            vm.ProcesarPedidoCambiado(Aviso("v2"));
            vm.pedido.Model.comentarios = "un cambio mío";

            await vm.RecargarPedidoCambiadoAsync();

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>.That.Contains("se pierden los cambios"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.PedidoCambiadoPorOtro);
        }
    }
}
