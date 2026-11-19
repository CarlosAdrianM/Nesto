using ControlesUsuario.Models;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.Services;
using Nesto.Modulos.Cajas.ViewModels;

namespace CajasTests
{
    /// <summary>
    /// Carlos (19/11/26, caso del cliente 33895): la señal de un evento se cobra en Cajas. Al elegir la clienta salen sus reservas sin
    /// pagar; marcando una y contabilizando con la forma de pago de siempre, la API apunta el cobro, la pone pagada, factura el curso y
    /// emite el cupón. La reserva se cobra sola y entera.
    /// </summary>
    [TestClass]
    public class CajasCobroReservaEventoTests
    {
        private const string CAJA = "57000005";
        private IConfiguracion _configuracion = null!;
        private IClientesService _clientes = null!;
        private IContabilidadService _contabilidad = null!;
        private IServicioDialogos _dialogos = null!;
        private List<string> _errores = null!;
        private List<string> _avisos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _configuracion = A.Fake<IConfiguracion>();
            _clientes = A.Fake<IClientesService>();
            _contabilidad = A.Fake<IContabilidadService>();
            _dialogos = A.Fake<IServicioDialogos>();
            _errores = [];
            _avisos = [];
            A.CallTo(() => _dialogos.ShowError(A<string>._)).Invokes((string m) => _errores.Add(m));
            A.CallTo(() => _dialogos.ShowNotification(A<string>._)).Invokes((string m) => _avisos.Add(m));
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.CajaDefecto)).Returns(CAJA);
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.CuentaBancoTarjeta)).Returns("57200013");
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.DiarioCaja)).Returns("_CajaReina");
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.DelegacionDefecto)).Returns("REI");
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.FormaVentaDefecto)).Returns("TIE");
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.FondoCaja)).Returns("100");
            A.CallTo(() => _contabilidad.LeerCuentas(A<string>._, A<string>._)).Returns(Task.FromResult(new List<CuentaContableDTO>
            {
                new() { Cuenta = CAJA },
                new() { Cuenta = "57200013" },
                new() { Cuenta = Constantes.Cuentas.CAJA_PENDIENTE_RECIBIR_TIENDAS }
            }));
            A.CallTo(() => _clientes.LeerDeudas("33895")).Returns(Task.FromResult(new List<ExtractoClienteDTO>
            {
                new() { Id = 1, Empresa = "1", Cliente = "33895", Contacto = "0", Documento = "NV26/000100", Efecto = "1", Tipo = "1",
                    Importe = 30M, ImportePendiente = 30M, Vencimiento = new DateTime(2026, 11, 19) }
            }));
            A.CallTo(() => _clientes.LeerReservasEventoSinPagar("33895")).Returns(Task.FromResult(new List<ReservaEventoCaja>
            {
                new() { InscripcionId = 412, EventoId = 7, Evento = "Masterclass Rosácea", FechaEvento = new DateTime(2026, 11, 24), Empresa = "1",
                    Cliente = "33895", Contacto = "0", Estado = "Reservada", EstadoTexto = "Reservada", Importe = 100M,
                    Descripcion = "Señal Masterclass Rosácea (24/11)", Documento = "INS412", Cobrable = true },
                new() { InscripcionId = 413, EventoId = 9, Evento = "Curso PDRN", Importe = 50M, Descripcion = "Señal Curso PDRN (01/12)",
                    Documento = "INS413", Cobrable = false, Motivo = "La señal de «Curso PDRN» ya está apuntada por transferencia." }
            }));
            A.CallTo(() => _clientes.CobrarReservaEvento(A<int>._, A<CobroReservaEventoCaja>._)).Returns(Task.FromResult(new ResultadoCobroReservaEventoCaja
            {
                Asiento = 4711, Documento = "INS412", Importe = 100M, Factura = "CV26/000900", Pedido = 931500, EstadoFacturacion = "Facturada",
                Cupon = "EV-2611-7K3Q", Mensaje = "Cobrada la señal de «Masterclass Rosácea»: 100,00 € (efectivo), asiento 4711. Factura del curso CV26/000900 (pedido 931500), cobrada. Cupón EV-2611-7K3Q."
            }));
        }

        private async Task<CajasViewModel> CajaConClienta()
        {
            var caja = new CajasViewModel(_contabilidad, _configuracion, _dialogos, _clientes, A.Fake<IServicioAutenticacion>());
            await caja.PrepararCobroAsync("1", "33895", "0", null);
            return caja;
        }

        [TestMethod]
        public async Task AlElegirLaClienta_SalenSusReservasSinPagar()
        {
            CajasViewModel caja = await CajaConClienta();

            Assert.IsTrue(caja.HayReservasEvento);
            Assert.AreEqual(2, caja.ListaReservasEvento.Count);
            Assert.AreEqual(1, caja.ListaDeudas.Count, "Las deudas, como siempre");
            Assert.AreEqual("Se puede cobrar", caja.ListaReservasEvento[0].Situacion);
            StringAssert.Contains(caja.ListaReservasEvento[1].Situacion, "transferencia");
        }

        [TestMethod]
        public async Task MarcarLaReserva_PoneSuSenalComoCobrado_YQuitarlaLaVacia()
        {
            CajasViewModel caja = await CajaConClienta();

            caja.ReservaEventoSeleccionada = caja.ListaReservasEvento[0];
            Assert.AreEqual(100M, caja.TotalCobrado);
            Assert.IsTrue(caja.ContabilizarCobroCommand.CanExecute(null));
            StringAssert.Contains(caja.TextoReservaEvento, "se factura el curso");

            caja.QuitarReservaEventoCommand.Execute(null);
            Assert.IsNull(caja.ReservaEventoSeleccionada);
            Assert.AreEqual(0M, caja.TotalCobrado);
        }

        [TestMethod]
        public async Task Contabilizar_ConLaReserva_LaCobraPorLaApiConLaCajaDeSiempre_YNoMeteApuntesAMano()
        {
            CajasViewModel caja = await CajaConClienta();
            CobroReservaEventoCaja? enviado = null;
            ResultadoCobroReservaEventoCaja resultado = await _clientes.CobrarReservaEvento(0, new CobroReservaEventoCaja());
            A.CallTo(() => _clientes.CobrarReservaEvento(412, A<CobroReservaEventoCaja>._))
                .Invokes((int id, CobroReservaEventoCaja c) => enviado = c).Returns(Task.FromResult(resultado));
            bool cerrado = false;
            caja.CobroContabilizado += (s, e) => cerrado = true;
            caja.ConceptoAdicionalCobros = "ticket 7";
            caja.ReservaEventoSeleccionada = caja.ListaReservasEvento[0];

            caja.ContabilizarCobroCommand.Execute(null);

            Assert.IsNotNull(enviado);
            Assert.AreEqual("EFC", enviado!.FormaPago, "En la tienda el cobro empieza en efectivo");
            Assert.AreEqual(100M, enviado.Importe);
            Assert.AreEqual(CAJA, enviado.CuentaCobro);
            Assert.AreEqual("_CajaReina", enviado.Diario);
            Assert.AreEqual("REI", enviado.Delegacion);
            Assert.AreEqual("TIE", enviado.FormaVenta);
            Assert.AreEqual("ticket 7", enviado.ConceptoAdicional);
            A.CallTo(() => _contabilidad.Contabilizar(A<List<PreContabilidadDTO>>._)).MustNotHaveHappened();
            StringAssert.Contains(_avisos.Single(), "Cupón EV-2611-7K3Q");
            Assert.IsTrue(cerrado, "El diálogo de cobro se cierra como con un cobro normal");
            Assert.IsNull(caja.ReservaEventoSeleccionada);
            Assert.AreEqual(0M, caja.TotalCobrado);
            Assert.AreEqual(0, _errores.Count);
        }

        [TestMethod]
        public async Task Contabilizar_ConTarjeta_MandaTarYElBancoDelDatafono()
        {
            CajasViewModel caja = await CajaConClienta();
            CobroReservaEventoCaja? enviado = null;
            A.CallTo(() => _clientes.CobrarReservaEvento(412, A<CobroReservaEventoCaja>._)).Invokes((int id, CobroReservaEventoCaja c) => enviado = c);
            caja.FormaPagoTarjetaSeleccionada = true;
            caja.ReservaEventoSeleccionada = caja.ListaReservasEvento[0];

            caja.ContabilizarCobroCommand.Execute(null);

            Assert.AreEqual("TAR", enviado!.FormaPago);
            Assert.AreEqual("57200013", enviado.CuentaCobro);
        }

        [TestMethod]
        public async Task Contabilizar_ReservaConFacturasMarcadas_NoCobraYLoDice()
        {
            CajasViewModel caja = await CajaConClienta();
            caja.SeleccionarDeudasCommand.Execute(new System.Collections.ArrayList { caja.ListaDeudas[0] });
            caja.ReservaEventoSeleccionada = caja.ListaReservasEvento[0];

            caja.ContabilizarCobroCommand.Execute(null);

            StringAssert.Contains(_errores.Single(), "se cobra sola");
            A.CallTo(() => _clientes.CobrarReservaEvento(A<int>._, A<CobroReservaEventoCaja>._)).MustNotHaveHappened();
            A.CallTo(() => _contabilidad.Contabilizar(A<List<PreContabilidadDTO>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Contabilizar_ImporteCambiado_NoCobra_LaSenalEntera()
        {
            CajasViewModel caja = await CajaConClienta();
            caja.ReservaEventoSeleccionada = caja.ListaReservasEvento[0];
            caja.TotalCobrado = 50M;

            caja.ContabilizarCobroCommand.Execute(null);

            StringAssert.Contains(_errores.Single(), "entera");
            A.CallTo(() => _clientes.CobrarReservaEvento(A<int>._, A<CobroReservaEventoCaja>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Contabilizar_ReservaNoCobrable_DiceElMotivo()
        {
            CajasViewModel caja = await CajaConClienta();
            caja.ReservaEventoSeleccionada = caja.ListaReservasEvento[1];

            caja.ContabilizarCobroCommand.Execute(null);

            StringAssert.Contains(_errores.Single(), "transferencia");
            A.CallTo(() => _clientes.CobrarReservaEvento(A<int>._, A<CobroReservaEventoCaja>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Contabilizar_LaApiLoRechaza_EnsenaSuMensajeYRecargaLasReservas()
        {
            CajasViewModel caja = await CajaConClienta();
            A.CallTo(() => _clientes.CobrarReservaEvento(A<int>._, A<CobroReservaEventoCaja>._))
                .ThrowsAsync(new ErrorApiCajasException("No se ha cobrado la señal del evento\nLa inscripción 412 ya no está pendiente de pago (pagada): no se ha cobrado nada. Recargue."));
            caja.ReservaEventoSeleccionada = caja.ListaReservasEvento[0];

            caja.ContabilizarCobroCommand.Execute(null);

            StringAssert.Contains(_errores.Single(), "no se ha cobrado nada");
            A.CallTo(() => _clientes.LeerReservasEventoSinPagar("33895")).MustHaveHappenedTwiceExactly();
            Assert.IsFalse(caja.EstaOcupado);
        }

        [TestMethod]
        public async Task SinReservas_OSiFallaLeerlas_ElCobroDeSiempreSigue()
        {
            A.CallTo(() => _clientes.LeerReservasEventoSinPagar(A<string>._)).ThrowsAsync(new Exception("caída"));
            List<PreContabilidadDTO>? enviadas = null;
            A.CallTo(() => _contabilidad.Contabilizar(A<List<PreContabilidadDTO>>._))
                .Invokes((List<PreContabilidadDTO> l) => enviadas = l).Returns(Task.FromResult(4712));

            CajasViewModel caja = await CajaConClienta();
            Assert.IsFalse(caja.HayReservasEvento);
            Assert.AreEqual(0, _errores.Count, "No se molesta a quien cobra");

            caja.SeleccionarDeudasCommand.Execute(new System.Collections.ArrayList { caja.ListaDeudas[0] });
            caja.ContabilizarCobroCommand.Execute(null);

            Assert.IsNotNull(enviadas, "El cobro de la factura va por el camino de siempre");
            A.CallTo(() => _clientes.CobrarReservaEvento(A<int>._, A<CobroReservaEventoCaja>._)).MustNotHaveHappened();
        }
    }
}
