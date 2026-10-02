using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.PedidoVenta;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity;

namespace PedidoVentaTests
{
    /// <summary>
    /// NestoAPI#582: lo que se deja en carpeta va a una nota de entrega que nace sin fecha (no sale en el picking).
    /// Al hacer el albarán desde el detalle, Nesto pregunta cuándo se entrega, con la opción «Todavía no se sabe».
    /// </summary>
    [TestClass]
    public class FechaEntregaNotaDetalleTests
    {
        private IPedidoVentaService servicio;
        private IServicioDialogos dialogos;
        private DetallePedidoViewModel vm;

        [TestInitialize]
        public void Setup()
        {
            servicio = A.Fake<IPedidoVentaService>();
            dialogos = A.Fake<IServicioDialogos>();
            vm = new DetallePedidoViewModel(A.Fake<IServicioNavegacion>(), A.Fake<IConfiguracion>(), servicio, new WeakReferenceMessenger(),
                dialogos, A.Fake<IUnityContainer>(), A.Fake<IServicioAutenticacion>());
        }

        private void NotasSinFecha(params int[] numeros)
        {
            var notas = new List<NotaEntregaSinFecha>();
            foreach (int n in numeros)
            {
                notas.Add(new NotaEntregaSinFecha { Numero = n, Albaran = 730321 });
            }
            A.CallTo(() => servicio.LeerNotasEntregaSinFecha("1", 927116)).Returns(Task.FromResult(notas));
        }

        [TestMethod]
        public async Task ConFechaElegida_SeLaPoneALaNota()
        {
            NotasSinFecha(927519);
            A.CallTo(() => dialogos.GetDate(A<string>._, A<string>._, A<DateTime?>._)).Returns(new DateTime(2026, 10, 6));

            await vm.PreguntarFechaNotasEntregaAsync("1", 927116);

            A.CallTo(() => dialogos.GetDate(A<string>._, A<string>.That.Contains("927519"), A<DateTime?>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.PonerFechaEntregaNota("1", 927519, new DateTime(2026, 10, 6))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task TodaviaNoSeSabe_NoTocaLaNotaYAvisaDeQueNoSaldraEnElPicking()
        {
            NotasSinFecha(927519);
            A.CallTo(() => dialogos.GetDate(A<string>._, A<string>._, A<DateTime?>._)).Returns((DateTime?)null);

            await vm.PreguntarFechaNotasEntregaAsync("1", 927116);

            A.CallTo(() => servicio.PonerFechaEntregaNota(A<string>._, A<int>._, A<DateTime>._)).MustNotHaveHappened();
            A.CallTo(() => dialogos.ShowNotification(A<string>._, A<string>.That.Contains("picking"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task SinNotasSinFecha_NoPreguntaNada()
        {
            NotasSinFecha();

            await vm.PreguntarFechaNotasEntregaAsync("1", 927116);

            A.CallTo(() => dialogos.GetDate(A<string>._, A<string>._, A<DateTime?>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task SiFallaLaApi_LoDiceYNoRevienta()
        {
            A.CallTo(() => servicio.LeerNotasEntregaSinFecha("1", 927116)).Throws(new Exception("Sin conexión"));

            await vm.PreguntarFechaNotasEntregaAsync("1", 927116);

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("Sin conexión"))).MustHaveHappenedOnceExactly();
        }
    }
}
