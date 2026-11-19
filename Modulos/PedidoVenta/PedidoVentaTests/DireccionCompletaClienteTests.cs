using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoVenta;
using Newtonsoft.Json;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#879 (novedad 1320): la cabecera del pedido (ClienteCompleto, del selector) y la ficha (ClienteJson) pintan la dirección
    /// completa que manda la API (Dirección + Dirección 2); con la API vieja, la de siempre. Nada de esto se guarda.
    /// </summary>
    [TestClass]
    public class DireccionCompletaClienteTests
    {
        [TestMethod]
        public void ClienteCompleto_DelDetalle_PintaLaCompleta()
        {
            var cliente = JsonConvert.DeserializeObject<ControlesUsuario.Models.ClienteDTO>(
                "{\"cliente\":\"42143\",\"direccion\":\"CALLE MAYOR, 3\",\"direccionCompleta\":\"CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ ESTUDIO\"}");
            Assert.AreEqual("CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ ESTUDIO", cliente.DireccionMostrar);
            Assert.AreEqual("CALLE MAYOR, 3", cliente.direccion);
        }

        [TestMethod]
        public void ClienteJson_DeLaFicha_PintaLaCompletaYSinElla_LaDeSiempre()
        {
            var cliente = JsonConvert.DeserializeObject<ClienteJson>(
                "{\"cliente\":\"42143\",\"direccion\":\"CALLE MAYOR, 3\",\"direccionCompleta\":\"CALLE MAYOR, 3, 1º D\"}");
            Assert.AreEqual("CALLE MAYOR, 3, 1º D", cliente.DireccionMostrar);
            Assert.AreEqual("CALLE MAYOR, 3", new ClienteJson { direccion = "CALLE MAYOR, 3" }.DireccionMostrar);
        }

        [TestMethod]
        public void ClienteJson_AlGuardarLaFicha_NoMandaDireccionMostrar()
        {
            string json = JsonConvert.SerializeObject(new ClienteJson { cliente = "42143", direccion = "CALLE MAYOR, 3", direccionCompleta = "CALLE MAYOR, 3, 1º D" });
            Assert.IsFalse(json.Contains("DireccionMostrar"));
            Assert.IsTrue(json.Contains("\"direccion\":\"CALLE MAYOR, 3\""), "Lo que se guarda sigue siendo la dirección de siempre");
        }
    }
}
