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
using Unity;

namespace PlantillaVentaTests
{
    /// <summary>
    /// NestoAPI#606 (corte 2): en la plantilla se enseña qué día entregamos el pedido a la agencia. Lo calcula la API
    /// (POST api/PedidosVenta/FechaEntregaAgencia) con el mismo pedido que ModoServicioSugerido; aquí se prueba que se
    /// pide cuando toca, nunca en cada tecla, y que el texto sale bien.
    /// </summary>
    [TestClass]
    public class FechaEntregaAgenciaPlantillaTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 10, 13); // martes

        private static (PlantillaVentaViewModel vm, IServicioFechaEntregaAgencia servicioFecha, List<PedidoVentaDTO> enviados) CrearViewModel(FechaEntregaAgenciaDTO respuesta)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IUnityContainer>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", ruta = "FW", servirJunto = false };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "38093", cantidad = 5, precio = 10M });

            var enviados = new List<PedidoVentaDTO>();
            var servicioFecha = A.Fake<IServicioFechaEntregaAgencia>();
            A.CallTo(() => servicioFecha.CalcularPlantilla(A<PedidoVentaDTO>._))
                .Invokes((PedidoVentaDTO p) => enviados.Add(p))
                .Returns(Task.FromResult(respuesta));
            vm.ServicioFechaEntregaAgencia = servicioFecha;
            vm.Hoy = () => Hoy;
            return (vm, servicioFecha, enviados);
        }

        private static FechaEntregaAgenciaDTO Fecha(DateTime? dia, DateTime? completa = null, string motivo = "Hay stock de todo en Algete.")
        {
            return new FechaEntregaAgenciaDTO
            {
                FechaEntregaAgencia = dia,
                PrimeraEntrega = dia,
                EntregaCompleta = completa ?? dia,
                Aplica = FechaEntregaAgenciaDTO.APLICA_PRIMERA,
                Motivo = motivo
            };
        }

        [TestMethod]
        public async Task ConRespuesta_EnsenaElDiaYElMotivoEnElTooltip()
        {
            var (vm, _, _) = CrearViewModel(Fecha(new DateTime(2026, 10, 15)));

            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsTrue(vm.FechaEntregaAgencia.HayTexto);
            Assert.AreEqual("Se entrega a la agencia el jueves 15/10", vm.FechaEntregaAgencia.Texto);
            Assert.AreEqual("Hay stock de todo en Algete.", vm.FechaEntregaAgencia.Motivo);
        }

        [TestMethod]
        public async Task HoyYManana()
        {
            var (vmHoy, _, _) = CrearViewModel(Fecha(Hoy));
            var (vmManana, _, _) = CrearViewModel(Fecha(Hoy.AddDays(1), completa: Hoy.AddDays(6)));

            await vmHoy.RefrescarFechaEntregaAgencia();
            await vmManana.RefrescarFechaEntregaAgencia();

            Assert.AreEqual("Se entrega a la agencia hoy", vmHoy.FechaEntregaAgencia.Texto);
            Assert.AreEqual("Se entrega a la agencia mañana · completo el lunes 19/10", vmManana.FechaEntregaAgencia.Texto);
        }

        [TestMethod]
        public async Task SinFecha_DiceSinFechaTodavia()
        {
            var (vm, _, _) = CrearViewModel(Fecha(null));

            await vm.RefrescarFechaEntregaAgencia();

            Assert.AreEqual("Sin fecha todavía", vm.FechaEntregaAgencia.Texto);
        }

        [TestMethod]
        public async Task ApiAntiguaSinElEndpoint_NoSeEnsenaNadaNiSeAvisa()
        {
            // El servicio convierte el 404 en null.
            var (vm, _, _) = CrearViewModel(null);

            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsFalse(vm.FechaEntregaAgencia.HayTexto);
            Assert.IsNull(vm.FechaEntregaAgencia.Texto);
        }

        [TestMethod]
        public async Task SinLineas_NoSePreguntaYNoSeEnsenaNada()
        {
            var (vm, servicioFecha, _) = CrearViewModel(Fecha(Hoy));
            vm.ListaFiltrableProductos.ListaOriginal.Clear();

            await vm.RefrescarFechaEntregaAgencia();

            A.CallTo(() => servicioFecha.CalcularPlantilla(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            Assert.IsFalse(vm.FechaEntregaAgencia.HayTexto);
        }

        [TestMethod]
        public async Task ConElPedidoQuieto_NoSeVuelveAPreguntar()
        {
            var (vm, servicioFecha, _) = CrearViewModel(Fecha(Hoy));

            await vm.RefrescarFechaEntregaAgencia();
            await vm.RefrescarFechaEntregaAgencia();
            await vm.RefrescarFechaEntregaAgencia();

            A.CallTo(() => servicioFecha.CalcularPlantilla(A<PedidoVentaDTO>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, vm.PeticionesFechaEntregaAgenciaEnviadas);
        }

        [TestMethod]
        public void AlTeclear_NoSeLlamaEnCadaTecla_SoloSeReprogramaElReloj()
        {
            var (vm, servicioFecha, _) = CrearViewModel(Fecha(Hoy));
            int antes = vm.ProgramacionesFechaEntregaAgencia;

            for (int i = 0; i < 5; i++)
            {
                vm.ActualizarTotales(); // lo que pasa al cambiar cantidades o precios
            }

            A.CallTo(() => servicioFecha.CalcularPlantilla(A<PedidoVentaDTO>._)).MustNotHaveHappened();
            Assert.AreEqual(antes + 5, vm.ProgramacionesFechaEntregaAgencia, "Cada cambio reprograma el mismo reloj");
        }

        [TestMethod]
        public async Task AlCambiarElModoDeServicio_SeRecalcula()
        {
            var (vm, servicioFecha, enviados) = CrearViewModel(Fecha(Hoy));
            await vm.RefrescarFechaEntregaAgencia();
            int antes = vm.ProgramacionesFechaEntregaAgencia;

            vm.ModoServicio = ModosServicio.TODO_JUNTO;
            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsTrue(vm.ProgramacionesFechaEntregaAgencia > antes, "Cambiar el modo programa la petición");
            Assert.AreEqual(2, enviados.Count);
            Assert.AreEqual(ModosServicio.TODO_JUNTO, enviados[1].modoServicio);
        }

        [TestMethod]
        public async Task AlCambiarElModoDeFacturacion_SeRecalcula()
        {
            var (vm, _, enviados) = CrearViewModel(Fecha(Hoy));
            await vm.RefrescarFechaEntregaAgencia();
            int antes = vm.ProgramacionesFechaEntregaAgencia;
            byte otroModo = vm.ModoFacturacion == ModosFacturacion.AL_COMPLETAR ? ModosFacturacion.POR_ENTREGAS : ModosFacturacion.AL_COMPLETAR;

            vm.ModoFacturacion = otroModo;
            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsTrue(vm.ProgramacionesFechaEntregaAgencia > antes, "Cambiar la facturación programa la petición");
            Assert.AreEqual(2, enviados.Count);
            Assert.AreEqual(otroModo, enviados[1].modoFacturacion);
        }

        [TestMethod]
        public async Task AlCambiarLaRuta_SeRecalcula()
        {
            var (vm, _, enviados) = CrearViewModel(Fecha(Hoy));
            await vm.RefrescarFechaEntregaAgencia();
            int antes = vm.ProgramacionesFechaEntregaAgencia;

            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "1", ruta = "AT", servirJunto = false };
            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsTrue(vm.ProgramacionesFechaEntregaAgencia > antes, "Cambiar de dirección (y de ruta) programa la petición");
            Assert.AreEqual(2, enviados.Count);
            Assert.AreEqual("AT", enviados[1].ruta);
        }

        [TestMethod]
        public async Task SiElServicioRevienta_NoSePropaga()
        {
            var (vm, servicioFecha, _) = CrearViewModel(Fecha(Hoy));
            A.CallTo(() => servicioFecha.CalcularPlantilla(A<PedidoVentaDTO>._)).Throws(new InvalidOperationException("boom"));

            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsFalse(vm.FechaEntregaAgencia.HayTexto);
        }
    }
}
