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

        // ----- NestoAPI#282: líneas de filtro (familia y/o prefijo del nombre) -----

        [TestMethod]
        public void Guardar_LineaDeFiltro_ViajaEnElCreateModelConProductoNull()
        {
            // Caso Lisap: fila de filtro (72 tintes OPC) + regalo de producto concreto.
            OfertaCombinadaCreateModel capturado = null;
            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .Invokes((OfertaCombinadaCreateModel m) => capturado = m)
                .Returns(Task.FromResult(new OfertaCombinadaModel
                {
                    Id = 1,
                    Nombre = "Lisap OPC",
                    Detalles = new List<OfertaCombinadaDetalleModel>()
                }));

            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper { Nombre = "Lisap OPC" };
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Familia = " Lisap ", FiltroProducto = "LK OPC ", Cantidad = 72, Precio = 0 });
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Producto = "45473", Cantidad = 1, Precio = 0 });

            vm.GuardarOfertaCombinadaCommand.Execute(oferta);

            Assert.IsNotNull(capturado, "Debe llamar a CreateOfertaCombinada");
            var filtro = capturado.Detalles.Single(d => d.Producto == null);
            Assert.AreEqual("Lisap", filtro.Familia, "La familia debe viajar recortada");
            Assert.AreEqual("LK OPC", filtro.FiltroProducto, "El filtro debe viajar recortado");
            var regalo = capturado.Detalles.Single(d => d.Producto == "45473");
            Assert.IsNull(regalo.Familia, "La fila de producto no lleva familia");
            Assert.IsNull(regalo.FiltroProducto, "La fila de producto no lleva filtro");
        }

        [TestMethod]
        public void Guardar_LineaSinProductoNiFiltro_NoLlamaAlServicio()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper { Nombre = "Incompleta" };
            oferta.Detalles.Add(Detalle("SERUM", 1, 10m));
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Cantidad = 1, Precio = 0 }); // ni producto ni filtro

            vm.GuardarOfertaCombinadaCommand.Execute(oferta);

            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .MustNotHaveHappened();
        }

        [TestMethod]
        public void Guardar_LineaConProductoYFiltroALaVez_NoLlamaAlServicio()
        {
            // Una línea es de producto concreto O de filtro; ambas cosas a la vez es ambiguo
            // (el motor ignoraría el filtro) y se rechaza antes de enviar.
            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper { Nombre = "Ambigua" };
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Producto = "45473", Familia = "Lisap", Cantidad = 1, Precio = 0 });
            oferta.Detalles.Add(Detalle("SERUM", 1, 10m));

            vm.GuardarOfertaCombinadaCommand.Execute(oferta);

            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .MustNotHaveHappened();
        }

        [TestMethod]
        public void Guardar_LineaDeFiltroEnGrupoAlternativa_NoLlamaAlServicio()
        {
            // Los grupos de alternativas siguen siendo solo de producto concreto (NestoAPI#282).
            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper { Nombre = "Filtro en grupo" };
            oferta.Detalles.Add(new DetalleOfertaCombinadaWrapper { Familia = "Lisap", Cantidad = 6, Precio = 0, GrupoAlternativa = 1 });
            oferta.Detalles.Add(Detalle("SERUM", 1, 10m));

            vm.GuardarOfertaCombinadaCommand.Execute(oferta);

            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .MustNotHaveHappened();
        }

        [TestMethod]
        public void ProductoAlternativo_SobreLineaDeFiltro_NoAnadeAlternativa()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper();
            var filtro = new DetalleOfertaCombinadaWrapper { Familia = "Lisap", FiltroProducto = "LK OPC", Cantidad = 72, Precio = 0 };
            oferta.Detalles.Add(filtro);
            vm.OfertaCombinadaSeleccionada = oferta;
            vm.DetalleSeleccionado = filtro;

            vm.NuevoDetalleAlternativoCommand.Execute(null);

            Assert.AreEqual(1, oferta.Detalles.Count, "No debe añadir alternativa sobre una línea de filtro");
            Assert.IsNull(filtro.GrupoAlternativa, "La línea de filtro no debe recibir grupo de alternativas");
        }

        // ==== NestoAPI#289: filtro por grupo/subgrupo (combo de subgrupos) ====

        [TestMethod]
        public void GrupoSubgrupoClave_SeleccionarEnElCombo_ReparteGrupoYSubgrupo()
        {
            var detalle = new DetalleOfertaCombinadaWrapper();

            detalle.GrupoSubgrupoClave = "COS|107";

            Assert.AreEqual("COS", detalle.Grupo);
            Assert.AreEqual("107", detalle.Subgrupo);
            Assert.IsTrue(detalle.EsFiltro, "Una fila solo con subgrupo es una fila de filtro");
        }

        [TestMethod]
        public void GrupoSubgrupoClave_OpcionEnBlanco_DejaGrupoYSubgrupoANull()
        {
            var detalle = new DetalleOfertaCombinadaWrapper { Grupo = "COS", Subgrupo = "107" };

            detalle.GrupoSubgrupoClave = "|";

            Assert.IsNull(detalle.Grupo);
            Assert.IsNull(detalle.Subgrupo);
            Assert.IsFalse(detalle.EsFiltro, "Sin ningún criterio la fila deja de ser de filtro");
        }

        [TestMethod]
        public void SeleccionarSubgrupo_EnOfertaExistente_ActivaGuardar()
        {
            var oferta = OfertaExistenteConDosDetalles();
            Assert.IsFalse(oferta.HaCambiado, "Recién cargada no debe estar marcada como cambiada");

            oferta.Detalles.First().GrupoSubgrupoClave = "COS|107";

            Assert.IsTrue(oferta.HaCambiado, "Elegir subgrupo debe activar el botón Guardar");
        }

        [TestMethod]
        public void Guardar_LineaDeFiltroPorSubgrupo_ViajaEnElCreateModel()
        {
            // Caso aceites CV (NestoAPI#289): UNA fila de filtro Familia CV + Grupo COS +
            // Subgrupo 107, cantidad 3, con "Regalo menor importe" y sin importe mínimo.
            OfertaCombinadaCreateModel capturado = null;
            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .Invokes((OfertaCombinadaCreateModel m) => capturado = m)
                .Returns(Task.FromResult(new OfertaCombinadaModel
                {
                    Id = 1,
                    Nombre = "2+1 aceites CV",
                    Detalles = new List<OfertaCombinadaDetalleModel>()
                }));

            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper { Nombre = "2+1 aceites CV", ImporteMinimo = 0, RegalarMenorImporte = true };
            var filtro = new DetalleOfertaCombinadaWrapper { Familia = "CV", Cantidad = 3, Precio = 0 };
            filtro.GrupoSubgrupoClave = "COS|107";
            oferta.Detalles.Add(filtro);

            vm.GuardarOfertaCombinadaCommand.Execute(oferta);

            Assert.IsNotNull(capturado, "Debe llamar a CreateOfertaCombinada");
            var fila = capturado.Detalles.Single();
            Assert.IsNull(fila.Producto);
            Assert.AreEqual("COS", fila.Grupo);
            Assert.AreEqual("107", fila.Subgrupo);
            Assert.AreEqual("CV", fila.Familia);
        }

        [TestMethod]
        public void Guardar_LineaSoloConSubgrupo_EsUnFiltroValido()
        {
            // El subgrupo puede ir solo (sin familia ni prefijo): sigue siendo una fila de filtro.
            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .Returns(Task.FromResult(new OfertaCombinadaModel
                {
                    Id = 1,
                    Nombre = "Solo subgrupo",
                    Detalles = new List<OfertaCombinadaDetalleModel>()
                }));

            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper { Nombre = "Solo subgrupo", ImporteMinimo = 100 };
            var filtro = new DetalleOfertaCombinadaWrapper { Cantidad = 3, Precio = 0 };
            filtro.GrupoSubgrupoClave = "COS|107";
            oferta.Detalles.Add(filtro);
            oferta.Detalles.Add(Detalle("REGALO", 1, 0m));

            vm.GuardarOfertaCombinadaCommand.Execute(oferta);

            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .MustHaveHappened();
        }

        [TestMethod]
        public void CargarSubgrupos_InsertaLaOpcionEnBlancoLaPrimera()
        {
            A.CallTo(() => _service.GetSubgrupos()).Returns(Task.FromResult(new List<SubgrupoComboModel>
            {
                new SubgrupoComboModel { Grupo = "COS", Subgrupo = "107", Nombre = "Aceites, fluidos y geles profesionales" },
                new SubgrupoComboModel { Grupo = "PEL", Subgrupo = "001", Nombre = "Champús" }
            }));

            var vm = CrearViewModel();

            Assert.IsNotNull(vm.Subgrupos, "El combo debe cargarse al crear el ViewModel");
            Assert.AreEqual(3, vm.Subgrupos.Count);
            Assert.AreEqual("|", vm.Subgrupos[0].Clave, "La primera opción es la de en blanco (sin subgrupo)");
            Assert.AreEqual("COS/107 - Aceites, fluidos y geles profesionales", vm.Subgrupos[1].Etiqueta);
        }

        [TestMethod]
        public void Guardar_UnaSolaLineaSinImporteMinimoConRegalarMenorImporte_LlamaAlServicio()
        {
            // Regla relajada (NestoAPI#289/#290): con "Regalo menor importe" la oferta de una
            // sola fila sin importe mínimo es válida (suelo dinámico).
            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .Returns(Task.FromResult(new OfertaCombinadaModel
                {
                    Id = 1,
                    Nombre = "2+1 aceites",
                    Detalles = new List<OfertaCombinadaDetalleModel>()
                }));

            var vm = CrearViewModel();
            var oferta = new OfertaCombinadaWrapper { Nombre = "2+1 aceites", ImporteMinimo = 0, RegalarMenorImporte = true };
            var filtro = new DetalleOfertaCombinadaWrapper { Cantidad = 3, Precio = 0 };
            filtro.GrupoSubgrupoClave = "COS|107";
            oferta.Detalles.Add(filtro);

            vm.GuardarOfertaCombinadaCommand.Execute(oferta);

            A.CallTo(() => _service.CreateOfertaCombinada(A<OfertaCombinadaCreateModel>._))
                .MustHaveHappened();
        }
    }
}
