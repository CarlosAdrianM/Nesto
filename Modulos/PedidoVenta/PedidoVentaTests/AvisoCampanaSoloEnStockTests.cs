using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Modulos.PedidoVenta;
using System.Collections.Generic;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#889 (Black Friday 2026): el aviso «solo en stock» que la API manda al guardar un pedido se enseña (no bloquea). Va
    /// por la misma llamada que el del curso vendido a mano, que ya está en los tres guardados (detalle, plantilla al crear y al modificar).
    /// </summary>
    [TestClass]
    public class AvisoCampanaSoloEnStockTests
    {
        [TestMethod]
        public void Mostrar_AvisoDeCampanaSoloEnStock_SeEnsenaConSuTitulo()
        {
            var mostrados = new List<(string Titulo, string Mensaje)>();
            var avisos = new List<AvisoPedidoModel>
            {
                new AvisoPedidoModel { Tipo = AvisoPedidoModel.TIPO_CAMPANA_SOLO_EN_STOCK, Mensaje = "El descuento de Black Friday del 45929 es solo para lo que haya en stock: hoy hay 2 de 5." },
                new AvisoPedidoModel { Tipo = "OtroTipoQueNoConoce", Mensaje = "No se enseña aquí" },
                new AvisoPedidoModel { Tipo = AvisoPedidoModel.TIPO_CAMPANA_SOLO_EN_STOCK, Mensaje = " " }
            };

            AvisoPedidoModel.MostrarCursosVendidosAMano(avisos, (titulo, mensaje) => mostrados.Add((titulo, mensaje)));

            Assert.AreEqual(1, mostrados.Count);
            Assert.AreEqual(AvisoPedidoModel.TITULO_CAMPANA_SOLO_EN_STOCK, mostrados[0].Titulo);
            StringAssert.Contains(mostrados[0].Mensaje, "hoy hay 2 de 5");
        }

        [TestMethod]
        public void Mostrar_ElCursoVendidoAMano_SigueSaliendo()
        {
            var mostrados = new List<string>();
            var avisos = new List<AvisoPedidoModel> { new AvisoPedidoModel { Tipo = AvisoPedidoModel.TIPO_CURSO_VENDIDO_A_MANO, Mensaje = "Inscriba a la clienta." } };

            AvisoPedidoModel.MostrarCursosVendidosAMano(avisos, (titulo, mensaje) => mostrados.Add(titulo));

            CollectionAssert.AreEqual(new[] { AvisoPedidoModel.TITULO_CURSO_VENDIDO_A_MANO }, mostrados);
        }
    }
}
