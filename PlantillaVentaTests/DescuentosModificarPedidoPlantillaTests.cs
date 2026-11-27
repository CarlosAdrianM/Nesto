using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
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
    /// Incidencia 1504 (María José, pedido 931500, 27/11/26, Black Friday): «cuando le das a modificar un pedido ya existente te
    /// quita los descuentos». En LinPedidoVta los de campaña y el 100 % de las muestras van en DescuentoProducto (Descuento = 0),
    /// y GET ParaPlantilla solo trae el de línea: la plantilla enseñaba 0 % y la base sin descontar (44709: 65,40 € en vez de
    /// 45,78 €) y, al guardar, mandaba DescuentoProducto = 0, que el PUT de NestoAPI copia tal cual en las líneas sin picking.
    /// </summary>
    [TestClass]
    public class DescuentosModificarPedidoPlantillaTests
    {
        private const int PEDIDO = 931500;
        private const int ID_CAMPANA = 3315001;
        private const int ID_CAMPANA_EN_PLANTILLA = 3315002;
        private const int ID_MUESTRA = 3315003;
        private const int ID_SIN_APLICAR_DTO = 3315004;
        private const int ID_DOS_DESCUENTOS = 3315005;

        /// <summary>El pedido como lo devuelve GET ParaPlantilla hoy: sin DescuentoProducto.</summary>
        private static PedidoParaPlantillaModel PedidoParaPlantilla() => new PedidoParaPlantillaModel
        {
            Empresa = "1",
            Cliente = "4730",
            Contacto = "0",
            NumeroPedido = PEDIDO,
            FormaPago = "RCB",
            PlazosPago = "CONTADO",
            Ruta = "00",
            FechaEntrega = DateTime.Today.AddDays(1),
            Almacen = "ALG",
            Lineas = new List<LineaParaPlantillaModel>
            {
                new LineaParaPlantillaModel { Producto = "44709", Texto = "CREMA CAMPAÑA 30", Cantidad = 1, Precio = 65.40M, Descuento = 0M, AplicarDescuento = true, IdLineaPago = ID_CAMPANA },
                new LineaParaPlantillaModel { Producto = "40001", Texto = "CHAMPU CAMPAÑA 25", Cantidad = 2, Precio = 20M, Descuento = 0M, AplicarDescuento = true, IdLineaPago = ID_CAMPANA_EN_PLANTILLA },
                new LineaParaPlantillaModel { Producto = "44206", Texto = "MUESTRA", Cantidad = 5, Precio = 0.57M, Descuento = 0M, AplicarDescuento = true, IdLineaPago = ID_MUESTRA },
                new LineaParaPlantillaModel { Producto = "38000", Texto = "APARATO SIN DTO", Cantidad = 1, Precio = 30M, Descuento = 0.10M, AplicarDescuento = false, IdLineaPago = ID_SIN_APLICAR_DTO }
            },
            Regalos = new List<RegaloParaPlantillaModel>()
        };

        /// <summary>El mismo pedido por GET api/PedidosVenta, con el descuento de producto de cada línea.</summary>
        private static PedidoVentaDTO PedidoCompleto()
        {
            var pedido = new PedidoVentaDTO { empresa = "1", numero = PEDIDO, cliente = "4730", contacto = "0" };
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = ID_CAMPANA, Producto = "44709", Cantidad = 1, PrecioUnitario = 65.40M, DescuentoLinea = 0M, DescuentoProducto = 0.30M, AplicarDescuento = true });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = ID_CAMPANA_EN_PLANTILLA, Producto = "40001", Cantidad = 2, PrecioUnitario = 20M, DescuentoLinea = 0M, DescuentoProducto = 0.25M, AplicarDescuento = true });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = ID_MUESTRA, Producto = "44206", Cantidad = 5, PrecioUnitario = 0.57M, DescuentoLinea = 0M, DescuentoProducto = 1M, AplicarDescuento = true });
            pedido.Lineas.Add(new LineaPedidoVentaDTO { id = ID_SIN_APLICAR_DTO, Producto = "38000", Cantidad = 1, PrecioUnitario = 30M, DescuentoLinea = 0.10M, DescuentoProducto = 0M, AplicarDescuento = false });
            return pedido;
        }

        private static PedidoParaPlantillaModel PedidoConDescuentosCompletos()
        {
            PedidoParaPlantillaModel pedido = PedidoParaPlantilla();
            DescuentosLineaPedido.Completar(pedido, PedidoCompleto());
            return pedido;
        }

        private static List<LineaPlantillaVenta> LineasDelBorrador(PedidoParaPlantillaModel pedido)
            => new BorradorPlantillaVentaService(A.Fake<IConfiguracion>()).CrearBorradorDesdePedido(pedido).LineasProducto;

        private static PedidoVentaDTO Enviar(IEnumerable<LineaPlantillaVenta> lineas)
        {
            var estado = new PlantillaVentaState { Empresa = "1", Cliente = "4730", Contacto = "0", AlmacenCodigo = "ALG" };
            estado.LineasProducto = lineas.ToList();
            return estado.ToPedidoVentaDTO("DIR", () => 1, () => "NV");
        }

        // ---------------------------------------------------------------- Completar

        [TestMethod]
        public void Completar_PoneElDescuentoDeProductoDeCadaLineaPorSuId()
        {
            PedidoParaPlantillaModel pedido = PedidoParaPlantilla();
            Assert.IsTrue(DescuentosLineaPedido.FaltaAlguno(pedido));

            int completadas = DescuentosLineaPedido.Completar(pedido, PedidoCompleto());

            Assert.AreEqual(4, completadas);
            Assert.IsFalse(DescuentosLineaPedido.FaltaAlguno(pedido));
            Assert.AreEqual(0.30M, pedido.Lineas.Single(l => l.IdLineaPago == ID_CAMPANA).DescuentoProducto);
            Assert.AreEqual(1M, pedido.Lineas.Single(l => l.IdLineaPago == ID_MUESTRA).DescuentoProducto);
            Assert.AreEqual(0M, pedido.Lineas.Single(l => l.IdLineaPago == ID_SIN_APLICAR_DTO).DescuentoProducto);
        }

        [TestMethod]
        public void Completar_UnaLineaQueNoEstaEnElPedidoSeQuedaSinDescuentoDeProducto()
        {
            PedidoParaPlantillaModel pedido = PedidoParaPlantilla();
            PedidoVentaDTO completo = PedidoCompleto();
            completo.Lineas.Remove(completo.Lineas.Single(l => l.id == ID_MUESTRA));

            DescuentosLineaPedido.Completar(pedido, completo);

            Assert.IsNull(pedido.Lineas.Single(l => l.IdLineaPago == ID_MUESTRA).DescuentoProducto);
        }

        // ---------------------------------------------------------------- Lo que se pinta

        [TestMethod]
        public void CrearBorrador_EnsenaElDescuentoDeProductoYLaBaseDescontada()
        {
            List<LineaPlantillaVenta> lineas = LineasDelBorrador(PedidoConDescuentosCompletos());

            LineaPlantillaVenta campana = lineas.Single(l => l.producto == "44709");
            Assert.AreEqual(0.30M, campana.descuento, "«% Dto.» enseña el 30 % de producto, no el 0 % de línea");
            Assert.AreEqual(0.30M, campana.descuentoProducto);
            Assert.AreEqual(45.78M, campana.baseImponible, "65,40 € al 30 %");

            LineaPlantillaVenta muestra = lineas.Single(l => l.producto == "44206");
            Assert.AreEqual(1M, muestra.descuento);
            Assert.AreEqual(0M, muestra.baseImponible, "la muestra va al 100 %, no a 2,85 €");

            LineaPlantillaVenta sinAplicar = lineas.Single(l => l.producto == "38000");
            Assert.AreEqual(0.10M, sinAplicar.descuento);
            Assert.AreEqual(0M, sinAplicar.descuentoProducto);
            Assert.IsFalse(sinAplicar.aplicarDescuento);
            Assert.AreEqual(27M, sinAplicar.baseImponible);
        }

        [TestMethod]
        public void CrearBorrador_ConDescuentoDeLineaYDeProducto_EnsenaLosDosCompuestosComoElPedido()
        {
            PedidoParaPlantillaModel pedido = PedidoConDescuentosCompletos();
            pedido.Lineas.Add(new LineaParaPlantillaModel { Producto = "45000", Texto = "DOS DESCUENTOS", Cantidad = 1, Precio = 100M, Descuento = 0.05M, DescuentoProducto = 0.30M, AplicarDescuento = true, IdLineaPago = ID_DOS_DESCUENTOS });

            LineaPlantillaVenta linea = LineasDelBorrador(pedido).Single(l => l.producto == "45000");

            var comoElPedido = new LineaPedidoVentaDTO { DescuentoLinea = 0.05M, DescuentoProducto = 0.30M, AplicarDescuento = true };
            Assert.AreEqual(comoElPedido.SumaDescuentos, linea.descuento);
            Assert.AreEqual(0.335M, linea.descuento);
            Assert.AreEqual(66.50M, linea.baseImponible);
        }

        [TestMethod]
        public void CrearBorrador_SinSaberElDescuentoDeProducto_ComoAntes()
        {
            LineaPlantillaVenta campana = LineasDelBorrador(PedidoParaPlantilla()).Single(l => l.producto == "44709");

            Assert.AreEqual(0M, campana.descuento);
            Assert.IsNull(campana.descuentosPedido);
        }

        // ---------------------------------------------------------------- Lo que se guarda

        [TestMethod]
        public void Guardar_LasLineasQueNoSeTocan_VanConSusDescuentosGrabados()
        {
            PedidoVentaDTO pedido = Enviar(LineasDelBorrador(PedidoConDescuentosCompletos()));

            LineaPedidoVentaDTO campana = pedido.Lineas.Single(l => l.id == ID_CAMPANA);
            Assert.AreEqual(0M, campana.DescuentoLinea);
            Assert.AreEqual(0.30M, campana.DescuentoProducto, "Antes iba a 0 y el PUT se lo quitaba a la línea");
            Assert.IsTrue(campana.AplicarDescuento);
            Assert.AreEqual(65.40M, campana.PrecioUnitario);

            LineaPedidoVentaDTO muestra = pedido.Lineas.Single(l => l.id == ID_MUESTRA);
            Assert.AreEqual(0M, muestra.DescuentoLinea);
            Assert.AreEqual(1M, muestra.DescuentoProducto);
            Assert.IsTrue(muestra.AplicarDescuento);

            LineaPedidoVentaDTO sinAplicar = pedido.Lineas.Single(l => l.id == ID_SIN_APLICAR_DTO);
            Assert.AreEqual(0.10M, sinAplicar.DescuentoLinea);
            Assert.AreEqual(0M, sinAplicar.DescuentoProducto);
            Assert.IsFalse(sinAplicar.AplicarDescuento);
        }

        [TestMethod]
        public void Guardar_ConDescuentoDeLineaYDeProducto_NoLosJuntaNiQuitaAplicarDto()
        {
            PedidoParaPlantillaModel pedido = PedidoConDescuentosCompletos();
            pedido.Lineas.Add(new LineaParaPlantillaModel { Producto = "45000", Texto = "DOS DESCUENTOS", Cantidad = 1, Precio = 100M, Descuento = 0.05M, DescuentoProducto = 0.30M, AplicarDescuento = true, IdLineaPago = ID_DOS_DESCUENTOS });

            LineaPedidoVentaDTO linea = Enviar(LineasDelBorrador(pedido)).Lineas.Single(l => l.id == ID_DOS_DESCUENTOS);

            Assert.AreEqual(0.05M, linea.DescuentoLinea);
            Assert.AreEqual(0.30M, linea.DescuentoProducto);
            Assert.IsTrue(linea.AplicarDescuento, "Si no, también perdería el descuento del cliente");
        }

        [TestMethod]
        public void Guardar_SoloCambiaLaCantidad_SigueConSusDescuentos()
        {
            List<LineaPlantillaVenta> lineas = LineasDelBorrador(PedidoConDescuentosCompletos());
            lineas.Single(l => l.producto == "44709").cantidad = 3;

            LineaPedidoVentaDTO campana = Enviar(lineas).Lineas.Single(l => l.id == ID_CAMPANA);

            Assert.AreEqual(3, campana.Cantidad);
            Assert.AreEqual(0.30M, campana.DescuentoProducto);
            Assert.AreEqual(0M, campana.DescuentoLinea);
        }

        [TestMethod]
        public void Guardar_SiSeCambiaElDescuentoAMano_VaComoCualquierLineaDeLaPlantilla()
        {
            List<LineaPlantillaVenta> lineas = LineasDelBorrador(PedidoConDescuentosCompletos());
            lineas.Single(l => l.producto == "44709").descuento = 0.40M;

            LineaPedidoVentaDTO campana = Enviar(lineas).Lineas.Single(l => l.id == ID_CAMPANA);

            Assert.AreEqual(0.40M, campana.DescuentoLinea);
            Assert.AreEqual(0M, campana.DescuentoProducto);
            Assert.IsFalse(campana.AplicarDescuento);
        }

        [TestMethod]
        public void Guardar_SiAlCambiarLaCantidadLlegaOtroPrecio_VaComoCualquierLineaDeLaPlantilla()
        {
            List<LineaPlantillaVenta> lineas = LineasDelBorrador(PedidoConDescuentosCompletos());
            LineaPlantillaVenta campana = lineas.Single(l => l.producto == "44709");
            // Lo que hace OnActualizarPrecioProducto con la respuesta de CargarPrecio (la campaña ha cambiado al 20 %)
            campana.cantidad = 2;
            campana.precio = 66M;
            campana.descuento = LineaPlantillaVenta.DescuentoTrasRecalcular(campana.descuento, campana.descuentoProducto, 0.20M, true);
            campana.descuentoProducto = 0.20M;

            LineaPedidoVentaDTO enviada = Enviar(lineas).Lineas.Single(l => l.id == ID_CAMPANA);

            Assert.AreEqual(66M, enviada.PrecioUnitario);
            Assert.AreEqual(0.20M, enviada.DescuentoProducto);
            Assert.AreEqual(0M, enviada.DescuentoLinea);
        }

        [TestMethod]
        public void Guardar_LineaNuevaAlModificar_ComoEnUnPedidoNuevo()
        {
            List<LineaPlantillaVenta> lineas = LineasDelBorrador(PedidoConDescuentosCompletos());
            lineas.Add(new LineaPlantillaVenta { producto = "44053", texto = "NUEVA", cantidad = 1, precio = 10M, aplicarDescuento = true, descuentoProducto = 0.25M, descuento = 0.25M });

            LineaPedidoVentaDTO nueva = Enviar(lineas).Lineas.Single(l => l.Producto == "44053");

            Assert.AreEqual(0, nueva.id);
            Assert.AreEqual(0.25M, nueva.DescuentoProducto);
            Assert.AreEqual(0M, nueva.DescuentoLinea);
            Assert.IsTrue(nueva.AplicarDescuento);
        }

        [TestMethod]
        public void Guardar_PedidoNuevo_NoCambia()
        {
            var lineas = new List<LineaPlantillaVenta>
            {
                new LineaPlantillaVenta { producto = "1", cantidad = 1, precio = 10M, aplicarDescuento = true, descuentoProducto = 0.25M, descuento = 0.25M },
                new LineaPlantillaVenta { producto = "2", cantidad = 1, precio = 10M, aplicarDescuento = true, descuentoProducto = 0.25M, descuento = 0.40M },
                new LineaPlantillaVenta { producto = "3", cantidad = 1, precio = 10M, aplicarDescuento = true, descuento = 0M }
            };

            PedidoVentaDTO pedido = Enviar(lineas);

            LineaPedidoVentaDTO calculado = pedido.Lineas.Single(l => l.Producto == "1");
            Assert.AreEqual((0M, 0.25M, true), (calculado.DescuentoLinea, calculado.DescuentoProducto, calculado.AplicarDescuento));
            LineaPedidoVentaDTO manual = pedido.Lineas.Single(l => l.Producto == "2");
            Assert.AreEqual((0.40M, 0M, false), (manual.DescuentoLinea, manual.DescuentoProducto, manual.AplicarDescuento));
            LineaPedidoVentaDTO sinDescuento = pedido.Lineas.Single(l => l.Producto == "3");
            Assert.AreEqual((0M, 0M, true), (sinDescuento.DescuentoLinea, sinDescuento.DescuentoProducto, sinDescuento.AplicarDescuento));
        }

        // ---------------------------------------------------------------- «Modificar con plantilla» de punta a punta

        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IPedidoVentaService _pedidosVenta;

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _pedidosVenta = A.Fake<IPedidoVentaService>();
            A.CallTo(() => _servicio.CargarClientesVendedor(A<string>._, A<string>._, A<bool>._))
                .ReturnsLazily((string filtro, string vendedor, bool todos) => Task.FromResult<ICollection<ClienteJson>>(new List<ClienteJson> { ClienteJson(filtro) }));
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", PEDIDO)).ReturnsLazily(() => Task.FromResult(PedidoParaPlantilla()));
            A.CallTo(() => _servicio.CargarListaPendientes(A<string>._, A<string>._)).Returns(Task.FromResult(new List<PedidoPendientePlantilla>()));
            // 40001 ya lo ha comprado: la línea del pedido se vuelca en la de su plantilla
            A.CallTo(() => _servicio.CargarProductosPlantilla(A<ClienteJson>._)).ReturnsLazily(() => Task.FromResult(new ObservableCollection<LineaPlantillaVenta>
            {
                new LineaPlantillaVenta { producto = "40001", texto = "CHAMPU CAMPAÑA 25", precio = 20M, aplicarDescuento = true, cantidadVendida = 12, iva = "G21" }
            }));
            A.CallTo(() => _servicio.PonerStocks(A<ObservableCollection<LineaPlantillaVenta>>._, A<string>._, A<List<string>>._))
                .ReturnsLazily((ObservableCollection<LineaPlantillaVenta> lineas, string almacen, List<string> almacenes) => Task.FromResult(lineas));
            A.CallTo(() => _pedidosVenta.cargarPedido("1", PEDIDO)).ReturnsLazily(() => Task.FromResult(PedidoCompleto()));
        }

        private static ClienteJson ClienteJson(string cliente) =>
            new ClienteJson { empresa = "1", cliente = cliente.Trim(), contacto = "0", iva = "G21", cifNif = "12345678A", nombre = "CLIENTE " + cliente.Trim(), ruta = "00" };

        private PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, _pedidosVenta, new BorradorPlantillaVentaService(configuracion),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm.PaginasWizard = new List<Xceed.Wpf.Toolkit.WizardPage>
            {
                new Xceed.Wpf.Toolkit.WizardPage { Name = "SeleccionCliente" },
                new Xceed.Wpf.Toolkit.WizardPage { Name = "SeleccionProductos" }
            };
            vm.ServicioChequesRegalo = A.Fake<IServicioChequesRegalo>();
            vm.ServicioCupones = A.Fake<IServicioCupones>();
            vm.ServicioRegistroErrores = A.Fake<IServicioRegistroErrores>();
            return vm;
        }

        private static LineaPlantillaVenta Linea(PlantillaVentaViewModel vm, string producto) =>
            vm.ListaFiltrableProductos.ListaOriginal.Cast<LineaPlantillaVenta>().Single(l => l.producto.Trim() == producto);

        [TestMethod]
        public void ModificarConPlantilla_EnsenaLosDescuentosDelPedidoYLosGuardaSinTocarlos() => EnStaConDispatcher(async () =>
        {
            var vm = CrearViewModel();
            await vm.CargarPedidoParaModificarAsync("1", PEDIDO);
            await Task.Delay(2500);

            Assert.AreEqual(PEDIDO, vm.NumeroPedidoEnEdicion);
            Assert.AreEqual(0.30M, Linea(vm, "44709").descuento, "No se vuelve a pedir el precio: se queda el descuento grabado");
            Assert.AreEqual(0.25M, Linea(vm, "40001").descuento, "También en la línea que ya estaba en la plantilla del cliente");
            Assert.AreEqual(1M, Linea(vm, "44206").descuento);
            Assert.AreEqual(0.10M, Linea(vm, "38000").descuento);
            // 45,78 + 30,00 + 0 + 27,00
            Assert.AreEqual(102.78M, vm.baseImponiblePedido);
            Assert.AreEqual(102.78M, vm.baseImponibleParaPortes);

            PedidoVentaDTO pedido = vm.PrepararPedido();

            Assert.AreEqual(0.30M, pedido.Lineas.Single(l => l.id == ID_CAMPANA).DescuentoProducto);
            Assert.AreEqual(0.25M, pedido.Lineas.Single(l => l.id == ID_CAMPANA_EN_PLANTILLA).DescuentoProducto);
            Assert.AreEqual(1M, pedido.Lineas.Single(l => l.id == ID_MUESTRA).DescuentoProducto);
            LineaPedidoVentaDTO sinAplicar = pedido.Lineas.Single(l => l.id == ID_SIN_APLICAR_DTO);
            Assert.AreEqual((0.10M, 0M, false), (sinAplicar.DescuentoLinea, sinAplicar.DescuentoProducto, sinAplicar.AplicarDescuento));
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
        });

        [TestMethod]
        public void ModificarConPlantilla_SiNoSePuedenLeerLosDescuentos_NoSeCargaElPedido() => EnStaConDispatcher(async () =>
        {
            A.CallTo(() => _pedidosVenta.cargarPedido("1", PEDIDO)).ThrowsAsync(new Exception("Sin conexión"));
            var vm = CrearViewModel();

            await vm.CargarPedidoParaModificarAsync("1", PEDIDO);

            Assert.IsNull(vm.NumeroPedidoEnEdicion);
            A.CallTo(() => _dialogos.ShowError(A<string>.That.Contains("descuentos"))).MustHaveHappenedOnceExactly();
        });

        private static void EnStaConDispatcher(Func<Task> prueba)
        {
            Exception error = null;
            var hilo = new System.Threading.Thread(() =>
            {
                System.Windows.Threading.Dispatcher dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                System.Threading.SynchronizationContext.SetSynchronizationContext(new System.Windows.Threading.DispatcherSynchronizationContext(dispatcher));
                _ = dispatcher.InvokeAsync(async () =>
                {
                    try
                    {
                        await prueba();
                    }
                    catch (Exception ex)
                    {
                        error = ex;
                    }
                    finally
                    {
                        dispatcher.BeginInvokeShutdown(System.Windows.Threading.DispatcherPriority.Background);
                    }
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
