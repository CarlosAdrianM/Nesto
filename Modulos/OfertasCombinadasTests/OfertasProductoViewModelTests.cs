using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.OfertasCombinadasTests
{
    /// <summary>
    /// Pestaña de ofertas de producto (los "6+2"). Nace de una petición por correo del 31/08/2026
    /// —poner el 6+2 del producto 44724—: hasta entonces solo se metían desde Nesto viejo, donde
    /// NO se les puede poner fecha, así que apagar una oferta era borrar la fila y acordarse.
    ///
    /// Las de un cliente concreto no se gestionan aquí: van en la ficha de ese cliente.
    /// </summary>
    [TestClass]
    public class OfertasProductoViewModelTests
    {
        private IOfertasCombinadasService _service;
        private IConfiguracion _configuracion;
        private IDialogService _dialogService;
        private IRegionManager _regionManager;
        private IServicioProducto _servicioProducto;

        [TestInitialize]
        public void Setup()
        {
            _service = A.Fake<IOfertasCombinadasService>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogService = A.Fake<IDialogService>();
            _regionManager = A.Fake<IRegionManager>();
            _servicioProducto = A.Fake<IServicioProducto>();

            A.CallTo(() => _service.GetOfertasCombinadas(A<string>._, A<bool>._))
                .Returns(Task.FromResult(new List<OfertaCombinadaModel>()));
            A.CallTo(() => _service.GetOfertasPermitidasFamilia(A<string>._))
                .Returns(Task.FromResult(new List<OfertaPermitidaFamiliaModel>()));
            A.CallTo(() => _service.GetOfertasEscalonadas(A<string>._, A<bool>._))
                .Returns(Task.FromResult(new List<OfertaEscalonadaModel>()));
            A.CallTo(() => _service.GetCampanas(A<bool>._, A<bool>._))
                .Returns(Task.FromResult(new List<CampanaModel>()));
            A.CallTo(() => _service.GetNombresDeCampana())
                .Returns(Task.FromResult(new List<ResumenCampanaModel>()));
            A.CallTo(() => _service.GetOfertasProducto(A<bool>._))
                .Returns(Task.FromResult(new List<OfertaProductoModel>()));
        }

        private OfertasCombinadasViewModel CrearViewModel()
        {
            return new OfertasCombinadasViewModel(_service, _configuracion, _dialogService, _regionManager, _servicioProducto);
        }

        private void ResponderALaConfirmacion(ButtonResult respuesta)
        {
            A.CallTo(() => _dialogService.ShowDialog("ConfirmationDialog", A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes((string _, IDialogParameters __, Action<IDialogResult> callback) =>
                    callback(new DialogResult(respuesta)));
        }

        /// <summary>
        /// El 6+2 sale puesto de serie: es con diferencia la oferta más común, y es exactamente la
        /// que pedía el correo que originó esta pestaña.
        /// </summary>
        [TestMethod]
        public void NuevaOferta_SalePreparadaComoSeisMasDos()
        {
            var vm = CrearViewModel();

            vm.NuevaOfertaProductoCommand.Execute(null);

            OfertaProductoWrapper nueva = vm.OfertasProducto.Single();
            Assert.AreEqual(6, nueva.CantidadConPrecio);
            Assert.AreEqual(2, nueva.CantidadRegalo);
            Assert.AreEqual("6+2", nueva.Resumen);
            Assert.AreEqual(0, nueva.NOrden);
        }

        [TestMethod]
        public void Resumen_SeLeeComoLaGenteLoDice()
        {
            var wrapper = new OfertaProductoWrapper { CantidadConPrecio = 3, CantidadRegalo = 1 };

            Assert.AreEqual("3+1", wrapper.Resumen);
        }

        [TestMethod]
        public async Task Cargar_TraeLasOfertasDelServicio()
        {
            A.CallTo(() => _service.GetOfertasProducto(A<bool>._))
                .Returns(Task.FromResult(new List<OfertaProductoModel>
                {
                    new OfertaProductoModel { NOrden = 792, Producto = "44724", ProductoNombre = "SERUM LEVEL LASH",
                                              CantidadConPrecio = 6, CantidadRegalo = 2, Vigente = true }
                }));
            var vm = CrearViewModel();

            vm.CargarCommand.Execute(null);
            await Task.Delay(80);

            OfertaProductoWrapper oferta = vm.OfertasProducto.Single();
            Assert.AreEqual("44724", oferta.Producto);
            Assert.AreEqual("SERUM LEVEL LASH", oferta.ProductoNombre);
            Assert.IsTrue(oferta.Vigente);
        }

        // Por defecto solo las vivas: si no, las del año pasado taparían las que están corriendo.
        [TestMethod]
        public void PorDefecto_NoSePidenLasCaducadas()
        {
            var vm = CrearViewModel();

            Assert.IsFalse(vm.IncluirOfertasProductoCaducadas);
        }

        [TestMethod]
        public async Task Guardar_SinProducto_NiSiquieraLlamaAlServicio()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaProductoWrapper { CantidadConPrecio = 6, CantidadRegalo = 2 };

            vm.GuardarOfertaProductoCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _service.CreateOfertaProducto(A<OfertaProductoModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_ConCantidadesACero_NiSiquieraLlamaAlServicio()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaProductoWrapper { Producto = "44724", CantidadConPrecio = 6, CantidadRegalo = 0 };

            vm.GuardarOfertaProductoCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _service.CreateOfertaProducto(A<OfertaProductoModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_Nueva_CreaYSeQuedaConLoQueDevuelveElServidor()
        {
            A.CallTo(() => _service.CreateOfertaProducto(A<OfertaProductoModel>._))
                .Returns(Task.FromResult(new OfertaProductoModel
                {
                    NOrden = 792, Producto = "44724", ProductoNombre = "SERUM LEVEL LASH",
                    CantidadConPrecio = 6, CantidadRegalo = 2, Vigente = true
                }));
            var vm = CrearViewModel();
            var oferta = new OfertaProductoWrapper { Producto = "44724", CantidadConPrecio = 6, CantidadRegalo = 2 };

            vm.GuardarOfertaProductoCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _service.CreateOfertaProducto(A<OfertaProductoModel>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(792, oferta.NOrden);
            Assert.AreEqual("SERUM LEVEL LASH", oferta.ProductoNombre, "El nombre lo resuelve el servidor");
            Assert.IsFalse(oferta.HaCambiado, "Recien guardada no puede quedar marcada como sucia");
        }

        [TestMethod]
        public async Task Guardar_Existente_ActualizaEnVezDeCrear()
        {
            A.CallTo(() => _service.UpdateOfertaProducto(A<int>._, A<OfertaProductoModel>._))
                .Returns(Task.FromResult(new OfertaProductoModel { NOrden = 792, Producto = "44724" }));
            var vm = CrearViewModel();
            var oferta = new OfertaProductoWrapper(new OfertaProductoModel
            {
                NOrden = 792, Producto = "44724", CantidadConPrecio = 6, CantidadRegalo = 2
            });
            oferta.CantidadRegalo = 3;

            vm.GuardarOfertaProductoCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _service.UpdateOfertaProducto(792, A<OfertaProductoModel>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _service.CreateOfertaProducto(A<OfertaProductoModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Eliminar_SinGuardar_LaQuitaSinLlamarAlServicio()
        {
            var vm = CrearViewModel();
            vm.NuevaOfertaProductoCommand.Execute(null);

            vm.EliminarOfertaProductoCommand.Execute(vm.OfertasProducto.Single());
            await Task.Delay(50);

            Assert.AreEqual(0, vm.OfertasProducto.Count);
            A.CallTo(() => _service.DeleteOfertaProducto(A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Eliminar_SiNoSeConfirma_NoBorraNada()
        {
            ResponderALaConfirmacion(ButtonResult.Cancel);
            var vm = CrearViewModel();
            var oferta = new OfertaProductoWrapper(new OfertaProductoModel { NOrden = 792, Producto = "44724" });
            vm.OfertasProducto.Add(oferta);

            vm.EliminarOfertaProductoCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _service.DeleteOfertaProducto(A<int>._)).MustNotHaveHappened();
            Assert.AreEqual(1, vm.OfertasProducto.Count);
        }

        [TestMethod]
        public async Task Eliminar_SiSeConfirma_BorraYLaQuitaDeLaLista()
        {
            ResponderALaConfirmacion(ButtonResult.OK);
            var vm = CrearViewModel();
            var oferta = new OfertaProductoWrapper(new OfertaProductoModel { NOrden = 792, Producto = "44724" });
            vm.OfertasProducto.Add(oferta);

            vm.EliminarOfertaProductoCommand.Execute(oferta);
            await Task.Delay(50);

            A.CallTo(() => _service.DeleteOfertaProducto(792)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, vm.OfertasProducto.Count);
        }

        // Cargar del servidor no es un cambio del usuario: si quedara sucia, saldría el botón de
        // Guardar en todas las filas nada más abrir la pestaña.
        [TestMethod]
        public void Wrapper_ReciencargadoDelServidor_NoEstaSucio()
        {
            var wrapper = new OfertaProductoWrapper(new OfertaProductoModel { NOrden = 792, Producto = "44724" });

            Assert.IsFalse(wrapper.HaCambiado);
        }

        [TestMethod]
        public void Wrapper_AlTocarloElUsuario_QuedaSucio()
        {
            var wrapper = new OfertaProductoWrapper(new OfertaProductoModel { NOrden = 792, Producto = "44724" });

            wrapper.CantidadRegalo = 3;

            Assert.IsTrue(wrapper.HaCambiado);
        }

        // El filtro vacío viaja como nulo: "" no es un filtro, es la ausencia de filtro.
        [TestMethod]
        public void Wrapper_FiltroVacio_ViajaNulo()
        {
            var wrapper = new OfertaProductoWrapper { Producto = "44724", FiltroProducto = "   " };

            Assert.IsNull(wrapper.AModelo().FiltroProducto);
        }

        [TestMethod]
        public void Wrapper_LlevaLasFechasIdaYVuelta()
        {
            var desde = new DateTime(2026, 9, 1);
            var hasta = new DateTime(2026, 9, 30);
            var wrapper = new OfertaProductoWrapper(new OfertaProductoModel
            {
                Producto = "44724", FechaDesde = desde, FechaHasta = hasta
            });

            Assert.AreEqual(desde, wrapper.AModelo().FechaDesde);
            Assert.AreEqual(hasta, wrapper.AModelo().FechaHasta);
        }
    }
}
