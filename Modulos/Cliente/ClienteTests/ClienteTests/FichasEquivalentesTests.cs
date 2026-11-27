using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// NestoAPI#876 (27/11/26): fichas de cliente equivalentes en Nesto: la pestaña de la ficha (ver el grupo, enlazar, deshacer, abrir),
    /// la casilla «Incluir fichas equivalentes» y que el extracto con equivalentes no deje tocar las filas de otra ficha.
    /// </summary>
    [TestClass]
    public class FichasEquivalentesTests
    {
        private readonly IServicioClientesEquivalentes servicio = A.Fake<IServicioClientesEquivalentes>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
        private readonly List<string> errores = new List<string>();
        private readonly List<string> notificaciones = new List<string>();
        private readonly List<string> confirmaciones = new List<string>();
        private bool respuestaConfirmacion = true;

        public FichasEquivalentesTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>._)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>._)).Invokes((string t, string m) => notificaciones.Add(m));
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._))
                .ReturnsLazily((string t, string m) => { confirmaciones.Add(m); return respuestaConfirmacion; });
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._, A<bool>._))
                .ReturnsLazily((string t, string m, bool no) => { confirmaciones.Add(m); return respuestaConfirmacion; });
        }

        private static FichaEquivalenteModel Ficha(string cliente, bool consultada = false, string anterior = null, string motivo = null,
            string nombre = null, string nif = null)
            => new FichaEquivalenteModel
            {
                Cliente = cliente,
                EsLaConsultada = consultada,
                ClienteAnterior = anterior,
                Motivo = motivo,
                Nombre = nombre ?? "CLIENTE " + cliente,
                Nif = nif ?? "B" + cliente,
                Activa = true,
                Estado = 0
            };

        private static ClientesEquivalentesModel Grupo(params FichaEquivalenteModel[] fichas)
            => new ClientesEquivalentesModel { Empresa = "1", Cliente = fichas.FirstOrDefault()?.Cliente, Fichas = fichas.ToList() };

        private FichasEquivalentesViewModel CrearViewModel() => new FichasEquivalentesViewModel(servicio, dialogos, navegacion);

        // ------------------------------------------------------------------ reglas de pantalla

        [TestMethod]
        public void TextoEnlace_ConMotivoQuienCuandoYComentario()
        {
            var f = Ficha("26815", anterior: "8244", motivo: "Duplicado");
            f.Usuario = "Carlos";
            f.Fecha = new DateTime(2026, 11, 26);
            f.Comentario = "dada de alta dos veces";

            Assert.AreEqual("→ 8244 · Duplicado · Carlos, 26/11/2026 · dada de alta dos veces", ReglasPantallaEquivalentes.TextoEnlace(f));
            Assert.AreEqual(string.Empty, ReglasPantallaEquivalentes.TextoEnlace(Ficha("8244")));
            Assert.AreEqual("Cambio de forma fiscal", ReglasPantallaEquivalentes.TextoMotivo("CambioFiscal"));
        }

        [TestMethod]
        public void MotivoNoSePuedeEnlazar_SinOtraMismaOSinMotivo()
        {
            Assert.AreEqual(ReglasPantallaEquivalentes.FALTA_OTRA_FICHA, ReglasPantallaEquivalentes.MotivoNoSePuedeEnlazar("1", " ", "Duplicado"));
            Assert.AreEqual(ReglasPantallaEquivalentes.MISMA_FICHA, ReglasPantallaEquivalentes.MotivoNoSePuedeEnlazar("15191", "15191 ", "Duplicado"));
            Assert.AreEqual(ReglasPantallaEquivalentes.FALTA_MOTIVO, ReglasPantallaEquivalentes.MotivoNoSePuedeEnlazar("15191", "8244", null));
            Assert.IsNull(ReglasPantallaEquivalentes.MotivoNoSePuedeEnlazar("15191", "8244", "CambioFiscal"));
        }

        [TestMethod]
        public void TextoResultado_MensajeYAvisosDebajo()
        {
            string texto = ReglasPantallaEquivalentes.TextoResultado(new ResultadoEnlaceEquivalenteModel
            {
                Mensaje = "Enlazados: el 15191 tiene como anterior al 8244 (Duplicado).",
                Avisos = new List<string> { "Los NIF no coinciden.", " " }
            });

            Assert.AreEqual("Enlazados: el 15191 tiene como anterior al 8244 (Duplicado).\n\nAvisos:\n· Los NIF no coinciden.", texto);
        }

        [TestMethod]
        public void HayEquivalentes_YEsDeOtraFicha()
        {
            Assert.IsFalse(ReglasPantallaEquivalentes.HayEquivalentes(Grupo(Ficha("15191", true)), "15191"));
            Assert.IsTrue(ReglasPantallaEquivalentes.HayEquivalentes(Grupo(Ficha("15191", true), Ficha("8244")), "15191"));
            Assert.IsTrue(ReglasPantallaEquivalentes.EsDeOtraFicha("8244", "15191"));
            Assert.IsFalse(ReglasPantallaEquivalentes.EsDeOtraFicha("15191 ", "15191"));
            Assert.IsFalse(ReglasPantallaEquivalentes.EsDeOtraFicha(null, "15191"));
        }

        // ------------------------------------------------------------------ servicio

        [TestMethod]
        public void Servicio_Urls()
        {
            Assert.AreEqual("Clientes/1/15191/Equivalentes", ServicioClientesEquivalentes.UrlLeer(null, " 15191 "));
            Assert.AreEqual("Clientes/1/15191/Equivalentes/8244", ServicioClientesEquivalentes.UrlDeshacer("1", "15191", "8244 "));
            Assert.AreEqual("ExtractosCliente?cliente=15191&incluirEquivalentes=true", ExtractoClienteService.UrlPendiente("15191", true));
            Assert.AreEqual("ExtractosCliente?cliente=15191", ExtractoClienteService.UrlPendiente("15191", false));
        }

        [TestMethod]
        public void Servicio_Errores_ConElMensajeYElCodigoDeLaApi()
        {
            var conflicto = Assert.ThrowsException<ClientesEquivalentesApiException>(() =>
                ServicioClientesEquivalentes.Comprobar(HttpStatusCode.Conflict, "{\"Message\":\"Haría un ciclo.\",\"Codigo\":\"CICLO\"}"));
            Assert.AreEqual("Haría un ciclo.", conflicto.Message);
            Assert.AreEqual("CICLO", conflicto.Codigo);

            var prohibido = Assert.ThrowsException<ClientesEquivalentesApiException>(() =>
                ServicioClientesEquivalentes.Comprobar(HttpStatusCode.Forbidden, ""));
            Assert.AreEqual(ServicioClientesEquivalentes.MENSAJE_SIN_PERMISO, prohibido.Message);

            var sinTabla = Assert.ThrowsException<ClientesEquivalentesApiException>(() =>
                ServicioClientesEquivalentes.Comprobar(HttpStatusCode.ServiceUnavailable, "{\"Message\":\"Falta la tabla.\"}"));
            Assert.IsTrue(sinTabla.SinTabla);
            StringAssert.StartsWith(sinTabla.Message, ServicioClientesEquivalentes.MENSAJE_SIN_TABLA);

            ServicioClientesEquivalentes.Comprobar(HttpStatusCode.OK, "{}"); // no lanza
        }

        // ------------------------------------------------------------------ pestaña de la ficha

        [TestMethod]
        public async Task Cargar_EnsenaElGrupo()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true, "8244", "Duplicado"), Ficha("8244")));
            var vm = CrearViewModel();

            await vm.CargarAsync("1", "15191 ");

            Assert.IsTrue(vm.Disponible);
            Assert.IsTrue(vm.HayEquivalentes);
            Assert.AreEqual(2, vm.Fichas.Count);
            Assert.IsTrue(vm.Fichas[0].TieneEnlace);
            Assert.IsFalse(vm.Fichas[1].TieneEnlace);
        }

        [TestMethod]
        public async Task Cargar_Con503_OcultaTodo()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._)).Throws(new ClientesEquivalentesApiException(503, "sin tabla"));
            var vm = CrearViewModel();

            await vm.CargarAsync("1", "15191");

            Assert.IsFalse(vm.Disponible);
            Assert.IsFalse(errores.Any());
        }

        [TestMethod]
        public async Task Cargar_SinEquivalentes_LoDice()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true)));
            var vm = CrearViewModel();

            await vm.CargarAsync("1", "15191");

            Assert.IsFalse(vm.HayEquivalentes);
            Assert.AreEqual("Esta ficha no tiene ninguna ficha equivalente.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Enlazar_SinMotivo_NoLlamaALaApi()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true)));
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");
            vm.EmpezarEnlaceCommand.Execute(null);
            vm.OtraFicha = "8244";

            await vm.EnlazarAsync();

            CollectionAssert.AreEqual(new[] { ReglasPantallaEquivalentes.FALTA_MOTIVO }, errores);
            A.CallTo(() => servicio.Enlazar(A<PeticionEnlaceEquivalenteModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Enlazar_EnsenaNombreYNifDeLasDos_MandaLaQueQueda_YEnsenaMensajeYAvisos()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true, nombre: "PELUQUERÍA ANA", nif: "12345678Z")));
            A.CallTo(() => servicio.Leer("1", "8244")).Returns(Grupo(Ficha("8244", true, nombre: "ANA PÉREZ", nif: "12345678Z")));
            PeticionEnlaceEquivalenteModel enviada = null;
            A.CallTo(() => servicio.Enlazar(A<PeticionEnlaceEquivalenteModel>._))
                .Invokes((PeticionEnlaceEquivalenteModel p) => enviada = p)
                .Returns(new ResultadoEnlaceEquivalenteModel { Mensaje = "Enlazados.", Avisos = new List<string> { "El 8244 sigue de alta." } });
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");
            vm.EmpezarEnlaceCommand.Execute(null);
            vm.OtraFicha = " 8244 ";
            vm.MotivoDuplicado = true;
            vm.Comentario = "  mismo NIF  ";

            await vm.EnlazarAsync();

            string confirmacion = confirmaciones.Single();
            StringAssert.Contains(confirmacion, "Queda: 15191 · PELUQUERÍA ANA · NIF 12345678Z");
            StringAssert.Contains(confirmacion, "La otra: 8244 · ANA PÉREZ · NIF 12345678Z");
            Assert.AreEqual("15191", enviada.Cliente);
            Assert.AreEqual("8244", enviada.ClienteAnterior);
            Assert.AreEqual("Duplicado", enviada.Motivo);
            Assert.AreEqual("mismo NIF", enviada.Comentario);
            Assert.AreEqual("Enlazados.\n\nAvisos:\n· El 8244 sigue de alta.", notificaciones.Single());
            Assert.IsFalse(errores.Any(), "Los avisos no son errores");
            Assert.IsFalse(vm.MostrandoEnlace);
            A.CallTo(() => servicio.Leer("1", "15191")).MustHaveHappenedTwiceExactly(); // se relee el grupo
        }

        [TestMethod]
        public async Task Enlazar_QuedaLaOtra_LaOtraEsLaQueApunta()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true)));
            A.CallTo(() => servicio.Leer("1", "8244")).Returns(Grupo(Ficha("8244", true)));
            PeticionEnlaceEquivalenteModel enviada = null;
            A.CallTo(() => servicio.Enlazar(A<PeticionEnlaceEquivalenteModel>._)).Invokes((PeticionEnlaceEquivalenteModel p) => enviada = p)
                .Returns(new ResultadoEnlaceEquivalenteModel());
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");
            vm.OtraFicha = "8244";
            vm.MotivoCambioFiscal = true;
            vm.QuedaLaOtra = true;

            await vm.EnlazarAsync();

            Assert.AreEqual("8244", enviada.Cliente);
            Assert.AreEqual("15191", enviada.ClienteAnterior);
            Assert.AreEqual("CambioFiscal", enviada.Motivo);
            Assert.IsNull(enviada.Comentario);
        }

        [TestMethod]
        public async Task Enlazar_NoConfirma_NoLlama()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._)).Returns(Grupo(Ficha("15191", true)));
            respuestaConfirmacion = false;
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");
            vm.OtraFicha = "8244";
            vm.MotivoDuplicado = true;

            await vm.EnlazarAsync();

            A.CallTo(() => servicio.Enlazar(A<PeticionEnlaceEquivalenteModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Enlazar_409_EnsenaElMensajeDeLaApi()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._)).Returns(Grupo(Ficha("15191", true)));
            A.CallTo(() => servicio.Enlazar(A<PeticionEnlaceEquivalenteModel>._))
                .Throws(new ClientesEquivalentesApiException(409, "Enlazarlos haría un ciclo.", "CICLO"));
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");
            vm.OtraFicha = "8244";
            vm.MotivoDuplicado = true;

            await vm.EnlazarAsync();

            CollectionAssert.AreEqual(new[] { "Enlazarlos haría un ciclo." }, errores);
            Assert.IsTrue(vm.Disponible);
        }

        [TestMethod]
        public async Task Enlazar_OtraFichaNoExiste_404ConSuMensaje()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true)));
            A.CallTo(() => servicio.Leer("1", "99999")).Throws(new ClientesEquivalentesApiException(404, "No existe el cliente 99999 en la empresa 1."));
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");
            vm.OtraFicha = "99999";
            vm.MotivoDuplicado = true;

            await vm.EnlazarAsync();

            CollectionAssert.AreEqual(new[] { "No existe el cliente 99999 en la empresa 1." }, errores);
            Assert.IsFalse(confirmaciones.Any());
        }

        [TestMethod]
        public async Task Deshacer_ConConfirmacion_LlamaConElEnlaceDeLaFila()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true, "8244", "Duplicado"), Ficha("8244")));
            A.CallTo(() => servicio.Deshacer("1", "15191", "8244")).Returns(new ResultadoEnlaceEquivalenteModel { Mensaje = "Deshecho el enlace." });
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");

            await vm.DeshacerAsync(vm.Fichas[0]);

            A.CallTo(() => servicio.Deshacer("1", "15191", "8244")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Deshecho el enlace.", notificaciones.Single());
        }

        [TestMethod]
        public async Task Deshacer_SinConfirmar_NoLlama_Y403ConSuMensaje()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true, "8244", "Duplicado"), Ficha("8244")));
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");
            respuestaConfirmacion = false;

            await vm.DeshacerAsync(vm.Fichas[0]);
            A.CallTo(() => servicio.Deshacer(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();

            respuestaConfirmacion = true;
            A.CallTo(() => servicio.Deshacer(A<string>._, A<string>._, A<string>._))
                .Throws(new ClientesEquivalentesApiException(403, "Solo Administración, Dirección, Informática o el vendedor…"));
            await vm.DeshacerAsync(vm.Fichas[0]);
            CollectionAssert.AreEqual(new[] { "Solo Administración, Dirección, Informática o el vendedor…" }, errores);
        }

        [TestMethod]
        public async Task Abrir_NavegaALaFichaDeLaOtra_YNoALaConsultada()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true), Ficha("8244")));
            var vm = CrearViewModel();
            await vm.CargarAsync("1", "15191");
            ParametrosNavegacion parametros = null;
            A.CallTo(() => navegacion.RequestNavigate(A<string>._, A<string>._, A<ParametrosNavegacion>._))
                .Invokes((string r, string v, ParametrosNavegacion p) => parametros = p);

            Assert.IsFalse(vm.AbrirCommand.CanExecute(vm.Fichas[0]));
            vm.AbrirCommand.Execute(vm.Fichas[1]);

            A.CallTo(() => navegacion.RequestNavigate("MainRegion", "CrearClienteView", A<ParametrosNavegacion>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("8244", parametros.GetValue<string>("clienteParameter"));
            Assert.AreEqual("1", parametros.GetValue<string>("empresaParameter"));
        }

        // ------------------------------------------------------------------ casilla y extracto

        [TestMethod]
        public async Task Casilla_SoloVisibleConOtraFicha_YSeApagaAlCambiarDeCliente()
        {
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true), Ficha("8244")));
            A.CallTo(() => servicio.Leer("1", "20000")).Returns(Grupo(Ficha("20000", true)));
            var casilla = new CasillaFichasEquivalentes(servicio);
            int cambios = 0;
            casilla.IncluirCambiado += (s, e) => cambios++;

            casilla.Incluir = true; // sin comprobar, no se puede marcar
            Assert.IsFalse(casilla.Incluir);

            await casilla.ComprobarAsync("1", "15191");
            Assert.IsTrue(casilla.Visible);
            Assert.AreEqual("Incluir fichas equivalentes (8244)", casilla.Texto);
            casilla.Incluir = true;
            Assert.AreEqual(1, cambios);

            await casilla.ComprobarAsync("1", "20000");
            Assert.IsFalse(casilla.Visible);
            Assert.IsFalse(casilla.Incluir);
        }

        [TestMethod]
        public async Task Casilla_SiLaApiFalla_NoSeVe()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._)).Throws(new ClientesEquivalentesApiException(503, "sin tabla"));
            var casilla = new CasillaFichasEquivalentes(servicio);

            await casilla.ComprobarAsync("1", "15191");

            Assert.IsFalse(casilla.Visible);
        }

        [TestMethod]
        public async Task Extracto_ConEquivalentes_PideConLaCasilla_MarcaLasFilasDeOtraFicha_YNoDejaLiquidarlas()
        {
            var extracto = A.Fake<IExtractoClienteService>();
            A.CallTo(() => servicio.Leer("1", "15191")).Returns(Grupo(Ficha("15191", true), Ficha("8244")));
            A.CallTo(() => extracto.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                new ExtractoClienteModel { Id = 1, Empresa = "1", Cliente = "15191", ImportePendiente = 10 }
            });
            A.CallTo(() => extracto.LeerExtractoPendiente("15191", true)).Returns(new List<ExtractoClienteModel>
            {
                new ExtractoClienteModel { Id = 1, Empresa = "1", Cliente = "15191", ImportePendiente = 10 },
                new ExtractoClienteModel { Id = 2, Empresa = "1", Cliente = "8244", ImportePendiente = -10 }
            });
            var vm = new ExtractoClienteViewModel(extracto, dialogos, new WeakReferenceMessenger(), null, null, null, servicio)
            {
                ClienteSeleccionado = "15191"
            };

            await vm.CargarAsync();
            Assert.IsTrue(vm.Equivalentes.Visible);
            Assert.AreEqual(1, vm.Movimientos.Count);
            A.CallTo(() => extracto.LeerExtractoPendiente("15191", true)).MustNotHaveHappened();

            vm.Equivalentes.Incluir = true; // recarga
            await vm.CargarAsync();

            Assert.AreEqual(2, vm.Movimientos.Count);
            ExtractoClienteModel propio = vm.Movimientos.Single(m => m.Id == 1);
            ExtractoClienteModel otro = vm.Movimientos.Single(m => m.Id == 2);
            Assert.IsFalse(propio.EsDeOtraFicha);
            Assert.IsTrue(otro.EsDeOtraFicha);
            otro.Seleccionado = true;
            Assert.IsFalse(otro.Seleccionado, "Un movimiento de otra ficha no se marca para liquidar");
            propio.Seleccionado = true;
            Assert.IsFalse(vm.LiquidarCommand.CanExecute(null));
            vm.MovimientoSeleccionado = otro;
            Assert.AreEqual(ReglasPantallaEquivalentes.MOTIVO_SOLO_LECTURA, vm.AvisoFilaSeleccionada);
        }

        [TestMethod]
        public async Task Extracto_SinServicioDeEquivalentes_ComoSiempre()
        {
            var extracto = A.Fake<IExtractoClienteService>();
            A.CallTo(() => extracto.LeerExtractoPendiente("15191")).Returns(new List<ExtractoClienteModel>
            {
                new ExtractoClienteModel { Id = 1, Empresa = "1", Cliente = "15191", ImportePendiente = 10 },
                new ExtractoClienteModel { Id = 2, Empresa = "1", Cliente = "15191", ImportePendiente = -10 }
            });
            var vm = new ExtractoClienteViewModel(extracto, dialogos, new WeakReferenceMessenger()) { ClienteSeleccionado = "15191" };

            await vm.CargarAsync();
            foreach (ExtractoClienteModel m in vm.Movimientos)
            {
                m.Seleccionado = true;
            }

            Assert.IsFalse(vm.Equivalentes.Visible);
            Assert.IsTrue(vm.LiquidarCommand.CanExecute(null));
        }
    }
}
