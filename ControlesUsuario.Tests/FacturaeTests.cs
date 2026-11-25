using ControlesUsuario.Dialogs;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#652 (NestoAPI#903): «Descargar para FACe (Facturae)». Reglas puras, el servicio contra un handler falso (nunca la API
    /// real) y el diálogo con el servicio y el guardador falsos (no se abre ni se guarda nada de verdad).
    /// </summary>
    [TestClass]
    public class FacturaeTests
    {
        #region Reglas
        [TestMethod]
        public void UrlFacturae_SinReferencias_SoloEmpresaFacturaYComprobar()
        {
            Assert.AreEqual("Facturas/Facturae?empresa=1&factura=NV2616075&comprobar=true",
                ReglasFacturae.UrlFacturae("1", " NV2616075 ", true, null));
        }

        [TestMethod]
        public void UrlFacturae_ConReferencias_MandaSoloLasEscritasYLimpiaLosDir3()
        {
            string url = ReglasFacturae.UrlFacturae(null, "NV2616075", false, new ReferenciasFacturae
            {
                OficinaContable = "ea 001-9807",
                OrganoGestor = "",
                SuPedido = "PO 12/3",
                Contrato = "  "
            });
            Assert.AreEqual("Facturas/Facturae?empresa=1&factura=NV2616075&oficinaContable=EA0019807&suPedido=PO%2012%2F3", url);
        }

        [TestMethod]
        public void UrlDir3_ConClienteYContacto()
        {
            Assert.AreEqual("Clientes/FacturaElectronica?empresa=1&cliente=33895&contacto=0", ReglasFacturae.UrlDir3("1", "33895 ", "0"));
        }

        [TestMethod]
        public void NombreFichero_ElDeLaApiSinComillas_YSiNoVieneElDeSiempre()
        {
            Assert.AreEqual("Facturae_NV2616075.xml", ReglasFacturae.NombreFichero("\"Facturae_NV2616075.xml\"", "X"));
            Assert.AreEqual("Facturae_NV2616075.xml", ReglasFacturae.NombreFichero(null, "NV2616075"));
        }

        [TestMethod]
        public void MotivoDir3NoValido_ComoLaApi()
        {
            Assert.IsNull(ReglasFacturae.MotivoDir3NoValido("EA0019807", "Oficina contable"));
            Assert.IsNull(ReglasFacturae.MotivoDir3NoValido("l01280796", "Oficina contable"));
            Assert.IsNull(ReglasFacturae.MotivoDir3NoValido("", "Oficina contable"), "Lo vacío lo decide quien llama");
            StringAssert.Contains(ReglasFacturae.MotivoDir3NoValido("L0128", "Órgano gestor"), "del órgano gestor «L0128» no es válido");
            Assert.IsNotNull(ReglasFacturae.MotivoDir3NoValido("1L0128079", "Oficina contable"), "Empieza por letra");
            Assert.IsNotNull(ReglasFacturae.MotivoDir3NoValido("LABCD1234", "Oficina contable"), "Al menos 5 cifras");
        }

        [TestMethod]
        public void MotivosNoSePuedeGuardarDir3_FaltanObligatorios_PeroLosCuatroVaciosEsQuitar()
        {
            Assert.AreEqual(0, ReglasFacturae.MotivosNoSePuedeGuardarDir3("", " ", null, "").Count);
            List<string> motivos = ReglasFacturae.MotivosNoSePuedeGuardarDir3("EA0019807", "", "XX", null);
            Assert.AreEqual(2, motivos.Count);
            StringAssert.Contains(motivos[0], "Falta el código DIR3 del órgano gestor");
            StringAssert.Contains(motivos[1], "de la unidad tramitadora «XX» no es válido");
            Assert.AreEqual(0, ReglasFacturae.MotivosNoSePuedeGuardarDir3("EA0019807", "EA0019807", "EA0019807", null).Count);
        }

        [TestMethod]
        public void MotivoReferenciaLarga_MasDe20()
        {
            Assert.IsNull(ReglasFacturae.MotivoReferenciaLarga(new string('A', 20), "Su pedido"));
            StringAssert.Contains(ReglasFacturae.MotivoReferenciaLarga(new string('A', 21), "Su pedido"), "más de 20 caracteres (tiene 21)");
        }

        [TestMethod]
        public void TextoOrigenSuPedido_NuestroPedido_LoExplica()
        {
            StringAssert.Contains(ReglasFacturae.TextoOrigenSuPedido("NuestroPedido"), "nuestro número de pedido");
            Assert.IsNull(ReglasFacturae.TextoOrigenSuPedido(null));
        }

        [TestMethod]
        public void Ayuda_LlevaLosPasosDeB2Brouter()
        {
            StringAssert.Contains(ReglasFacturae.AYUDA_B2BROUTER, "Ingresos");
            StringAssert.Contains(ReglasFacturae.AYUDA_B2BROUTER, "«Importar»");
            StringAssert.Contains(ReglasFacturae.AYUDA_B2BROUTER, "sin marcar ninguna casilla");
            StringAssert.Contains(ReglasFacturae.AYUDA_B2BROUTER, "«Emitir y enviar»");
        }
        #endregion

        #region Servicio
        private sealed class HandlerFalso : HttpMessageHandler
        {
            public HttpRequestMessage Peticion;
            public string Cuerpo;
            public HttpStatusCode Estado = HttpStatusCode.OK;
            public HttpContent Respuesta = new StringContent("{}", Encoding.UTF8, "application/json");

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Peticion = request;
                Cuerpo = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
                return new HttpResponseMessage(Estado) { Content = Respuesta };
            }
        }

        private static (ServicioFacturae, HandlerFalso) ServicioHttp()
        {
            var handler = new HandlerFalso();
            var factoria = A.Fake<IClienteApiFactory>();
            A.CallTo(() => factoria.Crear()).ReturnsLazily(() => new HttpClient(handler, disposeHandler: false) { BaseAddress = new Uri("http://api.local/api/") });
            return (new ServicioFacturae(factoria), handler);
        }

        [TestMethod]
        public async Task Servicio_Comprobar_LeeElJson()
        {
            var (servicio, handler) = ServicioHttp();
            handler.Respuesta = new StringContent("{\"Listo\":true,\"Cliente\":\"33895\",\"Total\":121.5,\"Centros\":[{\"Rol\":\"01\",\"Codigo\":\"EA0019807\"}]}", Encoding.UTF8, "application/json");

            ComprobacionFacturaeModel comprobacion = await servicio.Comprobar("1", "NV2616075", null);

            Assert.AreEqual("http://api.local/api/Facturas/Facturae?empresa=1&factura=NV2616075&comprobar=true", handler.Peticion.RequestUri.ToString());
            Assert.IsTrue(comprobacion.Listo);
            Assert.AreEqual(121.5m, comprobacion.Total);
            Assert.AreEqual("EA0019807", ReglasFacturae.CodigoDelRol(comprobacion, "01"));
        }

        [TestMethod]
        public async Task Servicio_Descargar_DevuelveLosBytesYElNombreDeContentDisposition()
        {
            var (servicio, handler) = ServicioHttp();
            var contenido = new ByteArrayContent(Encoding.UTF8.GetBytes("<fe:Facturae/>"));
            contenido.Headers.ContentType = new MediaTypeHeaderValue("application/xml");
            contenido.Headers.ContentDisposition = new ContentDispositionHeaderValue("attachment") { FileName = "Facturae_NV2616075.xml" };
            handler.Respuesta = contenido;

            FicheroFacturae fichero = await servicio.Descargar("1", "NV2616075", null);

            Assert.AreEqual("Facturae_NV2616075.xml", fichero.NombreFichero);
            Assert.AreEqual("<fe:Facturae/>", Encoding.UTF8.GetString(fichero.Contenido));
            Assert.IsFalse(handler.Peticion.RequestUri.ToString().Contains("comprobar"));
        }

        [TestMethod]
        public async Task Servicio_Descargar_400_LanzaConElTextoDeLaApi()
        {
            var (servicio, handler) = ServicioHttp();
            handler.Estado = HttpStatusCode.BadRequest;
            handler.Respuesta = new StringContent("Faltan los códigos DIR3 del organismo.", Encoding.UTF8, "text/plain");

            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.Descargar("1", "NV2616075", null));
            Assert.AreEqual("Faltan los códigos DIR3 del organismo.", ex.Message);
        }

        [TestMethod]
        public async Task Servicio_GuardarDir3_PutConElCuerpo_Y503ConElMensaje()
        {
            var (servicio, handler) = ServicioHttp();
            handler.Respuesta = new StringContent("{\"Guardado\":true,\"OficinaContable\":\"EA0019807\"}", Encoding.UTF8, "application/json");
            Dir3ClienteModel guardado = await servicio.GuardarDir3(new Dir3ClienteModel { Cliente = "33895", Contacto = "0", OficinaContable = "EA0019807" });
            Assert.AreEqual(HttpMethod.Put, handler.Peticion.Method);
            Assert.AreEqual("http://api.local/api/Clientes/FacturaElectronica", handler.Peticion.RequestUri.ToString());
            StringAssert.Contains(handler.Cuerpo, "\"OficinaContable\":\"EA0019807\"");
            Assert.IsTrue(guardado.Guardado);

            handler.Estado = HttpStatusCode.ServiceUnavailable;
            handler.Respuesta = new StringContent("\"Todavía no existe la tabla de los DIR3.\"", Encoding.UTF8, "application/json");
            var ex = await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => servicio.GuardarDir3(new Dir3ClienteModel()));
            Assert.AreEqual("Todavía no existe la tabla de los DIR3.", ex.Message);
        }
        #endregion

        #region Diálogo
        private IServicioFacturae _servicio;
        private IGuardadorFicherosDescarga _guardador;
        private readonly List<ReferenciasFacturae> _comprobadoCon = new List<ReferenciasFacturae>();

        private static ComprobacionFacturaeModel ComprobacionDeLaFicha(bool listo = true) => new ComprobacionFacturaeModel
        {
            Empresa = "1",
            Factura = "NV2616075",
            Cliente = "33895",
            Contacto = "0",
            Listo = listo,
            Faltas = listo ? new List<string>() : new List<string> { "Faltan los códigos DIR3 del organismo." },
            Avisos = new List<string> { "La empresa tiene varios IBAN: va el primero." },
            NombreReceptor = "AYUNTAMIENTO DE PRUEBA",
            NifReceptor = "P2800000A",
            OrigenDir3 = listo ? "Ficha" : null,
            Centros = listo
                ? new List<CentroFacturaeModel>
                {
                    new CentroFacturaeModel { Rol = "01", Descripcion = "Oficina contable", Codigo = "L01280796", Nombre = "Intervención" },
                    new CentroFacturaeModel { Rol = "02", Descripcion = "Órgano gestor", Codigo = "L01280796" },
                    new CentroFacturaeModel { Rol = "03", Descripcion = "Unidad tramitadora", Codigo = "L01280796" }
                }
                : new List<CentroFacturaeModel>(),
            SuPedido = "930555",
            OrigenSuPedido = "NuestroPedido",
            BaseImponible = 100m,
            Impuestos = new List<ImpuestoFacturaeModel> { new ImpuestoFacturaeModel { PorcentajeIva = 21, BaseImponible = 100, CuotaIva = 21 } },
            TotalImpuestos = 21m,
            Total = 121m,
            Vencimientos = new List<VencimientoFacturaeModel> { new VencimientoFacturaeModel { Fecha = new DateTime(2026, 12, 25), Importe = 121m, FormaPago = "TRN", Iban = "ES00 0000" } }
        };

        private FacturaeDialogViewModel Dialogo(params ComprobacionFacturaeModel[] respuestas)
        {
            _servicio = A.Fake<IServicioFacturae>();
            _guardador = A.Fake<IGuardadorFicherosDescarga>();
            int llamada = 0;
            A.CallTo(() => _servicio.Comprobar(A<string>._, A<string>._, A<ReferenciasFacturae>._))
                .ReturnsLazily((string e, string f, ReferenciasFacturae r) =>
                {
                    _comprobadoCon.Add(r);
                    return Task.FromResult(respuestas[Math.Min(llamada++, respuestas.Length - 1)]);
                });
            A.CallTo(() => _servicio.Descargar(A<string>._, A<string>._, A<ReferenciasFacturae>._))
                .Returns(Task.FromResult(new FicheroFacturae { NombreFichero = "Facturae_NV2616075.xml", Contenido = new byte[] { 1, 2 } }));
            return new FacturaeDialogViewModel(_servicio, _guardador);
        }

        private static async Task Abrir(FacturaeDialogViewModel vm)
        {
            vm.OnDialogOpened(new ParametrosDialogo { { FacturaeDialogViewModel.PARAMETRO_EMPRESA, "1" }, { FacturaeDialogViewModel.PARAMETRO_FACTURA, " NV2616075 " } });
            // OnDialogOpened lanza la comprobación sin esperar: con el servicio falso termina en el acto
            await Task.Yield();
        }

        [TestMethod]
        public async Task Dialogo_AlAbrir_CompruebaYEnseñaLoQueVaEnElFichero()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha());
            await Abrir(vm);

            Assert.IsTrue(vm.HayComprobacion);
            Assert.IsTrue(vm.Listo);
            StringAssert.Contains(vm.Receptor, "AYUNTAMIENTO DE PRUEBA (NIF P2800000A)");
            Assert.AreEqual(3, vm.Centros.Count);
            StringAssert.Contains(vm.Centros[0], "Oficina contable: L01280796 (Intervención)");
            StringAssert.Contains(vm.TextoSuPedido, "930555 (no hay P.O. del organismo: va nuestro número de pedido)");
            StringAssert.Contains(vm.Total, "121,00 €");
            Assert.AreEqual(1, vm.Avisos.Count);
            Assert.IsFalse(vm.HayFaltas);
            Assert.AreEqual("L01280796", vm.OficinaContable, "Los DIR3 de la ficha se enseñan en los campos");
            Assert.IsTrue(vm.PuedeDescargar);
        }

        [TestMethod]
        public async Task Dialogo_LosDir3DeLaFicha_NoSeVuelvenAMandarEnLaLlamada()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha());
            await Abrir(vm);

            await vm.ComprobarAsync();

            Assert.AreEqual(2, _comprobadoCon.Count);
            Assert.AreEqual(string.Empty, _comprobadoCon[1].OficinaContable);
            Assert.AreEqual(string.Empty, _comprobadoCon[1].UnidadTramitadora);
        }

        [TestMethod]
        public async Task Dialogo_ConFaltas_NoSePuedeDescargar_YAlEscribirLosDir3SeMandanYHayQueVolverAComprobar()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha(listo: false), ComprobacionDeLaFicha());
            await Abrir(vm);
            Assert.IsTrue(vm.HayFaltas);
            Assert.IsFalse(vm.PuedeDescargar);

            vm.OficinaContable = "ea0019807";
            vm.OrganoGestor = "EA0019807";
            vm.UnidadTramitadora = "EA0019807";
            Assert.IsTrue(vm.HayCambiosSinComprobar);
            Assert.IsTrue(vm.PuedeGuardarDir3EnFicha);

            await vm.ComprobarAsync();

            Assert.AreEqual("EA0019807", _comprobadoCon[1].OficinaContable);
            Assert.IsTrue(vm.Listo);
        }

        [TestMethod]
        public async Task Dialogo_TrasComprobar_CambiarElPO_ObligaAVolverAComprobar()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha());
            await Abrir(vm);
            Assert.IsTrue(vm.PuedeDescargar);

            vm.SuPedido = "PO-1234";

            Assert.IsTrue(vm.HayCambiosSinComprobar);
            Assert.IsFalse(vm.PuedeDescargar);
        }

        [TestMethod]
        public async Task Dialogo_CodigoQueNoVale_NoLlamaALaApiYLoDice()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha());
            await Abrir(vm);
            vm.OrganoGestor = "XX";

            await vm.ComprobarAsync();

            Assert.AreEqual(1, _comprobadoCon.Count);
            StringAssert.Contains(vm.Error, "«XX» no es válido");
        }

        [TestMethod]
        public async Task Dialogo_Descargar_GuardaDondeElijaYAbreLaCarpeta()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha());
            await Abrir(vm);
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, "Facturae_NV2616075.xml", A<string>._, A<string>._, ".xml"))
                .Returns(@"C:\Descargas\Facturae_NV2616075.xml");

            await vm.DescargarAsync();

            A.CallTo(() => _guardador.Guardar(@"C:\Descargas\Facturae_NV2616075.xml", A<byte[]>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _guardador.EnsenarEnCarpeta(@"C:\Descargas\Facturae_NV2616075.xml")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(@"C:\Descargas\Facturae_NV2616075.xml", vm.Descargado);
            StringAssert.Contains(vm.Mensaje, "B2Brouter");
        }

        [TestMethod]
        public async Task Dialogo_Descargar_SiCancelaElGuardar_NoGuardaNada()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha());
            await Abrir(vm);
            A.CallTo(() => _guardador.ElegirDondeGuardar(A<string>._, A<string>._, A<string>._, A<string>._, A<string>._)).Returns(null);

            await vm.DescargarAsync();

            A.CallTo(() => _guardador.Guardar(A<string>._, A<byte[]>._)).MustNotHaveHappened();
            Assert.IsNull(vm.Descargado);
        }

        [TestMethod]
        public async Task Dialogo_Descargar_ErrorDeLaApi_SeEnseña()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha());
            await Abrir(vm);
            A.CallTo(() => _servicio.Descargar(A<string>._, A<string>._, A<ReferenciasFacturae>._))
                .ThrowsAsync(new InvalidOperationException("Solo Administración, Dirección o Informática pueden descargar facturas para FACe."));

            await vm.DescargarAsync();

            StringAssert.Contains(vm.Error, "Solo Administración, Dirección o Informática");
            Assert.IsFalse(vm.Trabajando);
        }

        [TestMethod]
        public async Task Dialogo_GuardarDir3EnFicha_PutConElClienteYContactoDeLaFactura()
        {
            FacturaeDialogViewModel vm = Dialogo(ComprobacionDeLaFicha());
            await Abrir(vm);
            Dir3ClienteModel enviado = null;
            A.CallTo(() => _servicio.GuardarDir3(A<Dir3ClienteModel>._))
                .ReturnsLazily((Dir3ClienteModel d) => { enviado = d; return Task.FromResult(d); });
            vm.UnidadTramitadora = "GE0001234";

            await vm.GuardarDir3EnFichaAsync();

            Assert.AreEqual("33895", enviado.Cliente);
            Assert.AreEqual("0", enviado.Contacto);
            Assert.AreEqual("L01280796", enviado.OficinaContable);
            Assert.AreEqual("Intervención", enviado.NombreOficinaContable, "El nombre del centro que no ha cambiado se conserva");
            Assert.AreEqual("GE0001234", enviado.UnidadTramitadora);
            StringAssert.Contains(vm.Mensaje, "33895/0");
        }

        [TestMethod]
        public async Task Dialogo_ErrorAlComprobar_SeEnseñaYNoSePuedeDescargar()
        {
            _servicio = A.Fake<IServicioFacturae>();
            A.CallTo(() => _servicio.Comprobar(A<string>._, A<string>._, A<ReferenciasFacturae>._))
                .ThrowsAsync(new InvalidOperationException("No existe la factura NV2616075 en la empresa 1."));
            var vm = new FacturaeDialogViewModel(_servicio, A.Fake<IGuardadorFicherosDescarga>());

            await Abrir(vm);

            StringAssert.Contains(vm.Error, "No existe la factura NV2616075");
            Assert.IsFalse(vm.PuedeDescargar);
        }
        #endregion
    }
}
