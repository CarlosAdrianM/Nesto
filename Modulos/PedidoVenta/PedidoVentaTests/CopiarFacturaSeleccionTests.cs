using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PedidoVenta.Models.Rectificativas;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#638 (NestoAPI#899/#863): «Copiar factura» con «Solo unas líneas o albaranes…».
    /// </summary>
    [TestClass]
    public class CopiarFacturaSeleccionTests
    {
        private IPedidoVentaService _servicio;
        private List<CopiarFacturaRequestDTO> _enviadas;

        private static LineaFacturaParaCopiarDTO Linea(int orden, int? albaran, decimal cantidad, decimal yaAbonada = 0, bool cuenta = false,
            string producto = "17404", decimal baseImponible = 100, int pedido = 930001)
        {
            return new LineaFacturaParaCopiarDTO
            {
                Pedido = pedido,
                NumeroOrden = orden,
                Albaran = albaran,
                FechaAlbaran = new DateTime(2026, 11, 12),
                EsCuentaContable = cuenta,
                EsGastosEnvio = cuenta,
                Producto = producto,
                Texto = "PRODUCTO " + orden,
                Cantidad = cantidad,
                YaAbonada = yaAbonada,
                QuedanPorAbonar = Math.Max(0, Math.Abs(cantidad) - yaAbonada),
                BaseImponible = baseImponible
            };
        }

        private static LineasFacturaParaCopiarDTO Factura(params LineaFacturaParaCopiarDTO[] lineas)
        {
            return new LineasFacturaParaCopiarDTO
            {
                Empresa = "1",
                Factura = "NV26/000001",
                Albaranes = lineas.Where(l => l.Albaran.HasValue).Select(l => l.Albaran.Value).Distinct().ToList(),
                Lineas = lineas.ToList()
            };
        }

        private async Task<CopiarFacturaViewModel> Ventana(LineasFacturaParaCopiarDTO factura)
        {
            _servicio = A.Fake<IPedidoVentaService>();
            _enviadas = new List<CopiarFacturaRequestDTO>();
            A.CallTo(() => _servicio.LeerLineasCopiarFactura(A<string>._, A<string>._)).Returns(Task.FromResult(factura));
            A.CallTo(() => _servicio.CopiarFactura(A<CopiarFacturaRequestDTO>._))
                .ReturnsLazily((CopiarFacturaRequestDTO r) =>
                {
                    _enviadas.Add(r);
                    return Task.FromResult(new CopiarFacturaResponseDTO { Exitoso = true, NumeroPedido = 931000, Mensaje = "Abono hecho", UnidadesCopiadas = 3, ImporteCopiado = 60 });
                });
            var vm = new CopiarFacturaViewModel(_servicio, A.Fake<IConfiguracion>());
            vm.OnDialogOpened(new ParametrosDialogo { { "empresa", "1" }, { "cliente", "15191" }, { "numeroFactura", "NV26/000001" } });
            vm.SoloUnaParte = true;
            await vm.CargarLineasAsync();
            return vm;
        }

        [TestMethod]
        public async Task SinMarcarLaCasilla_LaPeticionEsLaDeSiempre()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5)));
            vm.SoloUnaParte = false;

            vm.EjecutarCommand.Execute(null);

            Assert.IsNull(_enviadas[0].Albaranes);
            Assert.IsNull(_enviadas[0].Lineas);
            Assert.IsNull(_enviadas[0].LineasNoVendibles);
            A.CallTo(() => _servicio.LeerLineasCopiarFactura("1", "NV26/000001")).MustHaveHappened();
        }

        [TestMethod]
        public async Task AlMarcarUnaLinea_PoneLoQueQuedaPorAbonar_YNoDejaPasarDeAhi()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5, yaAbonada: 2)));
            var linea = vm.LineasParaElegir.Single();

            linea.Marcada = true;
            Assert.AreEqual(3, linea.Cantidad, "Por defecto, lo que queda por abonar");

            linea.Cantidad = 9;
            Assert.AreEqual(3, linea.Cantidad, "No deja pasar de lo que queda");
            Assert.AreEqual(2m, linea.YaAbonadas);
            Assert.AreEqual(3m, linea.Quedan);
        }

        [TestMethod]
        public async Task UnaLineaYaAbonadaEntera_NoSePuedeMarcar()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5, yaAbonada: 5)));
            var linea = vm.LineasParaElegir.Single();

            linea.Marcada = true;

            Assert.IsFalse(linea.Marcada);
            Assert.IsFalse(vm.EjecutarCommand.CanExecute(null), "Sin nada marcado no se puede ejecutar");
        }

        [TestMethod]
        public async Task EnUnaCopiaSinInvertir_ElTopeEsLoFacturado()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5, yaAbonada: 2)));

            vm.InvertirCantidades = false;
            var linea = vm.LineasParaElegir.Single();
            linea.Marcada = true;

            Assert.AreEqual(5, linea.Cantidad);
        }

        [TestMethod]
        public async Task AlbaranEnteroVaComoAlbaran_LosPortesSoloSiSeMarcan()
        {
            var vm = await Ventana(Factura(
                Linea(1, 101, 2), Linea(2, 101, 1), Linea(3, 101, 1, cuenta: true, producto: null, baseImponible: 5),
                Linea(4, 102, 3)));

            vm.AlbaranesParaElegir.Single(a => a.Albaran == 101).Marcado = true;

            var portes = vm.LineasParaElegir.Single(l => l.NumeroOrden == 3);
            Assert.IsFalse(portes.Marcada, "Marcar el albarán no marca los portes");
            vm.EjecutarCommand.Execute(null);

            var request = _enviadas.Single();
            CollectionAssert.AreEqual(new[] { 101 }, request.Albaranes);
            Assert.IsNull(request.Lineas, "Las del albarán van con él");
            Assert.AreEqual("NV26/000001", request.NumeroFactura);
            Assert.IsNull(request.NumerosFactura);
        }

        [TestMethod]
        public async Task LineaSuelta_ConSuCantidad_YNoVendible_YPortesMarcados()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5), Linea(2, 101, 1), Linea(3, 101, 1, cuenta: true, producto: null, baseImponible: 5)));

            var primera = vm.LineasParaElegir.Single(l => l.NumeroOrden == 1);
            primera.Marcada = true;
            primera.Cantidad = 2;
            primera.NoVendible = true;
            vm.LineasParaElegir.Single(l => l.NumeroOrden == 3).Marcada = true;

            Assert.AreEqual("Marcadas 2 líneas, 3 unidades, 45,00 € (base imponible). 1 vuelve estropeada (no vendible).", vm.ResumenSeleccion);
            vm.EjecutarCommand.Execute(null);

            var request = _enviadas.Single();
            Assert.IsNull(request.Albaranes, "El albarán no va entero: solo sus líneas");
            Assert.AreEqual(2, request.Lineas.Count);
            Assert.AreEqual((short?)2, request.Lineas.Single(l => l.NumeroOrden == 1).Cantidad);
            Assert.IsNull(request.Lineas[0].Pedido, "El pedido solo si el Nº Orden se repite");
            CollectionAssert.AreEqual(new[] { 1 }, request.LineasNoVendibles);
            StringAssert.Contains(vm.Mensaje, "Copiadas 3 unidades (60,00 € de base imponible).");
        }

        [TestMethod]
        public void ConVariosPedidos_ElNumeroDeOrdenRepetidoLlevaSuPedido()
        {
            var a = new LineaCopiaElegible(Linea(1, 101, 1, pedido: 930001), 1) { Marcada = true };
            var b = new LineaCopiaElegible(Linea(1, 102, 1, pedido: 930002), 1) { Marcada = true };
            var c = new LineaCopiaElegible(Linea(2, 101, 1, pedido: 930001), 1);

            var seleccion = ReglasSeleccionCopiaFactura.Construir(new[] { a, b, c });

            Assert.AreEqual(1, seleccion.Lineas.Count, "La del 102 va con su albarán");
            Assert.AreEqual(930001, seleccion.Lineas[0].Pedido);
            CollectionAssert.AreEqual(new[] { 102 }, seleccion.Albaranes, "El 102 está entero; el 101 no");
        }

        [TestMethod]
        public async Task ConVariasFacturasElegidas_NoSeCarganLineas()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5)));
            vm.FacturasSeleccionadas = new System.Collections.ObjectModel.ObservableCollection<ControlesUsuario.Models.FacturaClienteDTO>
            {
                new ControlesUsuario.Models.FacturaClienteDTO { Documento = "NV26/000001" },
                new ControlesUsuario.Models.FacturaClienteDTO { Documento = "NV26/000002" }
            };

            await vm.CargarLineasAsync();

            Assert.AreEqual(0, vm.LineasParaElegir.Count);
            Assert.AreEqual("Para elegir líneas o albaranes, deje elegida una sola factura.", vm.ResumenSeleccion);
            Assert.IsFalse(vm.EjecutarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task SiLaApiNoDaLasLineas_SeDicePorQue()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5)));
            A.CallTo(() => _servicio.LeerLineasCopiarFactura(A<string>._, A<string>._)).Throws(new Exception("No existe la factura NV26/000009."));
            vm.NumeroFactura = "NV26/000009";

            await vm.CargarLineasAsync();

            Assert.AreEqual("No se han podido cargar las líneas de NV26/000009: No existe la factura NV26/000009.", vm.ResumenSeleccion);
        }

        [TestMethod]
        public async Task AlCambiarDeFactura_SeVacianLasLineasDeLaAnterior()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5)));
            vm.LineasParaElegir.Single().Marcada = true;

            vm.NumeroFactura = "NV26/000002";

            Assert.AreEqual(0, vm.LineasParaElegir.Count);
            Assert.IsFalse(vm.EjecutarCommand.CanExecute(null), "Lo marcado era de otra factura");
        }

        [TestMethod]
        public async Task ElErrorDeLaApiAlCopiar_SeEnsena()
        {
            var vm = await Ventana(Factura(Linea(1, 101, 5)));
            A.CallTo(() => _servicio.CopiarFactura(A<CopiarFacturaRequestDTO>._))
                .Throws(new Exception("Error al copiar factura: La línea 1 (17404) tiene 5 unidades en NV26/000001: no se pueden copiar 6 unidades."));
            vm.LineasParaElegir.Single().Marcada = true;

            vm.EjecutarCommand.Execute(null);

            StringAssert.Contains(vm.Mensaje, "no se pueden copiar 6 unidades");
        }
    }
}
