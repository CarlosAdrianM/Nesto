using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#417: clientes con NIF incorrecto para Verifactu. El VM resuelve el filtro por
    /// rol (administración/dirección ven todo; el resto, su vendedor) y delega la corrección
    /// en el servidor (revalida AEAT + propaga a contactos y facturas sin declarar).
    /// </summary>
    [TestClass]
    public class ClientesNifIncorrectosViewModelTests
    {
        private readonly INifIncorrectosService servicio;
        private readonly IConfiguracion configuracion;
        private readonly IDialogService dialogService;
        private bool respuestaConfirmacion = true;

        public ClientesNifIncorrectosViewModelTests()
        {
            servicio = A.Fake<INifIncorrectosService>();
            configuracion = A.Fake<IConfiguracion>();
            dialogService = A.Fake<IDialogService>();
            // ShowConfirmationAnswer/ShowError/ShowNotification son extensiones sobre
            // ShowDialog: se interceptan aquí (patrón ExtractoClienteViewModelTests).
            A.CallTo(() => dialogService.ShowDialog(
                    A<string>.Ignored, A<IDialogParameters>.Ignored, A<Action<IDialogResult>>.Ignored))
                .Invokes((string nombre, IDialogParameters parametros, Action<IDialogResult> callback) =>
                {
                    if (callback == null)
                    {
                        return;
                    }
                    IDialogResult resultado = A.Fake<IDialogResult>();
                    A.CallTo(() => resultado.Result)
                        .Returns(respuestaConfirmacion ? ButtonResult.OK : ButtonResult.Cancel);
                    callback(resultado);
                });
        }

        private ClientesNifIncorrectosViewModel CrearViewModel()
            => new ClientesNifIncorrectosViewModel(servicio, configuracion, dialogService);

        private static ClienteNifIncorrectoModel Fila(string cliente = "30676", string nif = "90021192",
            string paisSugerido = null)
            => new ClienteNifIncorrectoModel { Cliente = cliente, Nombre = "ANA ISABEL", Nif = nif, PaisIntracomunitarioSugerido = paisSugerido };

        // NestoAPI#354: la sugerencia de NIF-IVA intracomunitario preselecciona tipo 02 + país
        // para que "Marcar como extranjero" sea un clic. La decisión sigue siendo humana.

        [TestMethod]
        public void SeleccionarClienteConSugerencia_PreseleccionaTipo02YPais()
        {
            var vm = CrearViewModel();

            vm.ClienteSeleccionado = Fila(cliente: "41777", nif: "IT0280027", paisSugerido: "IT");

            Assert.AreEqual("02", vm.TipoIdentificacionSeleccionado?.Codigo);
            Assert.AreEqual("IT", vm.PaisIdentificacion);
            Assert.IsTrue(vm.MarcarExtranjeroCommand.CanExecute(), "Con la preselección, marcar es un clic");
        }

        [TestMethod]
        public void SeleccionarClienteSinSugerencia_LimpiaTipoYPais()
        {
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila(cliente: "41777", nif: "IT0280027", paisSugerido: "IT");

            vm.ClienteSeleccionado = Fila(cliente: "30676", nif: "90021192");

            Assert.IsNull(vm.TipoIdentificacionSeleccionado, "No debe arrastrar el tipo de la fila anterior");
            Assert.AreEqual(string.Empty, vm.PaisIdentificacion, "No debe arrastrar el país de la fila anterior");
            Assert.IsFalse(vm.MarcarExtranjeroCommand.CanExecute());
        }

        [TestMethod]
        public async Task Cargar_Administracion_VeTodosLosClientes()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            A.CallTo(() => servicio.LeerNifIncorrectos(null))
                .Returns(new List<ClienteNifIncorrectoModel> { Fila(), Fila("37980", "59526599Y") });
            var vm = CrearViewModel();

            await vm.CargarAsync();

            Assert.AreEqual(2, vm.Clientes.Count);
            A.CallTo(() => servicio.LeerNifIncorrectos(null)).MustHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_VendedorSinGrupoPrivilegiado_FiltraPorSuVendedor()
        {
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.Vendedor))
                .Returns(Task.FromResult("DV "));
            A.CallTo(() => servicio.LeerNifIncorrectos("DV"))
                .Returns(new List<ClienteNifIncorrectoModel> { Fila() });
            var vm = CrearViewModel();

            await vm.CargarAsync();

            Assert.AreEqual(1, vm.Clientes.Count);
            A.CallTo(() => servicio.LeerNifIncorrectos("DV")).MustHaveHappened();
            A.CallTo(() => servicio.LeerNifIncorrectos(null)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_SinGrupoNiVendedor_NoVeNada()
        {
            // Un usuario sin grupo privilegiado y sin vendedor asociado no puede ver la
            // cartera entera de NIF incorrectos.
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.Vendedor))
                .Returns(Task.FromResult(string.Empty));
            var vm = CrearViewModel();

            await vm.CargarAsync();

            Assert.AreEqual(0, vm.Clientes.Count);
            A.CallTo(() => servicio.LeerNifIncorrectos(A<string>.Ignored)).MustNotHaveHappened();
        }

        // NestoAPI#391: "Marcar como no censado" — un clic para el error humano sin NIF real
        // alcanzable: 07 + ES por debajo, reutilizando el circuito de la marca extranjera.

        [TestMethod]
        public async Task MarcarNoCensado_ConExito_LlamaConTipo07EspanaYRefrescaLaLista()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            A.CallTo(() => servicio.MarcarIdentificacionExtranjera("9093", "07", "ES", null))
                .Returns(new ResultadoCorreccionNifModel { Corregido = true, Motivo = "Cliente marcado como NO CENSADO" });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila(cliente: "9093", nif: "1000000");

            await vm.MarcarNoCensadoAsync();

            A.CallTo(() => servicio.MarcarIdentificacionExtranjera("9093", "07", "ES", null))
                .MustHaveHappenedOnceExactly();
            // Refresca para que el cliente desaparezca de la lista
            A.CallTo(() => servicio.LeerNifIncorrectos(null)).MustHaveHappenedTwiceOrMore();
        }

        [TestMethod]
        public async Task MarcarNoCensado_SiElUsuarioCancela_NoLlamaAlServicio()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            respuestaConfirmacion = false;
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila(cliente: "9093", nif: "1000000");

            await vm.MarcarNoCensadoAsync();

            A.CallTo(() => servicio.MarcarIdentificacionExtranjera(
                A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .MustNotHaveHappened();
        }

        [TestMethod]
        public void MarcarNoCensado_SinClienteSeleccionado_EstaDeshabilitado()
        {
            var vm = CrearViewModel();

            Assert.IsFalse(vm.MarcarNoCensadoCommand.CanExecute());

            vm.ClienteSeleccionado = Fila();
            Assert.IsTrue(vm.MarcarNoCensadoCommand.CanExecute());
        }

        [TestMethod]
        public async Task Corregir_ConExito_NotificaYRefrescaLaLista()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            A.CallTo(() => servicio.CorregirNif("30676", "90021192C"))
                .Returns(new ResultadoCorreccionNifModel
                {
                    Corregido = true,
                    Nif = "90021192C",
                    NombreAeat = "CUADRADO RODRIGUEZ ANA ISABEL",
                    ContactosActualizados = 2,
                    FacturasActualizadas = 1
                });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila();
            vm.NifNuevo = "90021192c"; // se normaliza a mayúsculas

            await vm.CorregirAsync();

            A.CallTo(() => servicio.CorregirNif("30676", "90021192C")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(string.Empty, vm.NifNuevo, "Tras corregir se limpia el campo");
            // La recarga tras corregir: al menos dos cargas (la inicial del ctor + el refresco)
            A.CallTo(() => servicio.LeerNifIncorrectos(null)).MustHaveHappenedTwiceOrMore();
        }

        [TestMethod]
        public async Task Corregir_SiElUsuarioCancela_NoLlamaAlServicio()
        {
            respuestaConfirmacion = false;
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila();
            vm.NifNuevo = "90021192C";

            await vm.CorregirAsync();

            A.CallTo(() => servicio.CorregirNif(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task MarcarExtranjero_ConTipoYPais_LlamaAlServicioYRefresca()
        {
            // NestoAPI#339: un pasaporte no se "corrige" — se marca como identificación
            // extranjera (tipo L7 + país) y sale de la lista.
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            A.CallTo(() => servicio.MarcarIdentificacionExtranjera("30676", "03", "MA", A<string>.Ignored))
                .Returns(new ResultadoCorreccionNifModel { Corregido = true, Motivo = "Identificación marcada como extranjera" });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila();
            vm.TipoIdentificacionSeleccionado = vm.TiposIdentificacion.First(t => t.Codigo == "03");
            vm.PaisIdentificacion = "ma"; // se normaliza a mayúsculas

            await vm.MarcarExtranjeroAsync();

            A.CallTo(() => servicio.MarcarIdentificacionExtranjera("30676", "03", "MA", A<string>.Ignored)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(string.Empty, vm.PaisIdentificacion, "Tras marcar se limpia el país");
            A.CallTo(() => servicio.LeerNifIncorrectos(null)).MustHaveHappenedTwiceOrMore();
        }

        [TestMethod]
        public async Task MarcarExtranjero_SinPais_NoLlamaAlServicio()
        {
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila();
            vm.TipoIdentificacionSeleccionado = vm.TiposIdentificacion.First();
            vm.PaisIdentificacion = "";

            await vm.MarcarExtranjeroAsync();

            A.CallTo(() => servicio.MarcarIdentificacionExtranjera(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .MustNotHaveHappened();
        }

        // Los dos botones son excluyentes: 'Corregir NIF' (español, valida AEAT) se deshabilita
        // en cuanto se indica país (extranjero → 'Marcar como extranjero').

        [TestMethod]
        public void CorregirNif_ConPaisIndicado_SeDeshabilita()
        {
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila();
            vm.NifNuevo = "IT01579720287";
            vm.PaisIdentificacion = "IT";

            Assert.IsFalse(vm.CorregirCommand.CanExecute(),
                "Con país indicado el cliente es extranjero: 'Corregir NIF' no aplica");
            Assert.IsFalse(vm.EsClienteEspanol);
        }

        [TestMethod]
        public void CorregirNif_SinPais_SeHabilitaConNif()
        {
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila();
            vm.NifNuevo = "90021192C";

            Assert.IsTrue(vm.CorregirCommand.CanExecute(), "Sin país (español) y con NIF, 'Corregir NIF' se habilita");
            Assert.IsTrue(vm.EsClienteEspanol);
        }

        [TestMethod]
        public async Task Corregir_SiLaAeatLoRechaza_MuestraElMotivoYNoRompe()
        {
            A.CallTo(() => servicio.CorregirNif(A<string>.Ignored, A<string>.Ignored))
                .Throws(new Exception("La AEAT no reconoce el NIF 99999999R para 'ANA ISABEL'. No se ha modificado nada."));
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila();
            vm.NifNuevo = "99999999R";

            await vm.CorregirAsync(); // no debe lanzar

            Assert.IsFalse(vm.EstaOcupado);
        }
    }
}
