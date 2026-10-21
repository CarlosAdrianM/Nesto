using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Models;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Nesto.Modulos.PedidoVenta;
using Nesto.Modulos.PlantillaVenta;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ControlesUsuario.Dialogs;

namespace PlantillaVentaTests
{
    /// <summary>
    /// NestoAPI#593 (c5): al elegir el cliente en la plantilla se pregunta por su cheque regalo. Con uno disponible sale
    /// el aviso con la casilla «Usar el cheque regalo de 50 €»: marcarla añade la línea del cheque al pedido que se manda
    /// y baja el total; desmarcarla la quita. En los demás estados, solo el texto.
    /// </summary>
    [TestClass]
    public class ChequeRegaloPlantillaTests
    {
        private static ChequeRegaloClienteDTO Cheque(string estado = ChequeRegaloClienteDTO.ESTADO_DISPONIBLE, int? pedidoCanje = null) => new ChequeRegaloClienteDTO
        {
            Campana = "CHEQUE50_OCT_2026",
            Empresa = "1",
            Cliente = "15191",
            Producto = "CHEQUE50_OCT26",
            Importe = 50M,
            MinimoCanje = 250M,
            CanjeHasta = new DateTime(2026, 11, 7),
            PrefijosNombreExcluidosMinimo = new List<string> { "PACK 26" },
            GruposExcluidosMinimo = new List<string> { "PEL" },
            Estado = estado,
            SePuedeUsar = estado == ChequeRegaloClienteDTO.ESTADO_DISPONIBLE,
            PedidoCanje = pedidoCanje,
            Mensaje = estado == ChequeRegaloClienteDTO.ESTADO_DISPONIBLE
                ? "Tiene un cheque regalo de 50,00 € + IVA para un pedido de más de 250,00 € de producto, hasta el 07/11/2026."
                : $"El cheque regalo ya está aplicado en el pedido {pedidoCanje}.",
            TextoLinea = "Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)"
        };

