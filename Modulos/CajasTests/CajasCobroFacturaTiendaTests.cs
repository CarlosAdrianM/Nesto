using ControlesUsuario.Models;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;

namespace CajasTests
{
    /// <summary>
    /// Nesto#645 (Novedades 1317 y 1296): el cobro en caja de la factura recién creada en la tienda (con el efecto ya marcado) y,
    /// en efectivo, «Entrega el cliente» y el cambio.
    /// </summary>
    [TestClass]
    public class CajasCobroFacturaTiendaTests
    {
        private const string CAJA = "57000005";
        private IConfiguracion _configuracion = null!;
        private IClientesService _clientes = null!;
        private IContabilidadService _contabilidad = null!;
        private IServicioDialogos _dialogos = null!;

        [TestInitialize]
        public void Inicializar()
        {
            _configuracion = A.Fake<IConfiguracion>();
            _clientes = A.Fake<IClientesService>();
            _contabilidad = A.Fake<IContabilidadService>();
            _dialogos = A.Fake<IServicioDialogos>();
            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.CajaDefecto)).Returns(CAJA);
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.CuentaBancoTarjeta)).Returns("57200013");
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.DiarioCaja)).Returns("_CajaReina");
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.FondoCaja)).Returns("100");
            A.CallTo(() => _contabilidad.LeerCuentas(A<string>._, A<string>._)).Returns(Task.FromResult(new List<CuentaContableDTO>
            {
                new() { Cuenta = CAJA },
                new() { Cuenta = "57200013" },
                new() { Cuenta = Constantes.Cuentas.CAJA_PENDIENTE_RECIBIR_TIENDAS }
            }));
            A.CallTo(() => _clientes.LeerDeudas("29064")).Returns(Task.FromResult(new List<ExtractoClienteDTO>
            {
                Deuda(1, "1", "NV26/000100", 50M),
                Deuda(2, "1", "NV26/000200", 30.25M),
                Deuda(3, "3", "NV26/000200", 7M) // mismo número en la otra empresa: no es la factura
            }));
        }

        private static ExtractoClienteDTO Deuda(int id, string empresa, string documento, decimal pendiente) => new()
        {
            Id = id, Empresa = empresa, Cliente = "29064", Contacto = "0", Documento = documento + "  ", Efecto = "1",
            Tipo = "1", Importe = pendiente, ImportePendiente = pendiente, Vencimiento = new DateTime(2026, 11, 17)
        };

        private CajasViewModel CrearCaja() =>
            new(_contabilidad, _configuracion, _dialogos, _clientes, A.Fake<IServicioAutenticacion>());

        [TestMethod]
        public async Task PrepararCobro_MarcaSoloElEfectoDeLaFacturaDeEsaEmpresa_YPoneSuImporteComoCobrado()
        {
            var caja = CrearCaja();
            IReadOnlyList<ExtractoClienteDTO>? marcadasEnVista = null;
            caja.DeudasPreseleccionadas += (s, d) => marcadasEnVista = d;

            await caja.PrepararCobroAsync("1", "29064", "0", "NV26/000200");

            Assert.AreEqual("29064", caja.ClienteSeleccionado);
            Assert.AreEqual(1, caja.DeudasSeleccionadas.Count());
            Assert.AreEqual(2, caja.DeudasSeleccionadas.Single().Id);
            Assert.AreEqual(30.25M, caja.ImporteDeudasSeleccionadas);
            Assert.AreEqual(30.25M, caja.TotalCobrado);
            Assert.IsNotNull(marcadasEnVista, "La vista marca la fila en la lista");
            Assert.AreEqual(2, marcadasEnVista!.Single().Id);
            Assert.IsTrue(caja.FormaPagoEfectivoSeleccionada, "En la tienda el cobro empieza en efectivo");
            Assert.AreEqual("EFC", caja.FormaPagoSeleccionada.formaPago, "Aunque el control aún no haya cargado su lista");
            Assert.IsTrue(caja.ContabilizarCobroCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task PrepararCobro_AlContabilizar_VaLaFacturaMarcadaYAvisaParaCerrarElDialogo()
        {
            List<PreContabilidadDTO>? enviadas = null;
            A.CallTo(() => _contabilidad.Contabilizar(A<List<PreContabilidadDTO>>._))
                .Invokes((List<PreContabilidadDTO> l) => enviadas = l).Returns(Task.FromResult(4711));
            var caja = CrearCaja();
            var dialogo = new CobroCajaDialogViewModel(caja);
            ResultadoDialogo? cierre = null;
            dialogo.RequestClose += r => cierre = r;
            dialogo.OnDialogOpened(new ParametrosDialogo
            {
                { CobroEnCajaFactura.PARAMETRO_EMPRESA, "1" }, { CobroEnCajaFactura.PARAMETRO_CLIENTE, "29064" },
                { CobroEnCajaFactura.PARAMETRO_CONTACTO, "0" }, { CobroEnCajaFactura.PARAMETRO_DOCUMENTO, "NV26/000200" }
            });
            await dialogo.PrepararAsync();
            Assert.AreEqual("Cobro en caja de la factura NV26/000200", dialogo.Title);

            caja.ContabilizarCobroCommand.Execute(null);

            Assert.IsNotNull(enviadas);
            var cliente = enviadas!.Single(l => l.TipoCuenta == Constantes.TiposCuenta.CLIENTE);
            Assert.AreEqual("NV26/000200  ", cliente.Documento);
            Assert.AreEqual(30.25M, cliente.Haber);
            Assert.AreEqual(2, cliente.Liquidado, "Liquida el efecto de la factura");
            Assert.AreEqual(CAJA, enviadas.Single(l => l.TipoCuenta == Constantes.TiposCuenta.CUENTA_CONTABLE).Cuenta);
            Assert.IsNotNull(cierre, "Contabilizado, el diálogo se cierra");
            Assert.AreEqual(ResultadoBoton.OK, cierre!.Result);
        }

        [TestMethod]
        public void Dialogo_CerrarSinCobrar_SeCierraConCancel()
        {
            var dialogo = new CobroCajaDialogViewModel(CrearCaja());
            ResultadoDialogo? cierre = null;
            dialogo.RequestClose += r => cierre = r;

            dialogo.CerrarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, cierre!.Result);
        }

        [TestMethod]
        public async Task PrepararCobro_SinEfectosDeEseDocumento_NoMarcaNada()
        {
            var caja = CrearCaja();

            await caja.PrepararCobroAsync("1", "29064", "0", "NV26/999999");

            Assert.AreEqual(0, caja.DeudasSeleccionadas.Count());
            Assert.AreEqual(0M, caja.TotalCobrado);
        }

        [TestMethod]
        public async Task EntregaCliente_EnEfectivoEnTienda_CalculaElCambio()
        {
            var caja = CrearCaja();
            await caja.PrepararCobroAsync("1", "29064", "0", "NV26/000200");
            Assert.IsTrue(caja.MostrarEntregaCliente);
            Assert.IsNull(caja.Cambio, "Sin entrega, no hay cambio");
            Assert.AreEqual(string.Empty, caja.TextoCambio);

            caja.EntregaCliente = 50M;

            Assert.AreEqual(19.75M, caja.Cambio);
            StringAssert.StartsWith(caja.TextoCambio, "Cambio: 19,75");

            caja.EntregaCliente = 20M;
            Assert.AreEqual(-10.25M, caja.Cambio);
            StringAssert.StartsWith(caja.TextoCambio, "Faltan 10,25");
        }

        [TestMethod]
        public async Task EntregaCliente_ConTarjetaONoEsTienda_NoSeEnsena()
        {
            var caja = CrearCaja();
            await caja.PrepararCobroAsync("1", "29064", "0", "NV26/000200");

            caja.FormaPagoTarjetaSeleccionada = true;
            Assert.IsFalse(caja.MostrarEntregaCliente, "Con tarjeta no hay cambio que dar");

            A.CallTo(() => _configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(false);
            caja.FormaPagoEfectivoSeleccionada = true;
            Assert.IsFalse(caja.MostrarEntregaCliente, "Solo en las tiendas");
        }

        [TestMethod]
        public async Task EntregaCliente_TrasContabilizar_SeVacia()
        {
            A.CallTo(() => _contabilidad.Contabilizar(A<List<PreContabilidadDTO>>._)).Returns(Task.FromResult(4712));
            var caja = CrearCaja();
            await caja.PrepararCobroAsync("1", "29064", "0", "NV26/000200");
            caja.EntregaCliente = 50M;

            caja.ContabilizarCobroCommand.Execute(null);

            Assert.IsNull(caja.EntregaCliente);
        }

        [TestMethod]
        public void CalcularCambio_Reglas()
        {
            Assert.IsNull(CajasViewModel.CalcularCambio(null, 10M));
            Assert.IsNull(CajasViewModel.CalcularCambio(0M, 10M));
            Assert.AreEqual(0M, CajasViewModel.CalcularCambio(10M, 10M));
            Assert.AreEqual(0.01M, CajasViewModel.CalcularCambio(20M, 19.99M));
        }
    }
}
