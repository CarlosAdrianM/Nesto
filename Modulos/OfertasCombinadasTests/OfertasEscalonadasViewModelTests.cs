using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using Prism.Regions;
using Prism.Services.Dialogs;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.OfertasCombinadasTests
{
    /// <summary>
    /// Pestaña de Ofertas Escalonadas (NestoAPI#226 / Nesto#376): lista de referencias combinables
    /// con escalado de descuento por cantidad. La UX clave es pegar las referencias como texto
    /// (comas, espacios o saltos de línea) y rellenar un grid Cantidad / % Descuento.
    /// </summary>
    [TestClass]
    public class OfertasEscalonadasViewModelTests
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

            // Al añadir referencias se resuelve nombre y precio de tarifa de cada producto.
            A.CallTo(() => _servicioProducto.BuscarProducto(A<string>._, A<string>._, A<string>._, A<string>._, A<short>._))
                .ReturnsLazily((string empresa, string producto, string cliente, string contacto, short cantidad) =>
                    Task.FromResult(new ProductoDTO
                    {
                        Producto = producto,
                        Nombre = "Producto " + producto,
                        Precio = 18.50m
                    }));
        }

        private OfertasCombinadasViewModel CrearViewModel()
        {
            return new OfertasCombinadasViewModel(_service, _configuracion, _dialogService, _regionManager, _servicioProducto);
        }

        #region Parseo de referencias

        [TestMethod]
        public void ParsearReferencias_SeparadasPorComas_DevuelveTodas()
        {
            var referencias = OfertasCombinadasViewModel.ParsearReferencias("44707,44708,44709");

            CollectionAssert.AreEqual(new[] { "44707", "44708", "44709" }, referencias);
        }

        [TestMethod]
        public void ParsearReferencias_MezclaDeSeparadores_DevuelveTodas()
        {
            // Lo típico al pegar de un correo o un Excel: comas, espacios, saltos de línea y tabuladores.
            var referencias = OfertasCombinadasViewModel.ParsearReferencias("44707, 44708;44709\r\n44710\t44711\n 44712 ");

            CollectionAssert.AreEqual(new[] { "44707", "44708", "44709", "44710", "44711", "44712" }, referencias);
        }

        [TestMethod]
        public void ParsearReferencias_ConDuplicadosYVacios_LosElimina()
        {
            var referencias = OfertasCombinadasViewModel.ParsearReferencias("44707,,44707 ,  ,44708");

            CollectionAssert.AreEqual(new[] { "44707", "44708" }, referencias);
        }

        [TestMethod]
        public void ParsearReferencias_TextoVacioONulo_DevuelveListaVacia()
        {
            Assert.AreEqual(0, OfertasCombinadasViewModel.ParsearReferencias(null).Count);
            Assert.AreEqual(0, OfertasCombinadasViewModel.ParsearReferencias("   ").Count);
        }

        #endregion

        #region Añadir referencias a la oferta

        [TestMethod]
        public async Task AnadirReferencias_ConOfertaSeleccionada_ResuelveNombreYPrecioDeTarifa()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper();
            vm.OfertasEscalonadas.Add(oferta);
            vm.OfertaEscalonadaSeleccionada = oferta;
            vm.ReferenciasTexto = "44707, 44708\r\n44709";

            vm.AnadirReferenciasCommand.Execute(null);
            await Task.Delay(50); // el comando es async void: dejamos que complete

            Assert.AreEqual(3, oferta.Productos.Count);
            Assert.IsTrue(oferta.Productos.All(p => p.PrecioBase == 18.50m),
                "El precio base se rellena con el precio de tarifa al añadir (sigue siendo editable)");
            Assert.AreEqual("Producto 44707", oferta.Productos.First().ProductoNombre,
                "El nombre del producto se resuelve al añadir para poder verificar las referencias");
            Assert.IsTrue(oferta.Productos.All(p => !p.NoEncontrado));
            Assert.AreEqual(string.Empty, vm.ReferenciasTexto, "El cuadro de texto se limpia tras añadir");
            Assert.IsTrue(oferta.HaCambiado);
        }

        [TestMethod]
        public async Task AnadirReferencias_ReferenciaInexistente_LaMarcaComoNoEncontrada()
        {
            A.CallTo(() => _servicioProducto.BuscarProducto(A<string>._, "99999", A<string>._, A<string>._, A<short>._))
                .Returns(Task.FromResult<ProductoDTO>(null));

            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper();
            vm.OfertasEscalonadas.Add(oferta);
            vm.OfertaEscalonadaSeleccionada = oferta;
            vm.ReferenciasTexto = "44707,99999";

            vm.AnadirReferenciasCommand.Execute(null);
            await Task.Delay(50);

            Assert.AreEqual(2, oferta.Productos.Count, "La referencia inexistente se añade igualmente para que el usuario la vea y corrija");
            var noEncontrado = oferta.Productos.Single(p => p.Producto == "99999");
            Assert.IsTrue(noEncontrado.NoEncontrado, "Debe marcarse para pintarla en rojo");
            Assert.IsNull(noEncontrado.PrecioBase);
        }

        [TestMethod]
        public async Task AnadirReferencias_ReferenciaYaEnLaOferta_NoLaDuplica()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper();
            oferta.AnadirProducto(new OfertaEscalonadaProductoWrapper { Producto = "44707" });
            vm.OfertasEscalonadas.Add(oferta);
            vm.OfertaEscalonadaSeleccionada = oferta;
            vm.ReferenciasTexto = "44707,44708";

            vm.AnadirReferenciasCommand.Execute(null);
            await Task.Delay(50);

            Assert.AreEqual(2, oferta.Productos.Count, "44707 ya estaba y no debe duplicarse");
        }

        [TestMethod]
        public void Wrapper_CorregirReferenciaNoEncontrada_QuitaLaMarcaRoja()
        {
            var producto = new OfertaEscalonadaProductoWrapper { Producto = "99999", NoEncontrado = true };

            producto.Producto = "44707";

            Assert.IsFalse(producto.NoEncontrado, "Al corregir la referencia desaparece la marca de no encontrada");
        }

        [TestMethod]
        public void AnadirReferencias_SinOfertaSeleccionada_NoSePuedeEjecutar()
        {
            var vm = CrearViewModel();
            vm.OfertaEscalonadaSeleccionada = null;

            Assert.IsFalse(vm.AnadirReferenciasCommand.CanExecute(null));
        }

        #endregion

        #region Tramos

        [TestMethod]
        public void NuevoTramo_SugiereUnaUnidadMasQueElUltimo()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper();
            oferta.AnadirTramo(new OfertaEscalonadaTramoWrapper { CantidadMinima = 2, DescuentoPorcentaje = 5 });
            oferta.AnadirTramo(new OfertaEscalonadaTramoWrapper { CantidadMinima = 3, DescuentoPorcentaje = 10 });
            vm.OfertasEscalonadas.Add(oferta);
            vm.OfertaEscalonadaSeleccionada = oferta;

            vm.NuevoTramoCommand.Execute(null);

            Assert.AreEqual(3, oferta.Tramos.Count);
            Assert.AreEqual<short>(4, oferta.Tramos.Last().CantidadMinima);
        }

        [TestMethod]
        public void NuevoTramo_SinTramosPrevios_EmpiezaEnDos()
        {
            // El primer tramo con sentido es 2 ("a partir de 2 unidades"); 1 unidad es el precio normal.
            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper();
            vm.OfertasEscalonadas.Add(oferta);
            vm.OfertaEscalonadaSeleccionada = oferta;

            vm.NuevoTramoCommand.Execute(null);

            Assert.AreEqual<short>(2, oferta.Tramos.Single().CantidadMinima);
        }

        #endregion

        #region Guardar

        [TestMethod]
        public async Task Guardar_MapeaPorcentajeATantoPorUno()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper { Nombre = "Escalado StarPil" };
            oferta.AnadirProducto(new OfertaEscalonadaProductoWrapper { Producto = "44707" });
            oferta.AnadirTramo(new OfertaEscalonadaTramoWrapper { CantidadMinima = 2, DescuentoPorcentaje = 5 });
            oferta.AnadirTramo(new OfertaEscalonadaTramoWrapper { CantidadMinima = 6, DescuentoPorcentaje = 25 });
            vm.OfertasEscalonadas.Add(oferta);
            vm.OfertaEscalonadaSeleccionada = oferta;

            OfertaEscalonadaCreateModel enviado = null;
            A.CallTo(() => _service.CreateOfertaEscalonada(A<OfertaEscalonadaCreateModel>._))
                .Invokes((OfertaEscalonadaCreateModel m) => enviado = m)
                .Returns(Task.FromResult(new OfertaEscalonadaModel { Id = 1, Nombre = "Escalado StarPil" }));

            vm.GuardarOfertaEscalonadaCommand.Execute(oferta);
            await Task.Delay(50); // el comando es async void: dejamos que complete

            Assert.IsNotNull(enviado, "Debe llamar al servicio de creación");
            Assert.AreEqual(0.05m, enviado.Tramos.Single(t => t.CantidadMinima == 2).Descuento,
                "El 5 % del grid debe viajar como 0.05 (tanto por uno)");
            Assert.AreEqual(0.25m, enviado.Tramos.Single(t => t.CantidadMinima == 6).Descuento);
        }

        [TestMethod]
        public void Guardar_SinTramos_MuestraErrorYNoLlamaAlServicio()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper { Nombre = "Escalado sin tramos" };
            oferta.AnadirProducto(new OfertaEscalonadaProductoWrapper { Producto = "44707" });
            vm.OfertasEscalonadas.Add(oferta);
            vm.OfertaEscalonadaSeleccionada = oferta;

            vm.GuardarOfertaEscalonadaCommand.Execute(oferta);

            A.CallTo(() => _service.CreateOfertaEscalonada(A<OfertaEscalonadaCreateModel>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Guardar_SinProductos_MuestraErrorYNoLlamaAlServicio()
        {
            var vm = CrearViewModel();
            var oferta = new OfertaEscalonadaWrapper { Nombre = "Escalado sin productos" };
            oferta.AnadirTramo(new OfertaEscalonadaTramoWrapper { CantidadMinima = 2, DescuentoPorcentaje = 5 });
            vm.OfertasEscalonadas.Add(oferta);
            vm.OfertaEscalonadaSeleccionada = oferta;

            vm.GuardarOfertaEscalonadaCommand.Execute(oferta);

            A.CallTo(() => _service.CreateOfertaEscalonada(A<OfertaEscalonadaCreateModel>._)).MustNotHaveHappened();
        }

        #endregion

        #region Wrapper

        [TestMethod]
        public void Wrapper_CargaDesdeModelo_OrdenaTramosYNoMarcaCambios()
        {
            var model = new OfertaEscalonadaModel
            {
                Id = 300,
                Nombre = "Escalado StarPil",
                Productos = new List<OfertaEscalonadaProductoModel>
                {
                    new OfertaEscalonadaProductoModel { Id = 1, Producto = "44707", PrecioBase = 18.50m }
                },
                Tramos = new List<OfertaEscalonadaTramoModel>
                {
                    new OfertaEscalonadaTramoModel { Id = 2, CantidadMinima = 6, Descuento = 0.25m },
                    new OfertaEscalonadaTramoModel { Id = 1, CantidadMinima = 2, Descuento = 0.05m }
                }
            };

            var wrapper = new OfertaEscalonadaWrapper(model);

            Assert.IsFalse(wrapper.HaCambiado, "Cargar desde el servidor no es un cambio");
            Assert.AreEqual<short>(2, wrapper.Tramos.First().CantidadMinima, "Los tramos se ordenan por cantidad");
            Assert.AreEqual(5m, wrapper.Tramos.First().DescuentoPorcentaje, "0.05 del API se muestra como 5 %");
            Assert.AreEqual(25m, wrapper.Tramos.Last().DescuentoPorcentaje);
        }

        [TestMethod]
        public void Wrapper_EditarUnTramoCargado_MarcaLaOfertaComoCambiada()
        {
            var model = new OfertaEscalonadaModel
            {
                Id = 300,
                Nombre = "Escalado StarPil",
                Productos = new List<OfertaEscalonadaProductoModel>(),
                Tramos = new List<OfertaEscalonadaTramoModel>
                {
                    new OfertaEscalonadaTramoModel { Id = 1, CantidadMinima = 2, Descuento = 0.05m }
                }
            };
            var wrapper = new OfertaEscalonadaWrapper(model);

            wrapper.Tramos.First().DescuentoPorcentaje = 7m;

            Assert.IsTrue(wrapper.HaCambiado, "Editar un tramo debe hacer visible el botón Guardar");
        }

        #endregion
    }
}
