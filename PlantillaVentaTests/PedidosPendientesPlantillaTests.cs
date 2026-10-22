using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    /// </summary>
    [TestClass]
    public class PedidosPendientesPlantillaTests
    {
        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IPedidoVentaService _servicioPedidos;
        private ParametrosDialogo _parametrosDialogo;
        private int _avisos;

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _servicioPedidos = A.Fake<IPedidoVentaService>();
            _parametrosDialogo = null;
            _avisos = 0;
            A.CallTo(() => _dialogos.ShowNotification("Pendientes", A<string>.Ignored)).Invokes(() => _avisos++);
            // Sin pedido: CargarPedidoParaModificarAsync avisa y no sigue (la prueba no depende de cargar el borrador)
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).Returns(Task.FromResult<PedidoParaPlantillaModel>(null));
            A.CallTo(() => _servicioPedidos.cargarPedido(A<string>._, A<int>._))
                .ReturnsLazily((string empresa, int numero) => Task.FromResult(new PedidoVentaDTO { empresa = empresa, numero = numero, fecha = new DateTime(2026, 10, 21) }));
        }

        private PlantillaVentaViewModel CrearViewModel(params int[] pendientes)
        {
            A.CallTo(() => _servicio.CargarListaPendientes("1", "15191")).Returns(Task.FromResult(new List<int>(pendientes)));
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

        [TestMethod]
        public async Task UnPendiente_Modificar_CargaEsePedidoEnLaPlantilla()
        {
            var vm = CrearViewModel(929105);
            DialogoElige(929105);

            await vm.ComprobarPendientesAsync();

            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", 929105)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, _avisos);
            string mensaje = _parametrosDialogo.GetValue<string>(ElegirPedidoPendienteDialogViewModel.PARAMETRO_MENSAJE);
            StringAssert.Contains(mensaje, "Este cliente tiene un pedido pendiente: 929105 (");
            StringAssert.Contains(mensaje, "del 21/10/2026)");
            StringAssert.Contains(mensaje, "modifíquelo: así el cheque, los portes y Ganavisiones cuentan todo el pedido.");
            var opciones = Opciones();
            Assert.AreEqual(2, opciones.Count);
            Assert.AreEqual("Modificar el pedido 929105", opciones[0].Texto);
            Assert.AreEqual("Hacer un pedido nuevo", opciones[1].Texto);
        }

        [TestMethod]
        public async Task UnPendiente_PedidoNuevo_NoCargaNingunPedido()
        {
            var vm = CrearViewModel(929105);
            DialogoElige(0);

            await vm.ComprobarPendientesAsync();

            A.CallTo(() => _servicio.CargarPedidoParaPlantilla(A<string>._, A<int>._)).MustNotHaveHappened();
            Assert.IsNull(vm.NumeroPedidoEnEdicion);
            Assert.AreEqual(0, _avisos);
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

            Assert.AreEqual(1, _avisos);
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

            Assert.AreEqual(1, _avisos);
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
            StringAssert.StartsWith(opciones[0].Texto, "Modificar el pedido 929105 (");
            StringAssert.EndsWith(opciones[0].Texto, ", del 21/10/2026)");
            StringAssert.StartsWith(opciones[1].Texto, "Modificar el pedido 929200 (");
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

            Assert.AreEqual(0, _avisos);
            A.CallTo(() => _dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
        }
    }
}
