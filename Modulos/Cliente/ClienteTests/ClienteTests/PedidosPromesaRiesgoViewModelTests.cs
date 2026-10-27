using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Models;
using Nesto.Infrastructure.Services;
using Nesto.Modulos.Cliente;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#546 (NestoAPI#693, corte 2): «Pedidos con la fecha en riesgo». La API decide quién ve qué y escribe el porqué;
    /// el VM lee, filtra pendientes/resueltos, abre el pedido y prepara el texto para avisar al cliente.
    /// </summary>
    [TestClass]
    public class PedidosPromesaRiesgoViewModelTests
    {
        private static readonly DateTime PROMETIDA = new DateTime(2026, 10, 22);

        private readonly IServicioPedidosPromesaRiesgo servicio = A.Fake<IServicioPedidosPromesaRiesgo>();
        private readonly IAbridorPedidos abridor = A.Fake<IAbridorPedidos>();
        private readonly List<string> copiados = new();

        private PedidosPromesaRiesgoViewModel Crear() => new(servicio, abridor, copiados.Add);

        private static PedidoPromesaRiesgo Riesgo(int pedido, DateTime? nueva, bool resuelto = false) => new()
        {
            Id = pedido % 1000,
            Empresa = "1",
            Pedido = pedido,
            Cliente = "40353",
            NombreCliente = "Peluquería Prueba",
            Vendedor = "JGP",
            FechaPrometida = PROMETIDA,
            FechaNueva = nueva,
            Motivo = "Faltan 2 de 3 del producto 42485 (PARCHES): Reina mandó 1 de 3 en la reposición 81094.",
            Evento = "reposición 81094 de Reina",
            Detectado = new DateTime(2026, 10, 21, 10, 5, 0),
            Resuelto = resuelto ? new DateTime(2026, 10, 24, 9, 15, 0) : null,
            MotivoResolucion = resuelto ? "Se ha servido." : null
        };

        private void LaApiDevuelve(bool incluirResueltos, params PedidoPromesaRiesgo[] riesgos)
            => A.CallTo(() => servicio.Leer("1", incluirResueltos)).Returns(Task.FromResult(riesgos.ToList()));

        // ---------------- Cargar ----------------

        [TestMethod]
        public void NoDependeDeLaNavegacionDePrism_YReutilizaLaPestana()
        {
            Assert.IsFalse(typeof(PedidosPromesaRiesgoViewModel).GetInterfaces().Any(i => i.FullName == "Prism.Regions.INavigationAware"));
            Assert.IsTrue(typeof(IReceptorNavegacion).IsAssignableFrom(typeof(PedidosPromesaRiesgoViewModel)));
            Assert.IsFalse(typeof(IReceptorNavegacionPestanaNueva).IsAssignableFrom(typeof(PedidosPromesaRiesgoViewModel)));
        }

        [TestMethod]
        public async Task Cargar_PorDefectoPideSoloPendientes_YPintaLasFilas()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)), Riesgo(929000, null));
            PedidosPromesaRiesgoViewModel vm = Crear();

            await vm.CargarAsync();

            A.CallTo(() => servicio.Leer("1", false)).MustHaveHappenedOnceExactly();
            A.CallTo(() => servicio.Leer("1", true)).MustNotHaveHappened();
            Assert.AreEqual(2, vm.Pedidos.Count);
            Assert.IsTrue(vm.Cargada);
            Assert.IsNull(vm.Mensaje);
            Assert.AreEqual("2 pedidos con la fecha en riesgo (1 sin fecha)", vm.Resumen);

            FilaPedidoPromesaRiesgo retrasado = vm.Pedidos[0];
            Assert.AreEqual("40353 · Peluquería Prueba", retrasado.ClienteYNombre);
            Assert.AreEqual("JGP", retrasado.Vendedor);
            Assert.AreEqual("22/10/2026", retrasado.TextoFechaPrometida);
            Assert.AreEqual("24/10/2026", retrasado.TextoFechaNueva);
            Assert.AreEqual("2 días", retrasado.TextoRetraso);
            Assert.AreEqual("Pendiente", retrasado.Estado);
            Assert.IsNull(retrasado.TextoResuelto);

            FilaPedidoPromesaRiesgo sinFecha = vm.Pedidos[1];
            Assert.IsTrue(sinFecha.SinFecha);
            Assert.AreEqual("Sin fecha", sinFecha.TextoFechaNueva);
            Assert.AreEqual("Sin fecha", sinFecha.TextoRetraso);
        }

        [TestMethod]
        public void Fila_VendedorDePeluqueria_SeEnsenaJuntoAlDeEstetica()
        {
            PedidoPromesaRiesgo riesgo = Riesgo(928908, PROMETIDA.AddDays(1));
            riesgo.VendedorPeluqueria = "ASH ";

            Assert.AreEqual("JGP / PEL: ASH", new FilaPedidoPromesaRiesgo(riesgo).Vendedor);
            riesgo.VendedorPeluqueria = "JGP";
            Assert.AreEqual("JGP", new FilaPedidoPromesaRiesgo(riesgo).Vendedor);
        }

        [TestMethod]
        public async Task Cargar_SiLaApiDice403_SeEnsenaSuMensaje()
        {
            A.CallTo(() => servicio.Leer("1", false)).Throws(new PedidosPromesaRiesgoException("Solo puede ver los pedidos de su vendedor o de los vendedores de su equipo.", 403));
            PedidosPromesaRiesgoViewModel vm = Crear();

            await vm.CargarAsync();

            Assert.AreEqual("Solo puede ver los pedidos de su vendedor o de los vendedores de su equipo.", vm.Mensaje);
            Assert.AreEqual(0, vm.Pedidos.Count);
            Assert.IsFalse(vm.EstaOcupado);
            Assert.IsTrue(vm.Cargada);
        }

        [TestMethod]
        public async Task Cargar_SiNoHayServidor_LoDice()
        {
            A.CallTo(() => servicio.Leer("1", false)).Throws(new System.Net.Http.HttpRequestException("sin red"));
            PedidosPromesaRiesgoViewModel vm = Crear();

            await vm.CargarAsync();

            Assert.AreEqual("No se ha podido hablar con el servidor: sin red", vm.Mensaje);
        }

        [TestMethod]
        public async Task Cargar_MantieneElPedidoElegido()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)), Riesgo(929000, null));
            PedidosPromesaRiesgoViewModel vm = Crear();
            await vm.CargarAsync();
            vm.Seleccionado = vm.Pedidos[1];

            await vm.CargarAsync();

            Assert.AreEqual(929000, vm.Seleccionado?.Pedido);
        }

        // ---------------- Filtro ----------------

        [TestMethod]
        public async Task Filtro_Resueltos_PideLosResueltosYDejaSoloEsos()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            LaApiDevuelve(true, Riesgo(928908, PROMETIDA.AddDays(2)), Riesgo(928000, PROMETIDA.AddDays(1), resuelto: true));
            PedidosPromesaRiesgoViewModel vm = Crear();
            await vm.CargarAsync();

            vm.Filtro = PedidosPromesaRiesgoViewModel.FILTRO_RESUELTOS;
            await EsperarCarga(vm);

            A.CallTo(() => servicio.Leer("1", true)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, vm.Pedidos.Count);
            Assert.AreEqual(928000, vm.Pedidos[0].Pedido);
            Assert.AreEqual("Resuelto", vm.Pedidos[0].Estado);
            Assert.AreEqual("24/10/2026 09:15 · Se ha servido.", vm.Pedidos[0].TextoResuelto);
            Assert.AreEqual("1 resuelto", vm.Resumen);
        }

        [TestMethod]
        public async Task Filtro_Todos_PendientesPrimeroYResueltosDetras()
        {
            LaApiDevuelve(true, Riesgo(928908, null), Riesgo(928000, PROMETIDA.AddDays(1), resuelto: true));
            PedidosPromesaRiesgoViewModel vm = Crear();

            vm.Filtro = PedidosPromesaRiesgoViewModel.FILTRO_TODOS;
            await EsperarCarga(vm);

            CollectionAssert.AreEqual(new[] { 928908, 928000 }, vm.Pedidos.Select(p => p.Pedido).ToArray());
            Assert.AreEqual("1 en riesgo (1 sin fecha) y 1 resuelto", vm.Resumen);
        }

        [TestMethod]
        public void Filtrar_EsPuro()
        {
            var lista = new List<PedidoPromesaRiesgo> { Riesgo(1, null), null, Riesgo(2, PROMETIDA, resuelto: true) };

            CollectionAssert.AreEqual(new[] { 1 }, PedidosPromesaRiesgoViewModel.Filtrar(lista, PedidosPromesaRiesgoViewModel.FILTRO_PENDIENTES).Select(r => r.Pedido).ToArray());
            CollectionAssert.AreEqual(new[] { 2 }, PedidosPromesaRiesgoViewModel.Filtrar(lista, PedidosPromesaRiesgoViewModel.FILTRO_RESUELTOS).Select(r => r.Pedido).ToArray());
            CollectionAssert.AreEqual(new[] { 1, 2 }, PedidosPromesaRiesgoViewModel.Filtrar(lista, PedidosPromesaRiesgoViewModel.FILTRO_TODOS).Select(r => r.Pedido).ToArray());
            Assert.AreEqual(0, PedidosPromesaRiesgoViewModel.Filtrar(null, "lo que sea").Count());
        }

        // ---------------- Llegar (cinta y campana) ----------------

        [TestMethod]
        public async Task AlLlegar_SinParametros_Lee()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            PedidosPromesaRiesgoViewModel vm = Crear();

            vm.AlLlegar(new ParametrosNavegacion());
            await EsperarCarga(vm);

            Assert.AreEqual(1, vm.Pedidos.Count);
            Assert.IsNull(vm.Seleccionado);
        }

        [TestMethod]
        public async Task AlLlegar_DesdeLaCampanaConElPedido_LoDejaElegido()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)), Riesgo(929000, null));
            PedidosPromesaRiesgoViewModel vm = Crear();

            vm.AlLlegar(new ParametrosNavegacion { { "pedido", 929000 }, { "empresa", "1" } });
            await EsperarCarga(vm);

            Assert.AreEqual(929000, vm.Seleccionado?.Pedido);
            Assert.IsTrue(vm.HaySeleccionado);
            Assert.IsNotNull(vm.TextoCliente);
        }

        [TestMethod]
        public async Task AlLlegar_ConUnPedidoQueYaSeResolvio_PasaATodosYLoElige()
        {
            // El aviso de la campana es de ayer; hoy el pedido ya cumple: se enseña entre los resueltos, no se pierde
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            LaApiDevuelve(true, Riesgo(928908, PROMETIDA.AddDays(2)), Riesgo(929000, PROMETIDA.AddDays(1), resuelto: true));
            PedidosPromesaRiesgoViewModel vm = Crear();

            vm.AlLlegar(new ParametrosNavegacion { { "pedido", "929000" } });
            await EsperarCarga(vm);

            Assert.AreEqual(PedidosPromesaRiesgoViewModel.FILTRO_TODOS, vm.Filtro);
            Assert.AreEqual(929000, vm.Seleccionado?.Pedido);
            Assert.IsNull(vm.Mensaje);
        }

        [TestMethod]
        public async Task AlLlegar_ConUnPedidoQueYaNoEsta_LoDice()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            LaApiDevuelve(true, Riesgo(928908, PROMETIDA.AddDays(2)));
            PedidosPromesaRiesgoViewModel vm = Crear();

            vm.AlLlegar(new ParametrosNavegacion { { "pedido", 777777 } });
            await EsperarCarga(vm);

            Assert.AreEqual("El pedido 777777 ya no está entre los pedidos con la fecha en riesgo.", vm.Mensaje);
            Assert.IsNull(vm.Seleccionado);
        }

        [TestMethod]
        public void LeerPedido_AdmiteEnteroYTexto_YRechazaBasura()
        {
            Assert.AreEqual(928908, PedidosPromesaRiesgoViewModel.LeerPedido(new ParametrosNavegacion { { "pedido", 928908 } }));
            Assert.AreEqual(928908, PedidosPromesaRiesgoViewModel.LeerPedido(new ParametrosNavegacion { { "pedido", "928908" } }));
            Assert.IsNull(PedidosPromesaRiesgoViewModel.LeerPedido(new ParametrosNavegacion { { "pedido", "x" } }));
            Assert.IsNull(PedidosPromesaRiesgoViewModel.LeerPedido(new ParametrosNavegacion { { "pedido", 0 } }));
            Assert.IsNull(PedidosPromesaRiesgoViewModel.LeerPedido(new ParametrosNavegacion()));
            Assert.IsNull(PedidosPromesaRiesgoViewModel.LeerPedido(null));
        }

        // ---------------- Acciones ----------------

        [TestMethod]
        public async Task AbrirPedido_ConLaFila_AbreEsePedido()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)), Riesgo(929000, null));
            PedidosPromesaRiesgoViewModel vm = Crear();
            await vm.CargarAsync();

            Assert.IsTrue(vm.AbrirPedidoCommand.CanExecute(vm.Pedidos[1]));
            vm.AbrirPedidoCommand.Execute(vm.Pedidos[1]);

            A.CallTo(() => abridor.Abrir("1", 929000)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AbrirPedido_SinFila_AbreElElegido_YSinElegidoNoPuede()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            PedidosPromesaRiesgoViewModel vm = Crear();
            await vm.CargarAsync();
            Assert.IsFalse(vm.AbrirPedidoCommand.CanExecute(null));
            Assert.IsFalse(vm.CopiarTextoClienteCommand.CanExecute(null));

            vm.Seleccionado = vm.Pedidos[0];

            Assert.IsTrue(vm.AbrirPedidoCommand.CanExecute(null));
            vm.AbrirPedidoCommand.Execute(null);
            A.CallTo(() => abridor.Abrir("1", 928908)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task AbrirPedido_SiElAbridorFalla_LoDiceYNoTiraLaPantalla()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            A.CallTo(() => abridor.Abrir("1", 928908)).Throws(new InvalidOperationException("sin vista"));
            PedidosPromesaRiesgoViewModel vm = Crear();
            await vm.CargarAsync();

            vm.AbrirPedidoCommand.Execute(vm.Pedidos[0]);

            Assert.AreEqual("No se ha podido abrir el pedido 928908: sin vista", vm.Mensaje);
        }

        [TestMethod]
        public async Task CopiarTextoCliente_CopiaElTextoDeUsted_YLoDice()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            PedidosPromesaRiesgoViewModel vm = Crear();
            await vm.CargarAsync();
            vm.Seleccionado = vm.Pedidos[0];

            vm.CopiarTextoClienteCommand.Execute(null);

            Assert.AreEqual(1, copiados.Count);
            Assert.AreEqual(vm.TextoCliente, copiados[0]);
            StringAssert.Contains(copiados[0], "su pedido 928908");
            StringAssert.Contains(copiados[0], "prometido para el 22/10/2026");
            StringAssert.Contains(copiados[0], "la fecha nueva de salida es el 24/10/2026");
            Assert.IsFalse(copiados[0].Contains("Reina"), "El porqué interno (reposiciones, inventarios) no va al cliente");
            Assert.AreEqual("Texto para el cliente del pedido 928908 copiado al portapapeles. Péguelo en el correo o en el WhatsApp.", vm.Mensaje);
        }

        [TestMethod]
        public void TextoParaCliente_SinFecha_NoInventaUna()
        {
            string texto = PedidosPromesaRiesgoViewModel.TextoParaCliente(Riesgo(929000, null));

            StringAssert.Contains(texto, "de momento no podemos darle una fecha nueva");
            StringAssert.Contains(texto, "en cuanto la tengamos se la confirmaremos");
            Assert.IsNull(PedidosPromesaRiesgoViewModel.TextoParaCliente(null));
        }

        [TestMethod]
        public async Task CopiarTextoCliente_SiElPortapapelesFalla_LoDice()
        {
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            var vm = new PedidosPromesaRiesgoViewModel(servicio, abridor, _ => throw new InvalidOperationException("ocupado"));
            await vm.CargarAsync();
            vm.Seleccionado = vm.Pedidos[0];

            vm.CopiarTextoClienteCommand.Execute(null);

            Assert.AreEqual("No se ha podido copiar al portapapeles: ocupado", vm.Mensaje);
        }

        [TestMethod]
        public async Task AccionesAplazadas_AbrenElPedidoYDicenDondeHacerlo()
        {
            // Cambiar el modo, la reposición puntual y sustituir el producto aún no se hacen en un clic desde aquí
            LaApiDevuelve(false, Riesgo(928908, PROMETIDA.AddDays(2)));
            PedidosPromesaRiesgoViewModel vm = Crear();
            await vm.CargarAsync();
            vm.Seleccionado = vm.Pedidos[0];

            vm.CambiarModoCommand.Execute(null);
            StringAssert.StartsWith(vm.Mensaje, "Se ha abierto el pedido 928908: cambie el modo de servir");
            vm.ReposicionPuntualCommand.Execute(null);
            StringAssert.Contains(vm.Mensaje, "reposición puntual");
            vm.SustituirProductoCommand.Execute(null);
            StringAssert.Contains(vm.Mensaje, "sustituya el producto");

            A.CallTo(() => abridor.Abrir("1", 928908)).MustHaveHappened(3, Times.Exactly);
        }

        [TestMethod]
        public void Textos_DeUstedYConTildes()
        {
            PedidosPromesaRiesgoViewModel vm = Crear();

            Assert.AreEqual("Pedidos con la fecha en riesgo", vm.Titulo);
            StringAssert.Contains(vm.Ayuda, "Usted ve los suyos");
            StringAssert.Contains(vm.Ayuda, "Dirección");
            Assert.IsFalse(vm.Ayuda.Contains(" tu "), "Nada de tú");
            Assert.AreEqual("Ningún pedido con la fecha en riesgo.", vm.Resumen);
        }

        private static async Task EsperarCarga(PedidosPromesaRiesgoViewModel vm)
        {
            for (int i = 0; i < 200 && (vm.EstaOcupado || !vm.Cargada); i++)
            {
                await Task.Delay(5);
            }
        }
    }
}
