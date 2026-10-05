using ControlesUsuario.Models;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;

namespace CajasTests
{
    [TestClass]
    public class CajasViewModelTests
    {
        private static CajasViewModel CrearViewModel(IContabilidadService servicioContabilidad)
        {
            var configuracion = A.Fake<IConfiguracion>();
            var dialogService = A.Fake<IServicioDialogos>();
            var servicioClientes = A.Fake<IClientesService>();
            var servicioAutenticacion = A.Fake<IServicioAutenticacion>();
            return new CajasViewModel(servicioContabilidad, configuracion, dialogService, servicioClientes, servicioAutenticacion);
        }

        // Un cobro a cuenta sin deudas: lo mínimo para que CanContabilizarCobro sea true y
        // OnContabilizarCobro llegue a llamar al servicio.
        private static void PrepararCobroACuenta(CajasViewModel sut)
        {
            sut.ClienteCompletoSeleccionado = new ClienteDTO { cliente = "15191", contacto = "0" };
            sut.ClienteSeleccionado = "15191";
            sut.FormaPagoSeleccionada = new FormaPago { formaPago = "EFC" };
            sut.CuentaCobro = new CuentaContableDTO { Cuenta = "570" };
            sut.TotalCobrado = 10M;
        }

        #region Nesto#464: un doble clic en Contabilizar creaba dos asientos

