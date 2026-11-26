using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;

namespace PedidoVentaTests
{
    /// <summary>
    /// Carlos 25/11/26: un pedido con el IVA de la cabecera vacío no tiene documentos descargables (la API da 403). En el detalle
    /// no se ofrecen descargar el PDF ni enviarlo por correo o WhatsApp, sin decir por qué.
    /// </summary>
    [TestClass]
    public class DocumentosPedidoSinIvaTests
    {
        private static DetallePedidoViewModel Vm(string iva)
        {
            var vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), A.Fake<IPedidoVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IAbridorPedidos>(), A.Fake<IServicioAutenticacion>());
            vm.ServicioFechaEntregaAgencia = A.Fake<IServicioFechaEntregaAgencia>();
            vm.pedido = new PedidoVentaWrapper(new PedidoVentaDTO
            {
                empresa = "1",
                numero = 931500,
                cliente = "15191",
                contacto = "0",
                iva = iva,
                Lineas = new List<LineaPedidoVentaDTO>()
            });
            return vm;
        }

        [TestMethod]
        public void SinIva_NoSeOfrecenDescargarNiEnviar()
        {
            foreach (string iva in new[] { null, "", "   " })
            {
                DetallePedidoViewModel vm = Vm(iva);

                Assert.IsFalse(vm.TieneDocumentosDescargables);
                Assert.IsFalse(vm.DescargarPresupuestoCommand.CanExecute(null));
                Assert.IsFalse(vm.EnviarPedidoPorCorreoCommand.CanExecute(null));
                Assert.IsFalse(vm.EnviarPedidoPorWhatsAppCommand.CanExecute(null));
            }
        }

        [TestMethod]
        public void ConIva_ComoSiempre()
        {
            DetallePedidoViewModel vm = Vm("G21");

            Assert.IsTrue(vm.TieneDocumentosDescargables);
            Assert.IsTrue(vm.DescargarPresupuestoCommand.CanExecute(null));
            Assert.IsTrue(vm.EnviarPedidoPorCorreoCommand.CanExecute(null));
            Assert.IsTrue(vm.EnviarPedidoPorWhatsAppCommand.CanExecute(null));
        }
    }
}
