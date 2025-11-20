using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Events;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Tests REALES de SelectorDireccionEntrega con servicio mockeado.
    /// Carlos 20/11/24: FASE 4 - Tests que verifican comportamiento del control
    /// usando FakeItEasy para mockear IServicioDireccionesEntrega.
    ///
    /// Estos tests complementan los tests de caracterización (SelectorDireccionEntregaTests.cs)
    /// verificando el comportamiento real del control con datos mockeados.
    /// </summary>
    [TestClass]
    public class SelectorDireccionEntregaTestsReales
    {
        #region Tests: Carga de Direcciones desde Servicio

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CargarDatos_ConEmpresaYCliente_LlamaServicioConParametrosCorrectos()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            var direccionesEsperadas = new List<DireccionesEntregaCliente>
            {
                new DireccionesEntregaCliente
                {
                    contacto = "0",
                    nombre = "Dirección Principal",
                    direccion = "Calle Test 123",
                    esDireccionPorDefecto = true
                }
            };

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega("1", "10", null))
                .Returns(Task.FromResult<IEnumerable<DireccionesEntregaCliente>>(direccionesEsperadas));

            SelectorDireccionEntrega sut = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                sut.Empresa = "1";
                sut.Cliente = "10";

                // Esperar a que se procese (el cambio de Empresa llama directamente a cargarDatos)
                System.Threading.Thread.Sleep(300);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Esperar un poco más para asegurar que la tarea async completó
            await Task.Delay(200);

            // Assert
            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega("1", "10", null))
                .MustHaveHappened();
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CargarDatos_ConDireccionesDevueltas_ActualizaListaDirecciones()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            var direccionesEsperadas = new List<DireccionesEntregaCliente>
            {
                new DireccionesEntregaCliente { contacto = "0", nombre = "Principal" },
                new DireccionesEntregaCliente { contacto = "5", nombre = "Secundaria" }
            };

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega(A<string>._, A<string>._, A<decimal?>._))
                .Returns(Task.FromResult<IEnumerable<DireccionesEntregaCliente>>(direccionesEsperadas));

            SelectorDireccionEntrega sut = null;
            int cantidadDirecciones = 0;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                sut.Empresa = "1";
                sut.Cliente = "10";

                System.Threading.Thread.Sleep(200); // Esperar carga
                cantidadDirecciones = sut.listaDireccionesEntrega.ListaOriginal?.Count ?? 0;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(300);

            // Assert
            Assert.AreEqual(2, cantidadDirecciones,
                "Debería haber cargado 2 direcciones desde el servicio mockeado");
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CargarDatos_ConTotalPedido_PasaTotalPedidoAlServicio()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            var direcciones = new List<DireccionesEntregaCliente> { };

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega("1", "10", 150.75m))
                .Returns(Task.FromResult<IEnumerable<DireccionesEntregaCliente>>(direcciones));

            SelectorDireccionEntrega sut = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                sut.Empresa = "1";
                sut.Cliente = "10";
                sut.TotalPedido = 150.75m;

                System.Threading.Thread.Sleep(200); // Esperar carga
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(300);

            // Assert
            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega("1", "10", 150.75m))
                .MustHaveHappened();
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CargarDatos_ConTotalPedidoCero_NoEnviaTotalPedidoAlServicio()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            var direcciones = new List<DireccionesEntregaCliente> { };

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega("1", "10", null))
                .Returns(Task.FromResult<IEnumerable<DireccionesEntregaCliente>>(direcciones));

            SelectorDireccionEntrega sut = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                sut.Empresa = "1";
                sut.Cliente = "10";
                sut.TotalPedido = 0; // Cero no se envía

                System.Threading.Thread.Sleep(200); // Esperar carga
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(300);

            // Assert: Verifica que se llamó con null, NO con 0
            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega("1", "10", null))
                .MustHaveHappened();
        }

        #endregion

        #region Tests: Auto-selección de Dirección Por Defecto

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CargarDatos_SinSeleccionPrevia_SeleccionaDireccionPorDefecto()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            var direcciones = new List<DireccionesEntregaCliente>
            {
                new DireccionesEntregaCliente
                {
                    contacto = "0",
                    nombre = "Principal",
                    esDireccionPorDefecto = true
                },
                new DireccionesEntregaCliente
                {
                    contacto = "5",
                    nombre = "Secundaria",
                    esDireccionPorDefecto = false
                }
            };

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega(A<string>._, A<string>._, A<decimal?>._))
                .Returns(Task.FromResult<IEnumerable<DireccionesEntregaCliente>>(direcciones));

            SelectorDireccionEntrega sut = null;
            DireccionesEntregaCliente direccionSeleccionada = null;
            var completado = new System.Threading.ManualResetEventSlim(false);

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                sut.Empresa = "1";
                sut.Cliente = "10";
                // NO establecer Seleccionada ni DireccionCompleta

                // Esperar carga y auto-selección de forma sincrónica
                System.Threading.Thread.Sleep(300);
                direccionSeleccionada = sut.DireccionCompleta;
                completado.Set();
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Esperar señal de que completó
            completado.Wait(1000);

            // Assert
            Assert.IsNotNull(direccionSeleccionada, "Debería auto-seleccionar una dirección");
            Assert.AreEqual("0", direccionSeleccionada.contacto,
                "Debería auto-seleccionar la dirección con esDireccionPorDefecto=true");
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CargarDatos_ConSeleccionadaExistente_RespetaSeleccion()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            var direcciones = new List<DireccionesEntregaCliente>
            {
                new DireccionesEntregaCliente
                {
                    contacto = "0",
                    nombre = "Principal",
                    esDireccionPorDefecto = true
                },
                new DireccionesEntregaCliente
                {
                    contacto = "5",
                    nombre = "Secundaria",
                    esDireccionPorDefecto = false
                }
            };

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega(A<string>._, A<string>._, A<decimal?>._))
                .Returns(Task.FromResult<IEnumerable<DireccionesEntregaCliente>>(direcciones));

            SelectorDireccionEntrega sut = null;
            DireccionesEntregaCliente direccionSeleccionada = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                sut.Seleccionada = "5"; // Pre-seleccionar contacto 5
                sut.Empresa = "1";
                sut.Cliente = "10";

                System.Threading.Thread.Sleep(300); // Esperar carga
                direccionSeleccionada = sut.DireccionCompleta;
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(300);

            // Assert
            Assert.IsNotNull(direccionSeleccionada, "Debería seleccionar la dirección especificada");
            Assert.AreEqual("5", direccionSeleccionada.contacto,
                "Debería respetar Seleccionada='5', NO auto-seleccionar la por defecto");
        }

        #endregion

        #region Tests: Manejo de Errores

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        [ExpectedException(typeof(System.Exception))]
        public async Task CargarDatos_CuandoServicioLanzaExcepcion_PropagaExcepcion()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega(A<string>._, A<string>._, A<decimal?>._))
                .Throws(new System.Exception("Error al obtener direcciones de entrega: NotFound"));

            SelectorDireccionEntrega sut = null;
            Exception excepcionCapturada = null;

            // Act
            Thread thread = new Thread(() =>
            {
                try
                {
                    sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                    sut.Empresa = "1";
                    sut.Cliente = "10";

                    System.Threading.Thread.Sleep(200); // Esperar a que intente cargar
                }
                catch (Exception ex)
                {
                    excepcionCapturada = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(300);

            // Assert
            if (excepcionCapturada != null)
            {
                throw excepcionCapturada;
            }
        }

        #endregion

        #region Tests: Servicio Null (Modo Degradado)

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CargarDatos_ConServicioNull_NoLanzaExcepcion()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();

            SelectorDireccionEntrega sut = null;
            bool seLanzoExcepcion = false;

            // Act
            Thread thread = new Thread(() =>
            {
                try
                {
                    sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, null);
                    sut.Empresa = "1";
                    sut.Cliente = "10";

                    System.Threading.Thread.Sleep(200); // Esperar
                }
                catch (Exception)
                {
                    seLanzoExcepcion = true;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(300);

            // Assert
            Assert.IsFalse(seLanzoExcepcion,
                "El control debería manejar gracefully cuando el servicio es null (modo degradado)");
            Assert.IsNotNull(sut, "El control debería crearse correctamente");
        }

        #endregion

        #region Tests: Sincronización con Cambios de Propiedades

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CambiarEmpresa_LlamaServicioImmediatamente()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega(A<string>._, A<string>._, A<decimal?>._))
                .Returns(Task.FromResult<IEnumerable<DireccionesEntregaCliente>>(new List<DireccionesEntregaCliente>()));

            SelectorDireccionEntrega sut = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                sut.Cliente = "10"; // Establecer cliente primero
                sut.Empresa = "1";  // Cambiar empresa llama directamente a cargarDatos (sin debouncing)

                System.Threading.Thread.Sleep(300);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            await Task.Delay(300);

            // Assert
            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega("1", "10", null))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        [TestCategory("TestsReales")]
        public async Task CambiarCliente_UsaDebouncingAntesLlamarServicio()
        {
            // Arrange
            var configuracion = A.Fake<IConfiguracion>();
            var eventAggregator = A.Fake<IEventAggregator>();
            var regionManager = A.Fake<IRegionManager>();
            var servicioMock = A.Fake<IServicioDireccionesEntrega>();

            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega(A<string>._, A<string>._, A<decimal?>._))
                .Returns(Task.FromResult<IEnumerable<DireccionesEntregaCliente>>(new List<DireccionesEntregaCliente>()));

            SelectorDireccionEntrega sut = null;

            // Act
            Thread thread = new Thread(() =>
            {
                sut = new SelectorDireccionEntrega(regionManager, eventAggregator, configuracion, servicioMock);
                sut.Empresa = "1"; // Establecer empresa primero
                sut.Cliente = "10"; // Cambiar cliente usa debouncing de 100ms

                // Esperar MENOS del debouncing
                System.Threading.Thread.Sleep(50);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Assert: No debería haberse llamado aún
            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega(A<string>._, A<string>._, A<decimal?>._))
                .MustNotHaveHappened();

            // Esperar el debouncing completo
            await Task.Delay(300);

            // Ahora SÍ debería haberse llamado
            A.CallTo(() => servicioMock.ObtenerDireccionesEntrega("1", "10", null))
                .MustHaveHappened();
        }

        #endregion
    }
}
