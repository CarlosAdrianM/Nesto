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
using System.ComponentModel;
using System.Threading.Tasks;

namespace PlantillaVentaTests
{
    /// <summary>
    /// Nesto#599 (Carlos, novedad 962, pedido 929816 de Manuel): junto al desplegable «Servir» se enseña por qué el pedido
    /// sale «Todo junto», producto a producto, en el tooltip del motivo («44873: 1 unidad sale desmontando el kit 43165.»).
    /// El motivo lo manda la API en la sugerencia de modo de servicio; el detalle sale del desglose de la fecha de entrega
    /// a la agencia. Sin motivo, o con una API anterior al desglose, no se enseña nada.
    /// </summary>
    [TestClass]
    public class MotivoModoServicioPlantillaTests
    {
        private static readonly DateTime Hoy = new DateTime(2026, 11, 3); // martes

        private static PlantillaVentaViewModel CrearViewModel(FechaEntregaAgenciaDTO respuesta)
        {
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.LeerParametroSync(Constantes.Empresas.EMPRESA_DEFECTO, Parametros.Claves.AlmacenRuta)).Returns("ALG");
            var vm = new PlantillaVentaViewModel(A.Fake<IAbridorPedidos>(), A.Fake<IServicioNavegacion>(), configuracion, A.Fake<IPlantillaVentaService>(),
                new WeakReferenceMessenger(), A.Fake<IServicioDialogos>(), A.Fake<IPedidoVentaService>(), A.Fake<IBorradorPlantillaVentaService>(),
                A.Fake<IServicioAutenticacion>());
            vm.ListaFiltrableProductos.ListaOriginal = new ObservableCollection<IFiltrableItem>();
            vm._clienteSeleccionado = new ClienteJson { empresa = "1", cliente = "15191", contacto = "0", iva = "G21", cifNif = "12345678A" };
            vm.direccionEntregaSeleccionada = new DireccionesEntregaCliente { contacto = "0", ruta = "FW", servirJunto = false };
            vm.ListaFiltrableProductos.ListaOriginal.Add(new LineaPlantillaVenta { producto = "44873", cantidad = 1, precio = 10M });

            var servicioFecha = A.Fake<IServicioFechaEntregaAgencia>();
            A.CallTo(() => servicioFecha.CalcularPlantilla(A<PedidoVentaDTO>._)).Returns(Task.FromResult(respuesta));
            vm.ServicioFechaEntregaAgencia = servicioFecha;
            vm.Hoy = () => Hoy;
            return vm;
        }

        private static ModoServicioSugeridoDTO TodoJunto(string motivo) => new ModoServicioSugeridoDTO
        {
            Modo = ModosServicio.TODO_JUNTO,
            Nombre = "Todo junto",
            Motivo = motivo,
            ModosPermitidos = new List<byte> { ModosServicio.TODO_JUNTO }
        };

        private static FechaEntregaAgenciaDTO FechaConDesglose(params PartidaFechaEntregaAgenciaDTO[] partidas) => new FechaEntregaAgenciaDTO
        {
            FechaEntregaAgencia = new DateTime(2026, 11, 5),
            PrimeraEntrega = new DateTime(2026, 11, 5),
            EntregaCompleta = new DateTime(2026, 11, 5),
            Aplica = FechaEntregaAgenciaDTO.APLICA_COMPLETA,
            Motivo = "Por qué: …",
            Desglose = new List<PartidaFechaEntregaAgenciaDTO>(partidas)
        };

        private static PartidaFechaEntregaAgenciaDTO Kit(string producto, string kit, int unidades = 1) => new PartidaFechaEntregaAgenciaDTO
        {
            Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_KIT, Producto = producto, Kit = kit, Unidades = unidades, PuedeSalirEl = Hoy
        };

        private const string MOTIVO_KITS = "Hay stock de todo en el almacén del pedido: sale todo junto (contando los kits que se pueden desmontar en Algete).";

