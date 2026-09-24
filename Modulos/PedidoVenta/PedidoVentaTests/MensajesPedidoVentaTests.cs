using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity;
using static Nesto.Modulos.PedidoVenta.PedidoVentaModel;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.1): los avisos entre pantallas de pedidos de venta van por el IMessenger de
    /// CommunityToolkit (antes IEventAggregator de Prism, todos con ThreadOption.PublisherThread).
    /// Cada test comprueba que el mensaje llega de verdad al receptor, con un messenger real.
    /// </summary>
    [TestClass]
    public class MensajesPedidoVentaTests
    {
        private IMessenger _messenger;
        private IPedidoVentaService _servicio;

        [TestInitialize]
        public void Initialize()
        {
            _messenger = new WeakReferenceMessenger();
            _servicio = A.Fake<IPedidoVentaService>();
        }

        private DetallePedidoViewModel CrearDetalle()
        {
            return new DetallePedidoViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), _servicio,
                _messenger, A.Fake<IDialogService>(), A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
        }

        private ListaPedidosVentaViewModel CrearLista()
        {
            return new ListaPedidosVentaViewModel(A.Fake<IConfiguracion>(), _servicio, _messenger,
                A.Fake<IDialogService>(), A.Fake<IRegionManager>());
        }

        [TestMethod]
        public void SacarPicking_LlegaAlDetalle_YEsteAvisaDeQueElPedidoHaCambiado()
        {
            var vm = CrearDetalle();
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 123 };
            vm.pedido = new PedidoVentaWrapper(pedido);
            var recibidos = new List<PedidoVentaDTO>();
            _messenger.Register<PedidoModificadoMensaje>(this, (r, m) => recibidos.Add(m.Value));

            _messenger.Send(new SacarPickingMensaje(1));

            Assert.AreEqual(1, recibidos.Count);
            Assert.AreSame(pedido, recibidos[0]);
        }

        [TestMethod]
        public void PedidoCreado_LlegaAlDetalle_YSustituyeElPedidoNuevo()
        {
            var vm = CrearDetalle();
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = 0 });
            var creado = new PedidoVentaDTO { empresa = "1", numero = 987654 };

            _messenger.Send(new PedidoCreadoMensaje(new PedidoCreadoEventArgs { Pedido = creado }));

            Assert.AreEqual(987654, vm.pedido.numero);
            Assert.AreEqual("Pedido Venta (987654)", vm.Titulo);
        }

        [TestMethod]
        public void ProductoSeleccionado_LlegaAlDetalle_YLoPoneEnLaLineaActual()
        {
            A.CallTo(() => _servicio.cargarProducto(A<string>._, A<string>._, A<string>._, A<string>._, A<short>._))
                .Returns(Task.FromResult(new Producto { producto = "17404", nombre = "PRODUCTO DE PRUEBA", precio = 10m }));
            var vm = CrearDetalle();
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", cliente = "15191", contacto = "0" });
            vm.lineaActual = new LineaPedidoVentaWrapper(new LineaPedidoVentaDTO { Cantidad = 2 });

            _messenger.Send(new ProductoSeleccionadoMensaje("17404"));

            Assert.AreEqual("17404", vm.lineaActual.Producto);
            Assert.AreEqual("PRODUCTO DE PRUEBA", vm.lineaActual.texto);
            A.CallTo(() => _servicio.cargarProducto("1", "17404", "15191", "0", A<short>._)).MustHaveHappened();
        }

        [TestMethod]
        public void PedidoModificado_LlegaALaLista_YActualizaElResumenSeleccionado()
        {
            var vm = CrearLista();
            var resumen = new ResumenPedido { empresa = "1", numero = 555, contacto = "0" };
            vm.ListaPedidos.ElementoSeleccionado = resumen;

            _messenger.Send(new PedidoModificadoMensaje(new PedidoVentaDTO { empresa = "1", numero = 555, contacto = "2", cliente = "15191", fecha = System.DateTime.Today }));

            Assert.AreEqual("2", resumen.contacto);
            Assert.AreEqual("15191", resumen.cliente);
        }

        [TestMethod]
        public void PedidoCreado_LlegaALaLista_YActualizaElResumenEnCreacion()
        {
            var vm = CrearLista();
            var resumen = new ResumenPedido { empresa = "1", numero = 0, esNuevo = true };
            vm.ListaPedidos.ElementoSeleccionado = resumen;

            _messenger.Send(new PedidoCreadoMensaje(new PedidoCreadoEventArgs
            {
                Pedido = new PedidoVentaDTO { empresa = "1", numero = 987654, cliente = "15191", fecha = System.DateTime.Today },
                NombreCliente = "Cliente de prueba"
            }));

            Assert.AreEqual(987654, resumen.numero);
            Assert.AreEqual("Cliente de prueba", resumen.nombre);
            Assert.IsFalse(resumen.esNuevo);
        }

        [TestMethod]
        public void SacarPicking_ElPopupLoEnviaAlTerminar()
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(true);
            var informes = A.Fake<IInformesService>();
            A.CallTo(() => informes.LeerUltimoPicking()).Returns(Task.FromResult(42));
            var vm = new PickingPopupViewModel(_servicio, _messenger, A.Fake<IDialogService>(), configuracion, informes)
            {
                esPickingRutas = true
            };
            int recibidos = 0;
            _messenger.Register<SacarPickingMensaje>(this, (r, m) => recibidos++);

            vm.cmdSacarPicking.Execute(null);

            Assert.AreEqual(1, recibidos);
        }
    }
}
