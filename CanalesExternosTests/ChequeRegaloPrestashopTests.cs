using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using Nesto.Modulos.CanalesExternos.Interfaces;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CanalesExternosTests
{
    /// <summary>
    /// Nesto#533: el cheque regalo canjeado en la tienda (regla de carrito NVCHEQUE-{Campana}-{cliente}) entra como la
    /// línea del cheque (producto de la campaña, −1), no como cupón; el resto de descuentos se reparte como siempre.
    /// </summary>
    [TestClass]
    public class ChequeRegaloPrestashopTests
    {
        private const string CLIENTE = "15191";
        private const string CODIGO = "NVCHEQUE-CHEQUE50_OCT_2026-15191";

        private IConfiguracion configuracion;
        private IClientesPorTelefonoService clientesLookup;
        private IServicioChequesRegalo chequesRegalo;
        private CultureInfo culturaAnterior;

        [TestInitialize]
        public void Initialize()
        {
            configuracion = A.Fake<IConfiguracion>();
            clientesLookup = A.Fake<IClientesPorTelefonoService>();
            A.CallTo(() => clientesLookup.LeerIvaProductoAsync(A<string>._, A<string>._)).Returns(Task.FromResult("G21"));
            chequesRegalo = A.Fake<IServicioChequesRegalo>();
            A.CallTo(() => chequesRegalo.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(Cheque()));
            culturaAnterior = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        }

        [TestCleanup]
        public void Cleanup()
        {
            CultureInfo.CurrentCulture = culturaAnterior;
        }

        private static ChequeRegaloClienteDTO Cheque(bool sePuedeUsar = true, string mensaje = null) => new()
        {
            Campana = "CHEQUE50_OCT_2026",
            Empresa = "1",
            Cliente = CLIENTE,
            Producto = "CHEQUE50_OCT26",
            Importe = 50,
            MinimoCanje = 250,
            Estado = sePuedeUsar ? "Disponible" : "EnPedido",
            SePuedeUsar = sePuedeUsar,
            Mensaje = mensaje ?? "Tiene un cheque regalo de 50,00 € + IVA…",
            TextoLinea = "Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)"
        };

        private static ClientePorTelefono Cliente(string cliente = CLIENTE) => new()
        {
            Cliente = cliente,
            Contacto = "0",
            ContactoDefecto = "0",
            ContactoCobro = "0",
            Vendedor = "NV",
            Iva = "G21"
        };

        /// <summary>Un pedido con productos de 300 € sin IVA (y una segunda línea opcional) y los descuentos dados.</summary>
        private static PedidoPrestashop Pedido(decimal descuentoSinIva, decimal descuentoConIva, decimal precioSegundaLinea = 0)
        {
            string Importe(decimal d) => d.ToString("0.000000", CultureInfo.InvariantCulture);
            var filas = new XElement("order_rows",
                Fila("37800", 300M));
            decimal totalProductos = 300M;
            if (precioSegundaLinea != 0)
            {
                filas.Add(Fila("38272", precioSegundaLinea));
                totalProductos += precioSegundaLinea;
            }
            var pedido = new XElement("order",
                new XElement("id", "4711"),
                new XElement("reference", "CHQPRUEBA"),
                new XElement("total_paid", Importe(totalProductos * 1.21M - descuentoConIva)),
                new XElement("total_products_wt", Importe(totalProductos * 1.21M)),
                new XElement("total_products", Importe(totalProductos)),
                new XElement("date_add", "2026-10-20 10:00:00"),
                new XElement("payment", "Pago con tarjeta Redsys"),
                new XElement("total_shipping_tax_incl", "0.000000"),
                new XElement("total_wrapping_tax_incl", "0.000000"),
                new XElement("total_discounts_tax_incl", Importe(descuentoConIva)),
                new XElement("total_discounts_tax_excl", Importe(descuentoSinIva)),
                new XElement("associations", filas));
            return new PedidoPrestashop
            {
                Pedido = pedido,
                Direccion = new XElement("address",
                    new XElement("firstname", "Ana"), new XElement("lastname", "Prueba"), new XElement("address1", "Calle Falsa 1"),
                    new XElement("address2", ""), new XElement("postcode", "28001"), new XElement("city", "Madrid"),
                    new XElement("phone", ""), new XElement("phone_mobile", "600000000"), new XElement("dni", "B12345678")),
                Cliente = new XElement("customer", new XElement("email", "ana@example.com")),
                Pais = new XElement("country", new XElement("iso_code", "ES")),
                Provincia = new XElement("state", new XElement("name", "Madrid"))
            };

            XElement Fila(string producto, decimal precioSinIva) => new("order_row",
                new XElement("product_reference", producto),
                new XElement("product_name", "Producto " + producto),
                new XElement("product_quantity", "1"),
                new XElement("unit_price_tax_excl", Importe(precioSinIva)),
                new XElement("unit_price_tax_incl", Importe(precioSinIva * 1.21M)));
        }

        private static ReglaCarritoPrestashop ReglaCheque(string codigo = CODIGO, decimal sinIva = 50M) => new()
        {
            Codigo = codigo,
            Nombre = "Cheque regalo 50 €",
            ValorSinIva = sinIva,
            ValorConIva = sinIva * 1.21M
        };

        private async Task<(List<LineaPedidoVentaDTO> lineas, PedidoVentaDTO pedido)> Importar(PedidoPrestashop entrada, string cliente = CLIENTE,
            IServicioChequesRegalo servicio = null, bool sinServicio = false)
        {
            var canal = new CanalExternoPedidosPrestashopNuevaVision(configuracion, clientesLookup, sinServicio ? null : servicio ?? chequesRegalo);
            PedidoCanalExterno pedido = canal.TransformarPedido(entrada, Cliente(cliente));
            List<LineaPedidoVentaDTO> lineas = (await canal.AnadirLineasAsync(pedido, entrada)).ToList();
            return (lineas, pedido.Pedido);
        }

        [TestMethod]
        public async Task AplicarDescuentoCupon_ChequeSolo_LineaDelChequeYNingunCupon()
        {
            PedidoPrestashop entrada = Pedido(50M, 60.5M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCheque() };

            var (lineas, pedido) = await Importar(entrada);

            Assert.AreEqual(2, lineas.Count, "El producto y el cheque");
            Assert.AreEqual(0M, lineas[0].DescuentoLinea, "El cheque no se reparte como descuento en el producto");
            LineaPedidoVentaDTO cheque = lineas[1];
            Assert.AreEqual("CHEQUE50_OCT26", cheque.Producto, "El producto sale de GET api/ChequesRegalo/Cliente");
            Assert.AreEqual((short)-1, cheque.Cantidad);
            Assert.AreEqual(50M, cheque.PrecioUnitario);
            Assert.AreEqual((byte)1, cheque.tipoLinea);
            Assert.IsFalse(cheque.AplicarDescuento);
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
            Assert.IsFalse(pedido.comentarios.Contains("CHEQUE REGALO"));
            A.CallTo(() => chequesRegalo.LeerDelCliente("1", CLIENTE)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AplicarDescuentoCupon_ChequeMasCupon10_ElCuponSeSigueRepartiendo()
        {
            // 10 % de 300 = 30 + cheque 50 = 80 sin IVA
            PedidoPrestashop entrada = Pedido(80M, 96.8M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop>
            {
                new() { Codigo = "OTONO10", Nombre = "10 % otoño", ValorSinIva = 30M, ValorConIva = 36.3M },
                ReglaCheque()
            };

            var (lineas, _) = await Importar(entrada);

            Assert.AreEqual(0.10M, lineas[0].DescuentoLinea, "El 10 % se detecta como siempre");
            Assert.AreEqual(1, lineas.Count(l => l.Producto == "CHEQUE50_OCT26"));
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
            Assert.AreEqual((short)-1, lineas.Last().Cantidad);
        }

        [TestMethod]
        public async Task AplicarDescuentoCupon_ChequeRechazado_CuponConAvisoYSinLineaDelCheque()
        {
            A.CallTo(() => chequesRegalo.LeerDelCliente(A<string>._, A<string>._))
                .Returns(Task.FromResult(Cheque(sePuedeUsar: false, mensaje: "El cheque regalo ya está aplicado en el pedido 929999.")));
            PedidoPrestashop entrada = Pedido(50M, 60.5M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCheque() };

            var (lineas, pedido) = await Importar(entrada);

            Assert.IsFalse(lineas.Any(l => l.Producto == "CHEQUE50_OCT26"));
            LineaPedidoVentaDTO cupon = lineas.Single(l => l.Producto == "TiCKET");
            Assert.AreEqual((short)-1, cupon.Cantidad);
            Assert.AreEqual(50M, cupon.PrecioUnitario);
            StringAssert.Contains(pedido.comentarios, "ATENCIÓN CHEQUE REGALO");
            StringAssert.Contains(pedido.comentarios, "ya está aplicado en el pedido 929999");
            StringAssert.Contains(pedido.comentarios, "revisar a mano");
        }

        [TestMethod]
        public async Task ChequeRechazado_NoSeConvierteEnRegaloDeUnaLineaQueValga50()
        {
            // Con el reparto de siempre, un cupón de 50 € igual a una línea de 50 € la ponía al 100 % (Issue #350)
            A.CallTo(() => chequesRegalo.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult<ChequeRegaloClienteDTO>(null));
            PedidoPrestashop entrada = Pedido(50M, 60.5M, precioSegundaLinea: 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCheque() };

            var (lineas, _) = await Importar(entrada);

            Assert.IsTrue(lineas.Where(l => l.Producto != "TiCKET").All(l => l.DescuentoLinea == 0));
            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
        }

        [TestMethod]
        public async Task ClienteGenerico_NoSePreguntaYQuedaComoCupon()
        {
            PedidoPrestashop entrada = Pedido(50M, 60.5M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCheque() };

            var (lineas, pedido) = await Importar(entrada, cliente: "31517");

            Assert.IsFalse(lineas.Any(l => l.Producto == "CHEQUE50_OCT26"));
            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            StringAssert.Contains(pedido.comentarios, "cliente genérico");
            A.CallTo(() => chequesRegalo.LeerDelCliente(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ElChequeEsDeOtroClienteQueElDelNif_QuedaComoCupon()
        {
            PedidoPrestashop entrada = Pedido(50M, 60.5M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCheque("NVCHEQUE-CHEQUE50_OCT_2026-20000") };

            var (lineas, pedido) = await Importar(entrada);

            Assert.IsFalse(lineas.Any(l => l.Producto == "CHEQUE50_OCT26"));
            StringAssert.Contains(pedido.comentarios, "es del cliente 20000");
        }

        [TestMethod]
        public async Task SinServicioDeCheques_QuedaComoCuponConAviso()
        {
            PedidoPrestashop entrada = Pedido(50M, 60.5M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCheque() };

            var (lineas, pedido) = await Importar(entrada, sinServicio: true);

            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            StringAssert.Contains(pedido.comentarios, "ATENCIÓN CHEQUE REGALO");
        }

        [TestMethod]
        public async Task ImporteDeLaTiendaDistintoDelCheque_QuedaComoCupon()
        {
            PedidoPrestashop entrada = Pedido(40M, 48.4M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCheque(sinIva: 40M) };

            var (lineas, pedido) = await Importar(entrada);

            Assert.IsFalse(lineas.Any(l => l.Producto == "CHEQUE50_OCT26"));
            StringAssert.Contains(pedido.comentarios, "ha descontado 40");
        }

        [TestMethod]
        public async Task SinReglaDeCheque_TodoComoSiempre()
        {
            PedidoPrestashop entrada = Pedido(7.5M, 9.08M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { new() { Codigo = "BIENVENIDA", ValorSinIva = 7.5M, ValorConIva = 9.08M } };

            var (lineas, pedido) = await Importar(entrada);

            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            Assert.IsFalse(pedido.comentarios.Contains("CHEQUE REGALO"));
            A.CallTo(() => chequesRegalo.LeerDelCliente(A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task NoSeHanPodidoLeerLasReglas_CuponComoSiempreYAviso()
        {
            PedidoPrestashop entrada = Pedido(50M, 60.5M);
            entrada.ErrorReglasCarrito = "La tienda Nueva Visión ha respondido 401 (Unauthorized) al leer order_cart_rules.";

            var (lineas, pedido) = await Importar(entrada);

            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            StringAssert.Contains(pedido.comentarios, "no se han podido leer las reglas de carrito");
        }

        [TestMethod]
        public async Task EvaVisnu_NoMiraCheques()
        {
            var canal = new CanalExternoPedidosPrestashopEvaVisnu(configuracion, clientesLookup);
            PedidoPrestashop entrada = Pedido(50M, 60.5M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCheque() };
            PedidoCanalExterno pedido = canal.TransformarPedido(entrada, Cliente());

            DecisionChequeRegaloPrestashop decision = await canal.DecidirChequeRegaloAsync(pedido.Pedido, entrada);

            Assert.AreSame(DecisionChequeRegaloPrestashop.Ninguna, decision);
            Assert.IsFalse(TiendaPrestashop.EvaVisnu.CanjeaChequesRegalo);
            Assert.IsTrue(TiendaPrestashop.NuevaVision.CanjeaChequesRegalo);
        }

        [TestMethod]
        [DataRow("NVCHEQUE-CHEQUE50_OCT_2026-15191", "CHEQUE50_OCT_2026", "15191")]
        [DataRow("nvcheque-CHEQUE50_OCT_2026-15191 ", "CHEQUE50_OCT_2026", "15191")]
        public void LeerCodigo_CampanaYCliente(string codigo, string campana, string cliente)
        {
            Assert.IsTrue(ChequeRegaloPrestashop.LeerCodigo(codigo, out string c, out string k));
            Assert.AreEqual(campana, c);
            Assert.AreEqual(cliente, k);
        }

        [TestMethod]
        [DataRow("NVCHEQUE-15191")]
        [DataRow("NVCHEQUE-CHEQUE50_OCT_2026-")]
        [DataRow("OTONO10")]
        [DataRow(null)]
        public void LeerCodigo_MalFormado(string codigo)
        {
            Assert.IsFalse(ChequeRegaloPrestashop.LeerCodigo(codigo, out _, out _));
        }

        [TestMethod]
        public void LeerImporte_NoDependeDeLaCultura()
        {
            Assert.AreEqual(60.5M, ChequeRegaloPrestashop.LeerImporte("60.500000"));
        }
    }
}
