using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#397: mapeo del pedido en forma de plantilla (GET ParaPlantilla) a un borrador EN
    /// MEMORIA, para cargarlo con el pipeline de borradores y guardar después con PUT.
    /// </summary>
    [TestClass]
    public class CrearBorradorDesdePedidoTests
    {
        private BorradorPlantillaVentaService _servicio;

        [TestInitialize]
        public void Setup()
        {
            var configuracion = A.Fake<IConfiguracion>();
            _servicio = new BorradorPlantillaVentaService(configuracion);
        }

        private static PedidoParaPlantillaModel PedidoDeEjemplo()
        {
            return new PedidoParaPlantillaModel
            {
                Empresa = "1",
                Cliente = "15191",
                Contacto = "0",
                NumeroPedido = 921838,
                FormaPago = "RCB",
                PlazosPago = "CONTADO",
                ComentarioPicking = "Dejar en portería",
                ServirJunto = true,
                FechaEntrega = new DateTime(2026, 7, 14),
                Almacen = "ALG",
                Lineas = new List<LineaParaPlantillaModel>
                {
                    new LineaParaPlantillaModel
                    {
                        Producto = "38697", Texto = "CHAMPU", Cantidad = 6, Precio = 10m,
                        CantidadOferta = 1, IdLineaPago = 101, IdLineaOferta = 102
                    },
                    new LineaParaPlantillaModel
                    {
                        Producto = "44707", Texto = "SERUM", Cantidad = 1, Precio = 20m,
                        CantidadOferta = 1, PersonalizarOferta = true, PrecioOferta = 20m,
                        DescuentoOferta = 0.5m, IdLineaPago = 201, IdLineaOferta = 202
                    }
                },
                Regalos = new List<RegaloParaPlantillaModel>
                {
                    new RegaloParaPlantillaModel { Producto = "45473", Texto = "REGALO", Cantidad = 1, IdLinea = 301 }
                }
            };
        }

        // Nesto#397 (Parte 1): un PedidoVentaDTO pegado desde el portapapeles (dump de ELMAH de
        // un pedido que NO llegó a crearse) se convierte en el servidor y llega aquí sin número.

        [TestMethod]
        public void CrearBorradorDesdePedido_SinNumeroDePedido_NoEsModoEdicion()
        {
            var pedido = PedidoDeEjemplo();
            pedido.NumeroPedido = 0; // dump de un pedido no creado

            var borrador = _servicio.CrearBorradorDesdePedido(pedido);

            Assert.IsNull(borrador.NumeroPedidoEnEdicion, "Sin número no hay edición: al guardar debe hacer POST");
        }

        [TestMethod]
        public void EsJsonPedidoVenta_ConDumpDePedido_True()
        {
            // Forma del PedidoVentaDTO que guarda ELMAH (cliente en minúscula, colección Lineas)
            string json = @"{ ""empresa"": ""1"", ""cliente"": ""15191"", ""contacto"": ""0"",
                ""Lineas"": [ { ""Producto"": ""38697"", ""Cantidad"": 6, ""PrecioUnitario"": 10.0 } ] }";

            Assert.IsTrue(_servicio.EsJsonPedidoVenta(json));
        }

        [TestMethod]
        public void EsJsonPedidoVenta_ConJsonDeBorrador_False()
        {
            // El formato borrador tiene LineasProducto/LineasRegalo: lo trata el flujo de siempre
            string json = @"{ ""Cliente"": ""15191"", ""LineasProducto"": [ { ""producto"": ""38697"" } ] }";

            Assert.IsFalse(_servicio.EsJsonPedidoVenta(json));
        }

        [TestMethod]
        public void EsJsonPedidoVenta_ConTextoQueNoEsJson_False()
        {
            Assert.IsFalse(_servicio.EsJsonPedidoVenta("esto no es un json"));
            Assert.IsFalse(_servicio.EsJsonPedidoVenta(null));
        }

        [TestMethod]
        public void CrearBorradorDesdePedido_MapeaCabeceraYModoEdicion()
        {
            var borrador = _servicio.CrearBorradorDesdePedido(PedidoDeEjemplo());

            Assert.AreEqual(921838, borrador.NumeroPedidoEnEdicion, "El borrador debe llevar el pedido en edición (PUT, no POST)");
            Assert.AreEqual("15191", borrador.Cliente);
            Assert.AreEqual("0", borrador.Contacto);
            Assert.AreEqual("RCB", borrador.FormaPago);
            Assert.AreEqual("CONTADO", borrador.PlazosPago);
            Assert.AreEqual("Dejar en portería", borrador.ComentarioPicking);
            Assert.IsTrue(borrador.ServirJunto);
            Assert.AreEqual(new DateTime(2026, 7, 14), borrador.FechaEntrega);
            Assert.AreEqual("ALG", borrador.AlmacenCodigo);
        }

        [TestMethod]
        public void CrearBorradorDesdePedido_MapeaLineasConOfertaYPersonalizacion()
        {
            var borrador = _servicio.CrearBorradorDesdePedido(PedidoDeEjemplo());

            Assert.AreEqual(2, borrador.LineasProducto.Count);

            var oferta = borrador.LineasProducto.Single(l => l.producto == "38697");
            Assert.AreEqual(6, oferta.cantidad);
            Assert.AreEqual(1, oferta.cantidadOferta);
            Assert.IsFalse(oferta.personalizarOferta);

            var personalizada = borrador.LineasProducto.Single(l => l.producto == "44707");
            Assert.IsTrue(personalizada.personalizarOferta, "Nesto#371: la 2ª unidad al 50% debe conservarse");
            Assert.AreEqual(20m, personalizada.precioOferta);
            Assert.AreEqual(0.5m, personalizada.descuentoOferta);
        }

        [TestMethod]
        public void CrearBorradorDesdePedido_ConservaLosIdsDeLineaParaElPut()
        {
            var borrador = _servicio.CrearBorradorDesdePedido(PedidoDeEjemplo());

            var oferta = borrador.LineasProducto.Single(l => l.producto == "38697");
            Assert.AreEqual(101, oferta.idLineaPedido);
            Assert.AreEqual(102, oferta.idLineaPedidoOferta);

            var regalo = borrador.LineasRegalo.Single();
            Assert.AreEqual("45473", regalo.producto);
            Assert.AreEqual(1, regalo.cantidad);
            Assert.AreEqual(301, regalo.idLineaPedido);
        }

        [TestMethod]
        public void CrearBorradorDesdePedido_MarcaElStockComoNoActualizado()
        {
            // El pedido no trae stock: al cargar en la plantilla, las líneas que no casen con la
            // plantilla del cliente deben saber que su stock está por refrescar.
            var borrador = _servicio.CrearBorradorDesdePedido(PedidoDeEjemplo());

            Assert.IsTrue(borrador.LineasProducto.All(l => !l.stockActualizado));
        }

        [TestMethod]
        public void CrearBorradorDesdePedido_BorradorNormalSigueSinNumeroPedidoEnEdicion()
        {
            // Round-trip de compatibilidad: un borrador de siempre (JSON antiguo sin el campo)
            // deserializa con NumeroPedidoEnEdicion = null → el guardado sigue siendo POST.
            var json = "{\"Cliente\":\"15191\",\"LineasProducto\":[{\"producto\":\"38697\",\"cantidad\":1}]}";

            var borrador = Newtonsoft.Json.JsonConvert.DeserializeObject<BorradorPlantillaVenta>(json);

            Assert.IsNull(borrador.NumeroPedidoEnEdicion);
        }
    }
}
