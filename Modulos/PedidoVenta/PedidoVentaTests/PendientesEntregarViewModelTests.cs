using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PedidoVentaTests
{
    /// <summary>
    /// Nesto#552 (sugerencia 717, Manuel): pantalla «Pendientes de entregar», de solo lectura, servida por
    /// GET api/PedidosVenta/PendientesEntregar.
    /// </summary>
    [TestClass]
    public class PendientesEntregarViewModelTests
    {
        private IPedidoVentaService servicio;
        private IAbridorPedidos abridor;
        private IServicioDialogos dialogos;
        private PendientesEntregarViewModel vm;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<IPedidoVentaService>();
            abridor = A.Fake<IAbridorPedidos>();
            dialogos = A.Fake<IServicioDialogos>();
            vm = new PendientesEntregarViewModel(servicio, abridor, dialogos);
        }

        private static LineaPendienteEntregar Linea(int pedido, string producto, decimal bruto, string nombre = "PELUQUERÍA")
            => new LineaPendienteEntregar { Pedido = pedido, Producto = producto, Bruto = bruto, NombreCliente = nombre, Cliente = "1000", Almacen = "ALG" };

        private void Devuelve(params LineaPendienteEntregar[] lineas)
        {
            A.CallTo(() => servicio.LeerPendientesEntregar(A<string>._, A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new List<LineaPendienteEntregar>(lineas)));
        }

        [TestMethod]
        public void AlCrearse_TituloYTodosLosAlmacenes()
        {
            Assert.AreEqual("Pendientes de entregar", vm.Titulo);
            Assert.AreEqual(PendientesEntregarViewModel.TODOS_LOS_ALMACENES, vm.Almacen);
            CollectionAssert.Contains(vm.Almacenes, "REI");
        }

        [TestMethod]
        public async Task Cargar_SinFiltros_PideTodoALaApi()
        {
            Devuelve();

            await vm.CargarAsync();

            A.CallTo(() => servicio.LeerPendientesEntregar("1", null, null, null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Cargar_ConFiltros_LosMandaRecortados()
        {
            Devuelve();
            vm.Almacen = "REI";
            vm.Producto = " 45958 ";
            vm.Vendedor = "ASH ";

            await vm.CargarAsync();

            A.CallTo(() => servicio.LeerPendientesEntregar("1", "REI", "45958", "ASH")).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Cargar_RellenaLaRejillaYLosTotales()
        {
            Devuelve(Linea(1, "45958", 10M), Linea(2, "17404", 5.5M));

            await vm.CargarAsync();

            Assert.AreEqual(2, vm.ListaLineas.Lista.Count);
            Assert.AreEqual(2, vm.NumeroLineas);
            Assert.AreEqual(15.5M, vm.TotalBruto);
            Assert.IsNull(vm.ListaLineas.ElementoSeleccionado, "No debe seleccionar la primera línea por su cuenta");
        }

        [TestMethod]
        public async Task FiltroDeTexto_ReduceLasLineasYLosTotales()
        {
            Devuelve(Linea(1, "45958", 10M, "PELUQUERÍA MANUELA"), Linea(2, "17404", 5.5M, "ESTÉTICA LUISA"));
            await vm.CargarAsync();

            vm.ListaLineas.Filtro = "luisa";

            Assert.AreEqual(1, vm.ListaLineas.Lista.Count);
            Assert.AreEqual(1, vm.NumeroLineas);
            Assert.AreEqual(5.5M, vm.TotalBruto);
        }

        [TestMethod]
        public async Task VolverACargar_MantieneElFiltroDeTexto()
        {
            Devuelve(Linea(1, "45958", 10M, "PELUQUERÍA MANUELA"), Linea(2, "17404", 5.5M, "ESTÉTICA LUISA"));
            await vm.CargarAsync();
            vm.ListaLineas.Filtro = "luisa";

            await vm.CargarAsync();

            Assert.AreEqual(1, vm.ListaLineas.Lista.Count);
        }

        [TestMethod]
        public async Task Cargar_SiFallaLaApi_AvisaDeUstedYNoSeQuedaOcupado()
        {
            A.CallTo(() => servicio.LeerPendientesEntregar(A<string>._, A<string>._, A<string>._, A<string>._))
                .ThrowsAsync(new Exception("sin conexión"));

            await vm.CargarAsync();

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("sin conexión"))).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public void AbrirPedido_AbreElPedidoDeLaLinea()
        {
            vm.AbrirPedidoCommand.Execute(Linea(928000, "45958", 1M));

            A.CallTo(() => abridor.Abrir("1", 928000)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void AbrirPedido_SinLinea_NoHaceNada()
        {
            vm.AbrirPedidoCommand.Execute(null);

            A.CallTo(() => abridor.Abrir(A<string>._, A<int>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ExportarExcel_DescargaConLosFiltrosYAbreElFichero()
        {
            A.CallTo(() => servicio.DescargarPendientesEntregarExcel(A<string>._, A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new byte[] { 0x50, 0x4B }));
            string abierto = null;
            vm.AbrirFichero = ruta => abierto = ruta;
            vm.Almacen = "ALC";

            await vm.ExportarExcelAsync();

            A.CallTo(() => servicio.DescargarPendientesEntregarExcel("1", "ALC", null, null)).MustHaveHappenedOnceExactly();
            Assert.IsNotNull(abierto);
            StringAssert.EndsWith(abierto, ".xlsx");
            Assert.IsTrue(System.IO.File.Exists(abierto));
            System.IO.File.Delete(abierto);
        }

        [TestMethod]
        public void Url_SoloLlevaLosFiltrosQueHay()
        {
            Assert.AreEqual("PedidosVenta/PendientesEntregar?empresa=1", PedidoVentaService.UrlPendientesEntregar(null, null, " ", null));
            Assert.AreEqual("PedidosVenta/PendientesEntregar/Excel?empresa=1&almacen=REI&producto=45958&vendedor=ASH",
                PedidoVentaService.UrlPendientesEntregar("1", "REI", "45958", "ASH", excel: true));
        }
    }
}
