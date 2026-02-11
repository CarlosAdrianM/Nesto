using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Tests para el modelo BorradorPlantillaVenta
    /// Issue #286: Borradores de PlantillaVenta
    /// </summary>
    [TestClass]
    public class BorradorPlantillaVentaModelTests
    {
        #region NumeroLineas Tests

        [TestMethod]
        public void NumeroLineas_SinLineas_DevuelveCero()
        {
            // Arrange
            var borrador = new BorradorPlantillaVenta();

            // Act
            int numeroLineas = borrador.NumeroLineas;

            // Assert
            Assert.AreEqual(0, numeroLineas);
        }

        [TestMethod]
        public void NumeroLineas_SoloLineasProducto_CuentaCorrectamente()
        {
            // Arrange
            var borrador = new BorradorPlantillaVenta
            {
                LineasProducto = new List<LineaPlantillaVenta>
                {
                    new LineaPlantillaVenta { producto = "PROD1" },
                    new LineaPlantillaVenta { producto = "PROD2" },
                    new LineaPlantillaVenta { producto = "PROD3" }
                }
            };

            // Act
            int numeroLineas = borrador.NumeroLineas;

            // Assert
            Assert.AreEqual(3, numeroLineas);
        }

        [TestMethod]
        public void NumeroLineas_SoloLineasRegalo_CuentaCorrectamente()
        {
            // Arrange
            var borrador = new BorradorPlantillaVenta
            {
                LineasRegalo = new List<LineaRegalo>
                {
                    new LineaRegalo { producto = "REG1" },
                    new LineaRegalo { producto = "REG2" }
                }
            };

            // Act
            int numeroLineas = borrador.NumeroLineas;

            // Assert
            Assert.AreEqual(2, numeroLineas);
        }

        [TestMethod]
        public void NumeroLineas_LineasProductoYRegalo_SumaAmbas()
        {
            // Arrange
            var borrador = new BorradorPlantillaVenta
            {
                LineasProducto = new List<LineaPlantillaVenta>
                {
                    new LineaPlantillaVenta { producto = "PROD1" },
                    new LineaPlantillaVenta { producto = "PROD2" }
                },
                LineasRegalo = new List<LineaRegalo>
                {
                    new LineaRegalo { producto = "REG1" }
                }
            };

            // Act
            int numeroLineas = borrador.NumeroLineas;

            // Assert
            Assert.AreEqual(3, numeroLineas);
        }

        #endregion

        #region Descripcion Tests

        [TestMethod]
        public void Descripcion_ConLineasProducto_MuestraNumeroLineas()
        {
            // Arrange
            var borrador = new BorradorPlantillaVenta
            {
                Cliente = "10001",
                NombreCliente = "Cliente Test",
                FechaCreacion = new DateTime(2025, 6, 15, 10, 30, 0),
                LineasProducto = new List<LineaPlantillaVenta>
                {
                    new LineaPlantillaVenta { producto = "PROD1" },
                    new LineaPlantillaVenta { producto = "PROD2" }
                }
            };

            // Act
            string descripcion = borrador.Descripcion;

            // Assert
            Assert.IsTrue(descripcion.Contains("10001"));
            Assert.IsTrue(descripcion.Contains("Cliente Test"));
            Assert.IsTrue(descripcion.Contains("2 lineas") || descripcion.Contains("2 líneas"));
            Assert.IsTrue(descripcion.Contains("15/06/2025"));
        }

        [TestMethod]
        public void Descripcion_ConRegalos_MuestraNumeroRegalos()
        {
            // Arrange
            var borrador = new BorradorPlantillaVenta
            {
                Cliente = "10001",
                NombreCliente = "Cliente Test",
                FechaCreacion = new DateTime(2025, 6, 15, 10, 30, 0),
                LineasProducto = new List<LineaPlantillaVenta>
                {
                    new LineaPlantillaVenta { producto = "PROD1" }
                },
                LineasRegalo = new List<LineaRegalo>
                {
                    new LineaRegalo { producto = "REG1" },
                    new LineaRegalo { producto = "REG2" }
                }
            };

            // Act
            string descripcion = borrador.Descripcion;

            // Assert
            Assert.IsTrue(descripcion.Contains("2 regalos"));
        }

        #endregion

        #region Propiedades Model Tests

        [TestMethod]
        public void BorradorPlantillaVenta_PropiedadesBasicas_SeAsignanCorrectamente()
        {
            // Arrange & Act
            var borrador = new BorradorPlantillaVenta
            {
                Id = "test-id-123",
                FechaCreacion = new DateTime(2025, 6, 15),
                Usuario = "usuario.test",
                MensajeError = "Error de prueba",
                Empresa = "1",
                Cliente = "10001",
                Contacto = "0",
                NombreCliente = "Cliente de Prueba",
                ComentarioRuta = "Dejar en recepcion",
                EsPresupuesto = true,
                FormaVenta = 2,
                FormaVentaOtrasCodigo = "WEB",
                FormaPago = "TRANSF",
                PlazosPago = "30D",
                FechaEntrega = new DateTime(2025, 6, 20),
                AlmacenCodigo = "ALG",
                MantenerJunto = true,
                ServirJunto = false,
                ComentarioPicking = "Fragil",
                Total = 150.50M
            };

            // Assert
            Assert.AreEqual("test-id-123", borrador.Id);
            Assert.AreEqual(new DateTime(2025, 6, 15), borrador.FechaCreacion);
            Assert.AreEqual("usuario.test", borrador.Usuario);
            Assert.AreEqual("Error de prueba", borrador.MensajeError);
            Assert.AreEqual("1", borrador.Empresa);
            Assert.AreEqual("10001", borrador.Cliente);
            Assert.AreEqual("0", borrador.Contacto);
            Assert.AreEqual("Cliente de Prueba", borrador.NombreCliente);
            Assert.AreEqual("Dejar en recepcion", borrador.ComentarioRuta);
            Assert.IsTrue(borrador.EsPresupuesto);
            Assert.AreEqual(2, borrador.FormaVenta);
            Assert.AreEqual("WEB", borrador.FormaVentaOtrasCodigo);
            Assert.AreEqual("TRANSF", borrador.FormaPago);
            Assert.AreEqual("30D", borrador.PlazosPago);
            Assert.AreEqual(new DateTime(2025, 6, 20), borrador.FechaEntrega);
            Assert.AreEqual("ALG", borrador.AlmacenCodigo);
            Assert.IsTrue(borrador.MantenerJunto);
            Assert.IsFalse(borrador.ServirJunto);
            Assert.AreEqual("Fragil", borrador.ComentarioPicking);
            Assert.AreEqual(150.50M, borrador.Total);
        }

        [TestMethod]
        public void BorradorPlantillaVenta_LineasProducto_ContienenDatosCompletos()
        {
            // Arrange
            var linea = new LineaPlantillaVenta
            {
                producto = "PROD001",
                texto = "Producto de prueba",
                cantidad = 5,
                cantidadOferta = 1,
                precio = 25.50M,
                descuento = 0.15M,
                aplicarDescuento = true,
                stock = 100,
                cantidadDisponible = 80,
                familia = "FAM1",
                subGrupo = "SUB1",
                grupo = "COS"
            };

            var borrador = new BorradorPlantillaVenta
            {
                LineasProducto = new List<LineaPlantillaVenta> { linea }
            };

            // Act
            var lineaRecuperada = borrador.LineasProducto[0];

            // Assert
            Assert.AreEqual("PROD001", lineaRecuperada.producto);
            Assert.AreEqual("Producto de prueba", lineaRecuperada.texto);
            Assert.AreEqual(5, lineaRecuperada.cantidad);
            Assert.AreEqual(1, lineaRecuperada.cantidadOferta);
            Assert.AreEqual(25.50M, lineaRecuperada.precio);
            Assert.AreEqual(0.15M, lineaRecuperada.descuento);
            Assert.IsTrue(lineaRecuperada.aplicarDescuento);
            Assert.AreEqual("COS", lineaRecuperada.grupo);
        }

        [TestMethod]
        public void BorradorPlantillaVenta_LineasRegalo_ContienenDatosCompletos()
        {
            // Arrange
            var regalo = new LineaRegalo
            {
                producto = "REG001",
                texto = "Regalo de prueba",
                cantidad = 2
            };

            var borrador = new BorradorPlantillaVenta
            {
                LineasRegalo = new List<LineaRegalo> { regalo }
            };

            // Act
            var regaloRecuperado = borrador.LineasRegalo[0];

            // Assert
            Assert.AreEqual("REG001", regaloRecuperado.producto);
            Assert.AreEqual("Regalo de prueba", regaloRecuperado.texto);
            Assert.AreEqual(2, regaloRecuperado.cantidad);
        }

        #endregion
    }
}
