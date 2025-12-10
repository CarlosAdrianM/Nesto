using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Tests para SelectorFormaVenta (Issue #252).
    ///
    /// Comportamientos clave documentados:
    /// - Carga de formas de venta cuando cambia Empresa
    /// - Auto-selección basada en valor previo de FormaVentaSeleccionada
    /// - Filtro opcional por VisiblePorComerciales
    /// - Sincronización entre FormaVentaSeleccionada (string) y FormaVentaSeleccionadaCompleta (objeto)
    /// - Protección contra bucles infinitos con flag _estaCargando
    ///
    /// Carlos 04/12/24: Tests para el nuevo control SelectorFormaVenta
    /// </summary>
    [TestClass]
    public class SelectorFormaVentaTests
    {
        #region Test: Dependency Properties

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("DependencyProperties")]
        public void SelectorFormaVenta_AlCambiarEmpresa_PropiedadSeEstableceCorrectamente()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            SelectorFormaVenta sut = null;
            string resultado = null;

            Thread thread = new Thread(() =>
            {
                sut = new SelectorFormaVenta(servicioFormaVenta);

                // Act
                sut.Empresa = "1";
                resultado = sut.Empresa;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.AreEqual("1", resultado, "La propiedad Empresa debería haberse establecido");
            Assert.IsNotNull(sut, "El control debería haberse creado correctamente");
        }

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("DependencyProperties")]
        public void SelectorFormaVenta_FormaVentaSeleccionada_EsTwoWay()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            string resultado = null;

            Thread thread = new Thread(() =>
            {
                var sut = new SelectorFormaVenta(servicioFormaVenta);

                // Act
                sut.FormaVentaSeleccionada = "DIR";
                resultado = sut.FormaVentaSeleccionada;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.AreEqual("DIR", resultado, "La propiedad FormaVentaSeleccionada debería haberse establecido");
        }

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("DependencyProperties")]
        public void SelectorFormaVenta_SoloVisiblesPorComerciales_PorDefectoEsFalse()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            bool resultado = true; // valor inicial diferente al esperado

            Thread thread = new Thread(() =>
            {
                var sut = new SelectorFormaVenta(servicioFormaVenta);

                // Act
                resultado = sut.SoloVisiblesPorComerciales;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.IsFalse(resultado, "SoloVisiblesPorComerciales debe ser false por defecto");
        }

        #endregion

        #region Test: Carga de datos

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("CargaDatos")]
        public async Task SelectorFormaVenta_AlEstablecerEmpresa_CargaFormasVenta()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            var formasVentaEsperadas = new List<FormaVentaItem>
            {
                new FormaVentaItem { Numero = "DIR", Descripcion = "Directa", VisiblePorComerciales = true },
                new FormaVentaItem { Numero = "TEL", Descripcion = "Teléfono", VisiblePorComerciales = true }
            };

            A.CallTo(() => servicioFormaVenta.ObtenerFormasVenta("1"))
                .Returns(Task.FromResult<IEnumerable<FormaVentaItem>>(formasVentaEsperadas));

            SelectorFormaVenta sut = null;
            ObservableCollection<FormaVentaItem> listaFormasVenta = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorFormaVenta(servicioFormaVenta);
                sut.Empresa = "1";

                Thread.Sleep(300);
                listaFormasVenta = sut.ListaFormasVenta;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.IsNotNull(listaFormasVenta, "La lista de formas de venta debería estar cargada");
            // NOTA: La lista incluye un elemento vacío al principio para permitir "sin selección"
            Assert.AreEqual(3, listaFormasVenta.Count, "Deberían haberse cargado 2 formas de venta + 1 elemento vacío");
            Assert.IsTrue(listaFormasVenta.Any(f => f.Numero == "DIR"), "Debería contener 'DIR'");
            Assert.IsTrue(listaFormasVenta.Any(f => f.Numero == "TEL"), "Debería contener 'TEL'");
        }

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("CargaDatos")]
        public async Task SelectorFormaVenta_ConFiltroVisiblesPorComerciales_FiltraCorrectamente()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            var formasVentaEsperadas = new List<FormaVentaItem>
            {
                new FormaVentaItem { Numero = "DIR", Descripcion = "Directa", VisiblePorComerciales = true },
                new FormaVentaItem { Numero = "INT", Descripcion = "Interna", VisiblePorComerciales = false },
                new FormaVentaItem { Numero = "TEL", Descripcion = "Teléfono", VisiblePorComerciales = true }
            };

            A.CallTo(() => servicioFormaVenta.ObtenerFormasVenta("1"))
                .Returns(Task.FromResult<IEnumerable<FormaVentaItem>>(formasVentaEsperadas));

            SelectorFormaVenta sut = null;
            ObservableCollection<FormaVentaItem> listaFormasVenta = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorFormaVenta(servicioFormaVenta);
                sut.SoloVisiblesPorComerciales = true; // Activar filtro
                sut.Empresa = "1";

                Thread.Sleep(300);
                listaFormasVenta = sut.ListaFormasVenta;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.IsNotNull(listaFormasVenta, "La lista de formas de venta debería estar cargada");
            // NOTA: La lista incluye un elemento vacío al principio para permitir "sin selección"
            Assert.AreEqual(3, listaFormasVenta.Count, "Deberían haberse cargado 2 formas visibles + 1 elemento vacío");
            Assert.IsFalse(listaFormasVenta.Any(f => f.Numero == "INT"), "La forma de venta 'INT' no debería estar en la lista");
            Assert.IsTrue(listaFormasVenta.Any(f => f.Numero == "DIR"), "Debería contener 'DIR'");
            Assert.IsTrue(listaFormasVenta.Any(f => f.Numero == "TEL"), "Debería contener 'TEL'");
        }

        #endregion

        #region Test: Auto-selección

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("AutoSeleccion")]
        public async Task SelectorFormaVenta_ConSeleccionPreviaValida_MantieneLaSeleccion()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            var formasVentaEsperadas = new List<FormaVentaItem>
            {
                new FormaVentaItem { Numero = "DIR", Descripcion = "Directa", VisiblePorComerciales = true },
                new FormaVentaItem { Numero = "TEL", Descripcion = "Teléfono", VisiblePorComerciales = true }
            };

            A.CallTo(() => servicioFormaVenta.ObtenerFormasVenta("1"))
                .Returns(Task.FromResult<IEnumerable<FormaVentaItem>>(formasVentaEsperadas));

            SelectorFormaVenta sut = null;
            string formaVentaSeleccionada = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorFormaVenta(servicioFormaVenta);
                sut.FormaVentaSeleccionada = "TEL"; // Pre-seleccionar antes de cargar
                sut.Empresa = "1";

                Thread.Sleep(300);
                formaVentaSeleccionada = sut.FormaVentaSeleccionada;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.AreEqual("TEL", formaVentaSeleccionada,
                "Debe mantener la selección previa 'TEL' si existe en la lista");
        }

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("AutoSeleccion")]
        public async Task SelectorFormaVenta_SinSeleccionPrevia_NoAutoSelecciona()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            var formasVentaEsperadas = new List<FormaVentaItem>
            {
                new FormaVentaItem { Numero = "DIR", Descripcion = "Directa", VisiblePorComerciales = true },
                new FormaVentaItem { Numero = "TEL", Descripcion = "Teléfono", VisiblePorComerciales = true }
            };

            A.CallTo(() => servicioFormaVenta.ObtenerFormasVenta("1"))
                .Returns(Task.FromResult<IEnumerable<FormaVentaItem>>(formasVentaEsperadas));

            SelectorFormaVenta sut = null;
            string formaVentaSeleccionada = "VALOR_INICIAL";

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorFormaVenta(servicioFormaVenta);
                // NO pre-seleccionar nada
                sut.Empresa = "1";

                Thread.Sleep(300);
                formaVentaSeleccionada = sut.FormaVentaSeleccionada;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.IsTrue(string.IsNullOrEmpty(formaVentaSeleccionada),
                "Sin selección previa, no debe auto-seleccionar ninguna forma de venta");
        }

        #endregion

        #region Test: Sincronización entre propiedades

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("Sincronizacion")]
        public async Task SelectorFormaVenta_AlCambiarFormaVentaSeleccionada_SincronizaConCompleta()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            var formasVentaEsperadas = new List<FormaVentaItem>
            {
                new FormaVentaItem { Numero = "DIR", Descripcion = "Directa", VisiblePorComerciales = true },
                new FormaVentaItem { Numero = "TEL", Descripcion = "Teléfono", VisiblePorComerciales = true }
            };

            A.CallTo(() => servicioFormaVenta.ObtenerFormasVenta("1"))
                .Returns(Task.FromResult<IEnumerable<FormaVentaItem>>(formasVentaEsperadas));

            SelectorFormaVenta sut = null;
            FormaVentaItem formaVentaCompleta = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorFormaVenta(servicioFormaVenta);
                sut.Empresa = "1";

                Thread.Sleep(300);

                // Cambiar la selección por string
                sut.FormaVentaSeleccionada = "DIR";

                Thread.Sleep(100);
                formaVentaCompleta = sut.FormaVentaSeleccionadaCompleta;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(200);

            // Assert
            Assert.IsNotNull(formaVentaCompleta, "FormaVentaSeleccionadaCompleta debería sincronizarse");
            Assert.AreEqual("DIR", formaVentaCompleta.Numero, "El objeto completo debe tener el mismo número");
            Assert.AreEqual("Directa", formaVentaCompleta.Descripcion, "El objeto completo debe tener la descripción correcta");
        }

        #endregion

        #region Test: Manejo de errores

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("ManejoErrores")]
        public void SelectorFormaVenta_SinEmpresa_NoLanzaExcepcion()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            SelectorFormaVenta sut = null;
            ObservableCollection<FormaVentaItem> lista = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorFormaVenta(servicioFormaVenta);
                // NO establecer Empresa

                Thread.Sleep(100);
                lista = sut.ListaFormasVenta;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.IsNotNull(sut, "El control debería crearse sin excepción");
            // La lista puede ser null o vacía, pero no debe haber crasheado
        }

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("ManejoErrores")]
        public void SelectorFormaVenta_SinServicio_FuncionaEnModoDegradado()
        {
            // Este test documenta que si el servicio es null (modo degradado),
            // el control no crashea sino que simplemente no carga datos.

            SelectorFormaVenta sut = null;

            Thread thread = new Thread(() =>
            {
                // Act: Crear control sin parámetros (sin servicio)
                sut = new SelectorFormaVenta();
                sut.Empresa = "1"; // Esto no debe crashear aunque no haya servicio
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.IsNotNull(sut, "El control debería funcionar en modo degradado sin crashear");
        }

        #endregion

        #region Test: Constructor

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("Constructor")]
        public void SelectorFormaVenta_ConstructorConServicio_CreaControlCorrectamente()
        {
            // Arrange
            var servicioFormaVenta = A.Fake<IServicioFormaVenta>();

            SelectorFormaVenta resultado = null;

            Thread thread = new Thread(() =>
            {
                // Act
                resultado = new SelectorFormaVenta(servicioFormaVenta);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.IsNotNull(resultado, "El control debería crearse correctamente con servicio inyectado");
        }

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("Constructor")]
        public void SelectorFormaVenta_ConstructorSinParametros_CreaControlParaXAMLDesigner()
        {
            // Este test documenta que el constructor sin parámetros existe
            // para que el XAML designer pueda cargar el control.

            SelectorFormaVenta resultado = null;

            Thread thread = new Thread(() =>
            {
                // Act
                resultado = new SelectorFormaVenta();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert
            Assert.IsNotNull(resultado, "El control debería crearse para XAML designer");
        }

        #endregion

        #region Test: Modelo FormaVentaItem

        [TestMethod]
        [TestCategory("SelectorFormaVenta")]
        [TestCategory("Modelo")]
        public void FormaVentaItem_TextoFormateado_DevuelveNumeroYDescripcion()
        {
            // Arrange
            var item = new FormaVentaItem
            {
                Numero = "DIR",
                Descripcion = "Directa"
            };

            // Act
            var textoFormateado = item.TextoFormateado;

            // Assert
            Assert.AreEqual("DIR - Directa", textoFormateado,
                "TextoFormateado debe ser 'Numero - Descripcion'");
        }

        #endregion
    }
}
