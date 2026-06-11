using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using Prism.Services.Dialogs;
using Prism.Regions;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.OfertasCombinadasTests
{
    [TestClass]
    public class OfertasCombinadasViewModelTests
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

            // El constructor lanza una carga inicial; devolvemos listas vacías para que no falle.
            A.CallTo(() => _service.GetOfertasCombinadas(A<string>._, A<bool>._))
                .Returns(Task.FromResult(new List<OfertaCombinadaModel>()));
            A.CallTo(() => _service.GetOfertasPermitidasFamilia(A<string>._))
                .Returns(Task.FromResult(new List<OfertaPermitidaFamiliaModel>()));
        }

        private OfertasCombinadasViewModel CrearViewModel()
        {
            return new OfertasCombinadasViewModel(_service, _configuracion, _dialogService, _regionManager, _servicioProducto);
        }

        private static DetalleOfertaCombinadaWrapper Detalle(string producto, short cantidad, decimal precio)
        {
            return new DetalleOfertaCombinadaWrapper { Producto = producto, Cantidad = cantidad, Precio = precio };
        }

        [TestMethod]
        public void ProductoAlternativo_LineaSinGrupo_AsignaGrupoYClonaCantidadYPrecio()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper();
            var camisetaS = Detalle("CAM_S", 1, 0m);
            oferta.Detalles.Add(camisetaS);
            vm.OfertaCombinadaSeleccionada = oferta;
            vm.DetalleSeleccionado = camisetaS;

            vm.NuevoDetalleAlternativoCommand.Execute(null);

            Assert.AreEqual(2, oferta.Detalles.Count, "Debe añadir una alternativa nueva");
            Assert.AreEqual(1, camisetaS.GrupoAlternativa, "La línea original pasa a ser la primera alternativa del grupo");
            var nueva = oferta.Detalles.Last();
            Assert.AreEqual(1, nueva.GrupoAlternativa, "La alternativa va en el mismo grupo");
            Assert.AreEqual<short>(1, nueva.Cantidad, "Hereda la cantidad");
            Assert.AreEqual(0m, nueva.Precio, "Hereda el precio");
            Assert.IsTrue(oferta.HaCambiado, "Debe marcar la oferta como cambiada");
        }

        [TestMethod]
        public void ProductoAlternativo_TercerasAlternativas_MismoGrupo()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper();
            var camisetaS = Detalle("CAM_S", 1, 0m);
            oferta.Detalles.Add(camisetaS);
            vm.OfertaCombinadaSeleccionada = oferta;
            vm.DetalleSeleccionado = camisetaS;

            vm.NuevoDetalleAlternativoCommand.Execute(null); // CAM_S + alternativa -> grupo 1
            vm.NuevoDetalleAlternativoCommand.Execute(null); // sobre la última (grupo 1) -> sigue grupo 1

            Assert.AreEqual(3, oferta.Detalles.Count);
            Assert.IsTrue(oferta.Detalles.All(d => d.GrupoAlternativa == 1),
                "Todas las alternativas comparten el mismo grupo");
        }

        [TestMethod]
        public void ProductoAlternativo_OtraLineaIndependiente_NuevoGrupo()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper();
            var camiseta = Detalle("CAM_S", 1, 0m);
            var gorra = Detalle("GORRA_A", 2, 0m);
            oferta.Detalles.Add(camiseta);
            oferta.Detalles.Add(gorra);
            vm.OfertaCombinadaSeleccionada = oferta;

            vm.DetalleSeleccionado = camiseta;
            vm.NuevoDetalleAlternativoCommand.Execute(null); // grupo 1

            vm.DetalleSeleccionado = gorra;
            vm.NuevoDetalleAlternativoCommand.Execute(null); // grupo 2 (independiente)

            Assert.AreEqual(1, camiseta.GrupoAlternativa);
            Assert.AreEqual(2, gorra.GrupoAlternativa, "Una línea sin grupo recibe un grupo nuevo distinto");
            Assert.AreEqual<short>(2, oferta.Detalles.Last().Cantidad, "La alternativa de la gorra hereda su cantidad (2)");
        }

        [TestMethod]
        public void ProductoAlternativo_SinSeleccion_NoSePuedeEjecutar()
        {
            var vm = CrearViewModel();
            vm.DetalleSeleccionado = null;

            Assert.IsFalse(vm.NuevoDetalleAlternativoCommand.CanExecute(null),
                "Sin línea seleccionada no debe poder añadirse alternativa");
        }
    }
}
