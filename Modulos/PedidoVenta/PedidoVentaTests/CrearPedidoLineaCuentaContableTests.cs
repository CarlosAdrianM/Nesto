using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Novedad 1453 (Laura, 26/11/26, pedido 931726): al crear un pedido con una línea de cuenta contable salía «Collection was
    /// modified; enumeration operation may not execute» con el pedido YA creado, y el siguiente guardado lo dejó en presupuesto.
    /// Causa: otro detalle con un pedido nuevo recibía el «pedido creado» y lo envolvía; envolver líneas sin Nº de orden las
    /// volvía a insertar en la colección que se estaba recorriendo. La pantalla que lo creaba se quedaba sin adoptar el pedido
    /// guardado (líneas con id 0) y el PUT siguiente no llevaba la línea guardada.
    /// </summary>
    [TestClass]
    public class CrearPedidoLineaCuentaContableTests
    {
        private static LineaPedidoVentaDTO LineaCuenta() => new LineaPedidoVentaDTO
        {
            id = 0,
            tipoLinea = 2,
            Producto = "70500009",
            texto = "Ingresos Sept. 26 YouTube",
            Cantidad = 1,
            PrecioUnitario = 360.55M,
            iva = "G21",
            almacen = "ALG",
            fechaEntrega = DateTime.Today,
            estado = 1
        };

        private static PedidoVentaDTO PedidoNuevo() => new PedidoVentaDTO
        {
            empresa = "1",
            numero = 0,
            cliente = "32203",
            contacto = "0",
            iva = "EX",
            formaPago = "TRN",
            plazosPago = "CONTADO",
            Lineas = new ObservableCollection<LineaPedidoVentaDTO> { LineaCuenta() }
        };

        private static DetallePedidoViewModel Detalle(IPedidoVentaService servicio, IMessenger messenger, IServicioDialogos dialogos)
        {
            return new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, messenger,
                dialogos, A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
        }

        private static IPedidoVentaService ServicioQueCrea(int numero)
        {
            IPedidoVentaService servicio = A.Fake<IPedidoVentaService>();
            A.CallTo(() => servicio.ConsultarAvisoCampanaSoloEnStock(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new AvisoCampanaSoloEnStockRespuesta { HayAviso = false }));
            A.CallTo(() => servicio.ConsultarAvisoCondicionesCampana(A<PedidoVentaDTO>._)).Returns(Task.FromResult(new AvisoCondicionesCampanaRespuesta { Avisar = false }));
            A.CallTo(() => servicio.CrearPedido(A<PedidoVentaDTO>._)).Returns(Task.FromResult(numero));
            return servicio;
        }

        [TestMethod]
        public void Envolver_UnPedidoConLineasSinNumeroDeOrden_NoLanzaNiDuplicaLasLineas()
        {
            PedidoVentaDTO pedido = PedidoNuevo();
            var lineas = new List<LineaPedidoVentaDTO>(pedido.Lineas) { new LineaPedidoVentaDTO { id = 0, tipoLinea = 1, Producto = "17404", Cantidad = 2 } };
            pedido.Lineas = new ObservableCollection<LineaPedidoVentaDTO>(lineas);

            var wrapper = new PedidoVentaWrapper(pedido);

            Assert.AreEqual(2, pedido.Lineas.Count);
            Assert.AreEqual(2, wrapper.Lineas.Count);
            Assert.AreSame(lineas[0], wrapper.Lineas[0].Model);
            Assert.AreSame(lineas[1], wrapper.Lineas[1].Model);
        }

        [TestMethod]
        public void AnadirUnaLineaNuevaEnPantalla_SigueEntrandoEnElModelo()
        {
            PedidoVentaDTO pedido = PedidoNuevo();
            var wrapper = new PedidoVentaWrapper(pedido);
            var nueva = new LineaPedidoVentaDTO { id = 0, tipoLinea = 1, Producto = "17404", Cantidad = 1 };

            wrapper.Lineas.Add(new LineaPedidoVentaWrapper(nueva));

            Assert.AreEqual(2, pedido.Lineas.Count);
            Assert.IsTrue(pedido.Lineas.Contains(nueva));
        }

        [TestMethod]
        public async Task CrearPedido_ConOtroDetalleConPedidoNuevoAbierto_NoFallaYAmbosConservanSuPedido()
        {
            var messenger = new WeakReferenceMessenger();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            IPedidoVentaService servicio = ServicioQueCrea(931726);
            DetallePedidoViewModel otro = Detalle(servicio, messenger, dialogos);
            PedidoVentaDTO pedidoDelOtro = new PedidoVentaDTO { empresa = "1", numero = 0, Lineas = new ObservableCollection<LineaPedidoVentaDTO>() };
            otro.pedido = new PedidoVentaWrapper(pedidoDelOtro);
            DetallePedidoViewModel vm = Detalle(servicio, messenger, dialogos);
            PedidoVentaDTO pedido = PedidoNuevo();
            vm.pedido = new PedidoVentaWrapper(pedido);

            await vm.ModificarPedidoAsync();

            Assert.AreEqual(931726, vm.pedido.numero);
            Assert.AreEqual(1, vm.pedido.Model.Lineas.Count, "La línea no se duplica en el modelo");
            Assert.AreSame(pedidoDelOtro, otro.pedido.Model, "El otro detalle sigue con su pedido nuevo");
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            GC.KeepAlive(otro);
        }

        [TestMethod]
        public async Task CrearPedido_SiFallaLoDeLaPantallaTrasCrear_LoDiceYVuelveALeerElPedido()
        {
            var messenger = new WeakReferenceMessenger();
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            IPedidoVentaService servicio = ServicioQueCrea(931726);
            DetallePedidoViewModel vm = Detalle(servicio, messenger, dialogos);
            vm.pedido = new PedidoVentaWrapper(PedidoNuevo());
            object receptor = new object();
            messenger.Register<PedidoCreadoMensaje>(receptor, (r, m) => throw new InvalidOperationException("Collection was modified; enumeration operation may not execute."));

            await vm.ModificarPedidoAsync();

            A.CallTo(() => dialogos.ShowError(A<string>.That.StartsWith("El pedido 931726 se ha creado, pero"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.cargarPedido("1", 931726)).MustHaveHappened();
            GC.KeepAlive(receptor);
        }

        [TestMethod]
        public void TextoFalloAlTerminarDeCrear_DiceQueEstaCreadoYElDetalle()
        {
            string texto = DetallePedidoViewModel.TextoFalloAlTerminarDeCrear(931726, "Collection was modified");

            StringAssert.StartsWith(texto, "El pedido 931726 se ha creado, pero la pantalla no ha podido terminar de ponerse al día.");
            StringAssert.Contains(texto, "Detalle: Collection was modified");
        }
    }
}
