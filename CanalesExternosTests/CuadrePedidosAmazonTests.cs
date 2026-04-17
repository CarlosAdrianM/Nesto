using FikaAmazonAPI.AmazonSpApiSDK.Models.Orders;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.Models.Cuadres;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CanalesExternosTests
{
    [TestClass]
    public class CuadrePedidosAmazonTests
    {
        private static readonly DateTime Inicio = new DateTime(2026, 2, 1);
        private static readonly DateTime Fin = new DateTime(2026, 2, 28, 23, 59, 59);

        private static ResumenPedidoVentaCanalExternoDto Nesto(string orderId, int numero = 1)
            => new ResumenPedidoVentaCanalExternoDto
            {
                Empresa = "1",
                Numero = numero,
                Fecha = new DateTime(2026, 2, 15),
                Cliente = "32624",
                CanalOrderId = orderId
            };

        private static Order Amz(string orderId, DateTime? fecha = null)
            => new Order
            {
                AmazonOrderId = orderId,
                PurchaseDate = (fecha ?? new DateTime(2026, 2, 15, 12, 0, 0)).ToString("o")
            };

        [TestMethod]
        public void Construir_MismoOrderIdEnAmbosLados_Cuadrado()
        {
            var nesto = new[] { Nesto("111-2222222-3333333") };
            var amazon = new[] { Amz("111-2222222-3333333") };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadrePedidos(nesto, amazon, Inicio, Fin);

            Assert.AreEqual(1, resultado.Cuadrados.Count);
            Assert.AreEqual("111-2222222-3333333", resultado.Cuadrados[0].Clave);
        }

        [TestMethod]
        public void Construir_PedidoEnAmazonSinContabilizar_SoloEnAmazon()
        {
            var nesto = new ResumenPedidoVentaCanalExternoDto[0];
            var amazon = new[] { Amz("999-8888888-7777777") };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadrePedidos(nesto, amazon, Inicio, Fin);

            Assert.AreEqual(1, resultado.SoloEnAmazon.Count);
            Assert.AreEqual("999-8888888-7777777", resultado.SoloEnAmazon[0].Clave);
        }

        [TestMethod]
        public void Construir_PedidoEnNestoSinOrderIdEnAmazon_SoloEnNesto()
        {
            var nesto = new[] { Nesto("555-4444444-3333333") };
            var amazon = new Order[0];

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadrePedidos(nesto, amazon, Inicio, Fin);

            Assert.AreEqual(1, resultado.SoloEnNesto.Count);
            Assert.AreEqual("555-4444444-3333333", resultado.SoloEnNesto[0].Clave);
        }

        [TestMethod]
        public void Construir_PedidoNestoSinCanalOrderId_SeIgnora()
        {
            // Sin CanalOrderId (p. ej. el parser de Comentarios no encontró formato OrderId),
            // el pedido no puede participar en el cuadre: ignorar evita falsos positivos.
            var nesto = new[]
            {
                Nesto(orderId: null, numero: 10),
                Nesto(orderId: "", numero: 11),
                Nesto(orderId: "   ", numero: 12)
            };
            var amazon = new Order[0];

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadrePedidos(nesto, amazon, Inicio, Fin);

            Assert.AreEqual(0, resultado.TotalElementos);
        }

        [TestMethod]
        public void Construir_PedidoAmazonFueraDeRango_SeIgnora()
        {
            // La Orders API de Amazon puede devolver pedidos anteriores al rango que pedimos;
            // filtrar por PurchaseDate mantiene la conciliación consistente con el lado Nesto.
            var nesto = new ResumenPedidoVentaCanalExternoDto[0];
            var amazon = new[]
            {
                Amz("111-1111111-1111111", fecha: new DateTime(2026, 1, 20)),  // antes del inicio
                Amz("222-2222222-2222222", fecha: new DateTime(2026, 3, 5))    // después del fin
            };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadrePedidos(nesto, amazon, Inicio, Fin);

            Assert.AreEqual(0, resultado.TotalElementos);
        }

        [TestMethod]
        public void Construir_PedidoFbaNesto_CuadraConMfnMismoOrderId()
        {
            // Normalización: el AmazonOrderId interno es el mismo aunque Nesto lo guarde con
            // prefijo "FBA " para identificar que es Fulfillment By Amazon.
            var nesto = new[] { Nesto("FBA 111-2222222-3333333") };
            var amazon = new[] { Amz("111-2222222-3333333") };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadrePedidos(nesto, amazon, Inicio, Fin);

            Assert.AreEqual(1, resultado.Cuadrados.Count);
            Assert.AreEqual("111-2222222-3333333", resultado.Cuadrados[0].Clave);
        }

        [TestMethod]
        public void Construir_OrderIdConEspaciosExtremos_SeNormaliza()
        {
            var nesto = new[] { Nesto("  111-2222222-3333333  ") };
            var amazon = new[] { Amz("111-2222222-3333333") };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadrePedidos(nesto, amazon, Inicio, Fin);

            Assert.AreEqual(1, resultado.Cuadrados.Count);
        }

        [TestMethod]
        public void Construir_MezclaCompleta_CadaElementoEnSuBloque()
        {
            var nesto = new[]
            {
                Nesto("CUADRADO", numero: 1),
                Nesto("SOLO-NESTO", numero: 2)
            };
            var amazon = new[]
            {
                Amz("CUADRADO"),
                Amz("SOLO-AMAZON")
            };

            var resultado = CanalExternoFacturasAmazon.ConstruirCuadrePedidos(nesto, amazon, Inicio, Fin);

            Assert.AreEqual(1, resultado.Cuadrados.Count);
            Assert.AreEqual("CUADRADO", resultado.Cuadrados[0].Clave);
            Assert.AreEqual(1, resultado.SoloEnNesto.Count);
            Assert.AreEqual("SOLO-NESTO", resultado.SoloEnNesto[0].Clave);
            Assert.AreEqual(1, resultado.SoloEnAmazon.Count);
            Assert.AreEqual("SOLO-AMAZON", resultado.SoloEnAmazon[0].Clave);
        }
    }
}
