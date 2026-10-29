using System;
using System.Collections.ObjectModel;
using System.Reflection;
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
    /// Sugerencia 864 (Paloma): al terminar la plantilla de un pedido que se paga por transferencia (PRE o CONTADO), se
    /// ofrecen los datos para que el cliente la haga, como se ofrece el enlace de pago con tarjeta.
    /// </summary>
    [TestClass]
    public class DatosTransferenciaPlantillaTests
    {
        private sealed class PortapapelesDePrueba : IPortapapelesTexto
        {
            public string Texto { get; private set; }
            public void CopiarTexto(string texto) => Texto = texto;
        }

        private IServicioDialogos _dialogos;
        private IPedidoVentaService _pedidos;

        private PlantillaVentaViewModel CrearViewModel()
        {
            _dialogos = A.Fake<IServicioDialogos>();
            _pedidos = A.Fake<IPedidoVentaService>();
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion,
                A.Fake<IPlantillaVentaService>(), new WeakReferenceMessenger(), _dialogos, _pedidos,
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "29606", contacto = "0", iva = "G21", cifNif = "12345678A" };
            return vm;
        }

        private static void PonerPago(PlantillaVentaViewModel vm, string forma, string plazos)
        {
            typeof(PlantillaVentaViewModel).GetField("_formaPagoSeleccionada", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new FormaPago { formaPago = forma });
            typeof(PlantillaVentaViewModel).GetField("_plazoPagoSeleccionado", BindingFlags.NonPublic | BindingFlags.Instance)
                .SetValue(vm, new PlazosPago { plazoPago = plazos });
        }

        [TestMethod]
        public void TransferenciaPrepagoOAlContado_SeOfreceMarcado_YConOtraFormaNo()
        {
            var vm = CrearViewModel();

            PonerPago(vm, "TRN", "PRE");
            Assert.IsTrue(vm.EsTransferencia);
            Assert.IsTrue(vm.CopiarDatosTransferencia, "Marcado de entrada");

            PonerPago(vm, "TRN", "CONTADO");
            Assert.IsTrue(vm.CopiarDatosTransferencia);

            PonerPago(vm, "TRN", "1/30");
            Assert.IsFalse(vm.EsTransferencia);
            Assert.IsFalse(vm.CopiarDatosTransferencia);

            PonerPago(vm, "TAR", "PRE");
            Assert.IsFalse(vm.CopiarDatosTransferencia);
        }

        [TestMethod]
        public void SiLoDesmarca_NoSeCopia()
        {
            var vm = CrearViewModel();
            PonerPago(vm, "TRN", "CONTADO");

            vm.CopiarDatosTransferencia = false;

            Assert.IsFalse(vm.CopiarDatosTransferencia);
        }

        [TestMethod]
        public async Task AlTerminar_CopiaYEnsenaLosDatosDeLaApi()
        {
            var vm = CrearViewModel();
            var portapapeles = new PortapapelesDePrueba();
            vm.PortapapelesTransferencia = portapapeles;
            const string texto = "IBAN: ES00 0000\r\nBeneficiario: NUEVA VISION\r\nConcepto: Cliente 29606 - Pedido 927160\r\nImporte: 120,00 €";
            A.CallTo(() => _pedidos.LeerDatosTransferencia("1", 927160)).Returns(new DatosTransferenciaPedidoModel { Texto = texto });

            string copiado = await vm.CopiarDatosTransferenciaAlTerminarAsync("1", "927160");

            Assert.AreEqual(texto, copiado);
            Assert.AreEqual(texto, portapapeles.Texto);
            A.CallTo(() => _dialogos.ShowNotification(DatosTransferenciaPedido.TITULO, A<string>.That.Contains("Concepto: Cliente 29606 - Pedido 927160"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AlTerminar_SiLaApiFalla_LoDiceSinRomperNada()
        {
            var vm = CrearViewModel();
            vm.PortapapelesTransferencia = new PortapapelesDePrueba();
            A.CallTo(() => _pedidos.LeerDatosTransferencia("1", 927160)).ThrowsAsync(new Exception("La empresa 1 no tiene ninguna cuenta bancaria para recibir transferencias."));

            string copiado = await vm.CopiarDatosTransferenciaAlTerminarAsync("1", "927160");

            Assert.IsNull(copiado);
            A.CallTo(() => _dialogos.ShowError(A<string>.That.Contains("se ha guardado, pero no se han podido copiar"))).MustHaveHappenedOnceExactly();
        }
    }
}
