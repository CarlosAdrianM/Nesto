using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#642 (ampliación, OK de Carlos 17/11/26): «Recargar conservando mis cambios». Dos personas con el mismo pedido abierto:
    /// una mete 100 líneas y la otra guarda un cambio pequeño. La primera no tiene que volver a meter las 100: se relee y se ponen
    /// encima sus cambios; solo se pregunta lo que han tocado los dos.
    /// </summary>
    [TestClass]
    public class FusionCambiosPedidoTests
    {
        private static LineaPedidoVentaDTO Linea(int id, string producto, int cantidad = 1, decimal precio = 10M) => new LineaPedidoVentaDTO
        {
            id = id,
            Producto = producto,
            Cantidad = cantidad,
            PrecioUnitario = precio,
            almacen = "ALG",
            tipoLinea = 1,
            fechaEntrega = new DateTime(2026, 11, 18)
        };

        /// <summary>El pedido guardado: dos líneas (Nº de orden 3 y 4), forma de pago EFC.</summary>
        private static PedidoVentaDTO Guardado()
        {
            var pedido = new PedidoVentaDTO { empresa = "1", numero = 929555, cliente = "15191", contacto = "0", formaPago = "EFC", comentarios = "", Version = "v1" };
            pedido.Lineas.Add(Linea(3, "38093", 5));
            pedido.Lineas.Add(Linea(4, "17404", 1));
            return pedido;
        }

        /// <summary>La foto al cargar, lo que tiene el usuario en pantalla (partiendo de ella) y lo que hay ahora en el servidor.</summary>
        private static (PedidoVentaDTO Foto, PedidoVentaDTO Mio, PedidoVentaDTO Servidor) Tres()
        {
            PedidoVentaDTO servidor = Guardado();
            servidor.Version = "v2";
            return (Guardado(), FusionCambiosPedido.ClonarPedido(Guardado()), servidor);
        }

        private static LineaPedidoVentaDTO LineaDe(PedidoVentaDTO pedido, int id) => pedido.Lineas.Single(l => l.id == id);

        [TestMethod]
        public void LineasNuevas_SeAnadenSinNumeroDeOrdenYNuncaChocan()
        {
            var (foto, mio, servidor) = Tres();
            mio.Lineas.Add(Linea(0, "11111", 2));
            mio.Lineas.Add(Linea(0, "22222", 3));
            servidor.Lineas.Add(Linea(5, "33333")); // el otro también añadió una

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count);
            Assert.AreEqual(5, servidor.Lineas.Count);
            Assert.AreEqual(2, servidor.Lineas.Count(l => l.id == 0));
            Assert.AreEqual("v2", r.Pedido.Version, "Queda con la versión nueva");
        }

        [TestMethod]
        public void LineaEnBlanco_NoSeAnade()
        {
            var (foto, mio, servidor) = Tres();
            mio.Lineas.Add(new LineaPedidoVentaDTO { id = 0, tipoLinea = 1, Producto = " " });

            FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(2, servidor.Lineas.Count);
        }

        [TestMethod]
        public void CambioEnUnaLinea_SinConflicto_SeAplica()
        {
            var (foto, mio, servidor) = Tres();
            LineaDe(mio, 3).Cantidad = 8;

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count);
            Assert.AreEqual(8, LineaDe(servidor, 3).Cantidad);
            Assert.AreEqual(1, r.CambiosAplicados);
        }

        [TestMethod]
        public void MismaLineaCamposDistintos_SeQuedanLosDos()
        {
            var (foto, mio, servidor) = Tres();
            LineaDe(mio, 3).Cantidad = 8;
            LineaDe(servidor, 3).PrecioUnitario = 12M; // el otro cambió el precio

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count);
            Assert.AreEqual(8, LineaDe(servidor, 3).Cantidad);
            Assert.AreEqual(12M, LineaDe(servidor, 3).PrecioUnitario);
        }

        [TestMethod]
        public void MismoCampoDeLaMismaLinea_Conflicto_PreguntaDeUstedConLosDosValores()
        {
            var (foto, mio, servidor) = Tres();
            LineaDe(mio, 3).Cantidad = 8;
            LineaDe(servidor, 3).Cantidad = 6;

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            ConflictoPedido c = r.Conflictos.Single();
            Assert.AreEqual(TipoConflictoPedido.CampoLinea, c.Tipo);
            Assert.AreEqual("Alfredo cambió la cantidad de la línea 3 (38093) a 6 y usted a 8: ¿con cuál se queda?", c.Pregunta("Alfredo"));
            Assert.AreEqual(6, LineaDe(servidor, 3).Cantidad, "Mientras no conteste, lo guardado");
            r.Resolver(c, true);
            Assert.AreEqual(8, LineaDe(servidor, 3).Cantidad);
        }

        [TestMethod]
        public void MismoCampo_ConLaRespuestaDelOtro_SeQuedaLoGuardado()
        {
            var (foto, mio, servidor) = Tres();
            LineaDe(mio, 3).Cantidad = 8;
            LineaDe(servidor, 3).Cantidad = 6;
            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            r.Resolver(r.Conflictos.Single(), false);

            Assert.AreEqual(6, LineaDe(servidor, 3).Cantidad);
        }

        [TestMethod]
        public void LosDosPusieronLoMismo_NoChoca()
        {
            var (foto, mio, servidor) = Tres();
            LineaDe(mio, 3).Cantidad = 8;
            LineaDe(servidor, 3).Cantidad = 8;

            Assert.AreEqual(0, FusionCambiosPedido.Fusionar(foto, mio, servidor).Conflictos.Count);
        }

        [TestMethod]
        public void CambiarElProducto_EsUnaSolaPregunta()
        {
            var (foto, mio, servidor) = Tres();
            LineaDe(mio, 3).Producto = "99999";
            LineaDe(mio, 3).PorcentajeIva = 0.21M;
            LineaDe(servidor, 3).Producto = "88888";
            LineaDe(servidor, 3).PorcentajeIva = 0.10M;

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(1, r.Conflictos.Count);
            r.Resolver(r.Conflictos[0], true);
            Assert.AreEqual("99999", LineaDe(servidor, 3).Producto);
            Assert.AreEqual(0.21M, LineaDe(servidor, 3).PorcentajeIva, "El IVA va con el producto");
        }

        [TestMethod]
        public void LineaBorradaPorElOtro_YCambiadaPorUsted_PreguntaSiSeVuelveAAnadir()
        {
            var (foto, mio, servidor) = Tres();
            LineaDe(mio, 3).Cantidad = 8;
            servidor.Lineas.Remove(LineaDe(servidor, 3));

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            ConflictoPedido c = r.Conflictos.Single();
            Assert.AreEqual(TipoConflictoPedido.LineaBorradaPorElOtro, c.Tipo);
            Assert.AreEqual("Alfredo borró la línea 3 (38093) y usted la cambió: ¿la vuelve a añadir con sus cambios?", c.Pregunta("Alfredo"));
            Assert.AreEqual(1, servidor.Lineas.Count);
            r.Resolver(c, true);
            Assert.AreEqual(2, servidor.Lineas.Count);
            LineaPedidoVentaDTO vuelta = servidor.Lineas.Single(l => l.id == 0);
            Assert.AreEqual("38093", vuelta.Producto);
            Assert.AreEqual(8, vuelta.Cantidad);
        }

        [TestMethod]
        public void LineaBorradaPorElOtro_SinTocarlaUsted_NadaQuePreguntar()
        {
            var (foto, mio, servidor) = Tres();
            servidor.Lineas.Remove(LineaDe(servidor, 3));

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count);
            Assert.AreEqual(1, servidor.Lineas.Count);
        }

        [TestMethod]
        public void LineaBorradaPorUsted_SinQueElOtroLaToque_SeBorra()
        {
            var (foto, mio, servidor) = Tres();
            mio.Lineas.Remove(LineaDe(mio, 4));

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count);
            Assert.IsFalse(servidor.Lineas.Any(l => l.id == 4));
        }

        [TestMethod]
        public void LineaBorradaPorUsted_QueElOtroCambio_Pregunta()
        {
            var (foto, mio, servidor) = Tres();
            mio.Lineas.Remove(LineaDe(mio, 4));
            LineaDe(servidor, 4).Cantidad = 3;

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            ConflictoPedido c = r.Conflictos.Single();
            Assert.AreEqual(TipoConflictoPedido.LineaBorradaPorUsted, c.Tipo);
            Assert.AreEqual("Alfredo cambió la línea 4 (17404) y usted la borró: ¿la borra igualmente?", c.Pregunta("Alfredo"));
            Assert.IsTrue(servidor.Lineas.Any(l => l.id == 4));
            r.Resolver(c, true);
            Assert.IsFalse(servidor.Lineas.Any(l => l.id == 4));
        }

        [TestMethod]
        public void Cabecera_CamposDistintos_SeQuedanLosDos()
        {
            var (foto, mio, servidor) = Tres();
            mio.comentarios = "Llamar antes";
            servidor.formaPago = "TRN";

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count);
            Assert.AreEqual("Llamar antes", servidor.comentarios);
            Assert.AreEqual("TRN", servidor.formaPago);
        }

        [TestMethod]
        public void Cabecera_MismoCampo_Conflicto()
        {
            var (foto, mio, servidor) = Tres();
            mio.formaPago = "RCB";
            servidor.formaPago = "TRN";

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            ConflictoPedido c = r.Conflictos.Single();
            Assert.AreEqual(TipoConflictoPedido.CampoCabecera, c.Tipo);
            Assert.AreEqual("Alfredo cambió la forma de pago a TRN y usted a RCB: ¿con cuál se queda?", c.Pregunta("Alfredo"));
            Assert.AreEqual("Con RCB (el mío)", c.TextoOpcionUsted);
            Assert.AreEqual("Con TRN", c.TextoOpcionOtro);
            r.Resolver(c, true);
            Assert.AreEqual("RCB", servidor.formaPago);
        }

        [TestMethod]
        public void Cabecera_RellenoDeLaBd_NoEsUnCambio()
        {
            var (foto, mio, servidor) = Tres();
            mio.formaPago = "EFC "; // char de la BD
            servidor.formaPago = "TRN";

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count);
            Assert.AreEqual("TRN", servidor.formaPago);
        }

        [TestMethod]
        public void CienLineasYElOtroCambiaLaFormaDePago_CeroPreguntas()
        {
            var (foto, mio, servidor) = Tres();
            for (int i = 0; i < 100; i++)
            {
                mio.Lineas.Add(Linea(0, (40000 + i).ToString(), i + 1));
            }
            servidor.formaPago = "TRN";

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count, "Ni una pregunta");
            Assert.AreEqual(102, servidor.Lineas.Count);
            Assert.AreEqual(100, r.CambiosAplicados);
            Assert.AreEqual("TRN", servidor.formaPago);
            CollectionAssert.AreEqual(Enumerable.Range(0, 100).Select(i => (40000 + i).ToString()).ToList(),
                servidor.Lineas.Where(l => l.id == 0).Select(l => l.Producto).ToList(), "En el orden en que las metió");
        }

        [TestMethod]
        public void Prepagos_ElQueAnadeElUsuarioSeConserva_ElQueQuitaSeQuita()
        {
            var (foto, mio, servidor) = Tres();
            foto.Prepagos.Add(new PrepagoDTO { ConceptoAdicional = "viejo", Importe = 10M });
            servidor.Prepagos.Add(new PrepagoDTO { ConceptoAdicional = "viejo", Importe = 10M });
            servidor.Prepagos.Add(new PrepagoDTO { ConceptoAdicional = "por TRN pedido 929555", Importe = 9728.47M }); // el de Aida
            mio.Prepagos.Add(new PrepagoDTO { ConceptoAdicional = "nuevo mío", Importe = 5M });

            ResultadoFusionPedido r = FusionCambiosPedido.Fusionar(foto, mio, servidor);

            Assert.AreEqual(0, r.Conflictos.Count);
            CollectionAssert.AreEquivalent(new[] { "por TRN pedido 929555", "nuevo mío" }, servidor.Prepagos.Select(p => p.ConceptoAdicional).ToList(),
                "El de Aida no se toca (el usuario nunca lo vio); el viejo lo quitó él");
        }

        [TestMethod]
        public void ClonarPedido_EsUnaCopiaIndependiente()
        {
            PedidoVentaDTO original = Guardado();
            PedidoVentaDTO copia = FusionCambiosPedido.ClonarPedido(original);

            copia.Lineas.First().Cantidad = 99;
            copia.formaPago = "TRN";

            Assert.AreEqual(5, original.Lineas.First().Cantidad);
            Assert.AreEqual("EFC", original.formaPago);
            Assert.AreEqual("v1", copia.Version);
        }
    }
}
