using ControlesUsuario.Models;
using FakeItEasy;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cajas.Interfaces;
using Nesto.Modulos.Cajas.Models;
using Nesto.Modulos.Cajas.ViewModels;

namespace CajasTests
{
    // Nesto#514: al cobrar a la vez facturas de dos empresas, la contrapartida al banco de cada empresa
    // llevaba el total cobrado de todas y los dos asientos descuadraban.
    [TestClass]
    public class CajasViewModelCobroTests
    {
        private const string CUENTA_BANCO = "57200001";

        private static (CajasViewModel sut, Func<List<PreContabilidadDTO>?> enviadas) Preparar()
        {
            var servicio = A.Fake<IContabilidadService>();
            List<PreContabilidadDTO>? enviadas = null;
            A.CallTo(() => servicio.Contabilizar(A<List<PreContabilidadDTO>>._))
                .Invokes((List<PreContabilidadDTO> l) => enviadas = l).Returns(Task.FromResult(0));
            var sut = new CajasViewModel(servicio, A.Fake<IConfiguracion>(), A.Fake<IServicioDialogos>(),
                A.Fake<IClientesService>(), A.Fake<IServicioAutenticacion>())
            {
                ClienteCompletoSeleccionado = new ClienteDTO { cliente = "11276", contacto = "0" },
                ClienteSeleccionado = "11276",
                FormaPagoSeleccionada = new FormaPago { formaPago = "EFC" },
                CuentaCobro = new CuentaContableDTO { Cuenta = CUENTA_BANCO }
            };
            return (sut, () => enviadas);
        }

        private static ExtractoClienteDTO Factura(int id, string empresa, string documento, decimal pendiente) => new()
        {
            Id = id, Empresa = empresa, Cliente = "11276", Contacto = "0", Documento = documento, Efecto = "1",
            Tipo = "Factura", Importe = pendiente, ImportePendiente = pendiente, Vencimiento = new DateTime(2026, 10, 7)
        };

        private static void AssertCuadraPorEmpresa(List<PreContabilidadDTO> lineas)
        {
            foreach (var grupo in lineas.GroupBy(l => l.Empresa))
            {
                Assert.AreEqual(grupo.Sum(l => l.Haber), grupo.Sum(l => l.Debe), $"El asiento de la empresa {grupo.Key} tiene que cuadrar");
            }
        }

        [TestMethod]
        public void ContabilizarCobro_FacturasDeDosEmpresas_CadaEmpresaCuadraConSuPropiaContrapartida()
        {
            // ELMAH 07/10/26 (Reina): CV2600613 empresa 1 191,00 + GB2601590 empresa 3 33,67
            var (sut, enviadas) = Preparar();
            sut.SeleccionarDeudasCommand.Execute(new List<object>
            {
                Factura(1, "1", "CV2600613", 191.00M), Factura(2, "3", "GB2601590", 33.67M)
            });
            sut.TotalCobrado = 224.67M;

            sut.ContabilizarCobroCommand.Execute(null);

            var lineas = enviadas();
            Assert.IsNotNull(lineas);
            AssertCuadraPorEmpresa(lineas!);
            var banco1 = lineas!.Single(l => l.Cuenta == CUENTA_BANCO && l.Empresa == "1");
            var banco3 = lineas.Single(l => l.Cuenta == CUENTA_BANCO && l.Empresa == "3");
            Assert.AreEqual(191.00M, banco1.Debe);
            Assert.AreEqual(33.67M, banco3.Debe);
            Assert.AreEqual("CV2600613", banco1.Documento, "El documento sale de una deuda de su empresa");
            Assert.AreEqual("GB2601590", banco3.Documento, "El documento sale de una deuda de su empresa");
            StringAssert.Contains(banco3.Concepto, "GB2601590");
            Assert.IsFalse(banco3.Concepto.Contains("y otros"), "En la empresa 3 solo hay una factura, pagada entera");
        }

        [TestMethod]
        public void ContabilizarCobro_DosFacturasDeUnaEmpresa_UnaContrapartidaPorElTotal()
        {
            var (sut, enviadas) = Preparar();
            sut.SeleccionarDeudasCommand.Execute(new List<object>
            {
                Factura(1, "1", "CV2600613", 191.00M), Factura(2, "1", "NV2600001", 33.67M)
            });
            sut.TotalCobrado = 224.67M;

            sut.ContabilizarCobroCommand.Execute(null);

            var lineas = enviadas()!;
            AssertCuadraPorEmpresa(lineas);
            var banco = lineas.Single(l => l.Cuenta == CUENTA_BANCO);
            Assert.AreEqual(224.67M, banco.Debe);
            Assert.AreEqual("CV2600613", banco.Documento);
            StringAssert.Contains(banco.Concepto, "y otros");
        }

