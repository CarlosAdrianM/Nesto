using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Incidencia 1506 (Paloma, tienda de Alcobendas, 27/11/26): al crear un pedido nuevo buscaba por teléfono a la
    /// clienta 40845 (de JGP) y no salía nada, porque el selector mandaba su vendedor (PA) aunque tuviera el parámetro
    /// PermitirVerClientesTodosLosVendedores = 1. El selector solo miraba si era de Administración o Dirección.
    /// </summary>
    [TestClass]
    public class SelectorClienteVerTodosLosVendedoresTests
    {
        private const string EMPRESA = "1";
        private const string FILTRO = "634899669";

        private static IConfiguracion Configuracion(string valorParametro, bool administracion = false, bool direccion = false)
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(administracion);
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(direccion);
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.PermitirVerClientesTodosLosVendedores))
                .Returns(Task.FromResult(valorParametro));
            return configuracion;
        }

        [TestMethod]
        public async Task ConElParametroAUno_UsuarioDeTienda_BuscaSinVendedor()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion("1"), EMPRESA, "PA", FILTRO, false);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=634899669", url);
        }

        [TestMethod]
        public async Task ConElParametroATrue_TambienBuscaSinVendedor()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion(" True "), EMPRESA, "PA", FILTRO, false);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=634899669", url);
        }

        [TestMethod]
        public async Task ConElParametroACero_BuscaConSuVendedor()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion("0"), EMPRESA, "PA", FILTRO, false);

            Assert.AreEqual("Clientes?empresa=1&vendedor=PA&filtro=634899669", url);
        }

        [TestMethod]
        public async Task SinFilaDelParametro_BuscaConSuVendedor()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion(string.Empty), EMPRESA, "PA", FILTRO, false);

            Assert.AreEqual("Clientes?empresa=1&vendedor=PA&filtro=634899669", url);
        }

        [TestMethod]
        public async Task SiFallaLaLecturaDelParametro_BuscaComoSiempreConSuVendedor()
        {
            var configuracion = Configuracion("1");
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.PermitirVerClientesTodosLosVendedores))
                .ThrowsAsync(new Exception("No se puede leer el parámetro"));

            var url = await new SelectorClienteService().ConstruirUrlBusqueda(configuracion, EMPRESA, "PA", FILTRO, false);

            Assert.AreEqual("Clientes?empresa=1&vendedor=PA&filtro=634899669", url);
        }

        [TestMethod]
        public async Task SiFallaLaLectura_NoSeGuarda_YLaSiguienteBusquedaLoVuelveALeer()
        {
            var configuracion = Configuracion("1");
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.PermitirVerClientesTodosLosVendedores))
                .ThrowsAsync(new Exception("No se puede leer el parámetro")).Once()
                .Then.Returns(Task.FromResult("1"));
            var servicio = new SelectorClienteService();

            await servicio.ConstruirUrlBusqueda(configuracion, EMPRESA, "PA", FILTRO, false);
            var url = await servicio.ConstruirUrlBusqueda(configuracion, EMPRESA, "PA", FILTRO, false);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=634899669", url);
        }

        [TestMethod]
        public async Task ElParametroSeLeeUnaSolaVez_NoEnCadaBusqueda()
        {
            var configuracion = Configuracion("1");
            var servicio = new SelectorClienteService();

            await servicio.ConstruirUrlBusqueda(configuracion, EMPRESA, "PA", "6348", false);
            await servicio.ConstruirUrlBusqueda(configuracion, EMPRESA, "PA", "63489", false);
            await servicio.ConstruirUrlBusqueda(configuracion, EMPRESA, "PA", FILTRO, false);

            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.PermitirVerClientesTodosLosVendedores))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Administracion_BuscaSinVendedor_ComoHoy()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion("0", administracion: true), EMPRESA, "CAR", FILTRO, false);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=634899669", url);
        }

        [TestMethod]
        public async Task Direccion_BuscaSinVendedor_ComoHoy()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion("0", direccion: true), EMPRESA, "CAR", FILTRO, false);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=634899669", url);
        }

        // Sugerencia 582 (Rapports): la API decide con vendedorTelefonico si quien busca es telefónico; «vendedor» es
        // solo el filtro y sigue la misma regla que el resto de búsquedas (Carlos 27/11/26).

        [TestMethod]
        public async Task ContactoTelefonicoPrimero_ConElParametroAUno_BuscaSinVendedor_YMandaVendedorTelefonico()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion("1"), EMPRESA, "PA", FILTRO, true);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=634899669&contactoTelefonicoPrimero=true&vendedorTelefonico=PA", url);
        }

        [TestMethod]
        public async Task ContactoTelefonicoPrimero_ConElParametroACero_BuscaConSuVendedor_YMandaVendedorTelefonico()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion("0"), EMPRESA, "PA", FILTRO, true);

            Assert.AreEqual("Clientes?empresa=1&vendedor=PA&filtro=634899669&contactoTelefonicoPrimero=true&vendedorTelefonico=PA", url);
        }

        [TestMethod]
        public async Task ContactoTelefonicoPrimero_Direccion_SinVendedor_YVendedorTelefonicoElSuyo()
        {
            // Antes Dirección mandaba vendedor= vacío y perdía la prioridad del contacto telefónico; ahora la API la decide con su vendedor
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion("0", direccion: true), EMPRESA, "CAR", "peluquería", true);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=peluquería&contactoTelefonicoPrimero=true&vendedorTelefonico=CAR", url);
        }

        [TestMethod]
        public async Task ContactoTelefonicoPrimero_CentralitaConElParametro_EncuentraClientesDeOtrosVendedores()
        {
            // Caso real: la centralita (vendedora telefónica MPP) con PermitirVerClientesTodosLosVendedores = 1 busca en
            // Rapports un cliente de otro vendedor: se busca en todos y la API sigue sabiendo que es telefónica.
            var configuracion = Configuracion("1");
            var servicio = new SelectorClienteService();

            var url = await servicio.ConstruirUrlBusqueda(configuracion, EMPRESA, "MPP", "peluquería", true);
            await servicio.ConstruirUrlBusqueda(configuracion, EMPRESA, "MPP", "peluquería sol", true);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=peluquería&contactoTelefonicoPrimero=true&vendedorTelefonico=MPP", url);
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.PermitirVerClientesTodosLosVendedores))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ContactoTelefonicoPrimero_SiFallaLaLecturaDelParametro_BuscaConSuVendedor()
        {
            var configuracion = Configuracion("1");
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.PermitirVerClientesTodosLosVendedores))
                .ThrowsAsync(new Exception("No se puede leer el parámetro"));

            var url = await new SelectorClienteService().ConstruirUrlBusqueda(configuracion, EMPRESA, "MPP", "peluquería", true);

            Assert.AreEqual("Clientes?empresa=1&vendedor=MPP&filtro=peluquería&contactoTelefonicoPrimero=true&vendedorTelefonico=MPP", url);
        }

        [TestMethod]
        public async Task ContactoTelefonicoPrimero_SinVendedor_NoMandaVendedorTelefonico()
        {
            var url = await new SelectorClienteService().ConstruirUrlBusqueda(Configuracion("0"), EMPRESA, string.Empty, "peluquería", true);

            Assert.AreEqual("Clientes?empresa=1&vendedor=&filtro=peluquería&contactoTelefonicoPrimero=true", url);
        }

        [TestMethod]
        public void EsSi_UnoOTrue_SiYLoDemasNo()
        {
            Assert.IsTrue(Parametros.EsSi("1"));
            Assert.IsTrue(Parametros.EsSi(" 1 "));
            Assert.IsTrue(Parametros.EsSi("TRUE"));
            Assert.IsFalse(Parametros.EsSi("0"));
            Assert.IsFalse(Parametros.EsSi(""));
            Assert.IsFalse(Parametros.EsSi(null));
            Assert.IsFalse(Parametros.EsSi("SI"));
        }
    }
}
