using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.Cliente;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Novedades 1258: pedir autorización de un pedido que no pasa la validación de ofertas y descuentos (desde el aviso de la
    /// plantilla) y la ventana «Autorizaciones de pedidos» (lista, detalle con las líneas marcadas, autorizar, denegar y anular).
    /// </summary>
    [TestClass]
    public class AutorizacionesPedidoTests
    {
        private readonly IServicioAutorizacionesPedido servicio = A.Fake<IServicioAutorizacionesPedido>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly IAbridorPedidos abridor = A.Fake<IAbridorPedidos>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly List<string> notificaciones = new();
        private readonly List<string> errores = new();

        [TestInitialize]
        public void Preparar()
        {
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>._)).Invokes((string _, string m) => notificaciones.Add(m));
            A.CallTo(() => dialogos.ShowError(A<string>._)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.usuario).Returns("NUEVAVISION\\Laura");
            A.CallTo(() => configuracion.UsuarioSinDominio).Returns("Laura");
        }

        private void ElBotonPulsado(ResultadoBoton boton)
            => A.CallTo(() => dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string _, ParametrosDialogo __, Action<ResultadoDialogo> cb) => cb(new ResultadoDialogo(boton)));

        private static PedidoVentaDTO Borrador()
        {
            var pedido = new PedidoVentaDTO { empresa = "1", cliente = "15191", contacto = "0" };
            pedido.Lineas.Add(new LineaPedidoVentaDTO { Producto = "45929", texto = "CREMA", Cantidad = 6, PrecioUnitario = 10m, DescuentoLinea = 0.2m, oferta = 3 });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { Producto = "17404", texto = "JABÓN", Cantidad = 1, PrecioUnitario = 5m });
            return pedido;
        }

        // ---------------- Pedir autorización (aviso de la plantilla) ----------------

        [TestMethod]
        public async Task Pedir_SinServicio_NoSeOfrece()
        {
            var resultado = await new SolicitudAutorizacionPedido(null, dialogos).OfrecerAsync(Borrador(), "No se encuentra autorizado el descuento");
            Assert.AreEqual(ResultadoOfrecerAutorizacion.NoOfrecida, resultado);
            A.CallTo(() => dialogos.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Pedir_ElAvisoTraeLosMotivosYElBotonPedirAutorizacion()
        {
            ParametrosDialogo vistos = null;
            A.CallTo(() => dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string _, ParametrosDialogo p, Action<ResultadoDialogo> cb) => { vistos = p; cb(new ResultadoDialogo(ResultadoBoton.Cancel)); });

            var resultado = await new SolicitudAutorizacionPedido(servicio, dialogos).OfrecerAsync(Borrador(), "No se encuentra autorizado el descuento del 20 %");

            Assert.AreEqual(ResultadoOfrecerAutorizacion.NoQuiso, resultado);
            Assert.AreEqual("Pedir autorización", vistos.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_SI));
            Assert.AreEqual("Cerrar", vistos.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_NO));
            StringAssert.Contains(vistos.GetValue<string>("message"), "No se encuentra autorizado el descuento del 20 %");
            A.CallTo(() => servicio.Pedir(A<PedidoVentaDTO>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Pedir_MandaElBorradorTalCualConElComentario_YDiceAQuien()
        {
            ElBotonPulsado(ResultadoBoton.OK);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("  Lo hablé con Carlos  ");
            PedidoVentaDTO borrador = Borrador();
            A.CallTo(() => servicio.Pedir(borrador, A<string>._))
                .Returns(new AutorizacionPedidoModel { Id = 7, Estado = "Pendiente", Destinatarios = new List<string> { "Carlos", "Alfredo", "Laura" } });

            var resultado = await new SolicitudAutorizacionPedido(servicio, dialogos).OfrecerAsync(borrador, "No pasa");

            Assert.AreEqual(ResultadoOfrecerAutorizacion.Pedida, resultado);
            A.CallTo(() => servicio.Pedir(borrador, "  Lo hablé con Carlos  ")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Se ha pedido autorización a Carlos, Alfredo y Laura. Le avisaremos por la campana cuando contesten.", notificaciones.Single());
        }

        [TestMethod]
        public async Task Pedir_CancelarElComentario_NoPideNada()
        {
            ElBotonPulsado(ResultadoBoton.OK);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns(null);

            var resultado = await new SolicitudAutorizacionPedido(servicio, dialogos).OfrecerAsync(Borrador(), "No pasa");

            Assert.AreEqual(ResultadoOfrecerAutorizacion.NoQuiso, resultado);
            A.CallTo(() => servicio.Pedir(A<PedidoVentaDTO>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Pedir_ComentarioDeMasDe500_SeVuelveAPedir()
        {
            ElBotonPulsado(ResultadoBoton.OK);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).ReturnsNextFromSequence(new string('x', 501), "corto");
            A.CallTo(() => servicio.Pedir(A<PedidoVentaDTO>._, A<string>._)).Returns(new AutorizacionPedidoModel { Id = 1 });

            await new SolicitudAutorizacionPedido(servicio, dialogos).OfrecerAsync(Borrador(), "No pasa");

            Assert.AreEqual(SolicitudAutorizacionPedido.MENSAJE_COMENTARIO_LARGO, errores.Single());
            A.CallTo(() => servicio.Pedir(A<PedidoVentaDTO>._, "corto")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Pedir_409YaSolicitada_DiceQueSiguePendiente()
        {
            ElBotonPulsado(ResultadoBoton.OK);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("");
            A.CallTo(() => servicio.Pedir(A<PedidoVentaDTO>._, A<string>._))
                .ThrowsAsync(ErroresApiReparto.Excepcion(409, "{\"Message\":\"Ya la pidió.\",\"Codigo\":\"YA_SOLICITADA\",\"Id\":7}", null));

            var resultado = await new SolicitudAutorizacionPedido(servicio, dialogos).OfrecerAsync(Borrador(), "No pasa");

            Assert.AreEqual(ResultadoOfrecerAutorizacion.YaPedida, resultado);
            Assert.AreEqual("Ya había pedido esta autorización; sigue pendiente.", notificaciones.Single());
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Pedir_400_EnsenaElMessageDeLaApi()
        {
            ElBotonPulsado(ResultadoBoton.OK);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("");
            A.CallTo(() => servicio.Pedir(A<PedidoVentaDTO>._, A<string>._))
                .ThrowsAsync(ErroresApiReparto.Excepcion(400, "{\"Message\":\"El pedido pasa la validación: créelo sin más.\"}", null));

            var resultado = await new SolicitudAutorizacionPedido(servicio, dialogos).OfrecerAsync(Borrador(), "No pasa");

            Assert.AreEqual(ResultadoOfrecerAutorizacion.Error, resultado);
            Assert.AreEqual("El pedido pasa la validación: créelo sin más.", errores.Single());
        }

        [TestMethod]
        public async Task Pedir_503oRutaQueNoExiste_DiceQueLlegaraConElServidor()
        {
            ElBotonPulsado(ResultadoBoton.OK);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("");
            A.CallTo(() => servicio.Pedir(A<PedidoVentaDTO>._, A<string>._))
                .ThrowsAsync(ErroresApiReparto.Excepcion(404, "{\"Message\":\"No HTTP resource was found that matches the request URI 'x'.\"}", null));

            await new SolicitudAutorizacionPedido(servicio, dialogos).OfrecerAsync(Borrador(), "No pasa");

            Assert.AreEqual(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR, errores.Single());
        }

        [TestMethod]
        public void CuerpoPedir_LlevaElPedidoYElComentario()
        {
            JObject cuerpo = JObject.Parse(ServicioAutorizacionesPedido.CuerpoPedir(Borrador(), " Urgente "));
            Assert.AreEqual("15191", (string)cuerpo["Pedido"]["cliente"]);
            Assert.AreEqual(2, ((JArray)cuerpo["Pedido"]["Lineas"]).Count);
            Assert.AreEqual("Urgente", (string)cuerpo["Comentario"]);
            Assert.AreEqual(JTokenType.Null, JObject.Parse(ServicioAutorizacionesPedido.CuerpoPedir(Borrador(), "  "))["Comentario"].Type);
            Assert.AreEqual("PedidosVenta/Autorizaciones?pendientes=true&mias=false", ServicioAutorizacionesPedido.RutaLista(true, false));
        }

        // ---------------- Ventana ----------------

        private static AutorizacionPedidoModel Solicitud(int id, string estado = "Pendiente", string solicitante = "NUEVAVISION\\Laura") => new()
        {
            Id = id,
            Estado = estado,
            Empresa = "1",
            Cliente = "15191",
            Contacto = "0",
            NombreCliente = "EL EDÉN",
            Solicitante = solicitante,
            NombreSolicitante = "Laura Villacieros",
            FechaSolicitud = new DateTime(2026, 11, 20, 10, 5, 0),
            Importe = 123.4m,
            Motivos = new List<string> { "No se encuentra autorizado el descuento del 20 %" },
            Errores = new List<ErrorAutorizacionPedido> { new() { Producto = "45929", Motivo = "Descuento del 20 % no autorizado" } },
            UsuarioResolucion = estado == "Pendiente" ? null : "Carlos",
            MotivoResolucion = estado == "Denegada" ? "No entra en la promoción" : null
        };

        private void PuedeAutorizarElUsuario(bool puede) => A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(puede);

        private AutorizacionesPedidoViewModel Crear() => new(servicio, dialogos, abridor, configuracion);

        [TestMethod]
        public async Task Lista_QuienNoPuedeAutorizar_PideSoloLasSuyas_YSoloPuedeAnular()
        {
            PuedeAutorizarElUsuario(false);
            A.CallTo(() => servicio.Leer(true, true)).Returns(new List<AutorizacionPedidoModel> { Solicitud(1) });
            var vm = Crear();

            await vm.CargarAsync();

            A.CallTo(() => servicio.Leer(true, true)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.PuedeAutorizar);
            vm.Seleccionada = vm.Solicitudes.Single();
            Assert.IsFalse(vm.AutorizarCommand.CanExecute(null));
            Assert.IsFalse(vm.DenegarCommand.CanExecute(null));
            Assert.IsTrue(vm.AnularCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Lista_QuienPuedeAutorizar_VeTodas_YPuedeAutorizarYDenegar()
        {
            PuedeAutorizarElUsuario(true);
            A.CallTo(() => servicio.Leer(true, false)).Returns(new List<AutorizacionPedidoModel> { Solicitud(1, solicitante: "NUEVAVISION\\Manuel") });
            var vm = Crear();

            await vm.CargarAsync();

            vm.Seleccionada = vm.Solicitudes.Single();
            Assert.IsTrue(vm.AutorizarCommand.CanExecute(null));
            Assert.IsTrue(vm.DenegarCommand.CanExecute(null));
            Assert.IsFalse(vm.AnularCommand.CanExecute(null), "No es suya");
            FilaAutorizacionPedido fila = vm.Seleccionada;
            Assert.AreEqual("15191/0 · EL EDÉN", fila.Cliente);
            Assert.AreEqual("Laura Villacieros", fila.Vendedor);
            Assert.AreEqual("20/11/2026 10:05", fila.TextoFecha);
            StringAssert.Contains(fila.TextoMotivos, "20 %");
        }

        [TestMethod]
        public async Task VerResueltas_VuelveALeerConTodas()
        {
            PuedeAutorizarElUsuario(true);
            A.CallTo(() => servicio.Leer(false, false)).Returns(new List<AutorizacionPedidoModel> { Solicitud(1), Solicitud(2, "Denegada") });
            var vm = Crear();
            await vm.CargarAsync();

            vm.VerResueltas = true;
            await Task.Yield();

            A.CallTo(() => servicio.Leer(false, false)).MustHaveHappened();
            Assert.AreEqual(2, vm.Solicitudes.Count);
            Assert.AreEqual("Denegada por Carlos: No entra en la promoción.", vm.Solicitudes.Single(s => s.Id == 2).TextoResolucion);
        }

        [TestMethod]
        public async Task Detalle_MarcaLasLineasConErrorYSuMotivo()
        {
            PuedeAutorizarElUsuario(true);
            A.CallTo(() => servicio.Leer(true, false)).Returns(new List<AutorizacionPedidoModel> { Solicitud(1) });
            AutorizacionPedidoModel detalle = Solicitud(1);
            detalle.Pedido = Borrador();
            A.CallTo(() => servicio.LeerUna(1)).Returns(detalle);
            var vm = Crear();
            await vm.CargarAsync();

            vm.Seleccionada = vm.Solicitudes.Single();
            await Task.Yield();

            Assert.AreEqual(2, vm.Lineas.Count);
            LineaAutorizacionPedido crema = vm.Lineas.Single(l => l.Producto == "45929");
            Assert.IsTrue(crema.ConError);
            Assert.AreEqual("Descuento del 20 % no autorizado", crema.MotivoError);
            Assert.AreEqual("20 %", crema.TextoDescuento);
            Assert.AreEqual("3", crema.TextoOferta);
            Assert.IsFalse(vm.Lineas.Single(l => l.Producto == "17404").ConError);
        }

        [TestMethod]
        public async Task Autorizar_EnsenaElPedidoCreadoYOfreceAbrirlo()
        {
            PuedeAutorizarElUsuario(true);
            A.CallTo(() => servicio.Leer(true, false)).Returns(new List<AutorizacionPedidoModel> { Solicitud(1) });
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("De acuerdo");
            A.CallTo(() => servicio.Autorizar(1, "De acuerdo")).Returns(new ResultadoAutorizarPedido { Id = 1, Estado = "Autorizada", Pedido = 931200 });
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>.That.Contains("931200"))).Returns(true);
            var vm = Crear();
            await vm.CargarAsync();
            vm.Seleccionada = vm.Solicitudes.Single();

            await vm.AutorizarCommand.ExecuteAsync(null);

            A.CallTo(() => servicio.Autorizar(1, "De acuerdo")).MustHaveHappenedOnceExactly();
            A.CallTo(() => abridor.Abrir("1", 931200)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Autorizar_409YaNoPendiente_EnsenaElMensajeYVuelveALeer()
        {
            PuedeAutorizarElUsuario(true);
            A.CallTo(() => servicio.Leer(true, false)).Returns(new List<AutorizacionPedidoModel> { Solicitud(1) });
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("");
            A.CallTo(() => servicio.Autorizar(1, A<string>._)).ThrowsAsync(ErroresApiReparto.Excepcion(409, "{\"Message\":\"Ya la contestó Alfredo.\"}", null));
            var vm = Crear();
            await vm.CargarAsync();
            vm.Seleccionada = vm.Solicitudes.Single();

            await vm.AutorizarCommand.ExecuteAsync(null);

            Assert.AreEqual("Ya la contestó Alfredo.", errores.Single());
            A.CallTo(() => servicio.Leer(true, false)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => abridor.Abrir(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Denegar_ElMotivoEsObligatorio()
        {
            PuedeAutorizarElUsuario(true);
            A.CallTo(() => servicio.Leer(true, false)).Returns(new List<AutorizacionPedidoModel> { Solicitud(1) });
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).ReturnsNextFromSequence("   ", "No entra en la promoción");
            var vm = Crear();
            await vm.CargarAsync();
            vm.Seleccionada = vm.Solicitudes.Single();

            await vm.DenegarCommand.ExecuteAsync(null);

            Assert.AreEqual(AutorizacionesPedidoViewModel.MENSAJE_MOTIVO_VACIO, errores.Single());
            A.CallTo(() => servicio.Denegar(1, "No entra en la promoción")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Autorización denegada.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Anular_PreguntaConNoPorDefecto_YAnula()
        {
            PuedeAutorizarElUsuario(false);
            A.CallTo(() => servicio.Leer(true, true)).Returns(new List<AutorizacionPedidoModel> { Solicitud(1) });
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, true)).Returns(true);
            var vm = Crear();
            await vm.CargarAsync();
            vm.Seleccionada = vm.Solicitudes.Single();

            await vm.AnularCommand.ExecuteAsync(null);

            A.CallTo(() => servicio.Anular(1)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Solicitud anulada.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Lista_ServidorSinActualizar_LoDiceSinRomper()
        {
            A.CallTo(() => servicio.Leer(A<bool>._, A<bool>._)).ThrowsAsync(ErroresApiReparto.Excepcion(404, "", null));
            var vm = Crear();

            await vm.CargarAsync();

            Assert.AreEqual(ErroresApiReparto.MENSAJE_SERVIDOR_SIN_ACTUALIZAR, vm.Mensaje);
            Assert.AreEqual(0, vm.Solicitudes.Count);
        }

        [TestMethod]
        public async Task DesdeLaCampana_UnaDenegada_SeEligeConLasResueltasYSeEnsenaElMotivo()
        {
            PuedeAutorizarElUsuario(false);
            A.CallTo(() => servicio.Leer(true, true)).Returns(new List<AutorizacionPedidoModel>());
            A.CallTo(() => servicio.Leer(false, true)).Returns(new List<AutorizacionPedidoModel> { Solicitud(5, "Denegada") });
            var vm = Crear();

            vm.AlLlegar(new ParametrosNavegacion { { NotificacionBuzon.PARAMETRO_AUTORIZACION_PEDIDO_ID, 5 } });
            await Task.Yield();

            Assert.IsTrue(vm.VerResueltas);
            Assert.AreEqual(5, vm.Seleccionada?.Id);
            StringAssert.Contains(notificaciones.Single(), "No entra en la promoción");
        }
    }
}
