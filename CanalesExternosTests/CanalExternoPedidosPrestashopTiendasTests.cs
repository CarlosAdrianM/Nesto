using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using Nesto.Modulos.CanalesExternos.Interfaces;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CanalesExternosTests
{
    /// <summary>
    /// Nesto#520: las dos tiendas Prestashop (Nueva Visión y Eva Visnú) comparten el núcleo; solo cambian la URL,
    /// la clave, cómo se presenta la clave (básica o ws_key en la URL) y la serie.
    /// </summary>
    [TestClass]
    public class CanalExternoPedidosPrestashopTiendasTests
    {
        private const string CLAVE_FALSA = "CLAVEDEPRUEBA0123456789ABCDEFGHI";

        private IConfiguracion configuracion;
        private IClientesPorTelefonoService clientesLookup;
        private CultureInfo culturaAnterior;

        [TestInitialize]
        public void Initialize()
        {
            configuracion = A.Fake<IConfiguracion>();
            clientesLookup = A.Fake<IClientesPorTelefonoService>();
            A.CallTo(() => clientesLookup.LeerIvaProductoAsync(A<string>._, A<string>._)).Returns(Task.FromResult("G21"));
            // Los importes de Prestashop («35.160000») se leen como en Nesto, con la cultura española
            culturaAnterior = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        }

        [TestCleanup]
        public void Cleanup()
        {
            CultureInfo.CurrentCulture = culturaAnterior;
        }

        private static PrestashopService Servicio(TiendaPrestashop tienda, string clave = CLAVE_FALSA)
        {
            return new PrestashopService(tienda, _ => clave);
        }

        #region Autenticación y URL

        [TestMethod]
        public void EvaVisnu_ConstruirUrl_LlevaLaClaveEnLaUrl()
        {
            var servicio = Servicio(TiendaPrestashop.EvaVisnu);

            Assert.AreEqual($"https://www.evavisnu.com/api/orders/251?ws_key={CLAVE_FALSA}",
                servicio.ConstruirUrl("https://www.evavisnu.com/api/orders/251"));
        }

        [TestMethod]
        public void EvaVisnu_ConstruirUrl_ConFiltro_AnadeLaClaveConAmpersand()
        {
            var servicio = Servicio(TiendaPrestashop.EvaVisnu);

            Assert.AreEqual($"https://www.evavisnu.com/api/orders?filter[reference]=ABC&ws_key={CLAVE_FALSA}",
                servicio.ConstruirUrl("https://www.evavisnu.com/api/orders?filter[reference]=ABC"));
        }

        [TestMethod]
        public void EvaVisnu_SinAutenticacionBasica()
        {
            // Con autenticación básica evavisnu.com devuelve 401 (comprobado 09/10/26)
            using var handler = Servicio(TiendaPrestashop.EvaVisnu).CrearHandler();

            Assert.IsNull(handler.Credentials);
        }

        [TestMethod]
        public void EvaVisnu_LeeLaClaveEV()
        {
            string claveLeida = null;
            var servicio = new PrestashopService(TiendaPrestashop.EvaVisnu, clave => { claveLeida = clave; return CLAVE_FALSA; });

            servicio.ConstruirUrl("https://www.evavisnu.com/api/orders");

            Assert.AreEqual("PrestashopWebserviceKeyEV", claveLeida);
        }

        [TestMethod]
        public void EvaVisnu_SinClaveEnLaConfiguracion_LoDiceConElNombreDeLaClave()
        {
            var servicio = Servicio(TiendaPrestashop.EvaVisnu, clave: null);

            var ex = Assert.ThrowsException<System.InvalidOperationException>(() => servicio.ConstruirUrl("https://www.evavisnu.com/api/orders"));
            StringAssert.Contains(ex.Message, "PrestashopWebserviceKeyEV");
            StringAssert.Contains(ex.Message, "Eva Visnú");
        }

        [TestMethod]
        public void NuevaVision_SigueConAutenticacionBasicaYLaUrlSinClave()
        {
            string claveLeida = null;
            var servicio = new PrestashopService(TiendaPrestashop.NuevaVision, clave => { claveLeida = clave; return CLAVE_FALSA; });
            string url = "https://www.productosdeesteticaypeluqueriaprofesional.com/api/orders/1234";

            Assert.AreEqual(url, servicio.ConstruirUrl(url));
            using var handler = servicio.CrearHandler();
            var credencial = handler.Credentials as System.Net.NetworkCredential;
            Assert.IsNotNull(credencial);
            Assert.AreEqual(CLAVE_FALSA, credencial.UserName);
            Assert.AreEqual("PrestashopWebserviceKeyNV", claveLeida);
        }

        [TestMethod]
        public void RecursoSinClave_NoDejaLaClaveEnLosMensajes()
        {
            string recurso = PrestashopService.RecursoSinClave($"https://www.evavisnu.com/api/addresses/7?ws_key={CLAVE_FALSA}");

            Assert.AreEqual("addresses/7", recurso);
        }

        [TestMethod]
        public void Tiendas_SeriesYUrls()
        {
            Assert.AreEqual("NV", TiendaPrestashop.NuevaVision.Serie);
            Assert.AreEqual("EV", TiendaPrestashop.EvaVisnu.Serie);
            Assert.AreEqual("https://www.evavisnu.com/api", TiendaPrestashop.EvaVisnu.UrlApi);
            Assert.AreEqual("https://www.productosdeesteticaypeluqueriaprofesional.com/api", TiendaPrestashop.NuevaVision.UrlApi);
        }

        #endregion

        #region Pedido y líneas

        private static ClientePorTelefono ClienteTienda() => new()
        {
            Cliente = "31517",
            Contacto = "0",
            ContactoDefecto = "0",
            ContactoCobro = "0",
            Vendedor = "NV",
            Iva = "G21"
        };

        private static PedidoPrestashop PedidoDeLaTienda(string formaPago = "Card via Stripe")
        {
            var pedido = new XElement("order",
                new XElement("reference", "XGXWKWNSV"),
                new XElement("total_paid", "47.900000"),
                new XElement("total_products_wt", "41.140000"),
                new XElement("total_products", "34.000000"),
                new XElement("date_add", "2026-10-07 18:54:00"),
                new XElement("payment", formaPago),
                new XElement("total_shipping_tax_incl", "6.050000"),
                new XElement("total_wrapping_tax_incl", "0.000000"),
                new XElement("total_discounts_tax_incl", "0.000000"),
                new XElement("total_discounts_tax_excl", "0.000000"),
                new XElement("associations",
                    new XElement("order_rows",
                        new XElement("order_row",
                            new XElement("product_reference", "38272"),
                            new XElement("product_name", "Crema facial"),
                            new XElement("product_quantity", "2"),
                            new XElement("unit_price_tax_excl", "17.000000"),
                            new XElement("unit_price_tax_incl", "20.570000")))));
            var direccion = new XElement("address",
                new XElement("firstname", "Ana"),
                new XElement("lastname", "Prueba"),
                new XElement("address1", "Calle Falsa 1"),
                new XElement("address2", ""),
                new XElement("postcode", "28001"),
                new XElement("city", "Madrid"),
                new XElement("phone", ""),
                new XElement("phone_mobile", "600000000"),
                new XElement("dni", ""));
            return new PedidoPrestashop
            {
                Pedido = pedido,
                Direccion = direccion,
                Cliente = new XElement("customer", new XElement("email", "ana@example.com")),
                Pais = new XElement("country", new XElement("iso_code", "ES")),
                Provincia = new XElement("state", new XElement("name", "Madrid"))
            };
        }

        [TestMethod]
        public void EvaVisnu_ElPedidoVaEnSerieEV()
        {
            var canal = new CanalExternoPedidosPrestashopEvaVisnu(configuracion, clientesLookup);

            var pedido = canal.TransformarPedido(PedidoDeLaTienda(), ClienteTienda());

            Assert.AreEqual("EV", pedido.Pedido.serie);
        }

        [TestMethod]
        public void NuevaVision_ElPedidoSigueEnSerieNV()
        {
            var canal = new CanalExternoPedidosPrestashopNuevaVision(configuracion, clientesLookup);

            var pedido = canal.TransformarPedido(PedidoDeLaTienda("PayPal"), ClienteTienda());

            Assert.AreEqual("NV", pedido.Pedido.serie);
            Assert.AreEqual("Tienda Online PayPal", pedido.Pedido.Prepagos.Single().ConceptoAdicional);
        }

        [TestMethod]
        public void EvaVisnu_ElRestoDeLaCabeceraEsComoEnNuevaVision()
        {
            var ev = new CanalExternoPedidosPrestashopEvaVisnu(configuracion, clientesLookup).TransformarPedido(PedidoDeLaTienda(), ClienteTienda());
            var nv = new CanalExternoPedidosPrestashopNuevaVision(configuracion, clientesLookup).TransformarPedido(PedidoDeLaTienda(), ClienteTienda());

            Assert.AreEqual(nv.Pedido.cliente, ev.Pedido.cliente);
            Assert.AreEqual(nv.Pedido.vendedor, ev.Pedido.vendedor);
            Assert.AreEqual(nv.Pedido.formaPago, ev.Pedido.formaPago);
            Assert.AreEqual(nv.Pedido.plazosPago, ev.Pedido.plazosPago);
            Assert.AreEqual(nv.Pedido.ruta, ev.Pedido.ruta);
            Assert.AreEqual(nv.Pedido.comentarios, ev.Pedido.comentarios);
            Assert.AreEqual(nv.PedidoCanalId, ev.PedidoCanalId);
            Assert.AreEqual(nv.Almacen, ev.Almacen);
        }

        [TestMethod]
        public void EvaVisnu_PagoConStripe_EsTarjetaPrepagadaEnLa57200013()
        {
            // Lo que se hacía a mano con los pedidos de evavisnu.com (EV2600043-45): TAR, PRE y prepago en la 57200013
            var pedido = new CanalExternoPedidosPrestashopEvaVisnu(configuracion, clientesLookup).TransformarPedido(PedidoDeLaTienda("Card via Stripe"), ClienteTienda());

            Assert.AreEqual("TAR", pedido.Pedido.formaPago);
            Assert.AreEqual("PRE", pedido.Pedido.plazosPago);
            var prepago = pedido.Pedido.Prepagos.Single();
            Assert.AreEqual("57200013", prepago.CuentaContable);
            Assert.AreEqual(47.90M, prepago.Importe);
            Assert.AreEqual("Tienda Online Eva Visnú Card via Stripe", prepago.ConceptoAdicional);
        }

        [DataTestMethod]
        [DataRow("Klarna via Stripe")]
        [DataRow("Link via Stripe")]
        public void ResolverCobro_OtrosPagosDeStripe_SonTarjetaPrepagada(string formaPago)
        {
            var cobro = CanalExternoPedidosPrestashop.ResolverCobro(formaPago);

            Assert.AreEqual("TAR", cobro.FormaPago);
            Assert.AreEqual("57200013", cobro.CuentaPrepago);
        }

        [TestMethod]
        public void ResolverCobro_TransferenciaDeEvaVisnu_SinPrepagoHastaQueLlegue()
        {
            var cobro = CanalExternoPedidosPrestashop.ResolverCobro("Pagos por transferencia bancaria");

            Assert.AreEqual("TRN", cobro.FormaPago);
            Assert.AreEqual("PRE", cobro.PlazosPago);
            Assert.IsNull(cobro.CuentaPrepago);
        }

        [TestMethod]
        public async Task EvaVisnu_LasLineasSonLasMismasQueEnNuevaVision()
        {
            var ev = new CanalExternoPedidosPrestashopEvaVisnu(configuracion, clientesLookup);
            var nv = new CanalExternoPedidosPrestashopNuevaVision(configuracion, clientesLookup);
            var pedidoEv = ev.TransformarPedido(PedidoDeLaTienda(), ClienteTienda());
            var pedidoNv = nv.TransformarPedido(PedidoDeLaTienda(), ClienteTienda());

            var lineasEv = (await ev.AnadirLineasAsync(pedidoEv, PedidoDeLaTienda())).ToList();
            var lineasNv = (await nv.AnadirLineasAsync(pedidoNv, PedidoDeLaTienda())).ToList();

            Assert.AreEqual(2, lineasEv.Count, "El producto y los portes");
            Assert.AreEqual(lineasNv.Count, lineasEv.Count);
            for (int i = 0; i < lineasNv.Count; i++)
            {
                Assert.AreEqual(lineasNv[i].Producto, lineasEv[i].Producto);
                Assert.AreEqual(lineasNv[i].Cantidad, lineasEv[i].Cantidad);
                Assert.AreEqual(lineasNv[i].PrecioUnitario, lineasEv[i].PrecioUnitario);
                Assert.AreEqual(lineasNv[i].iva, lineasEv[i].iva);
                Assert.AreEqual(lineasNv[i].tipoLinea, lineasEv[i].tipoLinea);
                Assert.AreEqual(lineasNv[i].formaVenta, lineasEv[i].formaVenta);
                Assert.AreEqual(lineasNv[i].almacen, lineasEv[i].almacen);
            }
            var producto = lineasEv[0];
            Assert.AreEqual("38272", producto.Producto);
            Assert.AreEqual((short)2, producto.Cantidad);
            Assert.AreEqual(17M, producto.PrecioUnitario);
            Assert.AreEqual("WEB", producto.formaVenta);
            Assert.AreEqual("62400003", lineasEv[1].Producto, "Portes a la cuenta de siempre");
        }

        #endregion

        #region Seguimiento

        [TestMethod]
        public void LeerDatosEnvio_TiendaSinTransportistasDeNestoAPI_SoloMandaElSeguimiento()
        {
            // Los transportistas de NestoAPI (160, 105, 103) son los de la tienda de Nueva Visión; en
            // evavisnu.com se deja el que ya tiene el pedido y solo se añade el seguimiento.
            var pedido = new PedidoCanalExterno
            {
                UltimoEnvio = new Nesto.Modulos.PedidoVenta.PedidoVentaModel.EnvioAgenciaDTO
                {
                    Numero = 1,
                    AgenciaNombre = "CTT",
                    NumeroSeguimiento = "0082800082809772528297"
                }
            };

            var datos = CanalExternoPedidosPrestashop.LeerDatosEnvio(pedido, exigirTransportista: false);

            Assert.IsNull(datos.AgenciaId);
            Assert.AreEqual("0082800082809772528297", datos.NumeroSeguimiento);
            Assert.IsFalse(TiendaPrestashop.EvaVisnu.UsaTransportistaDeNestoAPI);
            Assert.IsTrue(TiendaPrestashop.NuevaVision.UsaTransportistaDeNestoAPI);
        }

        #endregion
    }
}
