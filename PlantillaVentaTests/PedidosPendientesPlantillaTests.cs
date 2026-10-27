using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Novedad 735: al elegir un cliente con pedidos pendientes y la plantilla vacía, la plantilla pregunta si se quiere
    /// modificar el pedido pendiente (el mismo camino que «Modificar con plantilla», para que el cheque, los portes y
    /// Ganavisiones cuenten todo el pedido) o hacer uno nuevo. Con líneas o ya en edición, el aviso de siempre.
    /// Nesto#554 (sugerencia 775): las notas de entrega no se pueden modificar desde la plantilla, así que no se ofrecen.
    /// Nesto#550 (sugerencia 759): los pendientes llegan ya descritos (fecha, líneas, base, entrega) de la API.
    /// </summary>
    [TestClass]
    public class PedidosPendientesPlantillaTests
    {
        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IPedidoVentaService _servicioPedidos;
        private ParametrosDialogo _parametrosDialogo;
        private readonly List<string> _avisos = new List<string>();

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _servicioPedidos = A.Fake<IPedidoVentaService>();
            _parametrosDialogo = null;
            _avisos.Clear();
            A.CallTo(() => _dialogos.ShowNotification("Pendientes", A<string>.Ignored)).Invokes((string titulo, string texto) => _avisos.Add(texto));
            // Sin pedido: CargarPedidoParaModificarAsync avisa y no sigue (la prueba no depende de cargar el borrador)
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).Returns(Task.FromResult<PedidoParaPlantillaModel>(null));
        }

        /// <summary>Un pedido normal pendiente, del 21/10/2026, con 2 líneas y 175,00 € de base.</summary>
        private static PedidoPendientePlantilla Pendiente(int numero, bool notaEntrega = false) => new PedidoPendientePlantilla
        {
            Numero = numero,
            Fecha = new DateTime(2026, 10, 21),
            NumeroLineas = 2,
            BaseImponible = 175M,
            FechaEntrega = new DateTime(2026, 10, 22),
            NotaEntrega = notaEntrega
        };

        private PlantillaVentaViewModel CrearViewModel(params int[] pendientes) =>
            CrearViewModelCon(pendientes.Select(n => Pendiente(n)).ToArray());

        private PlantillaVentaViewModel CrearViewModelCon(params PedidoPendientePlantilla[] pendientes)
        {
            // Como el servicio de verdad: los pendientes de la API y «Pedido nuevo» (0) al final
            var lista = new List<PedidoPendientePlantilla>(pendientes) { PedidoPendientePlantilla.PedidoNuevo() };
            A.CallTo(() => _servicio.CargarListaPendientes("1", "15191")).Returns(Task.FromResult(lista));
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion,
                _servicio, new WeakReferenceMessenger(), _dialogos, _servicioPedidos,
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            return vm;
        }

        /// <summary>El diálogo contesta con el pedido elegido (0 = pedido nuevo; null = se cierra la ventana).</summary>
        private void DialogoElige(int? pedido)
        {
            A.CallTo(() => _dialogos.ShowDialogAsync(ElegirPedidoPendienteDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .ReturnsLazily((string nombre, ParametrosDialogo parametros) =>
                {
                    _parametrosDialogo = parametros;
                    if (!pedido.HasValue)
                    {
                        return Task.FromResult(new ResultadoDialogo(ResultadoBoton.Cancel));
                    }
                    var dialogo = new ElegirPedidoPendienteDialogViewModel();
                    dialogo.OnDialogOpened(parametros);
                    ResultadoDialogo resultado = null;
                    dialogo.RequestClose += r => resultado = r;
                    dialogo.ElegirCommand.Execute(dialogo.Opciones.Find(o => o.Numero == pedido.Value));
                    return Task.FromResult(resultado);
                });
        }

        private List<OpcionPedidoPendiente> Opciones() =>
            _parametrosDialogo.GetValue<List<OpcionPedidoPendiente>>(ElegirPedidoPendienteDialogViewModel.PARAMETRO_OPCIONES);

        private string Mensaje() => _parametrosDialogo.GetValue<string>(ElegirPedidoPendienteDialogViewModel.PARAMETRO_MENSAJE);

        [TestMethod]
        public async Task UnPendiente_Modificar_CargaEsePedidoEnLaPlantilla()
        {
            var vm = CrearViewModel(929105);
            DialogoElige(929105);

            await vm.ComprobarPendientesAsync();

            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", 929105)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, _avisos.Count);
            string mensaje = Mensaje();
            StringAssert.Contains(mensaje, "Este cliente tiene un pedido pendiente: 929105 · del 21/10/2026 · 2 líneas · 175,00 € · entrega 22/10/2026.");
            StringAssert.Contains(mensaje, "modifíquelo: así el cheque, los portes y Ganavisiones cuentan todo el pedido.");
            var opciones = Opciones();
            Assert.AreEqual(2, opciones.Count);
            Assert.AreEqual("Modificar el pedido 929105", opciones[0].Texto);
            Assert.AreEqual("Hacer un pedido nuevo", opciones[1].Texto);
            // Nesto#550: ya no hace falta leer cada pedido para describirlo; la API lo manda descrito
            A.CallTo(() => _servicioPedidos.cargarPedido(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task UnPendiente_PedidoNuevo_NoCargaNingunPedido()
        {
            var vm = CrearViewModel(929105);
            DialogoElige(0);

            await vm.ComprobarPendientesAsync();

            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsNull(vm.NumeroPedidoEnEdicion);
            Assert.AreEqual(0, _avisos.Count);
        }

        [TestMethod]
        public async Task UnPendiente_CerrarLaVentana_EsComoPedidoNuevo()
        {
            var vm = CrearViewModel(929105);
            DialogoElige(null);

            await vm.ComprobarPendientesAsync();

            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task PlantillaConLineas_AvisoDeSiempreSinPreguntar()
        {
            var vm = CrearViewModel(929105);
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "17404", cantidad = 1, precio = 10M });
            DialogoElige(929105);

            await vm.ComprobarPendientesAsync();

            Assert.AreEqual(1, _avisos.Count);
            StringAssert.Contains(_avisos[0], "929105 · del 21/10/2026 · 2 líneas · 175,00 € · entrega 22/10/2026");
            A.CallTo(() => _dialogos.ShowDialogAsync(ElegirPedidoPendienteDialogViewModel.NOMBRE, A<ParametrosDialogo>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task YaEnEdicion_AvisoDeSiempreSinPreguntar()
        {
            var vm = CrearViewModel(929105);
            vm.NumeroPedidoEnEdicion = 929105;
            DialogoElige(929105);

            await vm.ComprobarPendientesAsync();

            Assert.AreEqual(1, _avisos.Count);
            A.CallTo(() => _dialogos.ShowDialogAsync(ElegirPedidoPendienteDialogViewModel.NOMBRE, A<ParametrosDialogo>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task VariosPendientes_UnBotonPorPedido_YCargaElElegido()
        {
            var vm = CrearViewModel(929105, 929200);
            DialogoElige(929200);

            await vm.ComprobarPendientesAsync();

            var opciones = Opciones();
            Assert.AreEqual(3, opciones.Count);
            Assert.AreEqual("Modificar el pedido 929105 · del 21/10/2026 · 2 líneas · 175,00 € · entrega 22/10/2026", opciones[0].Texto);
            Assert.AreEqual("Modificar el pedido 929200 · del 21/10/2026 · 2 líneas · 175,00 € · entrega 22/10/2026", opciones[1].Texto);
            Assert.AreEqual("Hacer un pedido nuevo", opciones[2].Texto);
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", 929200)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", 929105)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SinPendientes_NiPreguntaNiAvisa()
        {
            var vm = CrearViewModel();
            DialogoElige(0);

            await vm.ComprobarPendientesAsync();

            Assert.AreEqual(0, _avisos.Count);
            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        #region Nesto#554: notas de entrega (sugerencia 775, Paloma, cliente 40535, pedido 928422)

        [TestMethod]
        public async Task SoloUnaNotaDeEntrega_AvisoInformativoSinOfrecerModificar()
        {
            var vm = CrearViewModelCon(Pendiente(928422, notaEntrega: true));
            DialogoElige(928422);

            await vm.ComprobarPendientesAsync();

            Assert.AreEqual(1, _avisos.Count);
            Assert.AreEqual("Este cliente tiene una nota de entrega pendiente (928422). Las notas de entrega no se pueden modificar desde la plantilla: se hará un pedido nuevo.", _avisos[0]);
            A.CallTo(() => _dialogos.ShowDialogAsync(ElegirPedidoPendienteDialogViewModel.NOMBRE, A<ParametrosDialogo>._)).MustNotHaveHappened();
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsNull(vm.NumeroPedidoEnEdicion);
        }

        [TestMethod]
        public async Task SoloNotasDeEntrega_Varias_AvisoEnPlural()
        {
            var vm = CrearViewModelCon(Pendiente(928422, notaEntrega: true), Pendiente(928500, notaEntrega: true));

            await vm.ComprobarPendientesAsync();

            Assert.AreEqual(1, _avisos.Count);
            Assert.AreEqual("Este cliente tiene 2 notas de entrega pendientes (928422, 928500). Las notas de entrega no se pueden modificar desde la plantilla: se hará un pedido nuevo.", _avisos[0]);
            A.CallTo(() => _dialogos.ShowDialogAsync(ElegirPedidoPendienteDialogViewModel.NOMBRE, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task PedidoNormalYNotaDeEntrega_SoloSeOfreceModificarElNormal()
        {
            var vm = CrearViewModelCon(Pendiente(928422, notaEntrega: true), Pendiente(929105));
            DialogoElige(929105);

            await vm.ComprobarPendientesAsync();

            Assert.AreEqual(0, _avisos.Count);
            var opciones = Opciones();
            Assert.AreEqual(2, opciones.Count, "Solo el pedido normal y «Hacer un pedido nuevo»; la nota de entrega no se ofrece");
            Assert.AreEqual(929105, opciones[0].Numero);
            Assert.AreEqual("Modificar el pedido 929105", opciones[0].Texto);
            Assert.AreEqual("Hacer un pedido nuevo", opciones[1].Texto);
            string mensaje = Mensaje();
            StringAssert.Contains(mensaje, "Este cliente tiene un pedido pendiente: 929105 ·");
            StringAssert.Contains(mensaje, "Tiene además una nota de entrega pendiente (928422), que no se puede modificar desde la plantilla.");
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", 929105)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task NotaDeEntrega_SigueEnElDesplegableAmpliarEnElPedido()
        {
            // Nesto#554 solo quita el botón «Modificar»; la lista de pendientes (y el desplegable del último paso) no cambia
            var vm = CrearViewModelCon(Pendiente(928422, notaEntrega: true));

            await vm.ComprobarPendientesAsync();

            Assert.IsTrue(vm.TienePedidosPendientes);
            CollectionAssert.AreEqual(new[] { 928422, 0 }, vm.ListaPedidosPendientes.Select(p => p.Numero).ToList());
            Assert.AreEqual("Pedido nuevo", vm.ListaPedidosPendientes.Last().Texto);
        }

        #endregion
    }
}