        [TestMethod]
        public async Task ConMotivo_SeEnsenaElMotivoYEnElTooltipElDetallePorProducto()
        {
            PlantillaVentaViewModel vm = CrearViewModel(FechaConDesglose(
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_ALGETE, Producto = "38093", Unidades = 3, PuedeSalirEl = Hoy },
                Kit("44873", "43165")));
            vm.AplicarSugerenciaModoServicio(TodoJunto(MOTIVO_KITS));

            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsTrue(vm.HayMotivoModoServicio);
            Assert.AreEqual(MOTIVO_KITS, vm.MotivoModoServicio);
            Assert.AreEqual("44873: 1 unidad sale desmontando el kit 43165.", vm.DetalleMotivoModoServicio,
                "Lo que está en Algete no se explica; el kit, sí");
        }

        [TestMethod]
        public async Task SinMotivoDeLaApi_NoSeEnsenaNiElTextoNiElTooltip()
        {
            PlantillaVentaViewModel vm = CrearViewModel(FechaConDesglose(Kit("44873", "43165")));
            vm.AplicarSugerenciaModoServicio(TodoJunto(null));

            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsFalse(vm.HayMotivoModoServicio, "Sin motivo, el texto va oculto (Collapsed)");
            Assert.IsNull(vm.MotivoModoServicio);
            Assert.IsNull(vm.DetalleMotivoModoServicio, "Sin motivo visible no hay tooltip que enseñar");
        }

        [TestMethod]
        public async Task ApiAnteriorSinDesglose_SeEnsenaElMotivoSinTooltip()
        {
            FechaEntregaAgenciaDTO sinDesglose = FechaConDesglose();
            sinDesglose.Desglose = null; // la API vieja no manda la propiedad
            PlantillaVentaViewModel vm = CrearViewModel(sinDesglose);
            vm.AplicarSugerenciaModoServicio(TodoJunto(MOTIVO_KITS));

            await vm.RefrescarFechaEntregaAgencia();

            Assert.IsTrue(vm.HayMotivoModoServicio);
            Assert.IsNull(vm.DetalleMotivoModoServicio);
        }

        [TestMethod]
        public void SinSugerenciaDeLaApi_NadaQueEnsenar()
        {
            PlantillaVentaViewModel vm = CrearViewModel(null);

            Assert.IsFalse(vm.HayMotivoModoServicio);
            Assert.IsNull(vm.DetalleMotivoModoServicio);
        }

        [TestMethod]
        public async Task AlLlegarElDesglose_AvisaDelCambioDelTooltip()
        {
            PlantillaVentaViewModel vm = CrearViewModel(FechaConDesglose(Kit("44873", "43165")));
            vm.AplicarSugerenciaModoServicio(TodoJunto(MOTIVO_KITS));
            var cambiadas = new List<string>();
            ((INotifyPropertyChanged)vm).PropertyChanged += (s, e) => cambiadas.Add(e.PropertyName);

            await vm.RefrescarFechaEntregaAgencia();

            CollectionAssert.Contains(cambiadas, nameof(PlantillaVentaViewModel.DetalleMotivoModoServicio));
        }

        [TestMethod]
        public void DetallePorLinea_RedactaCadaOrigenYSeSaltaAlgeteYPicking()
        {
            FechaEntregaAgenciaDTO fecha = FechaConDesglose(
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_PICKING, Producto = "11111", Unidades = 1, PuedeSalirEl = Hoy },
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_ALGETE, Producto = "22222", Unidades = 1, PuedeSalirEl = Hoy },
                Kit("44873", "43165", 2),
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_TIENDA, Producto = "45363", Unidades = 1, Tienda = "REI", PuedeSalirEl = new DateTime(2026, 11, 5) },
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_EN_CAMINO, Producto = "33333", Unidades = 2, PuedeSalirEl = new DateTime(2026, 11, 4) },
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_PROVEEDOR, Producto = "44444", Unidades = 1, FechaPrevistaProveedor = new DateTime(2026, 11, 9), PuedeSalirEl = new DateTime(2026, 11, 10) },
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_SIN_STOCK, Producto = "32565", Unidades = 1, MotivoSinFecha = "no hay stock y el proveedor no da fecha" },
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_SIN_STOCK, Producto = "55555", Unidades = 3 });

            string detalle = TextosFechaEntregaAgencia.DetallePorLinea(fecha, Hoy);

            string[] lineas = detalle.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
            CollectionAssert.AreEqual(new[]
            {
                "44873: 2 unidades salen desmontando el kit 43165.",
                "45363: 1 unidad en Reina: llega con la reposición y puede salir el jueves 05/11.",
                "33333: 2 unidades ya vienen de camino desde una tienda y pueden salir mañana.",
                "44444: 1 unidad del proveedor (prevista el lunes 09/11) y puede salir el martes 10/11.",
                "32565: 1 unidad sin fecha: no hay stock y el proveedor no da fecha.",
                "55555: 3 unidades sin stock y sin fecha de llegada."
            }, lineas);
        }

        [TestMethod]
        public void DetallePorLinea_TodoEnAlgete_OSinDesglose_EsNull()
        {
            Assert.IsNull(TextosFechaEntregaAgencia.DetallePorLinea(null, Hoy));
            Assert.IsNull(TextosFechaEntregaAgencia.DetallePorLinea(new FechaEntregaAgenciaDTO(), Hoy));
            Assert.IsNull(TextosFechaEntregaAgencia.DetallePorLinea(FechaConDesglose(
                new PartidaFechaEntregaAgenciaDTO { Origen = PartidaFechaEntregaAgenciaDTO.ORIGEN_ALGETE, Producto = "38093", Unidades = 5 }), Hoy));
        }
    }
}
