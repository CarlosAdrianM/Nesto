using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using Nesto.Modulos.CanalesExternos.Interfaces;
using Nesto.Modulos.CanalesExternos.ViewModels;
using System.Globalization;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CanalesExternosTests
{
    /// <summary>
    /// NestoAPI#878 (Carlos 26/11/26): address2 y company de PrestaShop son la Dirección 2 de la entrega, compuesta como en la API
    /// (gemela de NestoAPI.Tests Direccion2PedidoTiendaTests); en los comentarios va en UNA línea «DIRECCIÓN 2: …» y en la etiqueta
    /// con la regla del envío (DireccionEnvioCliente.Componer de la API).
    /// </summary>
    [TestClass]
    public class Direccion2PedidoTiendaTests
    {
        #region La regla (gemela de la API)

        [TestMethod]
        public void Componer_SoloAddress2_EnMayusculasYSinTipo()
        {
            Direccion2PedidoTiendaResultado d = Direccion2PedidoTienda.Componer("  3a ", null);

            Assert.AreEqual("3A", d.Direccion2);
            Assert.IsNull(d.TipoLugar, "no se inventa «Piso»");
        }

        [TestMethod]
        public void Componer_Address2YCompany_ConRotuloYLocal()
        {
            Direccion2PedidoTiendaResultado d = Direccion2PedidoTienda.Componer("Bajo", "Estética Goji");

            Assert.AreEqual("BAJO · RÓTULO: ESTÉTICA GOJI", d.Direccion2);
            Assert.AreEqual("Local", d.TipoLugar);
        }

        [TestMethod]
        public void Componer_CompanyYaContenidaEnAddress2_NoSeRepite()
        {
            Direccion2PedidoTiendaResultado d = Direccion2PedidoTienda.Componer("Local estetica goji", "Estética Goji");

            Assert.AreEqual("LOCAL ESTETICA GOJI", d.Direccion2);
            Assert.AreEqual("Local", d.TipoLugar);
        }

        [TestMethod]
        public void Componer_SoloCompany_ComoElAlta()
        {
            Assert.AreEqual("LOCAL · RÓTULO: VENUS ESTILISTAS", Direccion2PedidoTienda.Componer("", "Venus estilistas").Direccion2);
        }

        [TestMethod]
        public void Componer_Nada_Null()
        {
            Direccion2PedidoTiendaResultado d = Direccion2PedidoTienda.Componer(" ", null);

            Assert.IsNull(d.Direccion2);
            Assert.IsNull(d.TipoLugar);
        }

        [TestMethod]
        public void Componer_MuyLarga_SeQuedaEnCien()
        {
            string d2 = Direccion2PedidoTienda.Componer(new string('A', 80), new string('B', 80)).Direccion2;

            Assert.AreEqual(Direccion2PedidoTienda.LONGITUD, d2.Length);
        }

        [TestMethod]
        public void LineaComentario_ConEtiqueta_YNullSiNoHay()
        {
            Assert.AreEqual("DIRECCIÓN 2: 3A · RÓTULO: ESTÉTICA GOJI", Direccion2PedidoTienda.LineaComentario("3a · rótulo: estética goji"));
            Assert.IsNull(Direccion2PedidoTienda.LineaComentario("  "));
            Assert.IsNull(Direccion2PedidoTienda.LineaComentario(null));
        }

        #endregion

        #region La regla del envío (gemela de DireccionEnvioCliente)

        [TestMethod]
        public void Envio_Cabe_VaDetrasDeLaDireccion()
        {
            DireccionEnvioCompuesta r = DireccionEnvioPedidoExterno.Componer("CALLE LAS NEVERAS 2", "3A", "LLAMAR ANTES");

            Assert.AreEqual("CALLE LAS NEVERAS 2, 3A", r.Direccion);
            Assert.AreEqual("LLAMAR ANTES", r.Observaciones);
            Assert.IsFalse(r.EnObservaciones);
            Assert.IsNull(r.Aviso);
        }

        [TestMethod]
        public void Envio_NoCabeEnLaDireccion_VaDelanteDeLasObservaciones()
        {
            string calle = new string('C', 70);

            DireccionEnvioCompuesta r = DireccionEnvioPedidoExterno.Componer(calle, "LOCAL · RÓTULO: GOJI", "LLAMAR ANTES");

            Assert.AreEqual(calle, r.Direccion);
            Assert.AreEqual("LOCAL · RÓTULO: GOJI · LLAMAR ANTES", r.Observaciones);
            Assert.IsTrue(r.EnObservaciones);
        }

        [TestMethod]
        public void Envio_NoCabeEnNingunSitio_TodoComoEstabaYAviso()
        {
            string calle = new string('C', 70);
            string obs = new string('O', 70);

            DireccionEnvioCompuesta r = DireccionEnvioPedidoExterno.Componer(calle, "LOCAL · RÓTULO: GOJI", obs);

            Assert.AreEqual(calle, r.Direccion);
            Assert.AreEqual(obs, r.Observaciones);
            StringAssert.Contains(r.Aviso, "LOCAL · RÓTULO: GOJI");
            StringAssert.Contains(r.Aviso, "añádala a mano");
        }

        [TestMethod]
        public void Envio_SinDireccion2_Intacto()
        {
            DireccionEnvioCompuesta r = DireccionEnvioPedidoExterno.Componer(" CALLE 1 ", null, null);

            Assert.AreEqual(" CALLE 1 ", r.Direccion);
            Assert.IsNull(r.Observaciones);
            Assert.IsNull(r.Aviso);
        }

        [TestMethod]
        public void Etiqueta_UsaLaCalleYLaDireccion2DelPedido()
        {
            var pedido = new PedidoCanalExterno { Direccion = "CALLE LAS NEVERAS 2", Direccion2 = "BAJO · RÓTULO: GOJI", Observaciones = null };

            DireccionEnvioCompuesta r = CanalesExternosPedidosViewModel.DireccionEnvioEtiqueta(pedido);

            Assert.AreEqual("CALLE LAS NEVERAS 2, BAJO · RÓTULO: GOJI", r.Direccion);
        }

        #endregion

        #region El importador

        private IConfiguracion configuracion;
        private IClientesPorTelefonoService clientesLookup;
        private CultureInfo culturaAnterior;

        [TestInitialize]
        public void Initialize()
        {
            configuracion = A.Fake<IConfiguracion>();
            clientesLookup = A.Fake<IClientesPorTelefonoService>();
            A.CallTo(() => clientesLookup.LeerIvaProductoAsync(A<string>._, A<string>._)).Returns(Task.FromResult("G21"));
            culturaAnterior = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        }

        [TestCleanup]
        public void Cleanup()
        {
            CultureInfo.CurrentCulture = culturaAnterior;
        }

        private static ClientePorTelefono ClienteTienda() => new()
        {
            Cliente = "31517",
            Contacto = "0",
            ContactoDefecto = "0",
            ContactoCobro = "0",
            Vendedor = "NV",
            Iva = "G21"
        };

        private static PedidoPrestashop PedidoWeb(string address2, string company)
        {
            var pedido = new XElement("order",
                new XElement("reference", "XGXWKWNSV"),
                new XElement("total_paid", "47.900000"),
                new XElement("total_products_wt", "41.140000"),
                new XElement("date_add", "2026-10-07 18:54:00"),
                new XElement("payment", "Card via Stripe"));
            var direccion = new XElement("address",
                new XElement("firstname", "Ana"),
                new XElement("lastname", "Prueba"),
                new XElement("address1", "Calle Las Neveras 2"),
                new XElement("address2", address2),
                new XElement("postcode", "28110"),
                new XElement("city", "Algete"),
                new XElement("phone", ""),
                new XElement("phone_mobile", "600000000"),
                new XElement("dni", ""));
            if (company != null)
            {
                direccion.Add(new XElement("company", company));
            }
            return new PedidoPrestashop
            {
                Pedido = pedido,
                Direccion = direccion,
                Cliente = new XElement("customer", new XElement("email", "ana@example.com")),
                Pais = new XElement("country", new XElement("iso_code", "ES")),
                Provincia = new XElement("state", new XElement("name", "Madrid"))
            };
        }

        private PedidoCanalExterno Transformar(string address2, string company)
            => new CanalExternoPedidosPrestashopNuevaVision(configuracion, clientesLookup).TransformarPedido(PedidoWeb(address2, company), ClienteTienda());

        [TestMethod]
        public void Importador_ConAddress2YCompany_LineaDireccion2YCalleSola()
        {
            PedidoCanalExterno p = Transformar("Bajo", "Estética Goji");

            Assert.AreEqual("CALLE LAS NEVERAS 2", p.Direccion, "la calle va sola");
            Assert.AreEqual("BAJO · RÓTULO: ESTÉTICA GOJI", p.Direccion2);
            StringAssert.Contains(p.Pedido.comentarios, "CALLE LAS NEVERAS 2\r\nDIRECCIÓN 2: BAJO · RÓTULO: ESTÉTICA GOJI\r\n28110 ALGETE\r\n");
            Assert.IsFalse(p.Pedido.comentarios.Contains("\r\nBAJO\r\n"), "ya no va la línea suelta de address2");
        }

        [TestMethod]
        public void Importador_SinAddress2NiCompany_ComoAntesSinLinea()
        {
            PedidoCanalExterno p = Transformar("", null);

            Assert.AreEqual("CALLE LAS NEVERAS 2", p.Direccion);
            Assert.IsNull(p.Direccion2);
            StringAssert.Contains(p.Pedido.comentarios, "CALLE LAS NEVERAS 2\r\n28110 ALGETE\r\n");
            Assert.IsFalse(p.Pedido.comentarios.Contains("DIRECCIÓN 2"));
        }

        [TestMethod]
        public void Importador_ElFormatoDeLaTiendaNoCambia()
        {
            PedidoCanalExterno p = Transformar("3a", "Venus");

            Assert.IsTrue(p.Pedido.comentarios.StartsWith("XGXWKWNSV \r\nANA PRUEBA\r\nana@example.com\r\n"), "la referencia sigue al principio");
            StringAssert.Contains(p.Pedido.comentarios, "TOTAL PEDIDO: ");
            Assert.IsTrue(p.Pedido.comentarios.TrimEnd().EndsWith("€"), "TOTAL PEDIDO sigue siendo la última línea");
        }

        #endregion
    }
}
