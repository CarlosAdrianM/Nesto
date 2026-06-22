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
        public void Guardar_DetalleConPermitirCantidadMenor_ViajaEnElCreateModel()
        {
            // NestoAPI#239: al guardar, el flag "permitir cantidad menor" de cada línea debe
            // enviarse al servidor en el CreateModel.
            OfertaCombinadaCreateModel capturado = null;
            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .Invokes((OfertaCombinadaCreateModel m) => capturado = m)
                .Returns(Task.FromResult(new OfertaCombinadaModel
                {
                    Id = 1,
                    Nombre = "Level Lash Sérum 6+2",
                    Detalles = new List<OfertaCombinadaDetalleModel>()
                }));

            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper { Nombre = "Level Lash Sérum 6+2" }; // Id = 0 -> create
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Producto = "SERUM", Cantidad = 1, Precio = 10, PermitirCantidadMenor = false });
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Producto = "FOLLETO", Cantidad = 20, Precio = 0, PermitirCantidadMenor = true });

            vm.GuardarOfertaCombinadaCommand.Execute(oferta);

            Assert.IsNotNull(capturado, "Debe llamar a CreateOfertaCombinada");
            Assert.IsTrue(capturado.Detalles.Single(d => d.Producto == "FOLLETO").PermitirCantidadMenor,
                "El folleto debe enviarse con el flag a true");
            Assert.IsFalse(capturado.Detalles.Single(d => d.Producto == "SERUM").PermitirCantidadMenor,
                "El sérum debe enviarse sin el flag");
        }

        private static OfertaCombinadaWrapper OfertaExistenteConDosDetalles()
        {
            // Cargada desde el servidor (HaCambiado=false de inicio).
            return new OfertaCombinadaWrapper(new OfertaCombinadaModel
            {
                Id = 5,
                Nombre = "Oferta existente",
                ImporteMinimo = 0,
                Detalles = new List<OfertaCombinadaDetalleModel>
                {
                    new OfertaCombinadaDetalleModel { Id = 1, Producto = "SERUM", Cantidad = 1, Precio = 10 },
                    new OfertaCombinadaDetalleModel { Id = 2, Producto = "FOLLETO", Cantidad = 20, Precio = 0 }
                }
            });
        }

        [TestMethod]
        public void MarcarPermitirCantidadMenor_EnOfertaExistente_ActivaGuardar()
        {
            // Bug reportado: marcar "permitir cantidad menor" en una línea de una oferta ya guardada
            // no activaba el botón Guardar (HaCambiado seguía false), así que no se podía guardar.
            var oferta = OfertaExistenteConDosDetalles();
            Assert.IsFalse(oferta.HaCambiado, "Recién cargada no debe tener cambios");

            oferta.Detalles.Single(d => d.Producto == "FOLLETO").PermitirCantidadMenor = true;

            Assert.IsTrue(oferta.HaCambiado, "Marcar la casilla debe activar Guardar");
        }

        [TestMethod]
        public void EditarCantidadDetalle_EnOfertaExistente_ActivaGuardar()
        {
            // Mismo bug subyacente: editar cualquier campo del detalle (no solo la casilla nueva)
            // debe marcar la oferta como cambiada.
            var oferta = OfertaExistenteConDosDetalles();

            oferta.Detalles.First().Cantidad = 99;

            Assert.IsTrue(oferta.HaCambiado, "Editar la cantidad de un detalle debe activar Guardar");
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
