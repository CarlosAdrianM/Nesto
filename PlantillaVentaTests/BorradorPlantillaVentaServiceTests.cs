using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PlantillaVenta;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
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

        #region CrearBorradorDesdeJson Tests (Issue #288)

        /// <summary>
        /// JSON real generado por NestoApp (camelCase).
        /// Se usa en los tests para verificar compatibilidad real.
        /// </summary>
        private const string JSON_REAL_NESTOAPP = @"{
  ""id"": ""6bd921e3-9371-4c88-92f0-7c9519137073"",
  ""fechaCreacion"": ""2026-02-12T10:46:00.983Z"",
  ""usuario"": ""Carlos"",
  ""empresa"": ""1"",
  ""cliente"": ""15191"",
  ""contacto"": ""0"",
  ""nombreCliente"": ""CENTRO DE ESTÉTICA EL EDÉN, S.L.U."",
  ""lineasProducto"": [
    {
      ""producto"": ""17404"",
      ""texto"": ""ROLLO PAPEL CAMILLA"",
      ""cantidad"": 6,
      ""cantidadOferta"": 1,
      ""precio"": 7.49,
      ""descuento"": 0,
      ""aplicarDescuento"": false,
      ""iva"": ""G21"",
      ""grupo"": ""ACC"",
      ""familia"": ""Productos Genéricos"",
      ""subGrupo"": ""Desechables"",
      ""tamanno"": 100,
      ""unidadMedida"": ""m    "",
      ""urlImagen"": ""https://www.productosdeesteticaypeluqueriaprofesional.com/1279-home_default/rollo-papel-camilla.jpg"",
      ""aplicarDescuentoFicha"": true
    },
    {
      ""producto"": ""41828"",
      ""texto"": ""WHITENING CREMA DESPIGMENTANTE SPF 50"",
      ""cantidad"": 9,
      ""cantidadOferta"": 0,
      ""precio"": 26.26,
      ""descuento"": 0.45,
      ""aplicarDescuento"": true,
      ""iva"": ""G21"",
      ""grupo"": ""COS"",
      ""familia"": ""Maystar"",
      ""subGrupo"": ""Cremas mantenimiento"",
      ""tamanno"": 50,
      ""unidadMedida"": ""ml   "",
      ""urlImagen"": ""https://www.productosdeesteticaypeluqueriaprofesional.com/103013-home_default/crema-whitening-despigmentante-spf-50-50ml.jpg"",
      ""aplicarDescuentoFicha"": true
    }
  ],
  ""lineasRegalo"": [
    {
      ""producto"": ""44702"",
      ""texto"": ""CARTUCHO CERA TIBIA LAVANDA MIEL C/A.ESENCIAL"",
      ""precio"": 2.45,
      ""ganavisiones"": 1,
      ""iva"": ""G21"",
      ""cantidad"": 1
    },
    {
      ""producto"": ""43799"",
      ""texto"": ""QUITAESMALTE ESPECIAL"",
      ""precio"": 4.54,
      ""ganavisiones"": 5,
      ""iva"": ""G21"",
      ""cantidad"": 1
    }
  ],
  ""comentarioRuta"": ""Contacto 0, fecha entrega 14/02/2026 servir junto true y mantener junto false"",
  ""esPresupuesto"": false,
  ""formaPago"": ""PAG"",
  ""plazosPago"": ""1/30"",
  ""fechaEntrega"": ""2026-02-14T00:00:00+01:00"",
  ""almacenCodigo"": ""ALG"",
  ""mantenerJunto"": false,
  ""servirJunto"": true,
  ""comentarioPicking"": ""Comentarios para almacén "",
  ""total"": 174.92700000000002,
  ""servirPorGlovo"": false,
  ""mandarCobroTarjeta"": false,
  ""cobroTarjetaCorreo"": ""info@esteticaeleden.com"",
  ""cobroTarjetaMovil"": """"
}";

        [TestMethod]
        public void CrearBorradorDesdeJson_JsonCamelCaseDeNestoApp_CreaArchivoBorrador()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert
            Assert.IsNotNull(resultado);
            var rutaArchivo = Path.Combine(_carpetaTest, $"{resultado.Id}.json");
            Assert.IsTrue(File.Exists(rutaArchivo), $"El archivo borrador no se creó: {rutaArchivo}");
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_PreservaClienteYNombre()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert
            Assert.AreEqual("15191", resultado.Cliente);
            Assert.AreEqual("CENTRO DE ESTÉTICA EL EDÉN, S.L.U.", resultado.NombreCliente);
            Assert.AreEqual("1", resultado.Empresa);
            Assert.AreEqual("0", resultado.Contacto);
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_PreservaUsuarioYFechaCreacion()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert
            Assert.AreEqual("Carlos", resultado.Usuario);
            // El JSON tiene "2026-02-12T10:46:00.983Z" - comparamos fecha y hora sin milisegundos
            var fechaUtc = resultado.FechaCreacion.ToUniversalTime();
            Assert.AreEqual(2026, fechaUtc.Year);
            Assert.AreEqual(2, fechaUtc.Month);
            Assert.AreEqual(12, fechaUtc.Day);
            Assert.AreEqual(10, fechaUtc.Hour);
            Assert.AreEqual(46, fechaUtc.Minute);
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_PreservaIdOriginal()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert - Mantiene el ID original de NestoApp
            Assert.AreEqual("6bd921e3-9371-4c88-92f0-7c9519137073", resultado.Id);
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_PreservaLineasProducto()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert
            Assert.IsNotNull(resultado.LineasProducto);
            Assert.AreEqual(2, resultado.LineasProducto.Count);

            // Primera línea
            var linea1 = resultado.LineasProducto[0];
            Assert.AreEqual("17404", linea1.producto);
            Assert.AreEqual("ROLLO PAPEL CAMILLA", linea1.texto);
            Assert.AreEqual(6, linea1.cantidad);
            Assert.AreEqual(1, linea1.cantidadOferta);
            Assert.AreEqual(7.49M, linea1.precio);
            Assert.AreEqual(0M, linea1.descuento);
            Assert.IsFalse(linea1.aplicarDescuento);
            Assert.AreEqual("G21", linea1.iva);
            Assert.AreEqual("ACC", linea1.grupo);
            Assert.AreEqual("Productos Genéricos", linea1.familia);
            Assert.AreEqual("Desechables", linea1.subGrupo);
            Assert.AreEqual(100, linea1.tamanno);
            Assert.AreEqual(true, linea1.aplicarDescuentoFicha);

            // Segunda línea
            var linea2 = resultado.LineasProducto[1];
            Assert.AreEqual("41828", linea2.producto);
            Assert.AreEqual(9, linea2.cantidad);
            Assert.AreEqual(26.26M, linea2.precio);
            Assert.AreEqual(0.45M, linea2.descuento);
            Assert.IsTrue(linea2.aplicarDescuento);
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_PreservaLineasRegalo()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert
            Assert.IsNotNull(resultado.LineasRegalo);
            Assert.AreEqual(2, resultado.LineasRegalo.Count);

            var regalo1 = resultado.LineasRegalo[0];
            Assert.AreEqual("44702", regalo1.producto);
            Assert.AreEqual("CARTUCHO CERA TIBIA LAVANDA MIEL C/A.ESENCIAL", regalo1.texto);
            Assert.AreEqual(2.45M, regalo1.precio);
            Assert.AreEqual(1, regalo1.ganavisiones);
            Assert.AreEqual("G21", regalo1.iva);
            Assert.AreEqual(1, regalo1.cantidad);

            var regalo2 = resultado.LineasRegalo[1];
            Assert.AreEqual("43799", regalo2.producto);
            Assert.AreEqual("QUITAESMALTE ESPECIAL", regalo2.texto);
            Assert.AreEqual(4.54M, regalo2.precio);
            Assert.AreEqual(5, regalo2.ganavisiones);
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_PreservaConfiguracion()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert
            Assert.AreEqual("Contacto 0, fecha entrega 14/02/2026 servir junto true y mantener junto false",
                resultado.ComentarioRuta);
            Assert.IsFalse(resultado.EsPresupuesto);
            Assert.AreEqual("PAG", resultado.FormaPago);
            Assert.AreEqual("1/30", resultado.PlazosPago);
            Assert.AreEqual(new DateTime(2026, 2, 14), resultado.FechaEntrega.Date);
            Assert.AreEqual("ALG", resultado.AlmacenCodigo);
            Assert.IsFalse(resultado.MantenerJunto);
            Assert.IsTrue(resultado.ServirJunto);
            Assert.AreEqual("Comentarios para almacén ", resultado.ComentarioPicking);
            Assert.AreEqual(174.927M, Math.Round(resultado.Total, 3));
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_PreservaCamposEnvioYCobro()
        {
            // Arrange - Issue #288: servirPorGlovo, mandarCobroTarjeta, cobroTarjetaCorreo, cobroTarjetaMovil
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert - Las propiedades se preservan correctamente desde el JSON de NestoApp
            Assert.IsNotNull(resultado);
            Assert.IsFalse(resultado.ServirPorGlovo);
            Assert.IsFalse(resultado.MandarCobroTarjeta);
            Assert.AreEqual("info@esteticaeleden.com", resultado.CobroTarjetaCorreo);
            Assert.AreEqual("", resultado.CobroTarjetaMovil);
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_ArchivoSePuedeCargarConCargarBorrador()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var creado = servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Act - El archivo guardado debe poder leerse con el método estándar
            var cargado = servicio.CargarBorrador(creado.Id);

            // Assert
            Assert.IsNotNull(cargado);
            Assert.AreEqual("15191", cargado.Cliente);
            Assert.AreEqual("CENTRO DE ESTÉTICA EL EDÉN, S.L.U.", cargado.NombreCliente);
            Assert.AreEqual(2, cargado.LineasProducto.Count);
            Assert.AreEqual(2, cargado.LineasRegalo.Count);
            Assert.AreEqual("PAG", cargado.FormaPago);
            Assert.IsTrue(cargado.ServirJunto);
        }

        [TestMethod]
        public void CrearBorradorDesdeJson_IncrementaContadorBorradores()
        {
            // Arrange
            var servicio = CrearServicioTest();
            Assert.AreEqual(0, servicio.ContarBorradores());

            // Act
            servicio.CrearBorradorDesdeJson(JSON_REAL_NESTOAPP);

            // Assert
            Assert.AreEqual(1, servicio.ContarBorradores());
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CrearBorradorDesdeJson_JsonInvalido_LanzaArgumentException()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            servicio.CrearBorradorDesdeJson("esto no es JSON válido");
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CrearBorradorDesdeJson_JsonSinCliente_LanzaArgumentException()
        {
            // Arrange
            var servicio = CrearServicioTest();
            var jsonSinCliente = @"{ ""empresa"": ""1"", ""lineasProducto"": [] }";

            // Act
            servicio.CrearBorradorDesdeJson(jsonSinCliente);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CrearBorradorDesdeJson_Null_LanzaArgumentException()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            servicio.CrearBorradorDesdeJson(null);
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void CrearBorradorDesdeJson_StringVacio_LanzaArgumentException()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            servicio.CrearBorradorDesdeJson("");
        }

        #endregion

        #region EsJsonBorradorValido Tests (Issue #288)

        [TestMethod]
        public void EsJsonBorradorValido_JsonRealNestoApp_DevuelveTrue()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.EsJsonBorradorValido(JSON_REAL_NESTOAPP);

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_JsonPascalCaseNesto_DevuelveTrue()
        {
            // Arrange - JSON serializado por Nesto (PascalCase)
            var servicio = CrearServicioTest();
            var jsonPascalCase = @"{
  ""Id"": ""abc-123"",
  ""Cliente"": ""10001"",
  ""NombreCliente"": ""Test"",
  ""LineasProducto"": [{ ""producto"": ""PROD1"", ""cantidad"": 1 }]
}";

            // Act
            var resultado = servicio.EsJsonBorradorValido(jsonPascalCase);

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_TextoPlanoNoJson_DevuelveFalse()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.EsJsonBorradorValido("Hola, este es un correo electrónico normal");

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_Null_DevuelveFalse()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.EsJsonBorradorValido(null);

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_StringVacio_DevuelveFalse()
        {
            // Arrange
            var servicio = CrearServicioTest();

            // Act
            var resultado = servicio.EsJsonBorradorValido("");

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_JsonSinCliente_DevuelveFalse()
        {
            // Arrange - JSON válido pero sin campo cliente
            var servicio = CrearServicioTest();
            var json = @"{ ""empresa"": ""1"", ""total"": 100 }";

            // Act
            var resultado = servicio.EsJsonBorradorValido(json);

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_JsonSinLineas_DevuelveFalse()
        {
            // Arrange - JSON con cliente pero sin líneas de producto ni regalo
            var servicio = CrearServicioTest();
            var json = @"{ ""cliente"": ""15191"", ""nombreCliente"": ""Test"" }";

            // Act
            var resultado = servicio.EsJsonBorradorValido(json);

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_JsonSoloConLineasRegalo_DevuelveTrue()
        {
            // Arrange - JSON con solo regalos (sin productos) también es válido
            var servicio = CrearServicioTest();
            var json = @"{
  ""cliente"": ""15191"",
  ""lineasRegalo"": [{ ""producto"": ""REG1"", ""cantidad"": 1 }]
}";

            // Act
            var resultado = servicio.EsJsonBorradorValido(json);

            // Assert
            Assert.IsTrue(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_JsonArrayNoObjeto_DevuelveFalse()
        {
            // Arrange - Un array JSON no es un borrador válido
            var servicio = CrearServicioTest();
            var json = @"[1, 2, 3]";

            // Act
            var resultado = servicio.EsJsonBorradorValido(json);

            // Assert
            Assert.IsFalse(resultado);
        }

        [TestMethod]
        public void EsJsonBorradorValido_HtmlNoJson_DevuelveFalse()
        {
            // Arrange - Contenido HTML del portapapeles
            var servicio = CrearServicioTest();
            var html = @"<html><body><p>Hola</p></body></html>";

            // Act
            var resultado = servicio.EsJsonBorradorValido(html);

            // Assert
            Assert.IsFalse(resultado);
        }

        #endregion
    }
}
