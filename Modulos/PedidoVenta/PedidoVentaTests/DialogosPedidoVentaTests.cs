using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PedidoVenta.Models.Facturas;
using Nesto.Modulos.PedidoVenta.Services;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): los diálogos de PedidoVenta (Picking, Facturar rutas, Errores de
    /// facturación de rutas, Copiar factura y Grupo de comisión del inmovilizado) son IDialogoNesto
    /// (DialogoNestoBase): mismos títulos, mismas claves de parámetros y mismos resultados que cuando eran
    /// IDialogAware de Prism. (Hasta el 14/10/26 se probaban también con la ventana de Prism.)
    /// </summary>
    [TestClass]
    public class DialogosPedidoVentaTests
    {
        #region Picking

        private static PickingPopupViewModel CrearPicking()
            => new(A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IConfiguracion>(), A.Fake<IInformesService>());

        [TestMethod]
        public void Picking_RecibeElPedido()
        {
            var vm = CrearPicking();
            IDialogoNesto dialogo = vm;

            dialogo.OnDialogOpened(new ParametrosDialogo { { "pedidoPicking", new PedidoVentaDTO { numero = 901234, cliente = "15191" } } });

            Assert.AreEqual("Picking", dialogo.Title);
            Assert.AreEqual(901234, vm.numeroPedidoPicking);
            Assert.AreEqual("15191", vm.numeroClientePicking);
            Assert.IsTrue(dialogo.CanCloseDialog());
        }

        [TestMethod]
        public void Picking_SinPedido_SePoneACero()
        {
            var vm = CrearPicking();
            vm.numeroPedidoPicking = 5;

            vm.OnDialogOpened(new ParametrosDialogo());

            Assert.AreEqual("Picking", vm.Title);
            Assert.AreEqual(0, vm.numeroPedidoPicking);
        }

        #endregion

        #region Facturar rutas

        private static FacturarRutasPopupViewModel CrearFacturarRutas()
        {
            var servicio = A.Fake<IServicioFacturacionRutas>();
            A.CallTo(() => servicio.ObtenerTiposRuta()).Returns(Task.FromResult(new List<TipoRutaInfoDTO>
            {
                new() { Id = "PROPIA" },
                new() { Id = "AGENCIA" }
            }));
            return new FacturarRutasPopupViewModel(A.Fake<IConfiguracion>(), A.Fake<IServicioDialogos>(), servicio,
                A.Fake<IServicioImpresionDocumentos>(), A.Fake<IUnityContainer>());
        }

        [TestMethod]
        public void FacturarRutas_CargaLosTiposDeRutaYCancelarCierraConCancel()
        {
            var vm = CrearFacturarRutas();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo());
            vm.CancelarCommand.Execute(null);

            Assert.AreEqual("Facturar Rutas", dialogo.Title);
            Assert.AreEqual(2, vm.TiposRutaDisponibles.Count);
            Assert.AreEqual("PROPIA", vm.TipoRutaSeleccionado.Id);
            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        [TestMethod]
        public void FacturarRutas_MientrasFactura_NoSePuedeCerrar()
        {
            var vm = CrearFacturarRutas();

            vm.EstaProcesando = true;

            Assert.IsFalse(((IDialogoNesto)vm).CanCloseDialog());
        }

        #endregion

        #region Errores de facturación de rutas

        private static List<PedidoConErrorDTO> DosErrores() => new()
        {
            new() { Empresa = "1", NumeroPedido = 901001 },
            new() { Empresa = "1", NumeroPedido = 901002 }
        };

        [TestMethod]
        public void ErroresFacturacion_RecibeLosErroresYCerrarDevuelveOK()
        {
            var vm = new ErroresFacturacionRutasPopupViewModel(A.Fake<IAbridorPedidos>());
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo { { "errores", DosErrores() } });
            vm.Cerrar();

            Assert.AreEqual("Errores en Facturación de Rutas", dialogo.Title);
            Assert.AreEqual(2, vm.NumeroErrores);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }

        [TestMethod]
        public void ErroresFacturacion_AbrirPedido_LoAbreConElAbridorDePedidosYNoCierraLaVentana()
        {
            // Nesto#490 (4C.5, tramo 3a): antes, PedidoVentaViewModel.CargarPedido con el IUnityContainer
            var abridor = A.Fake<IAbridorPedidos>();
            var vm = new ErroresFacturacionRutasPopupViewModel(abridor);
            IDialogoNesto dialogo = vm;
            bool cerrado = false;
            dialogo.RequestClose += _ => cerrado = true;
            dialogo.OnDialogOpened(new ParametrosDialogo { { "errores", DosErrores() } });

            vm.cmdAbrirPedido.Execute(vm.Errores[1]);

            A.CallTo(() => abridor.Abrir("1", 901002)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(cerrado);
        }

        #endregion

        #region Copiar factura

        private static CopiarFacturaViewModel CrearCopiarFactura() => new(A.Fake<IPedidoVentaService>(), A.Fake<IConfiguracion>());

        [TestMethod]
        public void CopiarFactura_RecibeLaFacturaYCancelarCierraConCancel()
        {
            var vm = CrearCopiarFactura();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;

            dialogo.OnDialogOpened(new ParametrosDialogo { { "empresa", "3" }, { "cliente", "15191" }, { "numeroFactura", "NV26/001234" } });
            vm.CancelarCommand.Execute(null);

            Assert.AreEqual("Copiar Factura / Crear Rectificativa", dialogo.Title);
            Assert.AreEqual("3", vm.Empresa);
            Assert.AreEqual("15191", vm.Cliente);
            Assert.AreEqual("NV26/001234", vm.NumeroFactura);
            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        [TestMethod]
        public void CopiarFactura_SinParametros_SeQuedaLaEmpresaDeSiempre()
        {
            var vm = CrearCopiarFactura();
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;

            vm.OnDialogOpened(new ParametrosDialogo());
            vm.CancelarCommand.Execute(null);

            Assert.AreEqual("Copiar Factura / Crear Rectificativa", vm.Title);
            Assert.AreEqual(Nesto.Infrastructure.Shared.Constantes.Empresas.EMPRESA_DEFECTO, vm.Empresa);
            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        [TestMethod]
        public void CopiarFactura_MientrasCopia_NoSePuedeCerrar()
        {
            var vm = CrearCopiarFactura();

            vm.EstaProcesando = true;

            Assert.IsFalse(((IDialogoNesto)vm).CanCloseDialog());
        }

        #endregion

        #region Grupo de comisión del inmovilizado

        [TestMethod]
        public void SelectorGrupoComision_DevuelveElGrupoElegido()
        {
            var vm = new SelectorGrupoComisionDialogViewModel();
            IDialogoNesto dialogo = vm;
            ResultadoDialogo resultado = null;
            dialogo.RequestClose += r => resultado = r;
            var grupos = new List<GrupoProductoDTO> { new(), new() };

            dialogo.OnDialogOpened(new ParametrosDialogo { { "mensaje", "Línea 3: elige el grupo" }, { "grupos", grupos } });
            vm.GrupoSeleccionado = "APA";
            vm.AceptarCommand.Execute(null);

            Assert.AreEqual("Grupo de comisión del inmovilizado", dialogo.Title);
            Assert.AreEqual("Línea 3: elige el grupo", vm.Mensaje);
            Assert.AreSame(grupos, vm.Grupos);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("APA", resultado.Parameters.GetValue<string>("grupo"));
        }

        [TestMethod]
        public void SelectorGrupoComision_SinGrupoNoAceptaYCancelarCierraConCancel()
        {
            var vm = new SelectorGrupoComisionDialogViewModel();
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;

            vm.OnDialogOpened(new ParametrosDialogo());

            Assert.IsFalse(vm.AceptarCommand.CanExecute(null));
            vm.CancelarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
            Assert.IsFalse(resultado.Parameters.ContainsKey("grupo"));
        }

        #endregion
    }
}
