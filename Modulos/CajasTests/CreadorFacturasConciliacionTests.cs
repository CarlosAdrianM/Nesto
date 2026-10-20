using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Services;
using Nesto.Modulos.PedidoCompra;
using Nesto.Modulos.PedidoCompra.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CajasTests
{
    /// <summary>
    /// Nesto#536 / NestoAPI#668 (caso real 19/10/26): la regla «Remesa recibos» no pudo crear la factura
    /// de la comisión del banco de septiembre con las compras cerradas hasta el 30/09. Ahora la API
    /// devuelve 409 con la primera fecha permitida y aquí se pregunta (una vez) si se usa.
    /// </summary>
    [TestClass]
    public class CreadorFacturasConciliacionTests
    {
        private static readonly DateTime FECHA_BANCO = new(2026, 9, 30);
        private static readonly DateTime PRIMERA_FECHA_COMPRAS = new(2026, 10, 1);

        private IPedidoCompraService servicio = null!;
        private IServicioDialogos dialogos = null!;
        private List<DateTime?> fechasFacturaEnviadas = null!;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<IPedidoCompraService>();
            dialogos = A.Fake<IServicioDialogos>();
            fechasFacturaEnviadas = new List<DateTime?>();
        }

        [TestMethod]
        public async Task CrearFacturas_FechaPermitida_NoPreguntaNiMandaFechaFactura()
        {
            A.CallTo(() => servicio.CrearAlbaranYFactura(A<CrearFacturaCmpRequest>._))
                .Invokes((CrearFacturaCmpRequest r) => fechasFacturaEnviadas.Add(r.FechaFactura))
                .Returns(new CrearFacturaCmpResponse { Exito = true, Factura = 1 });

            ResultadoFacturasConciliacion resultado = await Creador().CrearFacturas(Facturas("A"), "57200001", "doc");

            Assert.AreEqual(1, resultado.Creadas);
            Assert.IsNull(resultado.FechaFacturaAceptada);
            CollectionAssert.AreEqual(new List<DateTime?> { null }, fechasFacturaEnviadas);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CrearFacturas_ComprasCerradasYAcepta_ReenviaConLaFechaPropuesta()
        {
            ConfigurarComprasCerradas();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            List<FacturaConciliacion> facturas = Facturas("A");

            ResultadoFacturasConciliacion resultado = await Creador().CrearFacturas(facturas, "57200001", "doc");

            Assert.AreEqual(1, resultado.Creadas);
            Assert.AreEqual(PRIMERA_FECHA_COMPRAS, resultado.FechaFacturaAceptada);
            CollectionAssert.AreEqual(new List<DateTime?> { null, PRIMERA_FECHA_COMPRAS }, fechasFacturaEnviadas);
            Assert.AreEqual(FECHA_BANCO, facturas[0].Pedido.Fecha, "El pedido sigue con la fecha del banco: es la del pago");
        }

        [TestMethod]
        public async Task CrearFacturas_VariasFacturasConComprasCerradas_PreguntaUnaSolaVez()
        {
            ConfigurarComprasCerradas();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);

            ResultadoFacturasConciliacion resultado = await Creador().CrearFacturas(Facturas("A", "B", "C"), "57200001", "doc");

            Assert.AreEqual(3, resultado.Creadas);
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
            CollectionAssert.AreEqual(new List<DateTime?> { null, PRIMERA_FECHA_COMPRAS, null, PRIMERA_FECHA_COMPRAS, null, PRIMERA_FECHA_COMPRAS },
                fechasFacturaEnviadas);
        }

        [TestMethod]
        public async Task CrearFacturas_ComprasCerradasYNoAcepta_NoReenviaYLoExplica()
        {
            ConfigurarComprasCerradas();
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(false);

            Exception ex = await Assert.ThrowsExceptionAsync<Exception>(() => Creador().CrearFacturas(Facturas("A"), "57200001", "doc"));

            StringAssert.Contains(ex.Message, "No se ha contabilizado la factura del documento A");
            StringAssert.Contains(ex.Message, "No se ha contabilizado nada.");
            Assert.AreEqual(1, fechasFacturaEnviadas.Count, "Sin el sí del usuario no se reenvía");
        }

        [TestMethod]
        public void Pregunta_EsDeUstedYLlevaLasTresFechas()
        {
            string pregunta = CreadorFacturasConciliacion.Pregunta(
                new FechaCompraNoPermitidaException(FECHA_BANCO, PRIMERA_FECHA_COMPRAS, null));

            StringAssert.Contains(pregunta, "Las compras están cerradas hasta el 30/09/2026.");
            StringAssert.Contains(pregunta, "¿Desea contabilizar la factura con fecha 01/10/2026?");
            StringAssert.Contains(pregunta, "El pago se queda con la fecha del banco (30/09/2026).");
        }

        private CreadorFacturasConciliacion Creador() => new(servicio, dialogos);

        // La API rechaza sin FechaFactura y acepta con la fecha propuesta
        private void ConfigurarComprasCerradas()
        {
            A.CallTo(() => servicio.CrearAlbaranYFactura(A<CrearFacturaCmpRequest>._))
                .ReturnsLazily((CrearFacturaCmpRequest r) =>
                {
                    fechasFacturaEnviadas.Add(r.FechaFactura);
                    if (r.FechaFactura is null)
                    {
                        throw new FechaCompraNoPermitidaException(r.Pedido.Fecha, PRIMERA_FECHA_COMPRAS, null);
                    }
                    return Task.FromResult(new CrearFacturaCmpResponse { Exito = true, Factura = 7 });
                });
        }

        private static List<FacturaConciliacion> Facturas(params string[] documentos)
        {
            var facturas = new List<FacturaConciliacion>();
            foreach (string documento in documentos)
            {
                facturas.Add(new FacturaConciliacion(new PedidoCompraDTO { Fecha = FECHA_BANCO, FacturaProveedor = documento }, documento));
            }
            return facturas;
        }
    }
}
