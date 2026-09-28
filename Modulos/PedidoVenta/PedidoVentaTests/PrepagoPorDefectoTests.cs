using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;
using System.Linq;

namespace PedidoVentaTests
{
    /// <summary>
    /// Carlos, 28/09/26: el prepago nuevo nace con «por TRN pedido NNNNNN» y la cuenta de Caixabank
    /// (57200013). El concepto identifica el prepago al guardar, así que nunca se repite.
    /// </summary>
    [TestClass]
    public class PrepagoPorDefectoTests
    {
        [TestMethod]
        public void PrepagoNuevoEnElGrid_NaceConConceptoYCuentaDeCaixabank()
        {
            var wrapper = new PedidoVentaWrapper(new PedidoVentaDTO { empresa = "1", numero = 927105 });

            var prepago = new PrepagoDTO();
            wrapper.Prepagos.Add(prepago);

            Assert.AreEqual("por TRN pedido 927105", prepago.ConceptoAdicional);
            Assert.AreEqual("57200013", prepago.CuentaContable);
        }

        [TestMethod]
        public void LoQueYaVieneRelleno_NoSeToca()
        {
            var prepago = new PrepagoDTO { ConceptoAdicional = "Bizum", CuentaContable = "57200020" };

            PedidoVentaWrapper.RellenarPrepagoPorDefecto(prepago, 927105, new List<PrepagoDTO> { prepago });

            Assert.AreEqual("Bizum", prepago.ConceptoAdicional);
            Assert.AreEqual("57200020", prepago.CuentaContable);
        }

        [TestMethod]
        public void SegundoPrepagoDelMismoPedido_NoRepiteElConcepto()
        {
            var primero = new PrepagoDTO { ConceptoAdicional = "por TRN pedido 927105", CuentaContable = "57200013" };
            var segundo = new PrepagoDTO();
            var tercero = new PrepagoDTO();
            var lista = new List<PrepagoDTO> { primero, segundo, tercero };

            PedidoVentaWrapper.RellenarPrepagoPorDefecto(segundo, 927105, lista);
            PedidoVentaWrapper.RellenarPrepagoPorDefecto(tercero, 927105, lista);

            Assert.AreEqual("por TRN pedido 927105 (2)", segundo.ConceptoAdicional);
            Assert.AreEqual("por TRN pedido 927105 (3)", tercero.ConceptoAdicional);
            Assert.AreEqual(3, lista.Select(p => p.ConceptoAdicional).Distinct().Count());
        }

        [TestMethod]
        public void PedidoNuevoSinNumero_ConceptoSinNumero()
        {
            var prepago = new PrepagoDTO();

            PedidoVentaWrapper.RellenarPrepagoPorDefecto(prepago, 0, new List<PrepagoDTO> { prepago });

            Assert.AreEqual("por TRN pedido", prepago.ConceptoAdicional);
        }

        [TestMethod]
        public void LosPrepagosQueVienenDelServidor_NoSeTocan()
        {
            var dto = new PedidoVentaDTO { empresa = "1", numero = 927105 };
            dto.Prepagos.Add(new PrepagoDTO { ConceptoAdicional = "Transferencia 12/09", CuentaContable = "57200013", Importe = 100 });

            var wrapper = new PedidoVentaWrapper(dto);

            Assert.AreEqual("Transferencia 12/09", wrapper.Prepagos.Single().ConceptoAdicional);
        }
    }
}
