using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PedidoVenta.Services;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#640 (NestoAPI#864): la casilla «Ofrecer el pago a plazos al grabar el pedido» del paso Finalizar. Solo se pinta si el
    /// interruptor de la API está encendido para el cliente (apagado, sin fila o sin poder leerlo: nada). Marcada, con el pedido
    /// YA grabado se pregunta a la API por ese pedido: si se puede, se abre el mismo diálogo que en el detalle del pedido; si no,
    /// se dice el motivo. Solo al crear un pedido. Nunca impide ni cambia que el pedido se grabe, se cierre la plantilla y se abra.
    /// </summary>
    [TestClass]
    public class PagoAPlazosPlantillaTests
    {
        private IPedidoVentaService _pedidosVenta;
        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IServicioNavegacion _navegacion;
        private IAbridorPedidos _abridorPedidos;
        private IConfiguracion _configuracion;
        private IBorradorPlantillaVentaService _borradores;
        private IServicioPagoAPlazos _pagoAPlazos;

        [TestInitialize]
        public void Setup()
        {
            _pedidosVenta = A.Fake<IPedidoVentaService>();
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _navegacion = A.Fake<IServicioNavegacion>();
            _abridorPedidos = A.Fake<IAbridorPedidos>();
            _configuracion = A.Fake<IConfiguracion>();
            _borradores = A.Fake<IBorradorPlantillaVentaService>();
            _pagoAPlazos = A.Fake<IServicioPagoAPlazos>();
            A.CallTo(() => _configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            A.CallTo(() => _configuracion.leerParametro(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.PlantillaMantenerAbierta))
                .Returns(Task.FromResult("0"));
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._))
                .Returns(Task.FromResult(new RespuestaCrearPedidoPlantilla { Numero = "930001" }));
            A.CallTo(() => _servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._))
                .Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta { Avisar = false }));
            ElInterruptorVale("Activo");
            LaApiDice(Disponibilidad());
        }

        private void ElInterruptorVale(string modo, string clientesPrueba = null)
            => A.CallTo(() => _pagoAPlazos.LeerInterruptor())
                .Returns(Task.FromResult(new InterruptorPagoAPlazosModel { Modo = modo, ClientesPrueba = clientesPrueba }));

        private void LaApiDice(DisponibilidadPagoAPlazosModel disponibilidad)
            => A.CallTo(() => _pagoAPlazos.LeerDisponible(A<string>._, A<int>._)).Returns(Task.FromResult(disponibilidad));

        private static DisponibilidadPagoAPlazosModel Disponibilidad(bool disponible = true, string motivo = null, string proveedor = "Prueba")
            => new DisponibilidadPagoAPlazosModel
            {
                Disponible = disponible,
                Motivo = motivo,
                Proveedor = proveedor,
                NombreProveedor = proveedor == null ? null : "Financiera Prueba",
                Importe = 265.6m,
                Canal = "Enlace"
            };

        private PlantillaVentaViewModel ListoParaGrabar()
        {
            var vm = new PlantillaVentaViewModel(_abridorPedidos, _navegacion, _configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, _pedidosVenta, _borradores, A.Fake<IServicioAutenticacion>());
            vm.ServicioPagoAPlazos = _pagoAPlazos;
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "9238", contacto = "0", iva = "G21", cifNif = "12345678A" };
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "RCB" };
            typeof(PlantillaVentaViewModel).GetField("_plazoPagoSeleccionado", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PlazosPago { plazoPago = "1/30" });
            vm.Estado.PlazosPago = "1/30";
            vm.formaVentaSeleccionada = 1;
            vm.almacenSeleccionado = new Sede { Codigo = "ALG", Nombre = "Algete" };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "45383", texto = "TRIPLE GLOW", cantidad = 1, precio = 219.5M, grupo = "COS", iva = "G21" });
            return vm;
        }

        private async Task<PlantillaVentaViewModel> ConLaCasillaMarcada()
        {
            var vm = ListoParaGrabar();
            await vm.CargarOfrecerPagoAPlazosAsync();
            vm.OfrecerPagoAPlazos = true;
            Assert.IsTrue(vm.OfrecerPagoAPlazos);
            return vm;
        }

        private void ElPedidoSeGrabaSeCierraLaPlantillaYSeAbre()
        {
            Assert.AreEqual(1, Fake.GetCalls(_servicio).Count(c => c.Method.Name == nameof(IPlantillaVentaService.CrearPedido)));
            A.CallTo(() => _navegacion.CerrarVistaActiva("MainRegion")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _abridorPedidos.Abrir("1", 930001)).MustHaveHappenedOnceExactly();
        }

        private void NoSeAbreElDialogo()
            => A.CallTo(() => _dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();

        #region La casilla solo se pinta si la API lo tiene encendido

        [TestMethod]
        public void SinLeerElInterruptor_LaCasillaNoSePinta_NiSePuedeMarcar()
        {
            var vm = ListoParaGrabar();

            Assert.IsFalse(vm.MostrarOfrecerPagoAPlazos);
            vm.OfrecerPagoAPlazos = true;
            Assert.IsFalse(vm.OfrecerPagoAPlazos, "Sin pintarse no cuenta como marcada");
        }

        [DataTestMethod]
        [DataRow("Apagado")]
        [DataRow("")]
        [DataRow("0")]
        public async Task InterruptorApagado_LaCasillaNoSePinta(string modo)
        {
            ElInterruptorVale(modo);
            var vm = ListoParaGrabar();

            await vm.CargarOfrecerPagoAPlazosAsync();

            Assert.IsFalse(vm.MostrarOfrecerPagoAPlazos);
        }

        [TestMethod]
        public async Task ElInterruptorNoSePuedeLeer_NoLanza_YLaCasillaNoSePinta()
        {
            // Sin la fila (404), sin red o con la API sin publicar: como apagado
            A.CallTo(() => _pagoAPlazos.LeerInterruptor()).ThrowsAsync(new Exception("Error 404 de la API."));
            var vm = ListoParaGrabar();

            await vm.CargarOfrecerPagoAPlazosAsync();

            Assert.IsFalse(vm.MostrarOfrecerPagoAPlazos);
        }

        [TestMethod]
        public async Task InterruptorActivo_LaCasillaSePinta_SinMarcar_YAvisaALaVista()
        {
            var vm = ListoParaGrabar();
            var cambios = new List<string>();
            vm.PropertyChanged += (s, e) => cambios.Add(e.PropertyName);

            await vm.CargarOfrecerPagoAPlazosAsync();

            Assert.IsTrue(vm.MostrarOfrecerPagoAPlazos);
            Assert.IsFalse(vm.OfrecerPagoAPlazos, "Nace sin marcar: es de cada pedido");
            CollectionAssert.Contains(cambios, nameof(PlantillaVentaViewModel.MostrarOfrecerPagoAPlazos));
            Assert.AreEqual("Ofrecer el pago a plazos al grabar el pedido", vm.TextoOfrecerPagoAPlazos);
            StringAssert.Contains(vm.AyudaOfrecerPagoAPlazos, "el pedido se graba igual");
        }

        [TestMethod]
        public async Task InterruptorEnPruebas_SoloSePintaConUnClienteDePrueba()
        {
            ElInterruptorVale("Pruebas", "15191, 9238");
            var vm = ListoParaGrabar();
            await vm.CargarOfrecerPagoAPlazosAsync();
            Assert.IsTrue(vm.MostrarOfrecerPagoAPlazos, "El 9238 es de prueba");
            vm.OfrecerPagoAPlazos = true;

            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "1234", contacto = "0", iva = "G21", cifNif = "12345678A" };

            Assert.IsFalse(vm.MostrarOfrecerPagoAPlazos, "El 1234 no es de prueba");
            Assert.IsFalse(vm.OfrecerPagoAPlazos, "Y lo marcado con el cliente anterior ya no cuenta");
        }

        #endregion

        #region Al grabar

        [TestMethod]
        public async Task Grabar_SinMarcarLaCasilla_NoSePreguntaNadaALaApi_YTodoComoSiempre()
        {
            var vm = ListoParaGrabar();
            await vm.CargarOfrecerPagoAPlazosAsync();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _pagoAPlazos.LeerDisponible(A<string>._, A<int>._)).MustNotHaveHappened();
            NoSeAbreElDialogo();
            ElPedidoSeGrabaSeCierraLaPlantillaYSeAbre();
        }

        [TestMethod]
        public async Task Grabar_ConLaCasillaYLaApiLoOfrece_AbreElDialogoDeEsePedido_DespuesDeGrabarYAntesDeAbrirElPedido()
        {
            DisponibilidadPagoAPlazosModel disponibilidad = Disponibilidad();
            LaApiDice(disponibilidad);
            ParametrosDialogo parametros = null;
            A.CallTo(() => _dialogos.ShowDialogAsync(PagoAPlazosDialogViewModel.NOMBRE, A<ParametrosDialogo>._))
                .Invokes((string nombre, ParametrosDialogo p) => parametros = p)
                .Returns(Task.FromResult(new ResultadoDialogo(ResultadoBoton.OK)));
            var vm = await ConLaCasillaMarcada();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly()
                .Then(A.CallTo(() => _pagoAPlazos.LeerDisponible("1", 930001)).MustHaveHappenedOnceExactly())
                .Then(A.CallTo(() => _dialogos.ShowDialogAsync(PagoAPlazosDialogViewModel.NOMBRE, A<ParametrosDialogo>._)).MustHaveHappenedOnceExactly())
                .Then(A.CallTo(() => _navegacion.CerrarVistaActiva("MainRegion")).MustHaveHappenedOnceExactly())
                .Then(A.CallTo(() => _abridorPedidos.Abrir("1", 930001)).MustHaveHappenedOnceExactly());
            Assert.IsNotNull(parametros);
            Assert.AreEqual("1", parametros.GetValue<string>(PagoAPlazosDialogViewModel.PARAMETRO_EMPRESA));
            Assert.AreEqual(930001, parametros.GetValue<int>(PagoAPlazosDialogViewModel.PARAMETRO_PEDIDO));
            Assert.AreSame(disponibilidad, parametros.GetValue<DisponibilidadPagoAPlazosModel>(PagoAPlazosDialogViewModel.PARAMETRO_DISPONIBILIDAD));
        }

        [TestMethod]
        public async Task Grabar_ConLaCasillaYEsePedidoNoSePuede_DiceElMotivoDeLaApi_YElPedidoSeAbreIgual()
        {
            LaApiDice(Disponibilidad(disponible: false, motivo: "El importe del pedido no llega al mínimo de Financiera Prueba."));
            var vm = await ConLaCasillaMarcada();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _dialogos.ShowNotification("Pago a plazos",
                A<string>.That.Matches(t => t.StartsWith("El pedido 930001 se ha grabado, pero no se puede pagar a plazos.")
                    && t.EndsWith("El importe del pedido no llega al mínimo de Financiera Prueba.")))).MustHaveHappenedOnceExactly();
            NoSeAbreElDialogo();
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            ElPedidoSeGrabaSeCierraLaPlantillaYSeAbre();
        }

        [TestMethod]
        public async Task Grabar_ConLaCasillaYLaApiYaNoLoOfrece_LoDice_YElPedidoSeAbreIgual()
        {
            // El interruptor se apagó entre llegar a la plantilla y grabar: Disponible viene sin proveedor
            LaApiDice(Disponibilidad(disponible: false, motivo: "El pago a plazos no está disponible.", proveedor: null));
            var vm = await ConLaCasillaMarcada();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _dialogos.ShowNotification("Pago a plazos", A<string>.That.EndsWith("El pago a plazos no está disponible."))).MustHaveHappenedOnceExactly();
            NoSeAbreElDialogo();
            ElPedidoSeGrabaSeCierraLaPlantillaYSeAbre();
        }

        [TestMethod]
        public async Task Grabar_ConLaCasillaYLaApiFalla_DiceQueSePidaDesdeElPedido_YElPedidoSeAbreIgual()
        {
            A.CallTo(() => _pagoAPlazos.LeerDisponible(A<string>._, A<int>._)).ThrowsAsync(new Exception("Error 503 de la API."));
            var vm = await ConLaCasillaMarcada();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _dialogos.ShowNotification("Pago a plazos",
                A<string>.That.Matches(t => t.Contains("no se ha podido comprobar") && t.Contains("«Pago a plazos…»") && t.EndsWith("Error 503 de la API."))))
                .MustHaveHappenedOnceExactly();
            NoSeAbreElDialogo();
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            ElPedidoSeGrabaSeCierraLaPlantillaYSeAbre();
        }

        [TestMethod]
        public async Task Grabar_ConLaCasillaYElDialogoRevienta_ElPedidoSeAbreIgual_SinErrorNiBorrador()
        {
            A.CallTo(() => _dialogos.ShowDialogAsync(PagoAPlazosDialogViewModel.NOMBRE, A<ParametrosDialogo>._))
                .ThrowsAsync(new InvalidOperationException("no se puede abrir el diálogo"));
            var vm = await ConLaCasillaMarcada();

            await vm.CrearPedidoAsync();

            // El pedido ya está grabado: no puede salir como error de grabación ni guardarse un borrador «por si acaso»
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(_borradores).Where(c => c.Method.Name.StartsWith("Guardar")).MustNotHaveHappened();
            ElPedidoSeGrabaSeCierraLaPlantillaYSeAbre();
        }

        [TestMethod]
        public async Task Grabar_ConLaCasillaYElPedidoNoSeGraba_NoSeOfreceNada()
        {
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new Exception("No se ha podido crear el pedido."));
            var vm = await ConLaCasillaMarcada();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _pagoAPlazos.LeerDisponible(A<string>._, A<int>._)).MustNotHaveHappened();
            NoSeAbreElDialogo();
            A.CallTo(() => _abridorPedidos.Abrir(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        #endregion

        #region Solo al crear un pedido

        [TestMethod]
        public async Task ModificarConPlantilla_ConLaCasilla_NoSeOfrece_SeDiceDondePedirlo_YElPedidoSeAbre()
        {
            var vm = await ConLaCasillaMarcada();
            vm.NumeroPedidoEnEdicion = 930001;
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", 930001)).Returns(Task.FromResult(new PedidoParaPlantillaModel
            {
                ModoServicio = ModosServicio.POR_DEFECTO,
                ServirJunto = false,
                Lineas = new List<LineaParaPlantillaModel>()
            }));
            A.CallTo(() => _pedidosVenta.modificarPedido(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new List<AvisoPedidoModel>()));

            await vm.CrearPedidoAsync();

            A.CallTo(() => _pedidosVenta.modificarPedido(A<PedidoVentaDTO>._)).MustHaveHappened();
            A.CallTo(() => _pagoAPlazos.LeerDisponible(A<string>._, A<int>._)).MustNotHaveHappened();
            NoSeAbreElDialogo();
            A.CallTo(() => _dialogos.ShowNotification("Pago a plazos", ReglasPagoAPlazos.TEXTO_PLANTILLA_SOLO_PEDIDO_NUEVO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _abridorPedidos.Abrir("1", 930001)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AmpliarUnPedidoPendiente_ConLaCasilla_NoSeOfrece_SeDiceDondePedirlo_YElPedidoSeAbre()
        {
            A.CallTo(() => _pedidosVenta.cargarPedido("1", 930001)).ReturnsLazily(() => Task.FromResult(
                new PedidoVentaDTO { empresa = "1", numero = 930001, cliente = "9238", Lineas = new List<LineaPedidoVentaDTO>() }));
            A.CallTo(() => _servicio.UnirPedidos("1", 930001, A<PedidoVentaDTO>._))
                .Returns(Task.FromResult(new PedidoVentaDTO { empresa = "1", numero = 930001 }));
            var vm = await ConLaCasillaMarcada();
            vm.PedidoPendienteSeleccionado = 930001;

            await vm.CrearPedidoAsync();

            A.CallTo(() => _servicio.UnirPedidos("1", 930001, A<PedidoVentaDTO>._)).MustHaveHappened();
            A.CallTo(() => _pagoAPlazos.LeerDisponible(A<string>._, A<int>._)).MustNotHaveHappened();
            NoSeAbreElDialogo();
            A.CallTo(() => _dialogos.ShowNotification("Pago a plazos", ReglasPagoAPlazos.TEXTO_PLANTILLA_SOLO_PEDIDO_NUEVO)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _abridorPedidos.Abrir("1", 930001)).MustHaveHappenedOnceExactly();
        }

        #endregion
    }
}
