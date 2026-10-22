using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#544 (corte 2): «✓ Añadir» desde la cinta del rapport. Con una plantilla abierta de ese cliente, ella contesta al
    /// mensaje, se trae al frente y mete el producto; otra plantilla de otro cliente (o una ya cerrada) no contesta. Sin
    /// plantilla abierta, la navegación trae el cliente y el producto y la plantilla nueva lo mete. En los dos casos se queda
    /// con la sesión del rapport y apunta «Anadido» contra la sugerencia del rapport.
    /// </summary>
    [TestClass]
    public class ProductoSugeridoDesdeRapportTests
    {
        private static readonly Guid SESION_RAPPORT = new Guid("a1b2c3d4-0000-4000-8000-000000000544");
        private static readonly Guid SESION_PLANTILLA = new Guid("6c1f8f0e-3c55-4a8e-9c1e-0b5d1a9a5370");

        private sealed class Entorno
        {
            public PlantillaVentaViewModel Vm;
            public IServicioSugerenciasProductos Sugerencias;
            public IPlantillaVentaService Servicio;
            public IServicioNavegacion Navegacion;
            public IServicioDialogos Dialogos;
        }

        private static Entorno Crear(IMessenger messenger, string cliente, bool abiertaEnSuPestana = true)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.Vendedor)).Returns(Task.FromResult("ASH"));
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            IPlantillaVentaService servicio = A.Fake<IPlantillaVentaService>();
            IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), navegacion, configuracion, servicio,
                messenger, dialogos, A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            A.CallTo(() => navegacion.ActivarVista("MainRegion", vm)).Returns(abiertaEnSuPestana);
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.ComprobadorSustitucion = null;
            if (cliente != null)
            {
                vm._clienteSeleccionado = Cliente(cliente);
                vm.Estado.Contacto = "0";
            }
            IServicioSugerenciasProductos sugerencias = A.Fake<IServicioSugerenciasProductos>();
            vm.ServicioSugerenciasProductos = sugerencias;
            return new Entorno { Vm = vm, Sugerencias = sugerencias, Servicio = servicio, Navegacion = navegacion, Dialogos = dialogos };
        }

        private static ClienteJson Cliente(string cliente, string contacto = "0") =>
            new ClienteJson { empresa = "1", cliente = cliente, contacto = contacto, iva = "G21", cifNif = "12345678A" };

        private static LineaPlantillaVenta Linea(PlantillaVentaViewModel vm, string producto) =>
            Linea(vm.ListaFiltrableProductos.ListaOriginal, producto);

        private static LineaPlantillaVenta Linea(ICollection<IFiltrableItem> lista, string producto)
        {
            var linea = new LineaPlantillaVenta { producto = producto, texto = "PRODUCTO " + producto, cantidad = 0, precio = 10M, cantidadVendida = 3 };
            lista.Add(linea);
            return linea;
        }

        private static ProductoSugeridoParaPlantilla Sugerido(string cliente = "15191", string producto = "38000") =>
            new ProductoSugeridoParaPlantilla
            {
                Empresa = "1",
                Cliente = cliente,
                Contacto = "2",
                Producto = producto,
                IdSugerencia = 77,
                IdSesion = SESION_RAPPORT
            };

        #region Con plantilla abierta del cliente

        [TestMethod]
        public async Task ConPlantillaAbiertaDelCliente_Contesta_SeTraeAlFrente_MeteElProducto_YApuntaAnadidoConLaSesionDelRapport()
        {
            var messenger = new WeakReferenceMessenger();
            Entorno plantilla = Crear(messenger, "15191");
            LineaPlantillaVenta linea = Linea(plantilla.Vm, "38000");
            plantilla.Vm.IdSesionSugerencias = SESION_PLANTILLA;
            Guid? sesionAlApuntar = null;
            List<ProductoCarritoSugerenciaDTO> carrito = null;
            A.CallTo(() => plantilla.Sugerencias.Responder(77, RespuestasSugerenciaProducto.ANADIDO, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Invokes((int _, string _, List<ProductoCarritoSugerenciaDTO> c) => { sesionAlApuntar = plantilla.Vm.IdSesionSugerencias; carrito = c; })
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO { IdSugerencia = 77 }));

            var mensaje = new AnadirProductoSugeridoMensaje(Sugerido());
            messenger.Send(mensaje);

            Assert.IsTrue(mensaje.HasReceivedResponse, "La plantilla de ese cliente contesta");
            Assert.IsTrue(await mensaje.Response);
            A.CallTo(() => plantilla.Navegacion.ActivarVista("MainRegion", plantilla.Vm)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, linea.cantidad);
            Assert.IsTrue(plantilla.Vm.listaProductosPedido.Any(l => l.producto == "38000"));
            Assert.AreEqual(SESION_RAPPORT, plantilla.Vm.IdSesionSugerencias, "La plantilla se queda con la sesión del rapport");
            Assert.AreEqual(SESION_RAPPORT, sesionAlApuntar, "«Anadido» va contra la sesión del rapport");
            Assert.IsTrue(carrito.Any(p => p.Producto == "38000"), "Con el carrito de la plantilla");
            A.CallTo(() => plantilla.Navegacion.RequestNavigate(A<string>._, A<string>._, A<ParametrosNavegacion>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ConPlantillaAbierta_SiElProductoTambienEstabaEnSuCinta_LaFichaSale_SinPulsarNada()
        {
            var messenger = new WeakReferenceMessenger();
            Entorno plantilla = Crear(messenger, "15191");
            Linea(plantilla.Vm, "38000");
            plantilla.Vm.AplicarSugerenciasProductos(new[]
            {
                new SugerenciaProductoDTO { IdSugerencia = 10, Producto = "38000", Posicion = 1 },
                new SugerenciaProductoDTO { IdSugerencia = 11, Producto = "39000", Posicion = 2 }
            });

            var mensaje = new AnadirProductoSugeridoMensaje(Sugerido());
            messenger.Send(mensaje);
            await mensaje.Response;

            CollectionAssert.AreEqual(new[] { "39000" }, plantilla.Vm.SugerenciasProductos.Select(s => s.Producto).ToArray());
            A.CallTo(() => plantilla.Sugerencias.Responder(10, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._)).MustNotHaveHappened();
            A.CallTo(() => plantilla.Sugerencias.Responder(77, RespuestasSugerenciaProducto.ANADIDO, A<List<ProductoCarritoSugerenciaDTO>>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ConPlantillaAbierta_ProductoQueNoSeEncuentra_ContestaFalse_YNoApuntaNada()
        {
            var messenger = new WeakReferenceMessenger();
            Entorno plantilla = Crear(messenger, "15191");
            A.CallTo(() => plantilla.Servicio.BuscarLineaProducto("1", "45685")).Returns(Task.FromResult<LineaPlantillaVenta>(null));

            var mensaje = new AnadirProductoSugeridoMensaje(Sugerido(producto: "45685"));
            messenger.Send(mensaje);

            Assert.IsFalse(await mensaje.Response);
            A.CallTo(() => plantilla.Dialogos.ShowError(A<string>.That.Contains("Búsquelo a mano"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => plantilla.Sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task OtraPlantillaDeOtroCliente_NoContesta_YContestaLaDelCliente()
        {
            var messenger = new WeakReferenceMessenger();
            Entorno otra = Crear(messenger, "34867");
            LineaPlantillaVenta enLaOtra = Linea(otra.Vm, "38000");
            Entorno buena = Crear(messenger, "15191");
            Linea(buena.Vm, "38000");

            var mensaje = new AnadirProductoSugeridoMensaje(Sugerido());
            messenger.Send(mensaje);

            Assert.IsTrue(await mensaje.Response);
            A.CallTo(() => otra.Navegacion.ActivarVista(A<string>._, A<object>._)).MustNotHaveHappened();
            Assert.AreEqual(0, enLaOtra.cantidad, "La plantilla de otro cliente no toca nada");
            A.CallTo(() => otra.Sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._)).MustNotHaveHappened();
            A.CallTo(() => buena.Navegacion.ActivarVista("MainRegion", buena.Vm)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void SoloPlantillasDeOtroCliente_NadieContesta()
        {
            var messenger = new WeakReferenceMessenger();
            Entorno otra = Crear(messenger, "34867");
            Entorno sinCliente = Crear(messenger, null);

            var mensaje = new AnadirProductoSugeridoMensaje(Sugerido());
            messenger.Send(mensaje);

            Assert.IsFalse(mensaje.HasReceivedResponse, "Sin respuesta, el rapport abre una plantilla nueva");
            A.CallTo(() => otra.Navegacion.ActivarVista(A<string>._, A<object>._)).MustNotHaveHappened();
            A.CallTo(() => sinCliente.Navegacion.ActivarVista(A<string>._, A<object>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void PlantillaDelClienteYaCerrada_NoContesta()
        {
            var messenger = new WeakReferenceMessenger();
            Entorno cerrada = Crear(messenger, "15191", abiertaEnSuPestana: false);
            Linea(cerrada.Vm, "38000");

            var mensaje = new AnadirProductoSugeridoMensaje(Sugerido());
            messenger.Send(mensaje);

            Assert.IsFalse(mensaje.HasReceivedResponse, "Una pestaña cerrada (su ViewModel aún vivo) no está en la región");
            Assert.AreEqual(0, cerrada.Vm.listaProductosPedido.Count);
        }

        #endregion

        #region Sin plantilla abierta: plantilla nueva

        [TestMethod]
        public void LosParametrosDeLaNavegacion_SeLeen()
        {
            var parametros = new ParametrosNavegacion
            {
                { ProductoSugeridoParaPlantilla.PARAMETRO_EMPRESA, "1" },
                { ProductoSugeridoParaPlantilla.PARAMETRO_CLIENTE, "15191" },
                { ProductoSugeridoParaPlantilla.PARAMETRO_CONTACTO, "2" },
                { ProductoSugeridoParaPlantilla.PARAMETRO_PRODUCTO, "38000" },
                { ProductoSugeridoParaPlantilla.PARAMETRO_ID_SUGERENCIA, 77 },
                { ProductoSugeridoParaPlantilla.PARAMETRO_ID_SESION, SESION_RAPPORT }
            };

            ProductoSugeridoParaPlantilla leido = PlantillaVentaViewModel.ProductoSugeridoDeLaNavegacion(parametros);

            Assert.AreEqual("1", leido.Empresa);
            Assert.AreEqual("15191", leido.Cliente);
            Assert.AreEqual("2", leido.Contacto);
            Assert.AreEqual("38000", leido.Producto);
            Assert.AreEqual(77, leido.IdSugerencia);
            Assert.AreEqual(SESION_RAPPORT, leido.IdSesion);
            Assert.IsNull(PlantillaVentaViewModel.ProductoSugeridoDeLaNavegacion(new ParametrosNavegacion()), "La navegación de siempre");
            Assert.IsNull(PlantillaVentaViewModel.ProductoSugeridoDeLaNavegacion(null));
        }

        [TestMethod]
        public void PlantillaNueva_EligeElClienteYElContacto_DejaLaPaginaDeProductos_MeteElProducto_YApuntaAnadido()
        {
            // Las páginas del wizard son controles (WizardPage): hilo STA con su Dispatcher
            EnStaConDispatcher(PlantillaNuevaAsync);
        }

        private static async Task PlantillaNuevaAsync()
        {
            Entorno plantilla = Crear(new WeakReferenceMessenger(), null);
            plantilla.Vm.PaginasWizard = new List<Xceed.Wpf.Toolkit.WizardPage>
            {
                new Xceed.Wpf.Toolkit.WizardPage { Name = "SeleccionCliente" },
                new Xceed.Wpf.Toolkit.WizardPage { Name = "SeleccionProductos" }
            };
            A.CallTo(() => plantilla.Servicio.CargarClientesVendedor("15191", A<string>._, A<bool>._))
                .Returns(Task.FromResult<ICollection<ClienteJson>>(new List<ClienteJson> { Cliente("15191", "0"), Cliente("15191", "2") }));
            A.CallTo(() => plantilla.Servicio.CargarProductosPlantilla(A<ClienteJson>._))
                .ReturnsLazily(() => Task.FromResult(new ObservableCollection<LineaPlantillaVenta>
                {
                    new LineaPlantillaVenta { producto = "38000", texto = "PRODUCTO 38000", precio = 10M, cantidadVendida = 3 }
                }));
            A.CallTo(() => plantilla.Servicio.PonerStocks(A<ObservableCollection<LineaPlantillaVenta>>._, A<string>._, A<List<string>>._))
                .ReturnsLazily((ObservableCollection<LineaPlantillaVenta> lineas, string _, List<string> _) => Task.FromResult(lineas));
            Guid? sesionAlApuntar = null;
            A.CallTo(() => plantilla.Sugerencias.Responder(77, RespuestasSugerenciaProducto.ANADIDO, A<List<ProductoCarritoSugerenciaDTO>>._))
                .Invokes(() => sesionAlApuntar = plantilla.Vm.IdSesionSugerencias)
                .Returns(Task.FromResult(new ResultadoRespuestaSugerenciaProductoDTO { IdSugerencia = 77 }));

            bool entro = await plantilla.Vm.AbrirConProductoSugeridoAsync(Sugerido());

            Assert.IsTrue(entro);
            Assert.AreEqual("15191", plantilla.Vm.clienteSeleccionado.cliente);
            Assert.AreEqual("2", plantilla.Vm.clienteSeleccionado.contacto, "El contacto del rapport");
            Assert.AreEqual("SeleccionProductos", plantilla.Vm.PaginaActual?.Name, "El wizard queda en la página de productos");
            LineaPlantillaVenta linea = plantilla.Vm.listaProductosPedido.SingleOrDefault(l => l.producto == "38000");
            Assert.IsNotNull(linea);
            Assert.AreEqual(1, linea.cantidad);
            Assert.AreEqual(SESION_RAPPORT, plantilla.Vm.IdSesionSugerencias);
            Assert.AreEqual(SESION_RAPPORT, sesionAlApuntar);
        }

        [TestMethod]
        public async Task PlantillaNueva_ClienteQueNoEsDelVendedor_AvisaYNoMeteNada()
        {
            Entorno plantilla = Crear(new WeakReferenceMessenger(), null);
            A.CallTo(() => plantilla.Servicio.CargarClientesVendedor(A<string>._, A<string>._, A<bool>._))
                .Returns(Task.FromResult<ICollection<ClienteJson>>(new List<ClienteJson>()));

            bool entro = await plantilla.Vm.AbrirConProductoSugeridoAsync(Sugerido());

            Assert.IsFalse(entro);
            A.CallTo(() => plantilla.Dialogos.ShowError(A<string>.That.Contains("No se encontró el cliente 15191"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => plantilla.Sugerencias.Responder(A<int>._, A<string>._, A<List<ProductoCarritoSugerenciaDTO>>._)).MustNotHaveHappened();
        }

        #endregion

        private static void EnStaConDispatcher(Func<Task> prueba)
        {
            Exception error = null;
            var hilo = new System.Threading.Thread(() =>
            {
                System.Windows.Threading.Dispatcher dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
                _ = dispatcher.InvokeAsync(async () =>
                {
                    try { await prueba(); }
                    catch (Exception ex) { error = ex; }
                    finally { dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background); }
                });
                System.Windows.Threading.Dispatcher.Run();
            });
            hilo.SetApartmentState(System.Threading.ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromSeconds(60)))
            {
                Assert.Fail("La prueba no ha terminado");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }
    }
}
