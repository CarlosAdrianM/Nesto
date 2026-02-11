using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PlantillaVenta;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Tests para BorradorPlantillaVentaService
    /// Issue #286: Borradores de PlantillaVenta
    ///
    /// Estos tests usan una carpeta temporal para no afectar los borradores reales del usuario.
    /// </summary>
    [TestClass]
    public class BorradorPlantillaVentaServiceTests
    {
        private string _carpetaTest;
        private IConfiguracion _configuracionMock;

        [TestInitialize]
        public void Setup()
        {
            // Crear carpeta temporal para tests
            _carpetaTest = Path.Combine(Path.GetTempPath(), "NestoTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_carpetaTest);

            _configuracionMock = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracionMock.usuario).Returns("usuario.test");
        }

        [TestCleanup]
        public void Cleanup()
        {
            // Limpiar carpeta temporal
            if (Directory.Exists(_carpetaTest))
            {
                try
                {
                    Directory.Delete(_carpetaTest, true);
                }
                catch
                {
                    // Ignorar errores de limpieza
                }
            }
        }

        /// <summary>
        /// Helper para crear un servicio que usa nuestra carpeta de test
        /// </summary>
        private BorradorPlantillaVentaService CrearServicioTest()
        {
            // Crear el servicio normalmente
            var servicio = new BorradorPlantillaVentaService(_configuracionMock);

            // Usar reflexion para cambiar la carpeta de borradores a nuestra carpeta de test
            var field = typeof(BorradorPlantillaVentaService).GetField("_carpetaBorradores",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(servicio, _carpetaTest);

            return servicio;
        }

        private BorradorPlantillaVenta CrearBorradorPrueba(string cliente = "10001")
        {
            return new BorradorPlantillaVenta
            {
                Empresa = "1",
                Cliente = cliente,
                Contacto = "0",
                NombreCliente = $"Cliente {cliente}",
                LineasProducto = new List<LineaPlantillaVenta>
                {
                    new LineaPlantillaVenta
                    {
                        producto = "PROD001",
                        texto = "Producto de prueba",
                        cantidad = 5,
                        precio = 10.00M
                    }
                },
                Total = 50.00M
            };
        }

        #region GuardarBorrador Tests

        [TestMethod]
        public void GuardarBorrador_AsignaId_SiNoTiene()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            Assert.IsNull(borrador.Id);

            // Act
            var resultado = servicio.GuardarBorrador(borrador);

            // Assert
            Assert.IsNotNull(resultado.Id);
            Assert.AreNotEqual(string.Empty, resultado.Id);
        }

        [TestMethod]
        public void GuardarBorrador_MantieneId_SiYaTiene()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.Id = "mi-id-personalizado";

            // Act
            var resultado = servicio.GuardarBorrador(borrador);

            // Assert
            Assert.AreEqual("mi-id-personalizado", resultado.Id);
        }

        [TestMethod]
        public void GuardarBorrador_AsignaFechaCreacion_SiNoTiene()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            Assert.AreEqual(DateTime.MinValue, borrador.FechaCreacion);

            // Act
            var resultado = servicio.GuardarBorrador(borrador);

            // Assert
            Assert.AreNotEqual(DateTime.MinValue, resultado.FechaCreacion);
            Assert.IsTrue(resultado.FechaCreacion > DateTime.Now.AddMinutes(-1));
        }

        [TestMethod]
        public void GuardarBorrador_AsignaUsuario_SiNoTiene()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            Assert.IsNull(borrador.Usuario);

            // Act
            var resultado = servicio.GuardarBorrador(borrador);

            // Assert
            Assert.AreEqual("usuario.test", resultado.Usuario);
        }

        [TestMethod]
        public void GuardarBorrador_CreaArchivoJson()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();

            // Act
            var resultado = servicio.GuardarBorrador(borrador);

            // Assert
            var rutaEsperada = Path.Combine(_carpetaTest, $"{resultado.Id}.json");
            Assert.IsTrue(File.Exists(rutaEsperada), $"El archivo no existe: {rutaEsperada}");
        }

        [TestMethod]
        public void GuardarBorrador_ArchivoContieneJsonValido()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();

            // Act
            var resultado = servicio.GuardarBorrador(borrador);

            // Assert
            var rutaArchivo = Path.Combine(_carpetaTest, $"{resultado.Id}.json");
            var json = File.ReadAllText(rutaArchivo);
            var borradorDeserializado = JsonConvert.DeserializeObject<BorradorPlantillaVenta>(json);

            Assert.IsNotNull(borradorDeserializado);
            Assert.AreEqual(borrador.Cliente, borradorDeserializado.Cliente);
            Assert.AreEqual(borrador.NombreCliente, borradorDeserializado.NombreCliente);
        }

        #endregion

        #region CargarBorrador Tests

        [TestMethod]
        public void CargarBorrador_DevuelveBorradorCompleto()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borradorOriginal = CrearBorradorPrueba();
            borradorOriginal.ComentarioRuta = "Comentario de prueba";
            borradorOriginal.EsPresupuesto = true;
            borradorOriginal.FormaVenta = 2;
            var guardado = servicio.GuardarBorrador(borradorOriginal);

            // Act
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.IsNotNull(cargado);
            Assert.AreEqual(guardado.Id, cargado.Id);
            Assert.AreEqual("10001", cargado.Cliente);
            Assert.AreEqual("Cliente 10001", cargado.NombreCliente);
            Assert.AreEqual("Comentario de prueba", cargado.ComentarioRuta);
            Assert.IsTrue(cargado.EsPresupuesto);
            Assert.AreEqual(2, cargado.FormaVenta);
        }

        [TestMethod]
        public void CargarBorrador_DevuelveLineasProducto()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borradorOriginal = CrearBorradorPrueba();
            var guardado = servicio.GuardarBorrador(borradorOriginal);

            // Act
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.IsNotNull(cargado.LineasProducto);
            Assert.AreEqual(1, cargado.LineasProducto.Count);
            Assert.AreEqual("PROD001", cargado.LineasProducto[0].producto);
            Assert.AreEqual(5, cargado.LineasProducto[0].cantidad);
            Assert.AreEqual(10.00M, cargado.LineasProducto[0].precio);
        }

        [TestMethod]
        public void CargarBorrador_DevuelveLineasRegalo()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borradorOriginal = CrearBorradorPrueba();
            borradorOriginal.LineasRegalo = new List<LineaRegalo>
            {
                new LineaRegalo { producto = "REG001", texto = "Regalo 1", cantidad = 2 }
            };
            var guardado = servicio.GuardarBorrador(borradorOriginal);

            // Act
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.IsNotNull(cargado.LineasRegalo);
            Assert.AreEqual(1, cargado.LineasRegalo.Count);
            Assert.AreEqual("REG001", cargado.LineasRegalo[0].producto);
            Assert.AreEqual(2, cargado.LineasRegalo[0].cantidad);
        }

        [TestMethod]
        public void CargarBorrador_IdInexistente_DevuelveNull()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CargarBorrador("id-que-no-existe");

            // Assert
            Assert.IsNull(resultado);
        }

        #endregion

        #region ObtenerBorradores Tests

        [TestMethod]
        public void ObtenerBorradores_SinBorradores_DevuelveListaVacia()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var borradores = servicio.ObtenerBorradores();

            // Assert
            Assert.IsNotNull(borradores);
            Assert.AreEqual(0, borradores.Count);
        }

        [TestMethod]
        public void ObtenerBorradores_ConBorradores_DevuelveListaOrdenada()
        {
            // Arrange
            var servicio = CrearServicioTest();

            var borrador1 = CrearBorradorPrueba("10001");
            borrador1.FechaCreacion = new DateTime(2025, 6, 10);
            servicio.GuardarBorrador(borrador1);

            var borrador2 = CrearBorradorPrueba("10002");
            borrador2.FechaCreacion = new DateTime(2025, 6, 15); // Mas reciente
            servicio.GuardarBorrador(borrador2);

            // Act
            var borradores = servicio.ObtenerBorradores();

            // Assert
            Assert.AreEqual(2, borradores.Count);
            // El mas reciente debe estar primero
            Assert.AreEqual("10002", borradores[0].Cliente);
            Assert.AreEqual("10001", borradores[1].Cliente);
        }

        [TestMethod]
        public void ObtenerBorradores_NoCargaLineas_ParaAhorrarMemoria()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.LineasRegalo = new List<LineaRegalo> { new LineaRegalo { producto = "REG1" } };
            servicio.GuardarBorrador(borrador);

            // Act
            var borradores = servicio.ObtenerBorradores();

            // Assert
            Assert.AreEqual(1, borradores.Count);
            Assert.IsNull(borradores[0].LineasProducto, "LineasProducto deberia ser null en la lista");
            Assert.IsNull(borradores[0].LineasRegalo, "LineasRegalo deberia ser null en la lista");
        }

        #endregion

        #region EliminarBorrador Tests

        [TestMethod]
        public void EliminarBorrador_EliminaArchivo()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            var guardado = servicio.GuardarBorrador(borrador);
            var rutaArchivo = Path.Combine(_carpetaTest, $"{guardado.Id}.json");
            Assert.IsTrue(File.Exists(rutaArchivo));

            // Act
            var resultado = servicio.EliminarBorrador(guardado.Id);

            // Assert
            Assert.IsTrue(resultado);
            Assert.IsFalse(File.Exists(rutaArchivo));
        }

        [TestMethod]
        public void EliminarBorrador_IdInexistente_DevuelveFalse()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.EliminarBorrador("id-que-no-existe");

            // Assert
            Assert.IsFalse(resultado);
        }

        #endregion

        #region ContarBorradores Tests

        [TestMethod]
        public void ContarBorradores_SinBorradores_DevuelveCero()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var count = servicio.ContarBorradores();

            // Assert
            Assert.AreEqual(0, count);
        }

        [TestMethod]
        public void ContarBorradores_ConBorradores_DevuelveCantidadCorrecta()
        {
            // Arrange
            var servicio = CrearServicioTest();
            servicio.GuardarBorrador(CrearBorradorPrueba("10001"));
            servicio.GuardarBorrador(CrearBorradorPrueba("10002"));
            servicio.GuardarBorrador(CrearBorradorPrueba("10003"));

            // Act
            var count = servicio.ContarBorradores();

            // Assert
            Assert.AreEqual(3, count);
        }

        #endregion

        #region EliminarTodosBorradores Tests

        [TestMethod]
        public void EliminarTodosBorradores_EliminaTodos()
        {
            // Arrange
            var servicio = CrearServicioTest();
            servicio.GuardarBorrador(CrearBorradorPrueba("10001"));
            servicio.GuardarBorrador(CrearBorradorPrueba("10002"));
            Assert.AreEqual(2, servicio.ContarBorradores());

            // Act
            var eliminados = servicio.EliminarTodosBorradores();

            // Assert
            Assert.AreEqual(2, eliminados);
            Assert.AreEqual(0, servicio.ContarBorradores());
        }

        [TestMethod]
        public void EliminarTodosBorradores_SinBorradores_DevuelveCero()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var eliminados = servicio.EliminarTodosBorradores();

            // Assert
            Assert.AreEqual(0, eliminados);
        }

        #endregion

        #region Persistencia de campos especificos Tests

        [TestMethod]
        public void GuardarYCargar_PreservaFormaVentaTipo3()
        {
            // Arrange - Issue #286: FormaVenta tipo 3 (otras)
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.FormaVenta = 3;
            borrador.FormaVentaOtrasCodigo = "WEB";

            // Act
            var guardado = servicio.GuardarBorrador(borrador);
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.AreEqual(3, cargado.FormaVenta);
            Assert.AreEqual("WEB", cargado.FormaVentaOtrasCodigo);
        }

        [TestMethod]
        public void GuardarYCargar_PreservaContacto()
        {
            // Arrange - Issue #286: Contacto no se cargaba
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.Contacto = "2"; // Contacto diferente al principal

            // Act
            var guardado = servicio.GuardarBorrador(borrador);
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.AreEqual("2", cargado.Contacto);
        }

        [TestMethod]
        public void GuardarYCargar_PreservaMantenerJunto()
        {
            // Arrange - Issue #286: MantenerJunto no se cargaba
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.MantenerJunto = true;

            // Act
            var guardado = servicio.GuardarBorrador(borrador);
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.IsTrue(cargado.MantenerJunto);
        }

        [TestMethod]
        public void GuardarYCargar_PreservaFormaPagoYPlazos()
        {
            // Arrange - Issue #286: FormaPago y PlazosPago no se cargaban
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.FormaPago = "TRANSF";
            borrador.PlazosPago = "60D";

            // Act
            var guardado = servicio.GuardarBorrador(borrador);
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.AreEqual("TRANSF", cargado.FormaPago);
            Assert.AreEqual("60D", cargado.PlazosPago);
        }

        [TestMethod]
        public void GuardarYCargar_PreservaComentarioPicking()
        {
            // Arrange - Issue #286: ComentarioPicking no se cargaba
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.ComentarioPicking = "Fragil - Manejar con cuidado";

            // Act
            var guardado = servicio.GuardarBorrador(borrador);
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.AreEqual("Fragil - Manejar con cuidado", cargado.ComentarioPicking);
        }

        [TestMethod]
        public void GuardarYCargar_PreservaFechaEntrega()
        {
            // Arrange - Issue #286: FechaEntrega no se cargaba
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.FechaEntrega = new DateTime(2025, 7, 15);

            // Act
            var guardado = servicio.GuardarBorrador(borrador);
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.AreEqual(new DateTime(2025, 7, 15), cargado.FechaEntrega);
        }

        [TestMethod]
        public void GuardarYCargar_PreservaAlmacen()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var borrador = CrearBorradorPrueba();
            borrador.AlmacenCodigo = "REI";

            // Act
            var guardado = servicio.GuardarBorrador(borrador);
            var cargado = servicio.CargarBorrador(guardado.Id);

            // Assert
            Assert.AreEqual("REI", cargado.AlmacenCodigo);
        }

        #endregion
    }
}
