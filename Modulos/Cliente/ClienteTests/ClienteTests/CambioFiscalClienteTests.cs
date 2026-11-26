using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models.Nesto.Models;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using ClienteCrear = Nesto.Modulos.Cliente.ClienteCrear;

namespace ClienteTests
{
    /// <summary>
    /// NestoAPI#876 (corte nuevo, Carlos 26/11/26): un cliente que cambia de forma fiscal. En el alta, una casilla por bloque que viaja
    /// en CopiarDelAnterior; después, lo copiado y, si el anterior tiene pedidos pendientes, el diálogo para pasarlos. En la ficha,
    /// «Copiar del cliente anterior…» (previa + copiar los marcados) y «Pasar pedidos pendientes…» (ensayo + pasar los marcados).
    /// </summary>
    [TestClass]
    public class CambioFiscalClienteTests
    {
        private const string CAMBIO = "CAMBIO-FISCAL";

        private readonly IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IClienteService servicio = A.Fake<IClienteService>();
        private readonly IOrigenesClienteService origenes = A.Fake<IOrigenesClienteService>();
        private readonly IServicioCambioFiscal cambioFiscal = A.Fake<IServicioCambioFiscal>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();
        private readonly List<string> avisos = new();

        public CambioFiscalClienteTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => dialogos.ShowNotification(A<string>.Ignored, A<string>.Ignored)).Invokes((string t, string m) => avisos.Add(m));
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            A.CallTo(() => configuracion.usuario).Returns("NUEVAVISION\\Laura");
            A.CallTo(() => origenes.LeerOrigenes()).Returns(new List<OrigenClienteModel>
            {
                new OrigenClienteModel { Codigo = CAMBIO, Descripcion = "Cambio de forma fiscal", Activo = true }
            });
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJ876", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle Mayor", Numero = "1", CodigoPostal = "28001" });
        }

        private async Task<CrearClienteViewModel> AltaCambioFiscal()
        {
            var vm = new CrearClienteViewModel(navegacion, configuracion, servicio, new WeakReferenceMessenger(), dialogos, origenes, cambioFiscal);
            await vm.CargaOrigenesEnCurso;
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ876" });
            vm.ClienteDireccion = "CALLE MAYOR, 1";
            vm.ClienteTipoLugar = "Casa";
            vm.ClienteOrigen = CAMBIO;
            vm.ClienteAnterior = "37283";
            return vm;
        }

        private static PedidoPendienteCambioFiscalModel Pendiente(int numero, bool picking = false) => new()
        {
            Pedido = numero, Contacto = "0", Fecha = new DateTime(2026, 11, 26), Importe = 123.45M, Lineas = 3, TienePicking = picking,
            Pickings = picking ? new List<int> { 100123 } : new List<int>(), SePuedePasar = true
        };

        // ---------- Alta ----------

        [TestMethod]
        public async Task Alta_LasCasillasEmpiezanMarcadasYViajanTodas()
        {
            CrearClienteViewModel vm = await AltaCambioFiscal();

            Assert.IsTrue(vm.MostrarBloquesCopiarDelAnterior);
            CollectionAssert.AreEqual(BloquesCambioFiscal.TODOS.ToList(), vm.CopiarDelAnteriorParaAlta);
            Assert.IsTrue(vm.BloquesCopiarDelAnterior.All(b => !string.IsNullOrWhiteSpace(b.Descripcion)), "Cada casilla dice qué copia");
        }

        [TestMethod]
        public async Task Alta_SinNingunaMarcada_NoViajaNada()
        {
            CrearClienteViewModel vm = await AltaCambioFiscal();
            foreach (FilaBloqueCambioFiscal b in vm.BloquesCopiarDelAnterior)
            {
                b.Marcado = false;
            }

            Assert.IsNull(vm.CopiarDelAnteriorParaAlta, "Una lista vacía sería «todos» para la API");
        }

        [TestMethod]
        public async Task Alta_SinClienteAnterior_NoViajaNada()
        {
            CrearClienteViewModel vm = await AltaCambioFiscal();
            vm.ClienteAnterior = null;

            Assert.IsFalse(vm.MostrarBloquesCopiarDelAnterior);
            Assert.IsNull(vm.CopiarDelAnteriorParaAlta);
        }

        [TestMethod]
        public async Task Alta_MandaLosMarcados_EnsenaLoCopiado_ReleeLaFichaYOfrecePasarLosPedidos()
        {
            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>._)).Returns(new Clientes
            {
                Empresa = "1", Nº_Cliente = "42178", Contacto = "0", ClienteAnterior = "37283", Estado = 0,
                CopiadoDelAnterior = new List<string> { "Contacto 0: IVA G21 → R52" },
                AvisoCopiaClienteAnterior = "La forma de pago necesita cuenta bancaria: désela de nuevo."
            });
            A.CallTo(() => servicio.LeerClienteCrear("1", "42178", "0")).Returns(new ClienteCrear { Estado = 6, Ruta = "AT" });
            A.CallTo(() => cambioFiscal.Previa("1", "37283", "42178")).Returns(new CopiaCambioFiscalModel
            {
                PedidosPendientes = new List<PedidoPendienteCambioFiscalModel> { Pendiente(931691) }
            });
            Clientes enviado = null;
            var messenger = new WeakReferenceMessenger();
            messenger.Register<Nesto.Infrastructure.Events.ClienteCreadoMensaje>(this, (r, m) => enviado = m.Value);
            var vm = new CrearClienteViewModel(navegacion, configuracion, servicio, messenger, dialogos, origenes, cambioFiscal);
            await vm.CargaOrigenesEnCurso;
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ876" });
            vm.ClienteDireccion = "CALLE MAYOR, 1";
            vm.ClienteTipoLugar = "Casa";
            vm.ClienteOrigen = CAMBIO;
            vm.ClienteAnterior = "37283";
            vm.BloquesCopiarDelAnterior.Single(b => b.Bloque == BloquesCambioFiscal.OFERTAS).Marcado = false;

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(400);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c => c.CopiarDelAnterior != null
                && c.CopiarDelAnterior.Count == 5 && !c.CopiarDelAnterior.Contains(BloquesCambioFiscal.OFERTAS)))).MustHaveHappened();
            string aviso = avisos.Single();
            StringAssert.Contains(aviso, "Copiado del cliente anterior:");
            StringAssert.Contains(aviso, "IVA G21 → R52");
            StringAssert.Contains(aviso, "désela de nuevo");
            Assert.AreEqual((short)6, enviado?.Estado, "Las demás pantallas reciben la ficha releída");
            Assert.AreEqual("AT", enviado?.Ruta);
            A.CallTo(() => dialogos.ShowDialogAsync(CambioFiscalPedidosDialogViewModel.NOMBRE,
                A<ParametrosDialogo>.That.Matches(p => p.GetValue<string>("clienteNuevo") == "42178"))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Alta_SinPedidosPendientes_NoAbreElDialogo()
        {
            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>._)).Returns(new Clientes { Empresa = "1", Nº_Cliente = "42178", Contacto = "0", ClienteAnterior = "37283" });
            A.CallTo(() => cambioFiscal.Previa(A<string>._, A<string>._, A<string>._)).Returns(new CopiaCambioFiscalModel { PedidosPendientes = new() });
            CrearClienteViewModel vm = await AltaCambioFiscal();

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => dialogos.ShowDialogAsync(A<string>._, A<ParametrosDialogo>._)).MustNotHaveHappened();
            Assert.AreEqual(0, errores.Count);
        }

        // ---------- Pasar pedidos ----------

        [TestMethod]
        public async Task Pedidos_ElEnsayoQuitaLaCasillaALosDePickingQueCambiarian()
        {
            A.CallTo(() => cambioFiscal.PasarPedidos(A<PasarPedidosCambioFiscalPeticion>._, true)).Returns(new PasarPedidosCambioFiscalModel
            {
                Ensayo = true,
                Pedidos = new()
                {
                    new PedidoPasadoCambioFiscalModel { Pedido = 931691, Pasado = true, SinCambiosDeImportes = true },
                    new PedidoPasadoCambioFiscalModel { Pedido = 931700, Pasado = false, TeniaPicking = true, Codigo = "CAMBIO_CLIENTE_CON_PICKING_CAMBIA", Mensaje = "Tiene picking y cambiarían los importes." }
                }
            });
            var paso = new PasoPedidosCambioFiscal(cambioFiscal);

            await paso.CargarAsync("1", "37283", "42178", new[] { Pendiente(931691), Pendiente(931700, picking: true) });

            Assert.IsTrue(paso.EnsayoHecho);
            FilaPedidoCambioFiscal conPicking = paso.Filas.Single(f => f.Pedido == 931700);
            Assert.IsFalse(conPicking.SePuedeMarcar);
            Assert.IsFalse(conPicking.Marcado);
            Assert.AreEqual("Tiene picking y cambiarían los importes.", conPicking.Motivo);
            StringAssert.Contains(conPicking.Texto, "con picking 100123");
            Assert.IsTrue(paso.Filas.Single(f => f.Pedido == 931691).Marcado);
            Assert.IsTrue(paso.PasarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Pedidos_PasaSoloLosMarcados()
        {
            A.CallTo(() => cambioFiscal.PasarPedidos(A<PasarPedidosCambioFiscalPeticion>._, true)).Returns(new PasarPedidosCambioFiscalModel
            {
                Pedidos = new() { new() { Pedido = 931691, Pasado = true }, new() { Pedido = 931692, Pasado = true } }
            });
            A.CallTo(() => cambioFiscal.PasarPedidos(A<PasarPedidosCambioFiscalPeticion>._, false)).Returns(new PasarPedidosCambioFiscalModel
            {
                Pedidos = new() { new() { Pedido = 931691, Pasado = true } }
            });
            var paso = new PasoPedidosCambioFiscal(cambioFiscal);
            await paso.CargarAsync("1", "37283", "42178", new[] { Pendiente(931691), Pendiente(931692) });
            paso.Filas.Single(f => f.Pedido == 931692).Marcado = false;

            await paso.PasarAsync();

            A.CallTo(() => cambioFiscal.PasarPedidos(A<PasarPedidosCambioFiscalPeticion>.That.Matches(p => p.Pedidos.SequenceEqual(new[] { 931691 })
                && p.ClienteAnterior == "37283" && p.ClienteNuevo == "42178"), false)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Se ha pasado el pedido 931691 al cliente 42178.", paso.Mensaje);
            Assert.IsTrue(paso.Pasados);
            Assert.IsFalse(paso.PasarCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Pedidos_SiElEnsayoFalla_NoSePasaNada()
        {
            A.CallTo(() => cambioFiscal.PasarPedidos(A<PasarPedidosCambioFiscalPeticion>._, true))
                .Throws(new CambioFiscalApiException(409, "No consta que el 42178 sea el 37283 con otra forma fiscal.", "NO_ES_CAMBIO_FISCAL"));
            var paso = new PasoPedidosCambioFiscal(cambioFiscal);

            await paso.CargarAsync("1", "37283", "42178", new[] { Pendiente(931691) });

            Assert.IsFalse(paso.EnsayoHecho);
            Assert.IsFalse(paso.PasarCommand.CanExecute(null));
            StringAssert.Contains(paso.Mensaje, "No consta que el 42178");
        }

        [TestMethod]
        public void Dialogo_AlAbrirCargaLosPedidos()
        {
            var vm = new CambioFiscalPedidosDialogViewModel(cambioFiscal, configuracion);

            vm.OnDialogOpened(new ParametrosDialogo
            {
                { "empresa", "1" }, { "clienteAnterior", "37283" }, { "clienteNuevo", "42178" },
                { "pedidos", new List<PedidoPendienteCambioFiscalModel> { Pendiente(931691) } }
            });

            Assert.AreEqual(1, vm.Paso.Filas.Count);
            StringAssert.Contains(vm.Texto, "37283");
        }

        // ---------- Ficha ----------

        private CambioFiscalClienteViewModel Ficha(string anterior = "37283")
        {
            var vm = new CambioFiscalClienteViewModel(cambioFiscal, dialogos, configuracion);
            vm.Cargar("1", "42178", anterior);
            return vm;
        }

        [TestMethod]
        public void Ficha_SinClienteAnterior_NoSeVe()
        {
            CambioFiscalClienteViewModel vm = Ficha(anterior: null);

            Assert.IsFalse(vm.Visible);
            Assert.IsFalse(vm.VerCopiaCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Ficha_Previa_MarcaLosBloquesConCambios_YCopiaLosMarcados()
        {
            A.CallTo(() => cambioFiscal.Previa("1", "37283", "42178")).Returns(new CopiaCambioFiscalModel
            {
                Bloques = new()
                {
                    new BloqueCambioFiscalModel { Bloque = "Fiscalidad", Nombre = "Tipo de IVA", Anterior = new() { "R52" }, Nuevo = new() { "G21" }, Cambios = new() { "Contacto 0: IVA G21 → R52" } },
                    new BloqueCambioFiscalModel { Bloque = "Ofertas", Nombre = "Ofertas permitidas" }
                }
            });
            A.CallTo(() => cambioFiscal.Copiar(A<CopiaCambioFiscalPeticion>._, false)).Returns(new CopiaCambioFiscalModel
            {
                Bloques = new() { new BloqueCambioFiscalModel { Bloque = "Fiscalidad", Cambios = new() { "Contacto 0: IVA G21 → R52" } } }
            });
            CambioFiscalClienteViewModel vm = Ficha();

            await vm.VerCopiaAsync();
            Assert.IsTrue(vm.MostrandoCopia);
            Assert.IsTrue(vm.Bloques.Single(b => b.Bloque == "Fiscalidad").Marcado);
            Assert.IsFalse(vm.Bloques.Single(b => b.Bloque == "Ofertas").Marcado, "Sin nada que copiar, sin marcar");

            await vm.CopiarAsync();

            A.CallTo(() => cambioFiscal.Copiar(A<CopiaCambioFiscalPeticion>.That.Matches(p => p.Bloques.SequenceEqual(new[] { "Fiscalidad" })
                && p.ClienteAnterior == "37283" && p.ClienteNuevo == "42178"), false)).MustHaveHappenedOnceExactly();
            StringAssert.Contains(avisos.Single(), "Se ha copiado del cliente anterior:");
        }

        [TestMethod]
        public async Task Ficha_CopiarSinMarcarNada_NoLlamaALaApi()
        {
            A.CallTo(() => cambioFiscal.Previa(A<string>._, A<string>._, A<string>._)).Returns(new CopiaCambioFiscalModel
            {
                Bloques = new() { new BloqueCambioFiscalModel { Bloque = "Ofertas" } }
            });
            CambioFiscalClienteViewModel vm = Ficha();
            await vm.VerCopiaAsync();

            await vm.CopiarAsync();

            A.CallTo(() => cambioFiscal.Copiar(A<CopiaCambioFiscalPeticion>._, A<bool>._)).MustNotHaveHappened();
            Assert.AreEqual(ReglasPantallaCambioFiscal.MOTIVO_NINGUN_BLOQUE, errores.Single());
        }

        [TestMethod]
        public async Task Ficha_403_SeEnsenaSuMensaje()
        {
            A.CallTo(() => cambioFiscal.Previa(A<string>._, A<string>._, A<string>._))
                .Throws(new CambioFiscalApiException(403, "Solo Administración, Dirección, Informática y el vendedor del cliente."));
            CambioFiscalClienteViewModel vm = Ficha();

            await vm.VerCopiaAsync();

            Assert.AreEqual("Solo Administración, Dirección, Informática y el vendedor del cliente.", vm.Mensaje);
            Assert.IsFalse(vm.MostrandoCopia);
        }

        [TestMethod]
        public async Task Ficha_PasarPedidos_CargaLaListaConElEnsayo()
        {
            A.CallTo(() => cambioFiscal.Previa(A<string>._, A<string>._, A<string>._)).Returns(new CopiaCambioFiscalModel
            {
                PedidosPendientes = new() { Pendiente(931691) }
            });
            A.CallTo(() => cambioFiscal.PasarPedidos(A<PasarPedidosCambioFiscalPeticion>._, true)).Returns(new PasarPedidosCambioFiscalModel
            {
                Pedidos = new() { new() { Pedido = 931691, Pasado = true, Cambios = new() { "Precio de 17404: 10 → 9" } } }
            });
            CambioFiscalClienteViewModel vm = Ficha();

            await vm.VerPedidosAsync();

            Assert.IsTrue(vm.MostrandoPedidos);
            Assert.AreEqual("Al pasarlo: Precio de 17404: 10 → 9", vm.Pedidos.Filas.Single().Motivo);
        }

        // ---------- Servicio y textos ----------

        [TestMethod]
        public void Servicio_409ConCodigo()
        {
            var ex = Assert.ThrowsException<CambioFiscalApiException>(() => ServicioCambioFiscal.Comprobar(HttpStatusCode.Conflict,
                "{\"Message\":\"No consta.\",\"Codigo\":\"NO_ES_CAMBIO_FISCAL\"}"));

            Assert.AreEqual("No consta.", ex.Message);
            Assert.AreEqual("NO_ES_CAMBIO_FISCAL", ex.Codigo);
            Assert.AreEqual(409, ex.Estado);
        }

        [TestMethod]
        public void Servicio_403SinCuerpo_MensajeDeUsted()
        {
            var ex = Assert.ThrowsException<CambioFiscalApiException>(() => ServicioCambioFiscal.Comprobar(HttpStatusCode.Forbidden, ""));

            Assert.AreEqual(ServicioCambioFiscal.MENSAJE_SIN_PERMISO, ex.Message);
        }

        [TestMethod]
        public void Servicio_UrlPrevia()
        {
            Assert.AreEqual("Clientes/CambioFiscal/Previa?empresa=1&clienteAnterior=37283&clienteNuevo=42178",
                ServicioCambioFiscal.UrlPrevia(null, " 37283 ", "42178"));
        }

        [TestMethod]
        public void TextoPedido_ConNotaPickingEImporte()
        {
            var p = Pendiente(931691, picking: true);
            p.NotaEntrega = true;

            Assert.AreEqual("931691 (nota de entrega) · contacto 0 · 26/11/2026 · 3 líneas · 123,45 € (con picking 100123)", ReglasPantallaCambioFiscal.TextoPedido(p));
        }
    }
}
