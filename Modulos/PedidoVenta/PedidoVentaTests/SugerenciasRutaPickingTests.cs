using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Modulos.PedidoVenta;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Sugerencia 575 (Alfredo, almacén; decisión de Carlos 13/10/26): al sacar el picking, los pedidos que van por OnTime y
    /// que seguramente saldrán por nuestra ruta (AT/16) se enseñan con la ruta propuesta y el motivo; almacén marca los que
    /// quiere cambiar (vía API) y sigue con el picking. Nunca se cambia nada sin marcarlo.
    /// </summary>
    [TestClass]
    public class SugerenciasRutaPickingTests
    {
        private IPedidoVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IConfiguracion _configuracion;
        private IInformesService _informes;

        [TestInitialize]
        public void Initialize()
        {
            _servicio = A.Fake<IPedidoVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _configuracion = A.Fake<IConfiguracion>();
            _informes = A.Fake<IInformesService>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(true);
            // Sin kits que desmontar: el aviso de kits no pregunta
            A.CallTo(() => _informes.LeerKitsQueSePuedenMontar(A<string>.Ignored, A<string>.Ignored, A<string>.Ignored, A<string>.Ignored))
                .Returns(Task.FromResult(new List<Nesto.Informes.KitsQueSePuedenMontarModel>()));
        }

        private static SugerenciaRutaPicking Sugerencia(int pedido, string propuesta = "16", string empresa = "3") => new()
        {
            Empresa = empresa,
            Pedido = pedido,
            Cliente = "5825",
            Nombre = "PELUQUERÍA PRUEBA",
            CodigoPostal = "28100",
            Poblacion = "ALCOBENDAS",
            RutaActual = "FW",
            RutaPropuesta = propuesta,
            Motivos = new List<string> { "Contado (empresa 3) con código postal de Madrid" }
        };

        private PickingPopupViewModel PickingDeRutas(params SugerenciaRutaPicking[] sugerencias)
        {
            A.CallTo(() => _servicio.LeerSugerenciasRutaPicking(A<string>.Ignored, A<int>.Ignored, A<string>.Ignored))
                .ReturnsLazily(() => Task.FromResult(sugerencias.ToList()));
            var vm = new PickingPopupViewModel(_servicio, new WeakReferenceMessenger(), _dialogos, _configuracion, _informes)
            {
                esPickingPedido = false,
                esPickingRutas = true
            };
            return vm;
        }

        private void ElDialogoDevuelve(ResultadoBoton boton, Func<List<SugerenciaRutaPicking>, List<SugerenciaRutaPicking>> marcar = null)
        {
            A.CallTo(() => _dialogos.ShowDialogAsync(SugerenciasRutaPickingPopupViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .ReturnsLazily((string _, ParametrosDialogo p) =>
                {
                    var lista = p.GetValue<List<SugerenciaRutaPicking>>("sugerencias");
                    var parametros = new ParametrosDialogo();
                    if (marcar != null)
                    {
                        parametros.Add("cambios", marcar(lista));
                    }
                    return Task.FromResult(new ResultadoDialogo(boton, parametros));
                });
        }

        // ===== PickingPopupViewModel

        [TestMethod]
        public async Task SacarPicking_SinSugerencias_NoAbreElAvisoYSacaElPicking()
        {
            var vm = PickingDeRutas();

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>.Ignored, A<ParametrosDialogo>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => _servicio.sacarPickingPedido()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SacarPicking_ConSugerencias_MarcaYCambiaLaRutaAntesDelPicking()
        {
            var vm = PickingDeRutas(Sugerencia(928241, "16"), Sugerencia(928038, "AT"));
            ElDialogoDevuelve(ResultadoBoton.OK, lista => lista.Where(s => s.Pedido == 928241).ToList());
            var orden = new List<string>();
            A.CallTo(() => _servicio.CambiarRutaPedidos(A<IEnumerable<SugerenciaRutaPicking>>.Ignored))
                .Invokes(() => orden.Add("ruta"))
                .Returns(Task.FromResult(new List<ResultadoCambioRutaPicking> { new() { Empresa = "3", Pedido = 928241, RutaNueva = "16", Cambiado = true } }));
            A.CallTo(() => _servicio.sacarPickingPedido()).Invokes(() => orden.Add("picking"));

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _servicio.CambiarRutaPedidos(A<IEnumerable<SugerenciaRutaPicking>>.That.Matches(c =>
                c.Count() == 1 && c.Single().Pedido == 928241 && c.Single().RutaNueva == "16"))).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new[] { "ruta", "picking" }, orden, "La ruta se cambia ANTES de sacar el picking");
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SacarPicking_SacarSinCambiar_NoTocaRutasYSacaElPicking()
        {
            var vm = PickingDeRutas(Sugerencia(928241));
            ElDialogoDevuelve(ResultadoBoton.No);

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _servicio.CambiarRutaPedidos(A<IEnumerable<SugerenciaRutaPicking>>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => _servicio.sacarPickingPedido()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SacarPicking_Cancelar_NoSacaElPicking()
        {
            var vm = PickingDeRutas(Sugerencia(928241));
            ElDialogoDevuelve(ResultadoBoton.Cancel);

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _servicio.CambiarRutaPedidos(A<IEnumerable<SugerenciaRutaPicking>>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => _servicio.sacarPickingPedido()).MustNotHaveHappened();
            Assert.IsFalse(vm.estaSacandoPicking);
        }

        [TestMethod]
        public async Task SacarPicking_SiUnCambioFalla_LoDiceYPreguntaSiSacarElPickingIgualmente()
        {
            var vm = PickingDeRutas(Sugerencia(928241), Sugerencia(928038, "AT"));
            ElDialogoDevuelve(ResultadoBoton.OK, lista => lista);
            A.CallTo(() => _servicio.CambiarRutaPedidos(A<IEnumerable<SugerenciaRutaPicking>>.Ignored))
                .Returns(Task.FromResult(new List<ResultadoCambioRutaPicking>
                {
                    new() { Empresa = "3", Pedido = 928241, Cambiado = true },
                    new() { Empresa = "3", Pedido = 928038, Cambiado = false, Error = "El pedido 928038 ya no va por la FW (o no existe): no se ha tocado." }
                }));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(false);

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Cambio de ruta", A<string>.That.Contains("928038: El pedido 928038 ya no va por la FW")))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.sacarPickingPedido()).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SacarPicking_SiLaApiDelCambioFalla_PreguntaYSiConfirma_SacaElPicking()
        {
            var vm = PickingDeRutas(Sugerencia(928241));
            ElDialogoDevuelve(ResultadoBoton.OK, lista => lista);
            A.CallTo(() => _servicio.CambiarRutaPedidos(A<IEnumerable<SugerenciaRutaPicking>>.Ignored)).ThrowsAsync(new Exception("API caída"));
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(true);

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _dialogos.ShowConfirmationAnswer("Cambio de ruta", A<string>.That.Contains("API caída"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.sacarPickingPedido()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SacarPicking_SiFallaLaLecturaDeSugerencias_SacaElPickingSinPreguntar()
        {
            var vm = PickingDeRutas();
            A.CallTo(() => _servicio.LeerSugerenciasRutaPicking(A<string>.Ignored, A<int>.Ignored, A<string>.Ignored)).ThrowsAsync(new Exception("404"));

            await vm.SacarPickingAsync(null);

            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>.Ignored, A<ParametrosDialogo>.Ignored)).MustNotHaveHappened();
            A.CallTo(() => _servicio.sacarPickingPedido()).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SacarPicking_LosYaRevisadosEnEstaVentana_NoSeVuelvenAPreguntar()
        {
            var vm = PickingDeRutas(Sugerencia(928241));
            ElDialogoDevuelve(ResultadoBoton.No);

            await vm.SacarPickingAsync(null);
            await vm.SacarPickingAsync(null);

            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>.Ignored, A<ParametrosDialogo>.Ignored)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.sacarPickingPedido()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task SacarPicking_ConCancelar_SeVuelveAPreguntarLaProximaVez()
        {
            var vm = PickingDeRutas(Sugerencia(928241));
            ElDialogoDevuelve(ResultadoBoton.Cancel);

            await vm.SacarPickingAsync(null);
            await vm.SacarPickingAsync(null);

            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>.Ignored, A<ParametrosDialogo>.Ignored)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Sugerencias_ConElAlcanceDelPicking_PedidoClienteORutas()
        {
            var vm = PickingDeRutas();
            vm.pedidoPicking = new Nesto.Models.PedidoVentaDTO { empresa = "3", numero = 928241 };
            vm.numeroPedidoPicking = 928241;
            vm.numeroClientePicking = "5825";

            vm.esPickingRutas = false;
            vm.esPickingPedido = true;
            await vm.SugerenciasRutaAsync();
            vm.esPickingPedido = false;
            vm.esPickingCliente = true;
            await vm.SugerenciasRutaAsync();
            vm.esPickingCliente = false;
            vm.esPickingRutas = true;
            await vm.SugerenciasRutaAsync();

            A.CallTo(() => _servicio.LeerSugerenciasRutaPicking("3", 928241, null)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerSugerenciasRutaPicking(null, 0, "5825")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.LeerSugerenciasRutaPicking(null, 0, null)).MustHaveHappenedOnceExactly();
        }

        // ===== SugerenciasRutaPickingPopupViewModel

        private static SugerenciasRutaPickingPopupViewModel Popup(params SugerenciaRutaPicking[] sugerencias)
        {
            var vm = new SugerenciasRutaPickingPopupViewModel();
            vm.OnDialogOpened(new ParametrosDialogo { { "sugerencias", sugerencias.ToList() } });
            return vm;
        }

        [TestMethod]
        public void Popup_NadaMarcadoAlAbrir_YLaRutaNuevaEsLaPropuesta()
        {
            var vm = Popup(Sugerencia(928241, "16"), Sugerencia(928038, "AT"));

            Assert.AreEqual(2, vm.Sugerencias.Count);
            Assert.IsFalse(vm.Sugerencias.Any(s => s.Marcado), "Nunca se cambia nada sin marcarlo");
            Assert.IsFalse(vm.CambiarYSacarCommand.CanExecute(null));
            Assert.AreEqual("16", vm.Sugerencias[0].RutaNueva);
            Assert.AreEqual("AT", vm.Sugerencias[1].RutaNueva);
            StringAssert.Contains(vm.Cabecera, "Estos 2 pedidos podrían ir por nuestra ruta");
            CollectionAssert.AreEqual(new[] { "AT", "16" }, vm.RutasPropias.ToArray());
        }

        [TestMethod]
        public void Popup_AlMarcarUno_SeHabilitaCambiarYDevuelveSoloLosMarcadosConLaRutaElegida()
        {
            var vm = Popup(Sugerencia(928241, "16"), Sugerencia(928038, "AT"));
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.Sugerencias[1].Marcado = true;
            vm.Sugerencias[1].RutaNueva = "16";
            Assert.IsTrue(vm.CambiarYSacarCommand.CanExecute(null));
            vm.CambiarYSacarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            var cambios = resultado.Parameters.GetValue<List<SugerenciaRutaPicking>>("cambios");
            Assert.AreEqual(928038, cambios.Single().Pedido);
            Assert.AreEqual("16", cambios.Single().RutaNueva);
        }

        [TestMethod]
        public void Popup_SacarSinCambiarYCancelar()
        {
            var vm = Popup(Sugerencia(928241));
            var resultados = new List<ResultadoBoton>();
            vm.RequestClose += r => resultados.Add(r.Result);

            vm.SacarSinCambiarCommand.Execute(null);
            vm.CancelarCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { ResultadoBoton.No, ResultadoBoton.Cancel }, resultados);
        }

        [TestMethod]
        public void Popup_ConLaVentanaDePrism_RecibeLasSugerenciasYDevuelveLosCambios()
        {
            var vm = new SugerenciasRutaPickingPopupViewModel();
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters { { "sugerencias", new List<SugerenciaRutaPicking> { Sugerencia(928241) } } });
            vm.Sugerencias[0].Marcado = true;
            vm.CambiarYSacarCommand.Execute(null);

            Assert.AreEqual("Pedidos que podrían ir por nuestra ruta", prism.Title);
            Assert.AreEqual(ButtonResult.OK, resultado.Result);
            Assert.AreEqual(928241, resultado.Parameters.GetValue<List<SugerenciaRutaPicking>>("cambios").Single().Pedido);
        }

        [TestMethod]
        public void Sugerencia_TextosParaLaPantalla()
        {
            var s = Sugerencia(928241);
            s.Motivos.Add("Su último pedido salió por la 16");

            Assert.AreEqual("5825 PELUQUERÍA PRUEBA (28100 ALCOBENDAS)", s.ClienteTexto);
            Assert.AreEqual("Contado (empresa 3) con código postal de Madrid · Su último pedido salió por la 16", s.MotivosTexto);
        }
    }
}
