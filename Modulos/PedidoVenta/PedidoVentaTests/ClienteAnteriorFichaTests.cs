using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json;

namespace PedidoVentaTests
{
    /// <summary>NestoAPI#876: la ficha (ClienteJson de GET Clientes) pinta «Cliente anterior: N» si lo trae; nunca lo manda de vuelta.</summary>
    [TestClass]
    public class ClienteAnteriorFichaTests
    {
        [TestMethod]
        public void ConClienteAnterior_LoPinta()
        {
            var cliente = JsonConvert.DeserializeObject<ClienteJson>("{\"cliente\":\"50001\",\"ClienteAnterior\":\"15191  \"}");

            Assert.AreEqual("Cliente anterior: 15191", cliente.TextoClienteAnterior);
        }

        [TestMethod]
        public void SinClienteAnterior_Nada()
        {
            var cliente = JsonConvert.DeserializeObject<ClienteJson>("{\"cliente\":\"50001\"}");

            Assert.IsNull(cliente.TextoClienteAnterior);
        }

        [TestMethod]
        public void AlGuardarLaFicha_NoViaja()
        {
            var cliente = new ClienteJson { cliente = "50001", clienteAnterior = "15191" };

            string json = JsonConvert.SerializeObject(cliente);

            Assert.IsFalse(json.Contains("clienteAnterior"), json);
            Assert.IsFalse(json.Contains("TextoClienteAnterior"), json);
        }
    }
}
