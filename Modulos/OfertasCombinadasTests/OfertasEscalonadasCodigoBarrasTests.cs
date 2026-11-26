using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.OfertasCombinadas.Interfaces;
using Nesto.Modulos.OfertasCombinadas.Models;
using Nesto.Modulos.OfertasCombinadas.ViewModels;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Nesto.Modulos.OfertasCombinadasTests
{
    /// <summary>
    /// Nesto#647: «Añadir referencias» de una oferta escalonada con un código de barras de varios productos. Antes se tragaba
    /// el duplicado y se añadía la línea como «*** NO EXISTE ***»; ahora se pregunta cuál es.
    /// </summary>
    [TestClass]
    public class OfertasEscalonadasCodigoBarrasTests
    {
        private const string EAN = "8437017506362";
        private IOfertasCombinadasService _service;
        private IServicioDialogos _dialogos;
        private IServicioProducto _servicioProducto;

        [TestInitialize]
        public void Setup()
        {
            _service = A.Fake<IOfertasCombinadasService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _servicioProducto = A.Fake<IServicioProducto>();
            A.CallTo(() => _service.GetOfertasCombinadas(A<string>._, A<bool>._))
                .Returns(Task.FromResult(new List<OfertaCombinadaModel>()));
            A.CallTo(() => _service.GetOfertasPermitidasFamilia(A<string>._))
                .Returns(Task.FromResult(new List<OfertaPermitidaFamiliaModel>()));
            A.CallTo(() => _servicioProducto.BuscarProducto(A<string>._, EAN, A<string>._, A<string>._, A<short>._))
                .Throws(new CodigoBarrasDuplicadoException(new List<ProductoCodigoBarrasDuplicado>
                {
                    new ProductoCodigoBarrasDuplicado { Producto = "41000", Nombre = "GUANTES M" },
                    new ProductoCodigoBarrasDuplicado { Producto = "41001", Nombre = "GUANTES P" }
                }));
            A.CallTo(() => _servicioProducto.BuscarProducto(A<string>._, "41001", A<string>._, A<string>._, A<short>._))
                .Returns(Task.FromResult(new ProductoDTO { Producto = "41001", Nombre = "GUANTES P", Precio = 5 }));
        }

        private OfertasCombinadasViewModel CrearViewModel()
        {
            var vm = new OfertasCombinadasViewModel(_service, A.Fake<IConfiguracion>(), _dialogos, _servicioProducto);
            vm.OfertaEscalonadaSeleccionada = new OfertaEscalonadaWrapper { Id = 1, Nombre = "Guantes" };
            vm.ReferenciasTexto = EAN;
            return vm;
        }

        [TestMethod]
        public async Task CodigoDeVariosProductos_PreguntaYAnadeElElegido()
        {
            A.CallTo(() => _dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO, A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "producto", "41001" } }));
            var vm = CrearViewModel();

            await vm.AnadirReferenciasAsync();

            var linea = vm.OfertaEscalonadaSeleccionada.Productos.Single();
            Assert.AreEqual("41001", linea.Producto);
            Assert.AreEqual("GUANTES P", linea.ProductoNombre);
            Assert.IsFalse(linea.NoEncontrado);
        }

        [TestMethod]
        public async Task CodigoDeVariosProductos_SiCancela_NoAnadeNadaNiNoExiste()
        {
            A.CallTo(() => _dialogos.ShowDialogAsync(ElectorProductoCodigoBarras.NOMBRE_DIALOGO, A<ParametrosDialogo>._))
                .Returns(new ResultadoDialogo(ResultadoBoton.Cancel));
            var vm = CrearViewModel();

            await vm.AnadirReferenciasAsync();

            Assert.AreEqual(0, vm.OfertaEscalonadaSeleccionada.Productos.Count);
        }
    }
}