        [TestMethod]
        public void ContabilizarCobro_FacturasDeDosEmpresasConImporteACuenta_LoACuentaVaALaEmpresaPorDefectoYCuadra()
        {
            var (sut, enviadas) = Preparar();
            sut.SeleccionarDeudasCommand.Execute(new List<object>
            {
                Factura(1, "1", "CV2600613", 191.00M), Factura(2, "3", "GB2601590", 33.67M)
            });
            sut.TotalCobrado = 234.67M; // 10 € a cuenta

            sut.ContabilizarCobroCommand.Execute(null);

            var lineas = enviadas()!;
            AssertCuadraPorEmpresa(lineas);
            Assert.AreEqual("1", lineas.Single(l => l.Documento == "A CUENTA").Empresa);
            Assert.AreEqual(201.00M, lineas.Single(l => l.Cuenta == CUENTA_BANCO && l.Empresa == "1").Debe);
            Assert.AreEqual(33.67M, lineas.Single(l => l.Cuenta == CUENTA_BANCO && l.Empresa == "3").Debe);
        }

        // Nesto#561 (Reina, 28/10/26, cliente 11276): el abono «A CUENTA» de 2,10 de la empresa 1 + la GB2601662 de 11,90 de
        // la empresa 3, cobrando 9,80 desde la caja de la empresa 1. Nesto manda todas las líneas en una llamada, cada una con
        // su empresa y su efecto, y la API contabiliza cada empresa por separado (antes solo la de la primera línea).
        [TestMethod]
        public void ContabilizarCobro_AbonoDeLa1YFacturaDeLa3_CadaLineaConSuEmpresaYSuEfectoEnUnaSolaLlamada()
        {
            var (sut, enviadas) = Preparar();
            var abono = Factura(3058952, "1", "A CUENTA", -2.10M);
            var factura = Factura(3058956, "3  ", "GB2601662", 11.90M);
            sut.SeleccionarDeudasCommand.Execute(new List<object> { factura, abono });
            sut.TotalCobrado = 9.80M;

            sut.ContabilizarCobroCommand.Execute(null);

            var lineas = enviadas()!;
            AssertCuadraPorEmpresa(lineas);
            var lineaAbono = lineas.Single(l => l.Liquidado == 3058952);
            var lineaFactura = lineas.Single(l => l.Liquidado == 3058956);
            Assert.AreEqual("1", lineaAbono.Empresa);
            Assert.AreEqual(2.10M, lineaAbono.Debe);
            Assert.AreEqual("3", lineaFactura.Empresa, "La GB2601662 es de la empresa 3: se liquida en la 3 aunque la caja abierta sea la de la 1");
            Assert.AreEqual(11.90M, lineaFactura.Haber);
            Assert.AreEqual(2.10M, lineas.Single(l => l.Cuenta == CUENTA_BANCO && l.Empresa == "1").Haber);
            Assert.AreEqual(11.90M, lineas.Single(l => l.Cuenta == CUENTA_BANCO && l.Empresa == "3").Debe, "Lo de la 3 va a la caja de la empresa 3");
        }

        [TestMethod]
        public void ContabilizarCobro_FacturaDeLa3ConACuentaEnLaCajaDeLa3_UnaSolaContrapartidaDeLa3()
        {
            // EMPRESA_ESPEJO es "3  ": lo «a cuenta» iba con relleno y salían dos contrapartidas de la 3
            var (sut, enviadas) = Preparar();
            // La caja de la 3 obliga a efectivo y busca la caja por defecto en las listas
            sut.ListaCuentasCaja = new System.Collections.ObjectModel.ObservableCollection<CuentaContableDTO> { new() { Cuenta = null } };
            sut.ListaFormaPago = new List<FormaPago> { new() { formaPago = "EFC" } };
            sut.CambiarEmpresaTraspasoCommand.Execute(null);
            Assert.AreEqual("3  ", sut.EmpresaTraspaso, "EMPRESA_ESPEJO lleva relleno");
            sut.CuentaCobro = new CuentaContableDTO { Cuenta = CUENTA_BANCO };
            sut.SeleccionarDeudasCommand.Execute(new List<object> { Factura(3058956, "3", "GB2601662", 11.90M) });
            sut.TotalCobrado = 20.00M;

            sut.ContabilizarCobroCommand.Execute(null);

            var lineas = enviadas()!;
            Assert.IsTrue(lineas.All(l => l.Empresa == "3"), string.Join(",", lineas.Select(l => $"[{l.Empresa}]")));
            AssertCuadraPorEmpresa(lineas);
            Assert.AreEqual(20.00M, lineas.Single(l => l.Cuenta == CUENTA_BANCO).Debe);
        }
    }
}
