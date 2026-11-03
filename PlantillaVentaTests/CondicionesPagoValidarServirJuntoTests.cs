using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services.ServirJunto;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#595 (Manuel, pedido 929816, cliente 40195 RCB al contado con CCC, 03/11/26): al cambiar el modo de
    /// servicio salió el aviso de comisión por reembolso. La plantilla validaba con la forma y los plazos de pago
    /// de Estado, que aún no estaban rellenos (se rellenan en la pantalla de pago). Ahora manda las condiciones
    /// efectivas: las elegidas y, si no, las de la ficha del contacto.
    /// </summary>
    [TestClass]
    public class CondicionesPagoValidarServirJuntoTests
    {
        private IServirJuntoService _servirJunto;
        private string _formaPagoEnviada;
        private string _plazosPagoEnviados;
        private string _cccEnviado;
        private string _periodoEnviado;
        private int _llamadas;

        [TestInitialize]
        public void Inicializar()
        {
            _servirJunto = A.Fake<IServirJuntoService>();
            _llamadas = 0;
            A.CallTo(() => _servirJunto.Validar(A<string>._, A<List<ProductoBonificadoConCantidadRequest>>._,
                    A<List<ProductoBonificadoConCantidadRequest>>._, A<string>._, A<string>._, A<string>._, A<string>._,
                    A<bool?>._, A<List<LineaPortesServirJuntoDTO>>._, A<int?>._, A<byte?>._))
                .Invokes(c =>
                {
                    _llamadas++;
                    _formaPagoEnviada = c.GetArgument<string>(3);
                    _plazosPagoEnviados = c.GetArgument<string>(4);
                    _cccEnviado = c.GetArgument<string>(5);
                    _periodoEnviado = c.GetArgument<string>(6);
                })
                .Returns(Task.FromResult(new ValidarServirJuntoResponse
                {
                    PuedeDesmarcar = true,
                    ProductosProblematicos = new List<ProductoSinStockDTO>()
                }));
        }

        private PlantillaVentaViewModel CrearViewModel(DireccionesEntregaCliente contacto)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(),
                A.Fake<IBorradorPlantillaVentaService>(), A.Fake<IServicioAutenticacion>());
            vm.ServicioServirJunto = _servirJunto;
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>(new[]
            {
                new LineaPlantillaVenta { producto = "12345", texto = "CHAMPU", cantidad = 1, precio = 10M }
            });
            if (contacto != null)
            {
                vm.direccionEntregaSeleccionada = contacto;
            }
            vm.ModoServicio = ModosServicio.TODO_JUNTO;
            return vm;
        }

        private static DireccionesEntregaCliente ContactoReciboConCcc()
        {
            return new DireccionesEntregaCliente
            {
                contacto = "0",
                servirJunto = true,
                formaPago = "RCB",
                plazosPago = "CONTADO",
                ccc = "1",
                periodoFacturacion = "NRM"
            };
        }

        [TestMethod]
        public void SinLlegarALaPantallaDePago_ValidaConLasCondicionesDeLaFichaDelContacto()
        {
            var vm = CrearViewModel(ContactoReciboConCcc());
            Assert.IsNull(vm.Estado.FormaPago, "Precondición: la forma de pago aún no se ha elegido");

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(1, _llamadas);
            Assert.AreEqual("RCB", _formaPagoEnviada);
            Assert.AreEqual("CONTADO", _plazosPagoEnviados);
            Assert.AreEqual("1", _cccEnviado);
            Assert.AreEqual("NRM", _periodoEnviado);
        }

        [TestMethod]
        public void ConLaFormaDePagoCambiadaEnLaPlantilla_ValidaConLaElegida()
        {
            var vm = CrearViewModel(ContactoReciboConCcc());
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "EFC", descripcion = "EFECTIVO", cccObligatorio = false };

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(1, _llamadas);
            Assert.AreEqual("EFC", _formaPagoEnviada);
            Assert.AreEqual("CONTADO", _plazosPagoEnviados);
            Assert.IsNull(_cccEnviado, "En efectivo el pedido no lleva CCC (como al crearlo)");
        }

        [TestMethod]
        public void ConReciboElegidoEnLaPlantilla_ValidaConElCccDelPedido()
        {
            var vm = CrearViewModel(ContactoReciboConCcc());
            vm.FormaPagoSeleccionada = new FormaPago { formaPago = "RCB", descripcion = "RECIBO", cccObligatorio = true };

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual("RCB", _formaPagoEnviada);
            Assert.AreEqual("1", _cccEnviado);
        }

        [TestMethod]
        public void ContactoEnEfectivoConCcc_NoMandaElCcc()
        {
            var contacto = ContactoReciboConCcc();
            contacto.formaPago = "EFC";
            var vm = CrearViewModel(contacto);

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual("EFC", _formaPagoEnviada);
            Assert.IsNull(_cccEnviado, "El pedido en efectivo no llevaría el CCC: sí es contra reembolso");
        }

        [TestMethod]
        public void SinCondicionesEnLaFicha_ValidaSinCondicionesComoAntes()
        {
            // Sin contacto el modo de servicio ni se valida; un contacto sin forma, plazos ni CCC manda lo de antes (nada).
            var vm = CrearViewModel(new DireccionesEntregaCliente { contacto = "0", servirJunto = true });

            vm.ModoServicio = ModosServicio.SEGUN_VAYA_ENTRANDO;

            Assert.AreEqual(1, _llamadas);
            Assert.IsNull(_formaPagoEnviada);
            Assert.IsNull(_plazosPagoEnviados);
            Assert.IsNull(_cccEnviado);
        }
    }
}