        [TestMethod]
        public void CajasViewModel_ContabilizarCobro_MientrasContabilizaNoSePuedeVolverAEjecutar()
        {
            // 03/09/26: la llamada tardó, el botón seguía habilitado, se pulsó dos veces y se
            // contabilizaron dos asientos iguales.
            var servicioContabilidad = A.Fake<IContabilidadService>();
            var enVuelo = new TaskCompletionSource<int>();
            A.CallTo(() => servicioContabilidad.Contabilizar(A<List<PreContabilidadDTO>>._)).Returns(enVuelo.Task);
            var sut = CrearViewModel(servicioContabilidad);
            PrepararCobroACuenta(sut);
            Assert.IsTrue(sut.ContabilizarCobroCommand.CanExecute(null), "precondición: se puede contabilizar");

            sut.ContabilizarCobroCommand.Execute(null);
            bool podiaRepetirEnVuelo = sut.ContabilizarCobroCommand.CanExecute(null);
            sut.ContabilizarCobroCommand.Execute(null); // el segundo clic
            enVuelo.SetResult(0); // termina la primera (0 = no se pudo, para no recargar datos)

            Assert.IsFalse(podiaRepetirEnVuelo, "con la llamada en vuelo el botón tiene que estar deshabilitado");
            A.CallTo(() => servicioContabilidad.Contabilizar(A<List<PreContabilidadDTO>>._)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(sut.EstaOcupado, "al terminar se libera");
            Assert.IsTrue(sut.ContabilizarCobroCommand.CanExecute(null), "y se puede volver a contabilizar");
        }

        [TestMethod]
        public void CajasViewModel_ContabilizarCobro_SiElServicioFallaSeLiberaIgualmente()
        {
            var servicioContabilidad = A.Fake<IContabilidadService>();
            A.CallTo(() => servicioContabilidad.Contabilizar(A<List<PreContabilidadDTO>>._)).Throws(new Exception("la API no responde"));
            var sut = CrearViewModel(servicioContabilidad);
            PrepararCobroACuenta(sut);

            sut.ContabilizarCobroCommand.Execute(null);

            Assert.IsFalse(sut.EstaOcupado);
            Assert.IsTrue(sut.ContabilizarCobroCommand.CanExecute(null));
        }

        #region ELMAH 05/10/26 (Reina): «Error en el algoritmo de cobros: 0,00 € es distinto a 0,01 €»

        private static ExtractoClienteDTO Deuda(int id, string documento, decimal pendiente) => new()
        {
            Id = id, Empresa = "1", Cliente = "26552", Contacto = "1", Documento = documento, Efecto = "1",
            Tipo = pendiente < 0 ? "Abono" : "Factura", Importe = pendiente, ImportePendiente = pendiente,
            Vencimiento = new DateTime(2026, 10, 5)
        };

        [TestMethod]
        public void CajasViewModel_ContabilizarCobro_ConUnAbonoDetrasDeLasFacturas_LoAplicaYCuadraLoACuenta()
        {
            // Cliente 26552: facturas de 147,50 y 175,45 y abono de -73,76 (total 249,19), cobrados 249,20.
            // El reparto paraba al llegar a cero con la segunda factura y el abono, que iba detrás, no se
            // aplicaba: quedaba 0,00 frente a 0,01 a cuenta y saltaba una excepción sin controlar.
            var servicioContabilidad = A.Fake<IContabilidadService>();
            List<PreContabilidadDTO>? enviadas = null;
            A.CallTo(() => servicioContabilidad.Contabilizar(A<List<PreContabilidadDTO>>._))
                .Invokes((List<PreContabilidadDTO> l) => enviadas = l).Returns(Task.FromResult(0));
            var sut = CrearViewModel(servicioContabilidad);
            PrepararCobroACuenta(sut);
            sut.SeleccionarDeudasCommand.Execute(new List<object>
            {
                Deuda(1, "CV2600605", 147.50M), Deuda(2, "NV2616199", 175.45M), Deuda(3, "NV2616201", -73.76M)
            });
            sut.TotalCobrado = 249.20M;

            sut.ContabilizarCobroCommand.Execute(null);

            Assert.IsNotNull(enviadas, "Se contabiliza en vez de reventar");
            Assert.AreEqual(73.76M, enviadas!.Single(l => l.Cuenta == "26552" && l.Documento == "NV2616201").Debe, "El abono se aplica");
            Assert.AreEqual(147.50M, enviadas.Single(l => l.Cuenta == "26552" && l.Documento == "CV2600605").Haber);
            Assert.AreEqual(175.45M, enviadas.Single(l => l.Cuenta == "26552" && l.Documento == "NV2616199").Haber, "La factura se paga entera, no «a cta.»");
            Assert.AreEqual(0.01M, enviadas.Single(l => l.Documento == "A CUENTA").Haber);
        }

        [TestMethod]
        public void CajasViewModel_ContabilizarCobro_SiElRepartoNoCuadra_AvisaAlUsuarioEnVezDeReventar()
        {
            var dialogService = A.Fake<IServicioDialogos>();
            var servicioContabilidad = A.Fake<IContabilidadService>();
            var sut = new CajasViewModel(servicioContabilidad, A.Fake<IConfiguracion>(), dialogService,
                A.Fake<IClientesService>(), A.Fake<IServicioAutenticacion>());
            PrepararCobroACuenta(sut);
            // Una deuda que no está seleccionada en el importe (selección incoherente): el reparto no cuadra
            sut.DeudasSeleccionadas = [Deuda(1, "CV2600605", 147.50M)];
            sut.TotalCobrado = 10M;

            sut.ContabilizarCobroCommand.Execute(null);

            A.CallTo(() => dialogService.ShowError(A<string>.That.Contains("No se ha contabilizado"))).MustHaveHappened();
            A.CallTo(() => servicioContabilidad.Contabilizar(A<List<PreContabilidadDTO>>._)).MustNotHaveHappened();
            Assert.IsFalse(sut.EstaOcupado);
        }

        #endregion

        [TestMethod]
        public void CajasViewModel_EstaOcupado_DeshabilitaLosTresBotonesDeContabilizar()
        {
            var sut = CrearViewModel(A.Fake<IContabilidadService>());
            PrepararCobroACuenta(sut);
            sut.CuentaOrigen = new CuentaContableDTO { Cuenta = "570" };
            sut.CuentaDestino = new CuentaContableDTO { Cuenta = "571" };
            sut.Importe = 90M;
            sut.Concepto = "Traspaso entre cajas";
            Assert.IsTrue(sut.ContabilizarCobroCommand.CanExecute(null));
            Assert.IsTrue(sut.ContabilizarTraspasoCommand.CanExecute(null));

            sut.EstaOcupado = true;

            Assert.IsFalse(sut.ContabilizarCobroCommand.CanExecute(null));
            Assert.IsFalse(sut.ContabilizarGastoCommand.CanExecute(null));
            Assert.IsFalse(sut.ContabilizarTraspasoCommand.CanExecute(null));
        }

        #endregion

        [TestMethod]
        public void CajasViewModel_ContabilizarTraspaso_NoSePuedeEjecutarSiLaCuentaOrigenYLaCuentaDestinoSonIguales()
        {
            // Arrange
            var servicioContabilidad = A.Fake<IContabilidadService>();
            var configuracion = A.Fake<IConfiguracion>();
            var dialogService = A.Fake<IServicioDialogos>();
            var servicioClientes = A.Fake<IClientesService>();
            var servicioAutenticacion = A.Fake<IServicioAutenticacion>();
            var sut = new CajasViewModel(servicioContabilidad, configuracion, dialogService, servicioClientes, servicioAutenticacion);
            CuentaContableDTO cuenta = new CuentaContableDTO
            {
                Cuenta = "570"
            };
            sut.CuentaDestino = cuenta;
            sut.CuentaOrigen = cuenta;

            // Act
            sut.ContabilizarTraspasoCommand.Execute(null);

            // Assert
            Assert.IsFalse(sut.ContabilizarTraspasoCommand.CanExecute(null));
        }

        [TestMethod]
        public void CajasViewModel_ContabilizarTraspaso_LlamaAContabilizarEnIContabilidadService()
        {
            // Arrange
            var servicioContabilidad = A.Fake<IContabilidadService>();
            var configuracion = A.Fake<IConfiguracion>();
            var dialogService = A.Fake<IServicioDialogos>();
            var servicioClientes = A.Fake<IClientesService>();
            var servicioAutenticacion = A.Fake<IServicioAutenticacion>();
            var sut = new CajasViewModel(servicioContabilidad, configuracion, dialogService, servicioClientes, servicioAutenticacion);
            CuentaContableDTO cuentaOrigen = new CuentaContableDTO { Cuenta = "570" };
            CuentaContableDTO cuentaDestino = new CuentaContableDTO { Cuenta = "571" };
            sut.CuentaOrigen = cuentaOrigen;
            sut.CuentaDestino = cuentaDestino;
            sut.Importe = 90M;
            sut.Concepto = "Traspaso entre cajas";

            // Act
            sut.ContabilizarTraspasoCommand.Execute(null);

            // Assert
            // Assert
            A.CallTo(() => servicioContabilidad.Contabilizar(A<PreContabilidadDTO>._))
                .MustHaveHappenedOnceExactly();
        }
    }
}