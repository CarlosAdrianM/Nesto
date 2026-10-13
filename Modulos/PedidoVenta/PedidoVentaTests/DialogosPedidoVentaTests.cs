using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PedidoVenta.Models.Facturas;
using Nesto.Modulos.PedidoVenta.Services;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): los cinco diálogos de PedidoVenta (Picking, Facturar rutas, Errores de
    /// facturación de rutas, Copiar factura y Grupo de comisión del inmovilizado) ya son IDialogoNesto
    /// (DialogoNestoBase) y tienen que abrirse igual con las dos ventanas: la de Prism (usuarios sin el piloto
    /// VentanaDialogosPropia), que los ve como IDialogAware, y la propia. Mismos títulos, mismas claves de
    /// parámetros y mismos resultados.
    /// </summary>
    [TestClass]
    public class DialogosPedidoVentaTests
    {
        #region Picking

        private static PickingPopupViewModel CrearPicking()
            => new(A.Fake<IPedidoVentaService>(), new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IConfiguracion>(), A.Fake<IInformesService>());

        [TestMethod]
        public void Picking_ConLaVentanaDePrism_RecibeElPedido()
        {
            var vm = CrearPicking();
            IDialogAware prism = vm;

            prism.OnDialogOpened(new DialogParameters { { "pedidoPicking", new PedidoVentaDTO { numero = 901234, cliente = "15191" } } });

            Assert.AreEqual("Picking", prism.Title);
            Assert.AreEqual(901234, vm.numeroPedidoPicking);
            Assert.AreEqual("15191", vm.numeroClientePicking);
            Assert.IsTrue(prism.CanCloseDialog());
        }

        [TestMethod]
        public void Picking_ConLaVentanaPropia_SinPedido_SePoneACero()
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
        public void FacturarRutas_ConLaVentanaDePrism_CargaLosTiposDeRutaYCancelarCierraConCancel()
        {
            var vm = CrearFacturarRutas();
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(null);
            vm.CancelarCommand.Execute(null);

            Assert.AreEqual("Facturar Rutas", prism.Title);
            Assert.AreEqual(2, vm.TiposRutaDisponibles.Count);
            Assert.AreEqual("PROPIA", vm.TipoRutaSeleccionado.Id);
            Assert.AreEqual(ButtonResult.Cancel, resultado.Result);
        }

        [TestMethod]
        public void FacturarRutas_ConLaVentanaPropia_CargaLosTiposDeRutaYCancelarCierraConCancel()
        {
            var vm = CrearFacturarRutas();
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;

            vm.OnDialogOpened(new ParametrosDialogo());
            vm.CancelarCommand.Execute(null);

            Assert.AreEqual("Facturar Rutas", vm.Title);
            Assert.AreEqual(2, vm.TiposRutaDisponibles.Count);
            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        [TestMethod]
        public void FacturarRutas_MientrasFactura_NoSePuedeCerrarConNingunaVentana()
        {
            var vm = CrearFacturarRutas();

            vm.EstaProcesando = true;

            Assert.IsFalse(((IDialogAware)vm).CanCloseDialog());
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
        public void ErroresFacturacion_ConLaVentanaDePrism_RecibeLosErroresYCerrarDevuelveOK()
        {
            var vm = new ErroresFacturacionRutasPopupViewModel(A.Fake<IUnityContainer>());
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters { { "errores", DosErrores() } });
            vm.Cerrar();

            Assert.AreEqual("Errores en Facturación de Rutas", prism.Title);
            Assert.AreEqual(2, vm.NumeroErrores);
            Assert.AreEqual(ButtonResult.OK, resultado.Result);
        }

        [TestMethod]
        public void ErroresFacturacion_ConLaVentanaPropia_RecibeLosErroresYCerrarDevuelveOK()
        {
            var vm = new ErroresFacturacionRutasPopupViewModel(A.Fake<IUnityContainer>());
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;

            vm.OnDialogOpened(new ParametrosDialogo { { "errores", DosErrores() } });
            vm.Cerrar();

            Assert.AreEqual(2, vm.Errores.Count);
            Assert.AreEqual(901002, vm.Errores[1].NumeroPedido);
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
        }

        #endregion

        #region Copiar factura

        private static CopiarFacturaViewModel CrearCopiarFactura() => new(A.Fake<IPedidoVentaService>(), A.Fake<IConfiguracion>());

        [TestMethod]
        public void CopiarFactura_ConLaVentanaDePrism_RecibeLaFacturaYCancelarCierraConCancel()
        {
            var vm = CrearCopiarFactura();
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;

            prism.OnDialogOpened(new DialogParameters { { "empresa", "3" }, { "cliente", "15191" }, { "numeroFactura", "NV26/001234" } });
            vm.CancelarCommand.Execute(null);

            Assert.AreEqual("Copiar Factura / Crear Rectificativa", prism.Title);
            Assert.AreEqual("3", vm.Empresa);
            Assert.AreEqual("15191", vm.Cliente);
            Assert.AreEqual("NV26/001234", vm.NumeroFactura);
            Assert.AreEqual(ButtonResult.Cancel, resultado.Result);
        }

        [TestMethod]
        public void CopiarFactura_ConLaVentanaPropia_SinParametros_SeQuedaLaEmpresaDeSiempre()
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
        public void CopiarFactura_MientrasCopia_NoSePuedeCerrarConNingunaVentana()
        {
            var vm = CrearCopiarFactura();

            vm.EstaProcesando = true;

            Assert.IsFalse(((IDialogAware)vm).CanCloseDialog());
            Assert.IsFalse(((IDialogoNesto)vm).CanCloseDialog());
        }

        #endregion

        #region Grupo de comisión del inmovilizado

        [TestMethod]
        public void SelectorGrupoComision_ConLaVentanaDePrism_DevuelveElGrupoElegido()
        {
            var vm = new SelectorGrupoComisionDialogViewModel();
            IDialogAware prism = vm;
            IDialogResult resultado = null;
            prism.RequestClose += r => resultado = r;
            var grupos = new List<GrupoProductoDTO> { new(), new() };

            prism.OnDialogOpened(new DialogParameters { { "mensaje", "Línea 3: elige el grupo" }, { "grupos", grupos } });
            vm.GrupoSeleccionado = "APA";
            vm.AceptarCommand.Execute(null);

            Assert.AreEqual("Grupo de comisión del inmovilizado", prism.Title);
            Assert.AreEqual("Línea 3: elige el grupo", vm.Mensaje);
            Assert.AreSame(grupos, vm.Grupos);
            Assert.AreEqual(ButtonResult.OK, resultado.Result);
            Assert.AreEqual("APA", resultado.Parameters.GetValue<string>("grupo"));
        }

        [TestMethod]
        public void SelectorGrupoComision_ConLaVentanaPropia_SinGrupoNoAceptaYCancelarCierraConCancel()
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

        [TestMethod]
        public void SelectorGrupoComision_ConLaVentanaPropia_DevuelveElGrupoElegido()
        {
            var vm = new SelectorGrupoComisionDialogViewModel();
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;

            vm.GrupoSeleccionado = "COS";
            vm.AceptarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("COS", resultado.Parameters.GetValue<string>("grupo"));
        }

        #endregion
    }
}
