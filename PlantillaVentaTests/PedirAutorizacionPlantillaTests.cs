using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Novedades 1258: al crear un pedido que no pasa la validación de ofertas y descuentos, quien NO puede crearlo «de todos modos» ve
    /// el aviso con «Pedir autorización» (manda el borrador tal cual y la plantilla se queda abierta); quien puede, la pregunta de siempre.
    /// </summary>
    [TestClass]
    public class PedirAutorizacionPlantillaTests
    {
        private const string MOTIVO = "No se encuentra autorizado el descuento del 20 % en el producto 45383";

        private IPlantillaVentaService _servicio;
        private IServicioDialogos _dialogos;
        private IServicioAutorizacionesPedido _autorizaciones;
        private IServicioNavegacion _navegacion;
        private IConfiguracion _configuracion;
        private readonly List<ParametrosDialogo> _avisosConBotones = new List<ParametrosDialogo>();

        [TestInitialize]
        public void Setup()
        {
            _servicio = A.Fake<IPlantillaVentaService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _autorizaciones = A.Fake<IServicioAutorizacionesPedido>();
            _navegacion = A.Fake<IServicioNavegacion>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _servicio.CrearPedido(A<PedidoVentaDTO>._)).ThrowsAsync(new ValidationException(MOTIVO));
            A.CallTo(() => _dialogos.ShowDialog(ServicioDialogosBase.ConfirmationDialog, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string _, ParametrosDialogo p, Action<ResultadoDialogo> cb) =>
                {
                    _avisosConBotones.Add(p);
                    cb(new ResultadoDialogo(ResultadoBoton.OK)); // «Pedir autorización»
                });
            A.CallTo(() => _dialogos.GetText(A<string>._, A<string>._)).Returns("Lo hablé con Carlos");
            A.CallTo(() => _autorizaciones.Pedir(A<PedidoVentaDTO>._, A<string>._))
                .Returns(new AutorizacionPedidoModel { Id = 4, Estado = "Pendiente", Destinatarios = new List<string> { "Carlos" } });
        }

        private PlantillaVentaViewModel CrearViewModel()
        {
            A.CallTo(() => _configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => _configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), _navegacion, _configuracion, _servicio,
                new WeakReferenceMessenger(), _dialogos, A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ServicioAutorizacionesPedido = _autorizaciones;
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

        [TestMethod]
        public async Task SinPermiso_OfrecePedirAutorizacion_MandaElBorradorYLaPlantillaSeQuedaAbierta()
        {
            var vm = CrearViewModel();

            await vm.CrearPedidoAsync();

            Assert.AreEqual(1, _avisosConBotones.Count);
            Assert.AreEqual(SolicitudAutorizacionPedido.TEXTO_PEDIR, _avisosConBotones[0].GetValue<string>(ConfirmationDialogViewModel.PARAMETRO_TEXTO_SI));
            StringAssert.Contains(_avisosConBotones[0].GetValue<string>("message"), MOTIVO);
            A.CallTo(() => _autorizaciones.Pedir(A<PedidoVentaDTO>.That.Matches(p => System.Linq.Enumerable.Any(p.Lineas, l => l.Producto == "45383")), "Lo hablé con Carlos")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogos.ShowNotification(SolicitudAutorizacionPedido.TITULO_PEDIR, A<string>.That.Contains("Carlos"))).MustHaveHappenedOnceExactly();
            A.CallTo(() => _dialogos.ShowError(A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _dialogos.ShowConfirmationAsync("Pedido no válido", A<string>._)).MustNotHaveHappened();
            A.CallTo(() => _navegacion.CerrarVistaActiva(A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(1, vm.ListaFiltrableProductos.ListaOriginal.Count, "No se pierde lo tecleado");
        }

        [TestMethod]
        public async Task ConPermiso_PreguntaDeTodosModos_YNoOfrecePedirAutorizacion()
        {
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(true);
            var vm = CrearViewModel();

            await vm.CrearPedidoAsync();

            A.CallTo(() => _dialogos.ShowConfirmationAsync("Pedido no válido", A<string>.That.Contains("¿Desea crearlo de todos modos?"))).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, _avisosConBotones.Count);
            A.CallTo(() => _autorizaciones.Pedir(A<PedidoVentaDTO>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
