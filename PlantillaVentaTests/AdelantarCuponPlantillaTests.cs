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
using System.Threading.Tasks;
using ControlesUsuario.Dialogs;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#648 (NestoAPI#885): en la plantilla, el cupón que todavía no vale y que la API deja adelantar sale con «Usar ya
    /// (excepción)». Adelantado, aparece la casilla «Usar el cupón» (sin marcar). Sin SePuedeAdelantar, como antes.
    /// </summary>
    [TestClass]
    public class AdelantarCuponPlantillaTests
    {
        private static CuponDTO Cupon(string estado = "Emitido", bool sePuedeAdelantar = true) => new CuponDTO
        {
            Id = 7,
            Codigo = "EV-2611-7K3Q",
            Empresa = "1",
            Cliente = "15191",
            Importe = 100M,
            PedidoMinimo = 200M,
            ValidoDesde = new DateTime(2026, 12, 1),
            Caducidad = new DateTime(2027, 6, 1),
            Estado = estado,
            SePuedeUsar = estado == "Activo",
            SePuedeAdelantar = estado == "Emitido" && sePuedeAdelantar,
            Producto = "CUPONEVENTO"
        };

        private static (PlantillaVentaViewModel vm, IServicioCupones cupones, IServicioDialogos dialogos) CrearViewModel(CuponDTO cupon)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.servidorAPI).Returns("http://127.0.0.1:9/api/");
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), dialogos, A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            IServicioCupones servicio = A.Fake<IServicioCupones>();
            A.CallTo(() => servicio.LeerDelCliente(A<string>._, A<string>._, A<bool>._)).Returns(Task.FromResult(new List<CuponDTO> { cupon }));
            vm.ServicioCupones = servicio;
            return (vm, servicio, dialogos);
        }

        [TestMethod]
        public async Task SinSePuedeAdelantar_NiBotonNiCasilla()
        {
            var (vm, _, _) = CrearViewModel(Cupon(sePuedeAdelantar: false));
            await vm.CargarCuponAsync();

            Assert.IsTrue(vm.Cupon.HayCupon);
            Assert.IsFalse(vm.PuedeAdelantarCupon);
            Assert.IsFalse(vm.AdelantarCuponCommand.CanExecute(null));
            Assert.IsFalse(vm.PuedeUsarCupon);
        }

        [TestMethod]
        public async Task MotivoCancelado_NoLlama()
        {
            var (vm, cupones, dialogos) = CrearViewModel(Cupon());
            await vm.CargarCuponAsync();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns((string)null);

            await vm.AdelantarCuponAsync();

            A.CallTo(() => cupones.AdelantarCupon(A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.IsTrue(vm.PuedeAdelantarCupon);
        }

        [TestMethod]
        public async Task Adelantado_SaleLaCasillaSinMarcar_YDesapareceElBoton()
        {
            var (vm, cupones, dialogos) = CrearViewModel(Cupon());
            await vm.CargarCuponAsync();
            Assert.IsTrue(vm.PuedeAdelantarCupon);
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("Viene hoy");
            A.CallTo(() => cupones.AdelantarCupon("EV-2611-7K3Q", "Viene hoy")).Returns(Task.FromResult(Cupon("Activo")));

            await vm.AdelantarCuponAsync();

            Assert.IsFalse(vm.PuedeAdelantarCupon);
            Assert.IsTrue(vm.PuedeUsarCupon);
            Assert.IsFalse(vm.UsarCupon, "No se marca sola");
        }

        [TestMethod]
        public async Task ErrorDeLaApi_EnsenaSuMensaje()
        {
            var (vm, cupones, dialogos) = CrearViewModel(Cupon());
            await vm.CargarCuponAsync();
            A.CallTo(() => dialogos.GetText(A<string>._, A<string>._)).Returns("Viene hoy");
            A.CallTo(() => cupones.AdelantarCupon(A<string>._, A<string>._)).ThrowsAsync(new InvalidOperationException("El cupón ya está usado."));

            await vm.AdelantarCuponAsync();

            A.CallTo(() => dialogos.ShowError("El cupón ya está usado.")).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.PuedeAdelantarCupon);
            Assert.IsFalse(vm.PuedeUsarCupon);
        }
    }
}