        private static (PlantillaVentaViewModel vm, IServicioChequesRegalo cheques) CrearViewModel(ChequeRegaloClienteDTO cheque, IServicioDialogos dialogos = null)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), dialogos ?? A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };

            IServicioChequesRegalo cheques = A.Fake<IServicioChequesRegalo>();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(cheque));
            vm.ServicioChequesRegalo = cheques;
            return (vm, cheques);
        }

        private static LineaPlantillaVenta Linea(PlantillaVentaViewModel vm, string producto, int cantidad, decimal precio, string texto = "CREMA MASAJE 1L", string grupo = "COS")
        {
            var linea = new LineaPlantillaVenta { producto = producto, texto = texto, cantidad = cantidad, precio = precio, grupo = grupo, iva = "G21" };
            vm.ListaFiltrableProductos.ListaOriginal.Add(linea);
            return linea;
        }

        private static PedidoVentaDTO PedidoConUnaLinea() => new PedidoVentaDTO
        {
            empresa = "1",
            cliente = "15191",
            Lineas = new List<LineaPedidoVentaDTO>
            {
                new LineaPedidoVentaDTO { tipoLinea = 1, Producto = "12345", Cantidad = 10, PrecioUnitario = 30M, almacen = "ALG", formaVenta = "DIR", iva = "G21", fechaEntrega = new DateTime(2026, 10, 14) }
            }
        };

        [TestMethod]
        public async Task SinCheque_NoHayAviso()
        {
            var (vm, cheques) = CrearViewModel(null);
            Linea(vm, "12345", 10, 30M);

            await vm.CargarChequeRegaloAsync();

            A.CallTo(() => cheques.LeerDelCliente("1", "15191")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.ChequeRegalo.HayCheque);
            Assert.IsFalse(vm.PuedeUsarChequeRegalo);
            Assert.IsFalse(vm.MostrarInfoChequeRegalo);
            vm.UsarChequeRegalo = true;
            Assert.IsFalse(vm.UsarChequeRegalo, "Sin cheque no se puede marcar");
            Assert.AreEqual(300M, vm.baseImponiblePedido);
        }

        [TestMethod]
        public async Task ChequeDisponible_SaleLaCasilla_YMarcarlaBajaElTotalYAnadeLaLinea()
        {
            var (vm, _) = CrearViewModel(Cheque());
            Linea(vm, "12345", 10, 30M);

            await vm.CargarChequeRegaloAsync();

            Assert.IsTrue(vm.PuedeUsarChequeRegalo);
            Assert.IsFalse(vm.MostrarInfoChequeRegalo);
            Assert.AreEqual("Usar el cheque regalo de 50 €", vm.ChequeRegalo.TextoUsar);
            Assert.IsFalse(vm.UsarChequeRegalo, "Sale desmarcada: la decide el vendedor");
            Assert.AreEqual(300M, vm.baseImponiblePedido);

            vm.UsarChequeRegalo = true;

            Assert.AreEqual(250M, vm.baseImponiblePedido);
            Assert.AreEqual(250M * 1.21M, vm.totalPedido);
            LineaPlantillaVenta virtual_ = vm.listaProductosPedidoConPortes.Single(l => l.esLineaChequeRegalo);
            Assert.AreEqual(-1, virtual_.cantidad);
            Assert.AreEqual(50M, virtual_.precio);

            PedidoVentaDTO pedido = PedidoConUnaLinea();
            vm.AnadirLineaChequeRegalo(pedido);
            LineaPedidoVentaDTO lineaCheque = pedido.Lineas.Single(l => l.Producto == "CHEQUE50_OCT26");
            Assert.AreEqual((byte?)1, lineaCheque.tipoLinea);
            Assert.AreEqual(-1, lineaCheque.Cantidad);
            Assert.AreEqual(50M, lineaCheque.PrecioUnitario);
            Assert.IsFalse(lineaCheque.AplicarDescuento);
            Assert.AreEqual("ALG", lineaCheque.almacen);
            Assert.AreEqual("Cheque regalo 50 € (campaña CHEQUE50_OCT_2026)", lineaCheque.texto);
        }

        [TestMethod]
        public async Task Desmarcar_QuitaLaLineaYElTotalVuelve()
        {
            var (vm, _) = CrearViewModel(Cheque());
            Linea(vm, "12345", 10, 30M);
            await vm.CargarChequeRegaloAsync();
            vm.UsarChequeRegalo = true;

            vm.UsarChequeRegalo = false;

            Assert.AreEqual(300M, vm.baseImponiblePedido);
            Assert.IsFalse(vm.listaProductosPedidoConPortes.Any(l => l.esLineaChequeRegalo));
            PedidoVentaDTO pedido = PedidoConUnaLinea();
            vm.AnadirLineaChequeRegalo(pedido);
            Assert.AreEqual(1, pedido.Lineas.Count, "Sin la casilla no va la línea del cheque");
        }

        [TestMethod]
        public async Task ChequeEnOtroPedido_SoloElTexto_SinCasilla()
        {
            var (vm, _) = CrearViewModel(Cheque(ChequeRegaloClienteDTO.ESTADO_EN_PEDIDO, 928123));
            Linea(vm, "12345", 10, 30M);

            await vm.CargarChequeRegaloAsync();

            Assert.IsFalse(vm.PuedeUsarChequeRegalo);
            Assert.IsTrue(vm.MostrarInfoChequeRegalo);
            Assert.AreEqual("El cheque regalo ya está aplicado en el pedido 928123.", vm.ChequeRegalo.Mensaje);
            vm.UsarChequeRegalo = true;
            Assert.IsFalse(vm.UsarChequeRegalo);
            Assert.AreEqual(300M, vm.baseImponiblePedido);
        }

        [TestMethod]
        public async Task ModificandoConLaPlantillaElPedidoQueLoLleva_SaleMarcada_ParaQueNoSeSuelte()
        {
            var (vm, _) = CrearViewModel(Cheque(ChequeRegaloClienteDTO.ESTADO_EN_PEDIDO, 928123));
            Linea(vm, "12345", 10, 30M);
            vm.NumeroPedidoEnEdicion = 928123;

            await vm.CargarChequeRegaloAsync();

            Assert.IsTrue(vm.PuedeUsarChequeRegalo);
            Assert.IsTrue(vm.UsarChequeRegalo);
            Assert.AreEqual(250M, vm.baseImponiblePedido);
        }

        [TestMethod]
        public async Task Falta_SinPack26NiPel_YDesapareceAlSuperarElMinimo()
        {
            var (vm, _) = CrearViewModel(Cheque());
            Linea(vm, "12345", 5, 30M);                                   // 150 computables
            Linea(vm, "22222", 1, 80M, texto: "PACK 26 NAVIDAD");          // no cuenta
            Linea(vm, "33333", 2, 40M, texto: "TINTE 60ML", grupo: "PEL"); // no cuenta
            await vm.CargarChequeRegaloAsync();

            Assert.AreEqual(150M, vm.BaseComputableChequeRegalo);
            Assert.IsTrue(vm.HayTextoFaltaChequeRegalo);
            StringAssert.Contains(vm.TextoFaltaChequeRegalo, "Faltan 100,01 €");

            Linea(vm, "44444", 1, 101M);
            Assert.IsFalse(vm.HayTextoFaltaChequeRegalo, "251 € computables superan el mínimo");
        }

        [TestMethod]
        public async Task UnaVezPorCliente_YConOtroClienteSeQuitaLaCasilla()
        {
            var (vm, cheques) = CrearViewModel(Cheque());
            Linea(vm, "12345", 10, 30M);
            await vm.CargarChequeRegaloAsync();
            vm.UsarChequeRegalo = true;

            await vm.CargarChequeRegaloAsync();
            Assert.IsTrue(vm.UsarChequeRegalo, "El mismo cliente: no se vuelve a preguntar ni se pierde la casilla");
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).MustHaveHappenedOnceExactly();

            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "34867", contacto = "0", iva = "G21" };
            await vm.CargarChequeRegaloAsync();
            Assert.IsFalse(vm.UsarChequeRegalo, "Con otro cliente, la casilla empieza desmarcada");
            A.CallTo(() => cheques.LeerDelCliente("1", "34867")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task LineaDelChequeTecleadaAMano_NoSeBloquea_NiSeDuplica()
        {
            var (vm, _) = CrearViewModel(Cheque());
            Linea(vm, "12345", 10, 30M);
            await vm.CargarChequeRegaloAsync();
            vm.UsarChequeRegalo = true;
            PedidoVentaDTO pedido = PedidoConUnaLinea();
            pedido.Lineas.Add(new LineaPedidoVentaDTO { tipoLinea = 1, Producto = "CHEQUE50_OCT26 ", Cantidad = 1 });

            vm.AnadirLineaChequeRegalo(pedido);

            Assert.AreEqual(1, pedido.Lineas.Count(l => l.Producto.Trim() == "CHEQUE50_OCT26"), "La tecleada va tal cual (la normaliza el servidor) y no se añade otra");
        }

        // ------------------------------------------------------------------ NestoAPI#593 (Ganavisiones)
        // Pedido 929066 (Javi, 21/10/26): 699,35 € de COS + 19,50 € de ACC = 718,85 € con el cheque de 50 € marcado. La
        // plantilla le enseñó 71 Ganavisiones (no restaba el cheque), cogió 20 + 49 = 69 y al grabar la API, que sí cuenta
        // la línea del cheque (COS, −50), dio 66 y rechazó el pedido. Lo correcto es 66.

        private static void LineasDeJavi(PlantillaVentaViewModel vm)
        {
            Linea(vm, "45959", 1, 699.35M, "PACK EPIGENETICA SKINCARE", "COS");
            Linea(vm, "40361", 1, 19.50M, "QUITACUTICULAS CUTICLE REMOVER", "ACC");
        }

        [TestMethod]
        public async Task Ganavisiones_ConElChequeMarcado_LaBaseBonificableRestaElCheque_66Y_No71()
        {
            var (vm, _) = CrearViewModel(Cheque());
            LineasDeJavi(vm);
            await vm.CargarChequeRegaloAsync();
            Assert.AreEqual(718.85M, vm.BaseImponibleBonificable, "sin cheque");
            Assert.AreEqual(71, vm.GanavisionesDisponibles, "sin cheque");

            vm.UsarChequeRegalo = true;

            Assert.AreEqual(668.85M, vm.BaseImponibleBonificable);
            Assert.AreEqual(66, vm.GanavisionesDisponibles);

            vm.UsarChequeRegalo = false;
            Assert.AreEqual(71, vm.GanavisionesDisponibles, "al desmarcarlo vuelve");
        }

        [TestMethod]
        public async Task Ganavisiones_ManaLaApi_SiDiceQueElChequeNoRestaDeLaBase_NoResta()
        {
            ChequeRegaloClienteDTO cheque = Cheque();
            cheque.RestaDeLaBaseGanavisiones = 0M; // p. ej. un cheque de un grupo que no genera Ganavisiones
            var (vm, _) = CrearViewModel(cheque);
            LineasDeJavi(vm);
            await vm.CargarChequeRegaloAsync();

            vm.UsarChequeRegalo = true;

            Assert.AreEqual(71, vm.GanavisionesDisponibles);
        }

        [TestMethod]
        public void Ganavisiones_RestaDeLaBase_SinElCampoDeLaApiEsElImporte()
        {
            ChequeRegaloClienteDTO cheque = Cheque();
            Assert.IsNull(cheque.RestaDeLaBaseGanavisiones);
            Assert.AreEqual(50M, ReglasChequeRegalo.RestaDeLaBaseGanavisiones(cheque), "API anterior: la línea del cheque es COS y resta");
            cheque.RestaDeLaBaseGanavisiones = 50M;
            Assert.AreEqual(50M, ReglasChequeRegalo.RestaDeLaBaseGanavisiones(cheque));
            Assert.AreEqual(0M, ReglasChequeRegalo.RestaDeLaBaseGanavisiones(null));
        }

        [TestMethod]
        public async Task Ganavisiones_MarcarElChequeConRegalosQueYaNoCaben_AvisaConLosNumeros()
        {
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var (vm, _) = CrearViewModel(Cheque(), dialogos);
            LineasDeJavi(vm);
            await vm.CargarChequeRegaloAsync();
            vm.ListaProductosBonificables = new ObservableCollection<LineaRegalo>
            {
                new LineaRegalo { producto = "44243", texto = "FUNDIDOR DE CERA", ganavisiones = 20, cantidad = 1 },
                new LineaRegalo { producto = "44357", texto = "LAMPARA LED", ganavisiones = 49, cantidad = 1 }
            };
            Assert.IsFalse(vm.GanavisionesExcedidos, "69 de 71 sin cheque");

            vm.UsarChequeRegalo = true;

            Assert.IsTrue(vm.GanavisionesExcedidos, "69 de 66 con cheque");
            Assert.IsFalse(vm.PuedePasarDePaginaRegalos);
            A.CallTo(() => dialogos.ShowError(A<string>.That.Matches(m => m.Contains("suman 69") && m.Contains("solo da 66") && m.Contains("50,00"))))
                .MustHaveHappenedOnceExactly();
        }
    }
}
