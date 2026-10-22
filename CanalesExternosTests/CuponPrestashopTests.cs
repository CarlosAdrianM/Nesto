using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Models;
using Nesto.Modulos.CanalesExternos;
using Nesto.Modulos.CanalesExternos.ApisExternas;
using Nesto.Modulos.CanalesExternos.Interfaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CanalesExternosTests
{
    /// <summary>
    /// Nesto#518 (NestoAPI#614): el cupón de un evento (o manual) gastado en la tienda (regla de carrito con el mismo código,
    /// EV-…/MN-…) entra como la línea del cupón con su código, no como descuento de la tienda. Si en Nesto ya no se puede usar,
    /// el pedido entra igual (TiCKET + aviso) y, si estaba usado o anulado, tras crearlo se le dice a NestoAPI.
    /// </summary>
    [TestClass]
    public class CuponPrestashopTests
    {
        private const string CLIENTE = "15191";
        private const string CODIGO = "EV-2610-7K3Q";

        private IConfiguracion configuracion;
        private IClientesPorTelefonoService clientesLookup;
        private IServicioCupones cupones;
        private CultureInfo culturaAnterior;

        [TestInitialize]
        public void Initialize()
        {
            configuracion = A.Fake<IConfiguracion>();
            clientesLookup = A.Fake<IClientesPorTelefonoService>();
            A.CallTo(() => clientesLookup.LeerIvaProductoAsync(A<string>._, A<string>._)).Returns(Task.FromResult("G21"));
            cupones = A.Fake<IServicioCupones>();
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(Cupon()));
            culturaAnterior = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        }

        [TestCleanup]
        public void Cleanup()
        {
            CultureInfo.CurrentCulture = culturaAnterior;
        }

        private static CuponDTO Cupon(string estado = "Activo", string cliente = CLIENTE, decimal importe = 50M, int? usadoEnPedido = null) => new()
        {
            Codigo = CODIGO,
            Empresa = "1",
            Cliente = cliente,
            Importe = importe,
            PedidoMinimo = importe * 2,
            Estado = estado,
            SePuedeUsar = estado == "Activo",
            UsadoEnPedido = usadoEnPedido,
            Producto = "CUPONEVENTO",
            TextoLinea = "Cupón EV-2610-7K3Q (Masterclass Cloasma)",
            Mensaje = estado == "Usado" ? $"El cupón {CODIGO} ya está aplicado en el pedido {usadoEnPedido}." : null
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

        /// <summary>Un pedido con productos de 300 € sin IVA y los descuentos dados.</summary>
        private static PedidoPrestashop Pedido(decimal descuentoSinIva, decimal descuentoConIva)
        {
            string Importe(decimal d) => d.ToString("0.000000", CultureInfo.InvariantCulture);
            var pedido = new XElement("order",
                new XElement("id", "4712"),
                new XElement("reference", "CUPPRUEBA"),
                new XElement("total_paid", Importe(363M - descuentoConIva)),
                new XElement("total_products_wt", Importe(363M)),
                new XElement("total_products", Importe(300M)),
                new XElement("date_add", "2026-11-02 10:00:00"),
                new XElement("payment", "Pago con tarjeta Redsys"),
                new XElement("total_shipping_tax_incl", "0.000000"),
                new XElement("total_wrapping_tax_incl", "0.000000"),
                new XElement("total_discounts_tax_incl", Importe(descuentoConIva)),
                new XElement("total_discounts_tax_excl", Importe(descuentoSinIva)),
                new XElement("associations", new XElement("order_rows",
                    new XElement("order_row",
                        new XElement("product_reference", "37800"),
                        new XElement("product_name", "Producto 37800"),
                        new XElement("product_quantity", "1"),
                        new XElement("unit_price_tax_excl", Importe(300M)),
                        new XElement("unit_price_tax_incl", Importe(363M))))));
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
        }

        private static ReglaCarritoPrestashop ReglaCupon(string codigo = CODIGO, decimal conIva = 50M) => new()
        {
            Codigo = codigo,
            Nombre = "Cupón Masterclass Cloasma",
            ValorConIva = conIva,
            ValorSinIva = Math.Round(conIva / 1.21M, 6)
        };

        private CanalExternoPedidosPrestashopNuevaVision Canal(bool sinServicio = false) =>
            new(configuracion, clientesLookup, null, sinServicio ? null : cupones);

        private async Task<(List<LineaPedidoVentaDTO> lineas, PedidoCanalExterno pedido, CanalExternoPedidosPrestashopNuevaVision canal)> Importar(
            PedidoPrestashop entrada, string cliente = CLIENTE, bool sinServicio = false)
        {
            CanalExternoPedidosPrestashopNuevaVision canal = Canal(sinServicio);
            PedidoCanalExterno pedido = canal.TransformarPedido(entrada, Cliente(cliente));
            pedido.PedidoCanalId = "CUPPRUEBA";
            List<LineaPedidoVentaDTO> lineas = (await canal.AnadirLineasAsync(pedido, entrada)).ToList();
            return (lineas, pedido, canal);
        }

        [TestMethod]
        public async Task CuponDeNesto_EntraConLaLineaDelCuponYSuCodigo_NoComoDescuento()
        {
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, _) = await Importar(entrada);

            Assert.AreEqual(2, lineas.Count, "El producto y el cupón");
            Assert.AreEqual(0M, lineas[0].DescuentoLinea, "El cupón no se reparte como descuento en el producto");
            LineaPedidoVentaDTO cupon = lineas[1];
            Assert.AreEqual(CODIGO, cupon.CodigoCupon);
            Assert.AreEqual("CUPONEVENTO", cupon.Producto);
            Assert.AreEqual((short)-1, cupon.Cantidad);
            Assert.AreEqual(41.3223M, cupon.PrecioUnitario, "50 € con IVA: la API la vuelve a calcular con el IVA y el recargo del cliente");
            Assert.AreEqual((byte)1, cupon.tipoLinea);
            Assert.IsFalse(cupon.AplicarDescuento);
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
            Assert.IsFalse(pedido.Pedido.comentarios.Contains("ATENCIÓN CUPÓN"));
            A.CallTo(() => cupones.LeerPorCodigo(CODIGO)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task CuponMasOtroDescuento10_ElDescuentoDeLaTiendaSeSigueRepartiendo()
        {
            // 10 % de 300 = 30 sin IVA (36,30 con IVA) + cupón 50 € con IVA
            PedidoPrestashop entrada = Pedido(30M + 41.322314M, 36.3M + 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop>
            {
                new() { Codigo = "OTONO10", Nombre = "10 % otoño", ValorSinIva = 30M, ValorConIva = 36.3M },
                ReglaCupon()
            };

            var (lineas, _, _) = await Importar(entrada);

            Assert.AreEqual(0.10M, lineas[0].DescuentoLinea, "El 10 % se detecta como siempre");
            Assert.AreEqual(1, lineas.Count(l => l.CodigoCupon == CODIGO));
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"));
        }

        [TestMethod]
        public async Task CuponYaUsadoEnNesto_ElPedidoEntraConTicketYAviso_YTrasCrearloSeDiceALaApi()
        {
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(Cupon("Usado", usadoEnPedido: 929999)));
            A.CallTo(() => cupones.AvisarUsoEnTienda(A<string>._, A<UsoCuponTiendaDTO>._)).Returns(Task.FromResult(true));
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, canal) = await Importar(entrada);

            Assert.IsFalse(lineas.Any(l => l.CodigoCupon != null));
            LineaPedidoVentaDTO ticket = lineas.Single(l => l.Producto == "TiCKET");
            Assert.AreEqual((short)-1, ticket.Cantidad);
            Assert.AreEqual("CUPÓN NO APLICADO: REVISAR", ticket.texto);
            StringAssert.Contains(pedido.Pedido.comentarios, "ATENCIÓN CUPÓN");
            StringAssert.Contains(pedido.Pedido.comentarios, "ya está aplicado en el pedido 929999");
            StringAssert.Contains(pedido.Pedido.comentarios, "revisar a mano");

            pedido.PedidoNestoId = 930500;
            Assert.IsTrue(await canal.AvisarCuponYaGastadoAsync(pedido));
            A.CallTo(() => cupones.AvisarUsoEnTienda(CODIGO, A<UsoCuponTiendaDTO>.That.Matches(u =>
                u.Pedido == 930500 && u.Canal == "PrestaShop" && u.PedidoCanal == "CUPPRUEBA" && u.ImporteDescontado == 50M && u.Empresa == "1")))
                .MustHaveHappenedOnceExactly();
            Assert.IsFalse(await canal.AvisarCuponYaGastadoAsync(pedido), "Solo una vez");
        }

        /// <summary>Nesto#547 (NestoAPI#694): el cupón que el módulo de la tienda ya ha marcado como usado en un pedido de la tienda.</summary>
        private static CuponDTO CuponUsadoEnLaTienda(string pedidoCanal = "CUPPRUEBA", string canalUso = "PrestaShop", int? usadoEnPedido = null)
        {
            CuponDTO cupon = Cupon("Usado", usadoEnPedido: usadoEnPedido);
            cupon.CanalUso = canalUso;
            cupon.PedidoCanal = pedidoCanal;
            cupon.ImporteDescontado = 50M;
            cupon.Mensaje = $"El cupón {CODIGO} ya se ha usado en la tienda online.";
            return cupon;
        }

        private static void AssertLineaDelCupon(List<LineaPedidoVentaDTO> lineas, PedidoCanalExterno pedido)
        {
            LineaPedidoVentaDTO cupon = lineas.Single(l => l.CodigoCupon != null);
            Assert.AreEqual(CODIGO, cupon.CodigoCupon);
            Assert.AreEqual("CUPONEVENTO", cupon.Producto);
            Assert.AreEqual((short)-1, cupon.Cantidad);
            Assert.IsFalse(lineas.Any(l => l.Producto == "TiCKET"), "Sin la línea de CUPÓN NO APLICADO");
            Assert.IsFalse(pedido.Pedido.comentarios.Contains("ATENCIÓN CUPÓN"));
        }

        [TestMethod]
        public async Task CuponUsadoEnEstePedidoDeLaTienda_EntraConLaLineaDelCupon_SinTicketNiAvisoALaApi()
        {
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(CuponUsadoEnLaTienda(canalUso: "prestashop")));
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, canal) = await Importar(entrada);

            AssertLineaDelCupon(lineas, pedido);
            pedido.PedidoNestoId = 930500;
            Assert.IsFalse(await canal.AvisarCuponYaGastadoAsync(pedido), "No hay que avisar: es el mismo pedido");
            A.CallTo(() => cupones.AvisarUsoEnTienda(A<string>._, A<UsoCuponTiendaDTO>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CuponUsadoEnLaTiendaElUltimoDia_PedidoImportadoAlDiaSiguienteYaCaducado_EntraConLaLineaDelCupon()
        {
            // Regresión Nesto#547: usado el último día de validez, se importa al día siguiente: la caducidad ya ha pasado
            CuponDTO usado = CuponUsadoEnLaTienda();
            usado.ValidoDesde = DateTime.Today.AddMonths(-3);
            usado.Caducidad = DateTime.Today.AddDays(-1);
            usado.FechaUso = DateTime.Today.AddDays(-1);
            usado.SePuedeUsar = false;
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(usado));
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, _) = await Importar(entrada);

            AssertLineaDelCupon(lineas, pedido);
        }

        [TestMethod]
        public async Task CuponUsadoEnOtroPedidoDeNesto_SigueAvisando()
        {
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(CuponUsadoEnLaTienda(usadoEnPedido: 929999)));
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, _) = await Importar(entrada);

            Assert.IsFalse(lineas.Any(l => l.CodigoCupon != null));
            Assert.AreEqual("CUPÓN NO APLICADO: REVISAR", lineas.Single(l => l.Producto == "TiCKET").texto);
            StringAssert.Contains(pedido.Pedido.comentarios, "ATENCIÓN CUPÓN");
        }

        [TestMethod]
        public async Task CuponUsadoEnLaTiendaConOtraReferencia_SigueAvisandoYSeDiceALaApi()
        {
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(CuponUsadoEnLaTienda(pedidoCanal: "OTRAREF")));
            A.CallTo(() => cupones.AvisarUsoEnTienda(A<string>._, A<UsoCuponTiendaDTO>._)).Returns(Task.FromResult(true));
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, canal) = await Importar(entrada);

            Assert.IsFalse(lineas.Any(l => l.CodigoCupon != null));
            Assert.AreEqual("CUPÓN NO APLICADO: REVISAR", lineas.Single(l => l.Producto == "TiCKET").texto);
            StringAssert.Contains(pedido.Pedido.comentarios, "ATENCIÓN CUPÓN");
            pedido.PedidoNestoId = 930500;
            Assert.IsTrue(await canal.AvisarCuponYaGastadoAsync(pedido));
        }

        [TestMethod]
        public async Task CuponUsadoEnOtroCanal_SigueAvisando()
        {
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(CuponUsadoEnLaTienda(canalUso: "Nesto")));
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, _, _) = await Importar(entrada);

            Assert.IsFalse(lineas.Any(l => l.CodigoCupon != null));
            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
        }

        [TestMethod]
        public async Task CuponActivo_AplicadoNoHayQueAvisarDespues()
        {
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };
            var (_, pedido, canal) = await Importar(entrada);
            pedido.PedidoNestoId = 930500;

            Assert.IsFalse(await canal.AvisarCuponYaGastadoAsync(pedido));
            A.CallTo(() => cupones.AvisarUsoEnTienda(A<string>._, A<UsoCuponTiendaDTO>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task CuponQueTodaviaNoVale_TicketYAviso_SinAvisarALaApi()
        {
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(Cupon("Emitido")));
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, canal) = await Importar(entrada);

            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            StringAssert.Contains(pedido.Pedido.comentarios, "ATENCIÓN CUPÓN");
            pedido.PedidoNestoId = 930500;
            Assert.IsFalse(await canal.AvisarCuponYaGastadoAsync(pedido));
        }

        [TestMethod]
        public async Task ClienteGenerico_NoSeAplica()
        {
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, _) = await Importar(entrada, "31517");

            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            StringAssert.Contains(pedido.Pedido.comentarios, "cliente genérico");
        }

        [TestMethod]
        public async Task CuponDeOtroCliente_NoSeAplica()
        {
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).Returns(Task.FromResult(Cupon(cliente: "99999")));
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, _) = await Importar(entrada);

            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            StringAssert.Contains(pedido.Pedido.comentarios, "es del cliente 99999");
        }

        [TestMethod]
        public async Task ImporteDeLaTiendaDistinto_NoSeAplica()
        {
            PedidoPrestashop entrada = Pedido(33.057851M, 40M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon(conIva: 40M) };

            var (lineas, pedido, _) = await Importar(entrada);

            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            StringAssert.Contains(pedido.Pedido.comentarios, "la tienda ha descontado 40,00 € con IVA y el cupón es de 50,00 €");
        }

        [TestMethod]
        public async Task DosCupones_NoSeAplicaNinguno()
        {
            PedidoPrestashop entrada = Pedido(82.644628M, 100M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon(), ReglaCupon("EV-2610-ZZZZ") };

            var (lineas, pedido, _) = await Importar(entrada);

            Assert.IsFalse(lineas.Any(l => l.CodigoCupon != null));
            StringAssert.Contains(pedido.Pedido.comentarios, "uno por pedido");
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SinServicioDeCupones_TicketConAviso()
        {
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };

            var (lineas, pedido, _) = await Importar(entrada, sinServicio: true);

            Assert.AreEqual(1, lineas.Count(l => l.Producto == "TiCKET"));
            StringAssert.Contains(pedido.Pedido.comentarios, "NestoAPI no encuentra el cupón");
        }

        [TestMethod]
        public async Task ReglasSinCupon_TodoComoSiempre()
        {
            PedidoPrestashop entrada = Pedido(30M, 36.3M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { new() { Codigo = "OTONO10", ValorSinIva = 30M, ValorConIva = 36.3M } };

            var (lineas, pedido, _) = await Importar(entrada);

            Assert.AreEqual(0.10M, lineas[0].DescuentoLinea);
            Assert.AreEqual(1, lineas.Count);
            Assert.IsFalse(pedido.Pedido.comentarios.Contains("CUPÓN"));
            A.CallTo(() => cupones.LeerPorCodigo(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task NoSeHanPodidoLeerLasReglas_UnSoloAvisoQueNombraLosCupones()
        {
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ErrorReglasCarrito = "La tienda Nueva Visión ha respondido 401 (Unauthorized) al leer order_cart_rules.";

            var (_, pedido, _) = await Importar(entrada);

            string comentarios = pedido.Pedido.comentarios;
            Assert.AreEqual(1, comentarios.Split("no se han podido leer las reglas de carrito").Length - 1, "Un solo aviso");
            StringAssert.Contains(comentarios, "cupón de Nesto (EV-…, MN-…)");
        }

        [TestMethod]
        public void SoloLaTiendaDeNuevaVisionEsEspejoDeLosCupones()
        {
            Assert.IsTrue(TiendaPrestashop.NuevaVision.CanjeaCupones);
            Assert.IsFalse(TiendaPrestashop.EvaVisnu.CanjeaCupones);
        }

        [TestMethod]
        public async Task EvaVisnu_NoMiraCupones()
        {
            var canal = new CanalExternoPedidosPrestashopEvaVisnu(configuracion, clientesLookup);
            PedidoPrestashop entrada = Pedido(41.322314M, 50M);
            entrada.ReglasCarrito = new List<ReglaCarritoPrestashop> { ReglaCupon() };
            PedidoCanalExterno pedido = canal.TransformarPedido(entrada, Cliente());

            DecisionCuponPrestashop decision = await canal.DecidirCuponAsync(pedido.Pedido, entrada);

            Assert.AreSame(DecisionCuponPrestashop.Ninguna, decision);
        }
    }
}
