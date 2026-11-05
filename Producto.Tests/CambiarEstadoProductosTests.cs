using ControlesUsuario.Dialogs;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#624 (NestoAPI#738, sugerencia 793 de Manuel): el estado de los productos, masivo (ventana «Cambiar estado») y de
    /// uno (desplegable de la ficha). La lógica es de la API; aquí: el filtro, la pregunta de los controles de stock UNA vez por
    /// lote (tres botones), la confirmación con los avisos, el resultado y que «Aplicar» solo se pueda cuando toca.
    /// </summary>
    [TestClass]
    public class CambiarEstadoProductosTests
    {
        private const short DISPONIBLE = 0;
        private const short EXTINGUIR = 3;

        private IServicioEstadoProductos _servicio = null!;
        private IServicioDialogos _dialogos = null!;

        private static readonly List<EstadoProductoDTO> ESTADOS = new()
        {
            new EstadoProductoDTO { Estado = DISPONIBLE, Descripcion = "DISPONIBLE" },
            new EstadoProductoDTO { Estado = EXTINGUIR, Descripcion = "EXTINGUIR (No queremos trabajarlo)", AExtinguir = true }
        };

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IServicioEstadoProductos>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _servicio.LeerEstados(A<string>._)).Returns(Task.FromResult(ESTADOS.ToList()));
            A.CallTo(() => _servicio.Cambiar(A<string>._, A<IEnumerable<string>>._, A<short>._, A<bool>._))
                .Returns(Task.FromResult(new ResultadoCambioEstadoProductosDTO { Cambiados = 1 }));
        }

        private static ProductoEstadoVistaPreviaDTO Fila(string producto, string? motivo = null, bool controlesAlg = false, params string[] avisos)
            => new()
            {
                Producto = producto,
                Nombre = "PRODUCTO " + producto,
                EstadoActual = DISPONIBLE,
                EstadoNuevo = EXTINGUIR,
                StockALG = 5,
                TieneControlesStock = new ControlesStockAlmacenesDTO { ALG = controlesAlg },
                PedidosPendientes = new List<PedidoPendienteEstadoDTO> { new() { Pedido = 900001, Cliente = "15191", Cantidad = 2 } },
                Avisos = avisos.ToList(),
                MotivoNoSeCambia = motivo
            };

        private void VistaPreviaDevuelve(VistaPreviaEstadoProductosDTO vista)
            => A.CallTo(() => _servicio.VistaPrevia(A<string>._, A<FiltroEstadoProductos>._, A<short>._)).Returns(Task.FromResult(vista));

        private void ElDialogoContesta(ResultadoBoton boton)
            => A.CallTo(() => _dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string _, ParametrosDialogo _, Action<ResultadoDialogo> callback) => callback(new ResultadoDialogo(boton)));

        private async Task<CambiarEstadoProductosViewModel> VmConVistaPrevia(VistaPreviaEstadoProductosDTO vista)
        {
            VistaPreviaDevuelve(vista);
            var vm = new CambiarEstadoProductosViewModel(_servicio, _dialogos);
            await vm.CargarEstadosAsync();
            vm.AnnadirReferencias("17404\r\n17405");
            vm.EstadoNuevo = vm.Estados.Single(e => e.Estado == EXTINGUIR);
            await vm.VistaPreviaAsync();
            return vm;
        }

        // ---------------------------------------------------------------- ventana masiva: filtro y vista previa

        [TestMethod]
        public async Task VistaPrevia_SinFiltro_AvisaYNoLlamaALaApi()
        {
            var vm = new CambiarEstadoProductosViewModel(_servicio, _dialogos);
            await vm.CargarEstadosAsync();
            vm.EstadoNuevo = vm.Estados.First();

            await vm.VistaPreviaAsync();

            Assert.AreEqual(CambiarEstadoProductosViewModel.MENSAJE_SIN_FILTRO, vm.Mensaje);
            A.CallTo(() => _servicio.VistaPrevia(A<string>._, A<FiltroEstadoProductos>._, A<short>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.AplicarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task VistaPrevia_SinEstadoNuevo_AvisaYNoLlamaALaApi()
        {
            var vm = new CambiarEstadoProductosViewModel(_servicio, _dialogos) { Proveedor = "65" };

            await vm.VistaPreviaAsync();

            Assert.AreEqual(CambiarEstadoProductosViewModel.MENSAJE_SIN_ESTADO, vm.Mensaje);
            A.CallTo(() => _servicio.VistaPrevia(A<string>._, A<FiltroEstadoProductos>._, A<short>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task VistaPrevia_MandaElFiltroYPintaLasFilas_YAplicarSePuede()
        {
            var vm = await VmConVistaPrevia(new VistaPreviaEstadoProductosDTO
            {
                Productos = { Fila("17404"), Fila("17405", motivo: "Ya está en estado 3.") },
                Avisos = { "1 producto tiene pedidos de clientes pendientes de servir." },
                PuedeCambiar = true
            });

            A.CallTo(() => _servicio.VistaPrevia("1", A<FiltroEstadoProductos>.That.Matches(f =>
                f.Productos!.SequenceEqual(new[] { "17404", "17405" }) && f.Proveedor == null), EXTINGUIR)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(2, vm.Filas.Count);
            Assert.IsTrue(vm.Filas[0].SeCambia);
            Assert.IsFalse(vm.Filas[1].SeCambia);
            Assert.AreEqual("0 · DISPONIBLE → 3 · EXTINGUIR (No queremos trabajarlo)", vm.Filas[0].CambioEstado);
            Assert.AreEqual(1, vm.Filas[0].NumeroPedidosPendientes);
            StringAssert.Contains(vm.Filas[0].DetallePedidosPendientes, "Pedido 900001 · cliente 15191 · 2 uds.");
            Assert.AreEqual("2 productos: 1 cambiará de estado y 1 no.", vm.ResumenVistaPrevia);
            Assert.IsTrue(vm.HayAvisosLote);
            Assert.IsNull(vm.MotivoNoSePuedeAplicar);
            Assert.IsTrue(vm.AplicarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task VistaPrevia_SinPermiso_AplicarDeshabilitadoConElMotivo()
        {
            var vm = await VmConVistaPrevia(new VistaPreviaEstadoProductosDTO { Productos = { Fila("17404") }, PuedeCambiar = false });

            Assert.IsFalse(vm.PuedeAplicar);
            Assert.IsFalse(vm.AplicarCommand.CanExecute(null));
            Assert.AreEqual("Solo Compras y Dirección pueden cambiar el estado de los productos.", vm.MotivoNoSePuedeAplicar);
        }

        [TestMethod]
        public async Task VistaPrevia_NingunoCambia_AplicarDeshabilitado()
        {
            var vm = await VmConVistaPrevia(new VistaPreviaEstadoProductosDTO
            {
                Productos = { Fila("17404", motivo: "El producto no existe.") },
                PuedeCambiar = true
            });

            Assert.IsFalse(vm.AplicarCommand.CanExecute(null));
            Assert.AreEqual(CambiarEstadoProductosViewModel.MOTIVO_NINGUNO_CAMBIA, vm.MotivoNoSePuedeAplicar);
        }

        [TestMethod]
        public async Task VistaPrevia_400DeLaApi_EnsenaSuTexto()
        {
            A.CallTo(() => _servicio.VistaPrevia(A<string>._, A<FiltroEstadoProductos>._, A<short>._))
                .ThrowsAsync(new EstadoProductosException("Son 2.345 productos: como mucho 2.000 por lote. Afine el filtro.", 400));
            var vm = new CambiarEstadoProductosViewModel(_servicio, _dialogos) { Proveedor = "65" };
            await vm.CargarEstadosAsync();
            vm.EstadoNuevo = vm.Estados.First();

            await vm.VistaPreviaAsync();

            Assert.AreEqual("Son 2.345 productos: como mucho 2.000 por lote. Afine el filtro.", vm.Mensaje);
            Assert.IsFalse(vm.AplicarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task CambiarElFiltro_QuitaLaVistaPrevia()
        {
            var vm = await VmConVistaPrevia(new VistaPreviaEstadoProductosDTO { Productos = { Fila("17404") }, PuedeCambiar = true });

            vm.Familia = "Lisap";

            Assert.AreEqual(0, vm.Filas.Count);
            Assert.IsNull(vm.VistaPrevia);
            Assert.AreEqual(CambiarEstadoProductosViewModel.MOTIVO_SIN_VISTA_PREVIA, vm.MotivoNoSePuedeAplicar);
        }

        [TestMethod]
        public void AnnadirReferencias_SeparaLoPegadoYNoRepite()
        {
            var vm = new CambiarEstadoProductosViewModel(_servicio, _dialogos) { ReferenciaNueva = "17404" };

            vm.AnnadirReferenciasCommand.Execute(null);
            vm.AnnadirReferencias(" 17405\r\n17404\t17406; 17407,");

            CollectionAssert.AreEqual(new[] { "17404", "17405", "17406", "17407" }, vm.Referencias.ToArray());
            Assert.IsNull(vm.ReferenciaNueva);
            Assert.AreEqual("4 referencias", vm.TextoReferencias);
        }

        // ---------------------------------------------------------------- ventana masiva: aplicar

        private static VistaPreviaEstadoProductosDTO ConControles() => new()
        {
            Productos = { Fila("17404", controlesAlg: true, avisos: "Se quitarán sus controles de stock en ALG."), Fila("17405") },
            Avisos = { "1 producto tiene controles de stock: si no los quita, no se le cambiará el estado." },
            ControlesStockAfectados = 1,
            PreguntaControlesStock = "1 producto tiene controles de stock en ALG. ¿Los quito y cambio el estado, o no cambio el estado de ese producto?",
            PuedeCambiar = true
        };

        [TestMethod]
        public async Task Aplicar_ConControles_QuitarlosYCambiar_MandaQuitarControlesTrue()
        {
            var vm = await VmConVistaPrevia(ConControles());
            ElDialogoContesta(ResultadoBoton.OK);

            await vm.AplicarAsync();

            A.CallTo(() => _dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>.That.Matches(p =>
                    p.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_SI) == "Quitarlos y cambiar"
                    && p.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_TERCERO) == "No cambiar esos"
                    && p.GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_NO) == "Cancelar"
                    && p.GetValue<string>("message").StartsWith("1 producto tiene controles de stock en ALG.")),
                A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Cambiar("1", A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "17404", "17405" }), EXTINGUIR, true))
                .MustHaveHappenedOnceExactly();
            // Con la pregunta de los controles no hay una segunda confirmación
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Aplicar_ConControles_NoCambiarEsos_MandaQuitarControlesFalse()
        {
            var vm = await VmConVistaPrevia(ConControles());
            ElDialogoContesta(ResultadoBoton.No);

            await vm.AplicarAsync();

            A.CallTo(() => _servicio.Cambiar("1", A<IEnumerable<string>>._, EXTINGUIR, false)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Aplicar_ConControles_Cancelar_NoCambiaNada()
        {
            var vm = await VmConVistaPrevia(ConControles());
            ElDialogoContesta(ResultadoBoton.Cancel);

            await vm.AplicarAsync();

            A.CallTo(() => _servicio.Cambiar(A<string>._, A<IEnumerable<string>>._, A<short>._, A<bool>._)).MustNotHaveHappened();
            Assert.IsNull(vm.Resultado);
        }

        [TestMethod]
        public async Task Aplicar_ConAvisosSinControles_PideConfirmacion_YSiDiceQueNoNoCambia()
        {
            var vm = await VmConVistaPrevia(new VistaPreviaEstadoProductosDTO
            {
                Productos = { Fila("17404") },
                Avisos = { "1 producto tiene pedidos de clientes pendientes de servir." },
                PuedeCambiar = true
            });
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(false);

            await vm.AplicarAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAnswer(FlujoCambioEstadoProductos.TITULO,
                A<string>.That.Contains("• 1 producto tiene pedidos de clientes pendientes de servir."))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Cambiar(A<string>._, A<IEnumerable<string>>._, A<short>._, A<bool>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Aplicar_SoloMandaLasQueCambian_EnsenaElResultadoYRefrescaLaVistaPrevia()
        {
            var vm = await VmConVistaPrevia(new VistaPreviaEstadoProductosDTO
            {
                Productos = { Fila("17404"), Fila("17405", motivo: "Ya está en estado 3.") },
                PuedeCambiar = true
            });
            A.CallTo(() => _servicio.Cambiar(A<string>._, A<IEnumerable<string>>._, A<short>._, A<bool>._))
                .Returns(Task.FromResult(new ResultadoCambioEstadoProductosDTO
                {
                    Cambiados = 1,
                    ControlesStockQuitados = 2,
                    Omitidos = { new ProductoOmitidoDTO { Producto = "17406", Motivo = "Tiene controles de stock en ALG y REI." } },
                    Avisos = { "1 producto no ha cambiado de estado porque tiene controles de stock." }
                }));

            await vm.AplicarAsync();

            A.CallTo(() => _servicio.Cambiar("1", A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { "17404" }), EXTINGUIR, false))
                .MustHaveHappenedOnceExactly();
            StringAssert.StartsWith(vm.Resultado, "Se ha cambiado el estado de 1 producto.");
            StringAssert.Contains(vm.Resultado, "Se han quitado 2 controles de stock.");
            StringAssert.Contains(vm.Resultado, "• 17406: Tiene controles de stock en ALG y REI.");
            StringAssert.Contains(vm.Resultado, "• 1 producto no ha cambiado de estado porque tiene controles de stock.");
            A.CallTo(() => _dialogos.ShowNotification(FlujoCambioEstadoProductos.TITULO, vm.Resultado)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.VistaPrevia(A<string>._, A<FiltroEstadoProductos>._, A<short>._)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Aplicar_403DeLaApi_EnsenaSuTexto()
        {
            var vm = await VmConVistaPrevia(new VistaPreviaEstadoProductosDTO { Productos = { Fila("17404") }, PuedeCambiar = true });
            A.CallTo(() => _servicio.Cambiar(A<string>._, A<IEnumerable<string>>._, A<short>._, A<bool>._))
                .ThrowsAsync(new EstadoProductosException("Solo Compras y Dirección pueden cambiar el estado de los productos.", 403));

            await vm.AplicarAsync();

            Assert.AreEqual("Solo Compras y Dirección pueden cambiar el estado de los productos.", vm.Mensaje);
            A.CallTo(() => _dialogos.ShowError("Solo Compras y Dirección pueden cambiar el estado de los productos.")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void TextoResultado_Singular_YControlesCreados()
        {
            string texto = FlujoCambioEstadoProductos.TextoResultado(new ResultadoCambioEstadoProductosDTO { Cambiados = 3, ControlesStockCreados = 1 });

            Assert.AreEqual("Se ha cambiado el estado de 3 productos.\nSe ha creado 1 control de stock con los máximos sugeridos.", texto);
        }

        // ---------------------------------------------------------------- el botón de la cinta

        [TestMethod]
        public void MenuBar_Compras_AbreLaVentana_YUnComercialNo()
        {
            var navegacion = A.Fake<IServicioNavegacion>();
            var configuracion = A.Fake<IConfiguracion>();
            var comercial = new ProductoMenuBarViewModel(navegacion, configuracion, A.Fake<IServicioDiferenciasReposicion>());
            Assert.IsFalse(comercial.AbrirCambiarEstadoProductosCommand.CanExecute(null));

            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)).Returns(true);
            var compras = new ProductoMenuBarViewModel(navegacion, configuracion, A.Fake<IServicioDiferenciasReposicion>());
            Assert.IsTrue(compras.AbrirCambiarEstadoProductosCommand.CanExecute(null));
            compras.AbrirCambiarEstadoProductosCommand.Execute(null);

            A.CallTo(() => navegacion.RequestNavigate("MainRegion", ProductoMenuBarViewModel.VISTA_CAMBIAR_ESTADO_PRODUCTOS)).MustHaveHappenedOnceExactly();
        }

        // ---------------------------------------------------------------- la ficha

        private const string PRODUCTO = "17404";

        private ProductoViewModel Ficha(out IProductoService productos, bool esDeCompras = true, short estado = DISPONIBLE)
        {
            var fake = A.Fake<IProductoService>();
            productos = fake;
            A.CallTo(() => fake.LeerProducto(PRODUCTO)).ReturnsLazily(() => Task.FromResult(new ProductoModel { Producto = PRODUCTO, Nombre = "CHAMPÚ", Estado = estado }));
            A.CallTo(() => fake.LeerVariantes(A<string>._)).Returns(new List<VarianteModel>());
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.COMPRAS)).Returns(esDeCompras);
            var vm = new ProductoViewModel(A.Fake<IServicioNavegacion>(), configuracion, fake, new WeakReferenceMessenger(), _dialogos,
                A.Fake<IServicioAutenticacion>());
            vm.ServicioEstadoProductos = _servicio;
            return vm;
        }

        [TestMethod]
        public async Task Ficha_AlCargar_ElDesplegableTieneElEstadoDelProducto_SinCambiarNada()
        {
            var vm = Ficha(out _);

            await vm.CargarProducto(PRODUCTO);

            Assert.AreEqual(2, vm.EstadosProducto.Count);
            Assert.AreEqual(DISPONIBLE, vm.EstadoProductoSeleccionado!.Estado);
            Assert.IsTrue(vm.DesplegableEstadoHabilitado);
            A.CallTo(() => _servicio.VistaPrevia(A<string>._, A<FiltroEstadoProductos>._, A<short>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Ficha_CambiarElEstado_VistaPreviaDeUno_Confirma_Aplica_YRecargaLaFicha()
        {
            var vm = Ficha(out IProductoService productos);
            await vm.CargarProducto(PRODUCTO);
            VistaPreviaDevuelve(new VistaPreviaEstadoProductosDTO
            {
                Productos = { Fila(PRODUCTO, avisos: "Le queda stock: ALG 5.") },
                PuedeCambiar = true
            });
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);

            vm.EstadoProductoSeleccionado = vm.EstadosProducto.Single(e => e.Estado == EXTINGUIR);
            await vm.CambioEstadoEnCurso;

            A.CallTo(() => _servicio.VistaPrevia("1", A<FiltroEstadoProductos>.That.Matches(f => f.Productos!.Single() == PRODUCTO), EXTINGUIR))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(FlujoCambioEstadoProductos.TITULO, A<string>.That.Matches(m =>
                m.Contains("¿Cambia el estado de 17404 de «0 · DISPONIBLE» a «3 · EXTINGUIR (No queremos trabajarlo)»?")
                && m.Contains("• Le queda stock: ALG 5.")))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Cambiar("1", A<IEnumerable<string>>.That.IsSameSequenceAs(new[] { PRODUCTO }), EXTINGUIR, false))
                .MustHaveHappenedOnceExactly();
            A.CallTo(() => productos.LeerProducto(PRODUCTO)).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Ficha_ConControles_LaPreguntaDeTresBotones_QuitarlosYCambiar()
        {
            var vm = Ficha(out _);
            await vm.CargarProducto(PRODUCTO);
            VistaPreviaDevuelve(ConControles());
            ElDialogoContesta(ResultadoBoton.OK);

            vm.EstadoProductoSeleccionado = vm.EstadosProducto.Single(e => e.Estado == EXTINGUIR);
            await vm.CambioEstadoEnCurso;

            A.CallTo(() => _servicio.Cambiar("1", A<IEnumerable<string>>._, EXTINGUIR, true)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Ficha_SiSeCancela_NoCambia_YElDesplegableVuelve()
        {
            var vm = Ficha(out _);
            await vm.CargarProducto(PRODUCTO);
            VistaPreviaDevuelve(new VistaPreviaEstadoProductosDTO { Productos = { Fila(PRODUCTO) }, PuedeCambiar = true });
            A.CallTo(() => _dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(false);

            vm.EstadoProductoSeleccionado = vm.EstadosProducto.Single(e => e.Estado == EXTINGUIR);
            await vm.CambioEstadoEnCurso;

            A.CallTo(() => _servicio.Cambiar(A<string>._, A<IEnumerable<string>>._, A<short>._, A<bool>._)).MustNotHaveHappened();
            Assert.AreEqual(DISPONIBLE, vm.EstadoProductoSeleccionado!.Estado);
        }

        [TestMethod]
        public async Task Ficha_ConMotivoNoSeCambia_LoDiceYNoCambia()
        {
            var vm = Ficha(out _);
            await vm.CargarProducto(PRODUCTO);
            VistaPreviaDevuelve(new VistaPreviaEstadoProductosDTO { Productos = { Fila(PRODUCTO, motivo: "Ya está en estado 3.") }, PuedeCambiar = true });

            vm.EstadoProductoSeleccionado = vm.EstadosProducto.Single(e => e.Estado == EXTINGUIR);
            await vm.CambioEstadoEnCurso;

            A.CallTo(() => _dialogos.ShowNotification(FlujoCambioEstadoProductos.TITULO, "Ya está en estado 3.")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.Cambiar(A<string>._, A<IEnumerable<string>>._, A<short>._, A<bool>._)).MustNotHaveHappened();
            Assert.AreEqual(DISPONIBLE, vm.EstadoProductoSeleccionado!.Estado);
        }

        [TestMethod]
        public async Task Ficha_SinSerDeComprasNiDireccion_ElDesplegableEstaDeshabilitado_YNoCambia()
        {
            var vm = Ficha(out _, esDeCompras: false);
            await vm.CargarProducto(PRODUCTO);

            Assert.IsFalse(vm.DesplegableEstadoHabilitado);
            vm.EstadoProductoSeleccionado = vm.EstadosProducto.Single(e => e.Estado == EXTINGUIR);
            await vm.CambioEstadoEnCurso;

            A.CallTo(() => _servicio.VistaPrevia(A<string>._, A<FiltroEstadoProductos>._, A<short>._)).MustNotHaveHappened();
            Assert.AreEqual(DISPONIBLE, vm.EstadoProductoSeleccionado!.Estado);
        }

        [TestMethod]
        public async Task Ficha_EstadoQueNoEstaEnLaTabla_SeEnsenaIgual()
        {
            var vm = Ficha(out _, estado: 8);

            await vm.CargarProducto(PRODUCTO);

            Assert.AreEqual((short)8, vm.EstadoProductoSeleccionado!.Estado);
            Assert.AreEqual("8", vm.EstadoProductoSeleccionado.Texto);
        }

        // ---------------------------------------------------------------- el diálogo de tres botones

        [TestMethod]
        public void Dialogo_ElTercerBoton_CierraConNo()
        {
            var dialogo = new ConfirmationDialogViewModel();
            dialogo.OnDialogOpened(new ParametrosDialogo
            {
                { "title", "t" }, { "message", "m" }, { ConfirmationDialogViewModel.PARAMETRO_TEXTO_TERCERO, "No cambiar esos" }
            });
            ResultadoDialogo? resultado = null;
            dialogo.RequestClose += r => resultado = r;

            Assert.IsTrue(dialogo.HayTercero);
            dialogo.CloseDialogCommand.Execute("tercero");

            Assert.AreEqual(ResultadoBoton.No, resultado!.Result);
        }

        [TestMethod]
        public void Dialogo_SinTercerBoton_ElDeSiempre()
        {
            var dialogo = new ConfirmationDialogViewModel();
            dialogo.OnDialogOpened(new ParametrosDialogo { { "title", "t" }, { "message", "m" } });

            Assert.IsFalse(dialogo.HayTercero);
        }

        // ---------------------------------------------------------------- el servicio

        private sealed class HandlerFalso : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;
            public List<(string Metodo, string Url, string? Cuerpo)> Peticiones { get; } = new();

            public HandlerFalso(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                string? cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                Peticiones.Add((request.Method.Method, request.RequestUri!.PathAndQuery, cuerpo));
                return _responder(request);
            }
        }

        private static ServicioEstadoProductos Servicio(HandlerFalso handler)
        {
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() =>
                new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return new ServicioEstadoProductos(factoria);
        }

        private static HttpResponseMessage Json(HttpStatusCode codigo, string json)
            => new(codigo) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

        [TestMethod]
        public async Task Servicio_LeerEstados_GetConLaEmpresa()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK,
                "[{\"Estado\":0,\"Descripcion\":\"DISPONIBLE\",\"AExtinguir\":false,\"AAnular\":false},{\"Estado\":4,\"Descripcion\":\"ANULAR\",\"AExtinguir\":false,\"AAnular\":true}]"));

            List<EstadoProductoDTO> estados = await Servicio(handler).LeerEstados(null!);

            Assert.AreEqual(("GET", "/api/Productos/Estados?empresa=1"), (handler.Peticiones.Single().Metodo, handler.Peticiones.Single().Url));
            Assert.AreEqual(2, estados.Count);
            Assert.IsTrue(estados[1].AAnular);
        }

        [TestMethod]
        public async Task Servicio_VistaPrevia_PostConElFiltroLimpio()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK,
                "{\"Productos\":[{\"Producto\":\"17404\",\"EstadoActual\":0,\"EstadoNuevo\":3,\"PedidosPendientes\":[{\"Pedido\":900001,\"Cliente\":\"15191\",\"Cantidad\":2}],\"TieneControlesStock\":{\"ALG\":true,\"REI\":false,\"ALC\":false},\"MotivoNoSeCambia\":null}],\"ControlesStockAfectados\":1,\"PreguntaControlesStock\":\"¿Los quito?\",\"PuedeCambiar\":true}"));

            VistaPreviaEstadoProductosDTO vista = await Servicio(handler).VistaPrevia("1",
                new FiltroEstadoProductos { Proveedor = " 65 ", Familia = "  ", Productos = new List<string> { "17404", " 17404", "" } }, 3);

            var peticion = handler.Peticiones.Single();
            Assert.AreEqual(("POST", "/api/Productos/Estado/VistaPrevia"), (peticion.Metodo, peticion.Url));
            JObject cuerpo = JObject.Parse(peticion.Cuerpo!);
            Assert.AreEqual("1", (string?)cuerpo["Empresa"]);
            Assert.AreEqual(3, (int)cuerpo["EstadoNuevo"]!);
            Assert.AreEqual("65", (string?)cuerpo["Filtro"]!["Proveedor"]);
            Assert.AreEqual(JTokenType.Null, cuerpo["Filtro"]!["Familia"]!.Type);
            CollectionAssert.AreEqual(new[] { "17404" }, cuerpo["Filtro"]!["Productos"]!.Select(t => (string)t!).ToArray());
            Assert.IsTrue(vista.PuedeCambiar);
            Assert.AreEqual(1, vista.ControlesStockAfectados);
            Assert.IsTrue(vista.Productos.Single().TieneControlesStock.ALG);
            Assert.AreEqual(900001, vista.Productos.Single().PedidosPendientes.Single().Pedido);
        }

        [TestMethod]
        public async Task Servicio_Cambiar_PostConQuitarControles()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.OK,
                "{\"Cambiados\":1,\"Omitidos\":[{\"Producto\":\"17405\",\"Motivo\":\"Tiene controles de stock en ALG y REI.\"}],\"ControlesStockQuitados\":0,\"ControlesStockCreados\":0,\"Avisos\":[]}"));

            ResultadoCambioEstadoProductosDTO resultado = await Servicio(handler).Cambiar("1", new[] { "17404", "17405" }, 3, true);

            var peticion = handler.Peticiones.Single();
            Assert.AreEqual(("POST", "/api/Productos/Estado"), (peticion.Metodo, peticion.Url));
            JObject cuerpo = JObject.Parse(peticion.Cuerpo!);
            Assert.IsTrue((bool)cuerpo["QuitarControlesStock"]!);
            Assert.AreEqual(JTokenType.Null, cuerpo["Usuario"]!.Type);
            CollectionAssert.AreEqual(new[] { "17404", "17405" }, cuerpo["Productos"]!.Select(t => (string)t!).ToArray());
            Assert.AreEqual(1, resultado.Cambiados);
            Assert.AreEqual("17405", resultado.Omitidos.Single().Producto);
        }

        [TestMethod]
        public async Task Servicio_Cambiar_403EnTexto_LanzaConElTextoDeLaApi()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.Forbidden, "\"Solo Compras y Dirección pueden cambiar el estado de los productos.\""));

            EstadoProductosException ex = await Assert.ThrowsExceptionAsync<EstadoProductosException>(
                () => Servicio(handler).Cambiar("1", new[] { "17404" }, 3, false));

            Assert.AreEqual(403, ex.Codigo);
            Assert.AreEqual("Solo Compras y Dirección pueden cambiar el estado de los productos.", ex.Message);
        }

        [TestMethod]
        public async Task Servicio_VistaPrevia_400ConMessage_LanzaConElTextoDeLaApi()
        {
            var handler = new HandlerFalso(_ => Json(HttpStatusCode.BadRequest, "{\"Message\":\"El estado 8 no existe en la tabla de estados de los productos.\"}"));

            EstadoProductosException ex = await Assert.ThrowsExceptionAsync<EstadoProductosException>(
                () => Servicio(handler).VistaPrevia("1", new FiltroEstadoProductos { Proveedor = "65" }, 8));

            Assert.AreEqual(400, ex.Codigo);
            Assert.AreEqual("El estado 8 no existe en la tabla de estados de los productos.", ex.Message);
        }
    }
}
