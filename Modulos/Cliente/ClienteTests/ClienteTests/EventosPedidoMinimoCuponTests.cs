using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// NestoAPI#613 (Carlos, 29/10/26): el pedido mínimo para usar el cupón de la señal lo define cada evento (0 = cualquier
    /// importe, por defecto). Se edita en el mantenimiento de eventos y se manda en EventoDTO.PedidoMinimoCupon; con la API de
    /// antes (sin el campo), 0.
    /// </summary>
    [TestClass]
    public class EventosPedidoMinimoCuponTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<EventoModel> guardados = new();

        public EventosPedidoMinimoCuponTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => eventos.GuardarEvento(A<EventoModel>.Ignored)).ReturnsLazily((EventoModel e) =>
            {
                guardados.Add(e);
                return e;
            });
        }

        private async Task<MantenimientoEventosViewModel> Mantenimiento(EventoModel evento)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(A<int>.Ignored)).Returns(new List<InscripcionEventoModel>());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            return vm;
        }

        private static EventoModel Evento(decimal? pedidoMinimo) => new()
        {
            Id = 5, Empresa = "1", Titulo = "Curso Peeling", Fecha = DateTime.Today.AddDays(20), Almacen = "REI",
            Activo = true, ImporteSenal = 100, ImporteSenalNoReembolsable = 60, PedidoMinimoCupon = pedidoMinimo
        };

        [TestMethod]
        public async Task CargarEvento_EnsenaSuPedidoMinimo()
        {
            var vm = await Mantenimiento(Evento(200));

            Assert.AreEqual(200M, vm.PedidoMinimoCuponEdicion);
        }

        [TestMethod]
        public async Task ApiDeAntes_SinElCampo_Cero()
        {
            var vm = await Mantenimiento(Evento(null));

            Assert.AreEqual(0M, vm.PedidoMinimoCuponEdicion);
            await vm.GuardarAsync();

            Assert.AreEqual(0M, guardados.Single().PedidoMinimoCupon);
        }

        [TestMethod]
        public async Task Guardar_MandaElPedidoMinimo()
        {
            var vm = await Mantenimiento(Evento(0));
            vm.PedidoMinimoCuponEdicion = 200;

            await vm.GuardarAsync();

            Assert.AreEqual(0, errores.Count, string.Join(" | ", errores));
            Assert.AreEqual(200M, guardados.Single().PedidoMinimoCupon);
            StringAssert.Contains(JsonConvert.SerializeObject(guardados.Single()), "\"PedidoMinimoCupon\":200");
        }

        [TestMethod]
        public async Task Nuevo_CeroPorDefecto()
        {
            var vm = await Mantenimiento(Evento(200));

            vm.NuevoCommand.Execute(null);

            Assert.AreEqual(0M, vm.PedidoMinimoCuponEdicion);
        }

        [TestMethod]
        public async Task Negativo_AvisaDeUstedYNoGuarda()
        {
            var vm = await Mantenimiento(Evento(0));
            vm.PedidoMinimoCuponEdicion = -1;

            await vm.GuardarAsync();

            Assert.AreEqual(0, guardados.Count);
            StringAssert.Contains(errores.Single(), "no puede ser negativo");
        }
    }
}
