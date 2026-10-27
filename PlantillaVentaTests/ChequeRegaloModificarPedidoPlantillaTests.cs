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
    /// Novedad 749: al modificar un pedido con la plantilla («Modificar con plantilla» o el aviso de pendientes) no salía el
    /// recuadro del cheque regalo ni el del cupón: el cliente se elige por SeleccionarClienteParaBorradorAsync, que no los
    /// pedía (y, si el cliente ya estaba elegido, la clave coincidía y tampoco se volvían a pedir con el pedido en edición).
    /// </summary>
    [TestClass]
    public class ChequeRegaloModificarPedidoPlantillaTests
    {
        private const int PEDIDO = 929116;

        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IServicioChequesRegalo _cheques;
        private IServicioCupones _cupones;
        private Dictionary<string, ChequeRegaloClienteDTO> _chequePorCliente;
        private Dictionary<string, List<CuponDTO>> _cuponesPorCliente;

        private static ChequeRegaloClienteDTO Cheque(string cliente, string estado = ChequeRegaloClienteDTO.ESTADO_DISPONIBLE, int? pedidoCanje = null) => new ChequeRegaloClienteDTO
        {
            Campana = "CHEQUE50_OCT_2026",
            Empresa = "1",
            Cliente = cliente,
            Producto = "CHEQUE50_OCT26",
            Importe = 50M,
            MinimoCanje = 250M,
            CanjeHasta = new DateTime(2026, 11, 7),
            PrefijosNombreExcluidosMinimo = new List<string> { "PACK 26" },
            GruposExcluidosMinimo = new List<string> { "PEL" },
            Estado = estado,
            SePuedeUsar = estado == ChequeRegaloClienteDTO.ESTADO_DISPONIBLE,
            PedidoCanje = pedidoCanje,
            Mensaje = estado == ChequeRegaloClienteDTO.ESTADO_DISPONIBLE
                ? "Tiene un cheque regalo de 50,00 € + IVA para un pedido de más de 250,00 € de producto, hasta el 07/11/2026."
                : $"El cheque regalo ya está aplicado en el pedido {pedidoCanje}.",
            TextoLinea = "Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)"
        };

        private static CuponDTO Cupon(string cliente, string estado = "Activo", int? usadoEnPedido = null) => new CuponDTO
        {
            Id = 7,
            Codigo = "EV-2610-7K3Q",
            Empresa = "1",
            Cliente = cliente,
            Importe = 50M,
            PedidoMinimo = 100M,
            Origen = "Evento",
            ValidoDesde = new DateTime(2026, 10, 1),
            Caducidad = new DateTime(2027, 4, 30),
            Estado = estado,
            SePuedeUsar = estado == "Activo",
            UsadoEnPedido = usadoEnPedido,
            Producto = "CUPONEVENTO",
            TextoLinea = "Cupón EV-2610-7K3Q",
            Mensaje = "Tiene un cupón de 50,00 € (EV-2610-7K3Q) para un pedido de al menos 100,00 € de productos."
        };

        /// <summary>El pedido 929116 en forma de plantilla: 5 × 30 € = 150 € de producto computable.</summary>
        private static PedidoParaPlantillaModel Pedido(string cliente) => new PedidoParaPlantillaModel
        {
            Empresa = "1",
            Cliente = cliente,
            Contacto = "0",
            NumeroPedido = PEDIDO,
            FormaPago = "RCB",
            PlazosPago = "CONTADO",
            FechaEntrega = new DateTime(2026, 10, 23),
            Almacen = "ALG",
            Lineas = new List<LineaParaPlantillaModel>
            {
                new LineaParaPlantillaModel { Producto = "12345", Texto = "CREMA MASAJE 1L", Cantidad = 5, Precio = 30M, IdLineaPago = 101 }
            },
            Regalos = new List<RegaloParaPlantillaModel>()
        };

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _cheques = A.Fake<IServicioChequesRegalo>();
            _cupones = A.Fake<IServicioCupones>();
            _chequePorCliente = new Dictionary<string, ChequeRegaloClienteDTO>();
            _cuponesPorCliente = new Dictionary<string, List<CuponDTO>>();
            A.CallTo(() => _cheques.LeerDelCliente(A<string>._, A<string>._))
                .ReturnsLazily((string empresa, string cliente) => Task.FromResult(_chequePorCliente.TryGetValue(cliente.Trim(), out var c) ? c : null));
            A.CallTo(() => _cupones.LeerDelCliente(A<string>._, A<string>._, A<bool>._))
                .ReturnsLazily((string empresa, string cliente, bool todos) => Task.FromResult(_cuponesPorCliente.TryGetValue(cliente.Trim(), out var c) ? c : new List<CuponDTO>()));
            A.CallTo(() => _servicio.CargarClientesVendedor(A<string>._, A<string>._, A<bool>._))
                .ReturnsLazily((string filtro, string vendedor, bool todos) => Task.FromResult<ICollection<ClienteJson>>(new List<ClienteJson> { ClienteJson(filtro) }));
            A.CallTo(() => _servicio.CargarPedidoParaPlantilla("1", PEDIDO)).ReturnsLazily(() => Task.FromResult(Pedido("4730")));
            A.CallTo(() => _servicio.CargarListaPendientes(A<string>._, A<string>._)).Returns(Task.FromResult(new List<PedidoPendientePlantilla>()));
            A.CallTo(() => _servicio.CargarProductosPlantilla(A<ClienteJson>._)).ReturnsLazily(() => Task.FromResult(new ObservableCollection<LineaPlantillaVenta>()));
            A.CallTo(() => _servicio.PonerStocks(A<ObservableCollection<LineaPlantillaVenta>>._, A<string>._, A<List<string>>._))
                .ReturnsLazily((ObservableCollection<LineaPlantillaVenta> lineas, string almacen, List<string> almacenes) => Task.FromResult(lineas));
        }

        private static ClienteJson ClienteJson(string cliente) =>
            new ClienteJson { empresa = "1", cliente = cliente.Trim(), contacto = "0", iva = "G21", cifNif = "12345678A", nombre = "CLIENTE " + cliente.Trim() };

        private PlantillaVentaViewModel CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            IPedidoVentaService servicioPedidos = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicioPedidos.cargarPedido(A<string>._, A<int>._))
                .ReturnsLazily((string empresa, int numero) => Task.FromResult(new PedidoVentaDTO { empresa = empresa, numero = numero, fecha = new DateTime(2026, 10, 21) }));
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, servicioPedidos, new BorradorPlantillaVentaService(configuracion),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            // Las páginas del wizard son controles (WizardPage): por eso las pruebas van en un hilo STA con su Dispatcher
            vm.PaginasWizard = new List<Xceed.Wpf.Toolkit.WizardPage>
            {
                new Xceed.Wpf.Toolkit.WizardPage { Name = "SeleccionCliente" },
                new Xceed.Wpf.Toolkit.WizardPage { Name = "SeleccionProductos" }
            };
            vm.ServicioChequesRegalo = _cheques;
            vm.ServicioCupones = _cupones;
            return vm;
        }

        private void DialogoEligeModificar(int pedido)
        {
            A.CallTo(() => _dialogos.ShowDialogAsync(ElegirPedidoPendienteDialogViewModel.NOMBRE, A<ParametrosDialogo>.Ignored))
                .ReturnsLazily((string nombre, ParametrosDialogo parametros) =>
                {
                    var dialogo = new ElegirPedidoPendienteDialogViewModel();
                    dialogo.OnDialogOpened(parametros);
                    ResultadoDialogo resultado = null;
                    dialogo.RequestClose += r => resultado = r;
                    dialogo.ElegirCommand.Execute(dialogo.Opciones.Find(o => o.Numero == pedido));
                    return Task.FromResult(resultado);
                });
        }

        [TestMethod]
        public void ModificarConPlantilla_ClienteConCheque_SeVeElRecuadroYLoQueFaltaConTodoElPedido() => EnStaConDispatcher(async () =>
        {
            _chequePorCliente["4730"] = Cheque("4730");
            var vm = CrearViewModel();

            await vm.CargarPedidoParaModificarAsync("1", PEDIDO);

            Assert.AreEqual(PEDIDO, vm.NumeroPedidoEnEdicion);
            Assert.AreEqual("4730", vm.clienteSeleccionado?.cliente);
            Assert.IsTrue(vm.ChequeRegalo.HayCheque, "Sin el cheque no sale el recuadro amarillo");
            StringAssert.Contains(vm.ChequeRegalo.Mensaje, "Tiene un cheque regalo de 50,00 €");
            Assert.IsTrue(vm.PuedeUsarChequeRegalo, "Sale la casilla");
            Assert.AreEqual(150M, vm.BaseComputableChequeRegalo, "Cuenta todo el pedido cargado");
            Assert.IsTrue(vm.HayTextoFaltaChequeRegalo);
            StringAssert.StartsWith(vm.TextoFaltaChequeRegalo, "Faltan 100,01 €");
        });

        [TestMethod]
        public void ModificarConPlantilla_ChequeYaEnEsePedido_SaleLaCasillaMarcada() => EnStaConDispatcher(async () =>
        {
            _chequePorCliente["4730"] = Cheque("4730", ChequeRegaloClienteDTO.ESTADO_EN_PEDIDO, PEDIDO);
            var vm = CrearViewModel();

            await vm.CargarPedidoParaModificarAsync("1", PEDIDO);

            Assert.IsTrue(vm.ChequeRegalo.HayCheque);
            Assert.IsTrue(vm.ChequeEstaEnElPedidoEnEdicion);
            Assert.IsTrue(vm.PuedeUsarChequeRegalo);
            Assert.IsTrue(vm.UsarChequeRegalo, "Para que el PUT no lo suelte");
        });

        [TestMethod]
        public void AvisoDePendientes_ModificarConElClienteYaElegido_VuelveAPedirElChequeConElPedidoEnEdicion() => EnStaConDispatcher(async () =>
        {
            // El cheque se pidió al elegir el cliente (pedido nuevo): estaba en el pedido 929116, sin casilla
            _chequePorCliente["4730"] = Cheque("4730", ChequeRegaloClienteDTO.ESTADO_EN_PEDIDO, PEDIDO);
            var vm = CrearViewModel();
            vm._clienteSeleccionado = ClienteJson("4730");
            await vm.CargarChequeRegaloAsync();
            Assert.IsFalse(vm.PuedeUsarChequeRegalo);
            A.CallTo(() => _servicio.CargarListaPendientes("1", "4730")).Returns(Task.FromResult(new List<PedidoPendientePlantilla> { new PedidoPendientePlantilla { Numero = PEDIDO } }));
            DialogoEligeModificar(PEDIDO);

            await vm.ComprobarPendientesAsync();

            Assert.AreEqual(PEDIDO, vm.NumeroPedidoEnEdicion);
            A.CallTo(() => _cheques.LeerDelCliente("1", "4730")).MustHaveHappenedTwiceExactly();
            Assert.IsTrue(vm.ChequeRegalo.HayCheque);
            Assert.IsTrue(vm.PuedeUsarChequeRegalo, "Ahora es el pedido en edición: sale la casilla");
            Assert.IsTrue(vm.UsarChequeRegalo);
            Assert.AreEqual(150M, vm.BaseComputableChequeRegalo);
        });

        [TestMethod]
        public void AvisoDePendientes_ClienteConChequeDisponible_SeVeElRecuadroYLoQueFalta() => EnStaConDispatcher(async () =>
        {
            _chequePorCliente["4730"] = Cheque("4730");
            var vm = CrearViewModel();
            vm._clienteSeleccionado = ClienteJson("4730");
            A.CallTo(() => _servicio.CargarListaPendientes("1", "4730")).Returns(Task.FromResult(new List<PedidoPendientePlantilla> { new PedidoPendientePlantilla { Numero = PEDIDO } }));
            DialogoEligeModificar(PEDIDO);

            await vm.ComprobarPendientesAsync();

            Assert.AreEqual(PEDIDO, vm.NumeroPedidoEnEdicion);
            Assert.IsTrue(vm.ChequeRegalo.HayCheque);
            Assert.IsTrue(vm.PuedeUsarChequeRegalo);
            StringAssert.StartsWith(vm.TextoFaltaChequeRegalo, "Faltan 100,01 €");
        });

        [TestMethod]
        public void ModificarConPlantilla_ElClienteDeAntesNoDejaSuCheque() => EnStaConDispatcher(async () =>
        {
            _chequePorCliente["15191"] = Cheque("15191");
            var vm = CrearViewModel();
            vm._clienteSeleccionado = ClienteJson("15191");
            await vm.CargarChequeRegaloAsync();
            Assert.IsTrue(vm.ChequeRegalo.HayCheque);

            await vm.CargarPedidoParaModificarAsync("1", PEDIDO); // el pedido es del 4730, sin cheque

            Assert.AreEqual("4730", vm.clienteSeleccionado?.cliente);
            Assert.IsFalse(vm.ChequeRegalo.HayCheque, "El cheque del 15191 no es del 4730");
            Assert.IsFalse(vm.PuedeUsarChequeRegalo);
            Assert.IsFalse(vm.UsarChequeRegalo);
        });

        [TestMethod]
        public void ModificarConPlantilla_CuponYaEnEsePedido_PideTodosYSaleMarcado() => EnStaConDispatcher(async () =>
        {
            _cuponesPorCliente["4730"] = new List<CuponDTO> { Cupon("4730", "Usado", PEDIDO) };
            var vm = CrearViewModel();

            await vm.CargarPedidoParaModificarAsync("1", PEDIDO);

            A.CallTo(() => _cupones.LeerDelCliente("1", "4730", true)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Cupon.HayCupon);
            Assert.IsTrue(vm.PuedeUsarCupon);
            Assert.IsTrue(vm.UsarCupon, "Para que el PUT no lo suelte");
        });

        [TestMethod]
        public void ModificarConPlantilla_ElClienteDeAntesNoDejaSuCupon() => EnStaConDispatcher(async () =>
        {
            _cuponesPorCliente["15191"] = new List<CuponDTO> { Cupon("15191") };
            var vm = CrearViewModel();
            vm._clienteSeleccionado = ClienteJson("15191");
            await vm.CargarCuponAsync();
            Assert.IsTrue(vm.Cupon.HayCupon);

            await vm.CargarPedidoParaModificarAsync("1", PEDIDO);

            Assert.IsFalse(vm.Cupon.HayCupon, "El cupón del 15191 no es del 4730");
            Assert.IsFalse(vm.PuedeUsarCupon);
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
