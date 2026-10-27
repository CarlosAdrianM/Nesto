using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Rapports;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using idDescripcion = Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO.idDescripcion;

namespace RapportsTests
{
    /// <summary>
    /// NestoAPI#593: pestaña «Cheques regalo» junto a «clientes para contactar»: resumen por vendedor (lo que deja ver la
    /// API), pendientes del vendedor del desplegable, PDF de la factura y llamar/WhatsApp creando el rapport.
    /// </summary>
    [TestClass]
    public class ChequesRegaloRapportsTests
    {
        private IRapportService _servicio;
        private IConfiguracion _configuracion;
        private IServicioDialogos _dialogos;
        private List<string> _abiertos;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IRapportService>();
            _configuracion = A.Fake<IConfiguracion>();
            _dialogos = A.Fake<IServicioDialogos>();
            _abiertos = new List<string>();
            A.CallTo(() => _servicio.CargarListaTipos()).Returns(new List<idDescripcion>
            {
                new idDescripcion("V", "Visita"),
                new idDescripcion("T", "Teléfono"),
                new idDescripcion("W", "WhatsApp")
            });
            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new SugerenciasContactoRespuesta { Sugerencias = new List<ClienteProbabilidadVenta>() }));
            A.CallTo(() => _servicio.CargarVendedoresSugerencias()).Returns(Task.FromResult(new List<VendedorSugerencias>
            {
                new VendedorSugerencias { Vendedor = "ASH", Nombre = "Alberto" },
                new VendedorSugerencias { Vendedor = "JGP", Nombre = "Javier" }
            }));
            A.CallTo(() => _servicio.CargarResumenChequesRegalo()).Returns(Task.FromResult(Resumen()));
            A.CallTo(() => _servicio.CargarChequesRegaloPendientes(A<string>._))
                .ReturnsLazily((string vendedor) => Task.FromResult(new List<ChequeRegaloPendiente> { Pendiente("cliente de " + vendedor) }));
            A.CallTo(() => _servicio.cargarListaRapports(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ObservableCollection<SeguimientoClienteDTO>()));
            A.CallTo(() => _servicio.CargarResumenVentasCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ResumenVentasClienteResponse { Datos = new List<VentaClienteResumenDTO>() }));
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.Vendedor)).Returns(Task.FromResult("ASH"));
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.UltTipoSeguimientoCliente)).Returns(Task.FromResult("V"));
        }

        private static ResumenChequesRegalo Resumen() => new ResumenChequesRegalo
        {
            Campana = new CampanaChequesRegaloResumen { Codigo = "CHEQUE50_OCT_2026", Importe = 50m, CanjeHasta = new DateTime(2026, 11, 7), DiasRestantes = 5 },
            Vendedores = new List<ResumenChequesVendedor>
            {
                new ResumenChequesVendedor { Vendedor = "ASH", Nombre = "Alberto", Generados = 3, Pendientes = 2, Usados = 1 },
                new ResumenChequesVendedor { Vendedor = "JGP", Nombre = "Javier", Generados = 2, Pendientes = 1, Usados = 1 }
            },
            Total = new ResumenChequesVendedor { Nombre = "Total", Generados = 5, Pendientes = 3, Usados = 2 }
        };

        private static ChequeRegaloPendiente Pendiente(string cliente, string telefono = "914770142/686730444") => new ChequeRegaloPendiente
        {
            Cliente = cliente,
            Contacto = "0",
            Nombre = "Centro",
            Telefono = telefono,
            Estado = "Disponible",
            DiasRestantes = 5,
            EmpresaFactura = "3",
            FacturaOrigen = "GB2601615",
            FechaFactura = new DateTime(2026, 10, 13),
            TotalFactura = 133.5m
        };

        private ListaRapportsViewModel Abrir()
        {
            var vm = new ListaRapportsViewModel(A.Fake<IServicioNavegacion>(), _configuracion, _servicio,
                _dialogos, new WeakReferenceMessenger(), A.Fake<Nesto.Infrastructure.Services.IServicioSugerenciasProductos>())
            {
                AbrirConElSistema = ruta => _abiertos.Add(ruta)
            };
            vm.AlLlegar(new ParametrosNavegacion());
            vm.GrupoSubgrupoSeleccionado = string.Empty;
            return vm;
        }

        [TestMethod]
        public void Abrir_ConCampana_SaleLaPestanaConElResumenPeroSinPedirLosPendientes()
        {
            var vm = Abrir();

            Assert.IsTrue(vm.HayCampanaChequesRegalo);
            Assert.AreEqual(3, vm.ResumenChequesRegalo.FilasConTotal.Count);
            Assert.IsTrue(vm.ResumenChequesRegalo.FilasConTotal.Last().EsTotal);
            A.CallTo(() => _servicio.CargarChequesRegaloPendientes(A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void Abrir_SinCampanaOConLaApiAntigua_NoSaleLaPestana()
        {
            A.CallTo(() => _servicio.CargarResumenChequesRegalo()).Returns(Task.FromResult<ResumenChequesRegalo>(null));

            var vm = Abrir();

            Assert.IsFalse(vm.HayCampanaChequesRegalo);
        }

        [TestMethod]
        public void MostrarLaPestana_CargaLosPendientesDelVendedorDelDesplegable()
        {
            var vm = Abrir();

            vm.MostrandoChequesRegalo = true;

            A.CallTo(() => _servicio.CargarChequesRegaloPendientes("ASH")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("cliente de ASH", vm.ListaChequesPendientes.Single().Cliente);
            Assert.AreEqual("1 cheque sin usar de ASH", vm.TextoChequesPendientes);
            Assert.IsFalse(vm.IsLoadingChequesRegalo);

            vm.MostrandoChequesRegalo = false;
            vm.MostrandoChequesRegalo = true;
            A.CallTo(() => _servicio.CargarChequesRegaloPendientes(A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void CambiarDeVendedor_ConLosPendientesYaVistos_LosRecargaDeEseVendedor()
        {
            var vm = Abrir();
            vm.VendedorSugerencias = "JGP";
            A.CallTo(() => _servicio.CargarChequesRegaloPendientes(A<string>._)).MustNotHaveHappened();

            vm.MostrandoChequesRegalo = true;
            vm.VendedorSugerencias = "ASH";

            Assert.AreEqual("cliente de ASH", vm.ListaChequesPendientes.Single().Cliente);
            A.CallTo(() => _servicio.CargarChequesRegaloPendientes("JGP")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ElegirUnaFilaDelResumen_PasaAEseVendedor_ElTotalNo()
        {
            var vm = Abrir();
            vm.MostrandoChequesRegalo = true;

            vm.ResumenChequesVendedorSeleccionado = vm.ResumenChequesRegalo.FilasConTotal.Single(f => f.Vendedor == "JGP");
            Assert.AreEqual("JGP", vm.VendedorSugerencias);
            Assert.AreEqual("cliente de JGP", vm.ListaChequesPendientes.Single().Cliente);

            vm.ResumenChequesVendedorSeleccionado = vm.ResumenChequesRegalo.FilasConTotal.Last();
            Assert.AreEqual("JGP", vm.VendedorSugerencias);
        }

        [TestMethod]
        public async Task Llamar_CreaUnRapportDeTelefonoDeEseClienteConElVendedorDelUsuario()
        {
            var vm = Abrir();
            vm.VendedorSugerencias = "JGP";

            await vm.LlamarChequeAsync(Pendiente("37633"));

            SeguimientoClienteDTO nuevo = vm.listaRapports.Single(r => r.Id == 0);
            Assert.AreEqual("37633", nuevo.Cliente);
            Assert.AreEqual("0", nuevo.Contacto);
            Assert.AreEqual("T", nuevo.Tipo);
            Assert.AreEqual("ASH", nuevo.Vendedor);
            Assert.AreEqual(0, _abiertos.Count);
        }

        [TestMethod]
        public async Task WhatsApp_AbreElChatConElMovilYCreaUnRapportDeWhatsApp()
        {
            var vm = Abrir();

            await vm.WhatsAppChequeAsync(Pendiente("23854"));

            CollectionAssert.AreEqual(new[] { "https://wa.me/34686730444" }, _abiertos);
            SeguimientoClienteDTO nuevo = vm.listaRapports.Single(r => r.Id == 0);
            Assert.AreEqual("W", nuevo.Tipo);
            Assert.AreEqual("23854", nuevo.Cliente);
        }

        [TestMethod]
        public async Task WhatsApp_SinMovil_SoloElRapport()
        {
            var vm = Abrir();

            await vm.WhatsAppChequeAsync(Pendiente("23854", "914770142"));

            Assert.AreEqual(0, _abiertos.Count);
            Assert.AreEqual("W", vm.listaRapports.Single(r => r.Id == 0).Tipo);
        }

        [TestMethod]
        public async Task AbrirFactura_DescargaElPdfDeLaEmpresaDeLaFacturaYLoAbre()
        {
            A.CallTo(() => _servicio.DescargarFacturaPdf("3", "GB2601615")).Returns(Task.FromResult(new byte[] { 0x25, 0x50, 0x44, 0x46 }));
            var vm = Abrir();

            await vm.AbrirFacturaChequeAsync(Pendiente("5825"));

            string ruta = _abiertos.Single();
            StringAssert.EndsWith(ruta, "Factura_GB2601615.pdf");
            Assert.IsTrue(File.Exists(ruta));
            Assert.IsFalse(vm.IsLoadingChequesRegalo);
        }

        [TestMethod]
        public async Task AbrirFactura_SiFalla_LoDiceYNoAbreNada()
        {
            A.CallTo(() => _servicio.DescargarFacturaPdf(A<string>._, A<string>._)).ThrowsAsync(new Exception("No se ha podido obtener la factura GB2601615"));
            var vm = Abrir();

            await vm.AbrirFacturaChequeAsync(Pendiente("5825"));

            Assert.AreEqual(0, _abiertos.Count);
            A.CallTo(() => _dialogos.ShowError("No se ha podido obtener la factura GB2601615")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Textos_DiasRestantesFacturaYContacto()
        {
            var cheque = Pendiente("1");
            Assert.AreEqual("Quedan 5 días", cheque.TextoDiasRestantes);
            cheque.DiasRestantes = 1;
            Assert.AreEqual("Queda 1 día", cheque.TextoDiasRestantes);
            cheque.DiasRestantes = 0;
            Assert.AreEqual("Último día", cheque.TextoDiasRestantes);
            cheque.Estado = "PendienteDeActivar";
            cheque.FechaActivacion = new DateTime(2026, 10, 25);
            Assert.AreEqual("Se activa el 25/10", cheque.TextoDiasRestantes);

            Assert.AreEqual("Factura GB2601615 del 13/10: 133,50 €", cheque.TextoFactura);
            Assert.AreEqual("Sin contacto reciente", cheque.TextoUltimoContacto);
            cheque.DiasDesdeUltimoContacto = 3;
            Assert.AreEqual("Hablasteis hace 3 días", cheque.TextoUltimoContacto);
            Assert.AreEqual("686730444", cheque.Movil);
            Assert.AreEqual("Cheque de 50 € · se puede usar hasta el 07/11 (quedan 5 días)", Resumen().Campana.Texto);
        }
    }
}
