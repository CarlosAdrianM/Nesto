using CommunityToolkit.Mvvm.Messaging;
using ControlesUsuario.Dialogs;
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
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#612 (clienta 9238, pedido 929678): el cheque regalo se leía una vez al elegir el cliente y, si se generaba
    /// después, la casilla no salía. Al llegar a Finalizar (y antes de grabar) se vuelve a pedir sin perder lo que haya
    /// marcado el usuario; con el cheque «Disponible» y la base computable por encima del mínimo, la casilla sale MARCADA
    /// (decisión de Carlos, 04/11/26) y, si el vendedor la desmarca, no se vuelve a marcar sola.
    /// </summary>
    [TestClass]
    public class ChequeRegaloAlFinalizarTests
    {
        private static ChequeRegaloClienteDTO Cheque(string estado = ChequeRegaloClienteDTO.ESTADO_DISPONIBLE, int? pedidoCanje = null) => new ChequeRegaloClienteDTO
        {
            Campana = "CHEQUE50_OCT_2026",
            Empresa = "1",
            Cliente = "9238",
            Producto = "CHEQUE50_OCT26",
            Importe = 50M,
            MinimoCanje = 250M,
            CanjeHasta = new DateTime(2026, 11, 7),
            PrefijosNombreExcluidosMinimo = new List<string> { "PACK 26" },
            GruposExcluidosMinimo = new List<string> { "PEL" },
            Estado = estado,
            SePuedeUsar = estado == ChequeRegaloClienteDTO.ESTADO_DISPONIBLE,
            PedidoCanje = pedidoCanje
        };

        private static (PlantillaVentaViewModel vm, IServicioChequesRegalo cheques) CrearViewModel()
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "9238", contacto = "0", iva = "G21", cifNif = "12345678A" };
            IServicioChequesRegalo cheques = A.Fake<IServicioChequesRegalo>();
            vm.ServicioChequesRegalo = cheques;
            return (vm, cheques);
        }

        private static void Linea(PlantillaVentaViewModel vm, string producto, int cantidad, decimal precio, string texto = "CREMA MASAJE 1L", string grupo = "COS")
            => vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = producto, texto = texto, cantidad = cantidad, precio = precio, grupo = grupo, iva = "G21" });

        [TestMethod]
        public async Task ChequeGeneradoDespuesDeElegirElCliente_SeOfreceAlFinalizar_YConBaseSuperiorAlMinimoSaleMarcado()
        {
            var (vm, cheques) = CrearViewModel();
            A.CallTo(() => cheques.LeerDelCliente("1", "9238")).ReturnsNextFromSequence(
                Task.FromResult<ChequeRegaloClienteDTO>(null), Task.FromResult(Cheque()));
            Linea(vm, "12345", 10, 30M); // 300 computables
            await vm.CargarChequeRegaloAsync();
            Assert.IsFalse(vm.PuedeUsarChequeRegalo, "al elegir el cliente todavía no lo tenía");

            bool cambia = await vm.RefrescarChequeRegaloAsync();

            Assert.IsTrue(cambia);
            Assert.IsTrue(vm.PuedeUsarChequeRegalo);
            Assert.IsTrue(vm.UsarChequeRegalo, "Disponible y 300 > 250: sale marcada");
            Assert.AreEqual(250M, vm.baseImponiblePedido);
            A.CallTo(() => cheques.LeerDelCliente("1", "9238")).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task SinLlegarAlMinimo_SeOfrecePeroSaleDesmarcado()
        {
            var (vm, cheques) = CrearViewModel();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(Cheque()));
            Linea(vm, "12345", 5, 30M);                                   // 150 computables
            Linea(vm, "22222", 1, 150M, texto: "PACK 26 NAVIDAD");         // no cuenta
            Linea(vm, "33333", 1, 50M, texto: "TINTE 60ML", grupo: "PEL"); // no cuenta

            await vm.RefrescarChequeRegaloAsync();

            Assert.IsTrue(vm.PuedeUsarChequeRegalo);
            Assert.AreEqual(150M, vm.BaseComputableChequeRegalo);
            Assert.IsFalse(vm.UsarChequeRegalo, "350 € de base, pero solo 150 computables");
        }

        [TestMethod]
        public async Task JustoElMinimo_NoSeMarca_HayQueSuperarlo()
        {
            var (vm, cheques) = CrearViewModel();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(Cheque()));
            Linea(vm, "12345", 10, 25M); // 250

            await vm.RefrescarChequeRegaloAsync();

            Assert.IsFalse(vm.UsarChequeRegalo);
        }

        [TestMethod]
        public async Task DesmarcadoPorElUsuario_NoSeVuelveAMarcarSolo()
        {
            var (vm, cheques) = CrearViewModel();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(Cheque()));
            Linea(vm, "12345", 10, 30M);
            await vm.RefrescarChequeRegaloAsync();
            Assert.IsTrue(vm.UsarChequeRegalo);

            vm.UsarChequeRegalo = false; // el vendedor no lo quiere

            bool cambia = await vm.RefrescarChequeRegaloAsync(); // vuelve a Finalizar o pulsa grabar
            Assert.IsFalse(cambia);
            Assert.IsFalse(vm.UsarChequeRegalo);
            Assert.AreEqual(300M, vm.baseImponiblePedido);
        }

        [TestMethod]
        public async Task MarcadoPorElUsuarioSinLlegarAlMinimo_SeRespeta()
        {
            var (vm, cheques) = CrearViewModel();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(Cheque()));
            Linea(vm, "12345", 5, 30M);
            await vm.CargarChequeRegaloAsync();
            vm.UsarChequeRegalo = true;

            await vm.RefrescarChequeRegaloAsync();

            Assert.IsTrue(vm.UsarChequeRegalo, "lo que marca el usuario no se pierde al volver a leer el cheque");
        }

        [TestMethod]
        public async Task ElChequeYaNoSePuedeUsarAlFinalizar_SeQuitaLaCasilla()
        {
            var (vm, cheques) = CrearViewModel();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).ReturnsNextFromSequence(
                Task.FromResult(Cheque()), Task.FromResult(Cheque(ChequeRegaloClienteDTO.ESTADO_EN_PEDIDO, 929700)));
            Linea(vm, "12345", 10, 30M);
            await vm.CargarChequeRegaloAsync();
            vm.UsarChequeRegalo = true;

            bool cambia = await vm.RefrescarChequeRegaloAsync();

            Assert.IsTrue(cambia);
            Assert.IsFalse(vm.PuedeUsarChequeRegalo);
            Assert.IsFalse(vm.UsarChequeRegalo);
            Assert.IsTrue(vm.MostrarInfoChequeRegalo);
        }

        [TestMethod]
        public async Task SiLaApiFallaAlFinalizar_SeQuedaLoQueHabia()
        {
            var (vm, cheques) = CrearViewModel();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(Cheque())).Once();
            Linea(vm, "12345", 10, 30M);
            await vm.CargarChequeRegaloAsync();
            vm.UsarChequeRegalo = true;
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).ThrowsAsync(new Exception("sin conexión"));

            bool cambia = await vm.RefrescarChequeRegaloAsync();

            Assert.IsFalse(cambia);
            Assert.IsTrue(vm.PuedeUsarChequeRegalo);
            Assert.IsTrue(vm.UsarChequeRegalo);
        }

        [TestMethod]
        public async Task ConOtroCliente_LaDecisionAnteriorNoCuenta()
        {
            var (vm, cheques) = CrearViewModel();
            A.CallTo(() => cheques.LeerDelCliente(A<string>._, A<string>._)).Returns(Task.FromResult(Cheque()));
            Linea(vm, "12345", 10, 30M);
            await vm.CargarChequeRegaloAsync();
            vm.UsarChequeRegalo = true;
            vm.UsarChequeRegalo = false;

            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            await vm.CargarChequeRegaloAsync();
            await vm.RefrescarChequeRegaloAsync();

            Assert.IsTrue(vm.UsarChequeRegalo, "el otro cliente no ha decidido nada: sale marcada");
        }
    }
}
