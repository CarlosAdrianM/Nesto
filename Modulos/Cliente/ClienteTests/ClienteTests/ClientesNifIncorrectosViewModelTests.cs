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

        private static ClienteNifIncorrectoModel Fila(string cliente = "30676", string nif = "90021192")
            => new ClienteNifIncorrectoModel { Cliente = cliente, Nombre = "ANA ISABEL", Nif = nif };

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
            A.CallTo(() => servicio.MarcarIdentificacionExtranjera("30676", "03", "MA"))
                .Returns(new ResultadoCorreccionNifModel { Corregido = true, Motivo = "Identificación marcada como extranjera" });
            var vm = CrearViewModel();
            vm.ClienteSeleccionado = Fila();
            vm.TipoIdentificacionSeleccionado = vm.TiposIdentificacion.First(t => t.Codigo == "03");
            vm.PaisIdentificacion = "ma"; // se normaliza a mayúsculas

            await vm.MarcarExtranjeroAsync();

            A.CallTo(() => servicio.MarcarIdentificacionExtranjera("30676", "03", "MA")).MustHaveHappenedOnceExactly();
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

            A.CallTo(() => servicio.MarcarIdentificacionExtranjera(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .MustNotHaveHappened();
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
