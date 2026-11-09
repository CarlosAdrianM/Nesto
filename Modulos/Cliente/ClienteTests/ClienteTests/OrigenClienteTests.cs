using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClienteCrear = Nesto.Modulos.Cliente.ClienteCrear;
using PersonaContactoDTO = Nesto.Modulos.Cliente.PersonaContactoDTO;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#523 (NestoAPI#623): origen del cliente (campañas; primer origen META-HARMONIA-0926, leads de Meta de Harmonia) en
    /// «Crear cliente» (desplegable, POST/PUT con Origen) y en la ficha (texto y cambio con PUT). API sin los endpoints = sin error.
    /// </summary>
    [TestClass]
    public class OrigenClienteTests
    {
        private const string META = "META-HARMONIA-0926";

        private readonly IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IClienteService servicio = A.Fake<IClienteService>();
        private readonly IOrigenesClienteService origenes = A.Fake<IOrigenesClienteService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public OrigenClienteTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            A.CallTo(() => configuracion.usuario).Returns("NUEVAVISION\\Paloma");
            A.CallTo(() => origenes.LeerOrigenes()).Returns(Catalogo());
            A.CallTo(() => origenes.LeerOrigen(A<string>._, A<string>._)).Returns(Task.FromResult<OrigenDeClienteModel>(null));
        }

        private static List<OrigenClienteModel> Catalogo() => new()
        {
            new OrigenClienteModel { Codigo = META, Descripcion = "Lead de Meta", Campana = "Harmonia", Activo = true, HorasParaLlamar = 24 }
        };

        private CrearClienteViewModel Alta(IOrigenesClienteService servicioOrigenes)
            => new(navegacion, configuracion, servicio, new WeakReferenceMessenger(), dialogos, servicioOrigenes);

        private OrigenClienteViewModel Ficha() => new(origenes, dialogos, configuracion);

        private void EnGrupo(string grupo) => A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);

        // ---------- Crear cliente ----------

        [TestMethod]
        public async Task CrearCliente_AlAbrir_CargaElCatalogoConNingunoPrimeroYNadaElegido()
        {
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            Assert.IsTrue(vm.HayOrigenes);
            Assert.AreEqual(2, vm.Origenes.Count);
            Assert.AreEqual("(ninguno)", vm.Origenes[0].Nombre);
            Assert.AreEqual("Lead de Meta · campaña Harmonia", vm.Origenes[1].Nombre);
            Assert.AreSame(vm.Origenes[0], vm.OrigenSeleccionado);
            Assert.IsNull(vm.ClienteOrigen);
        }

        [TestMethod]
        public async Task CrearCliente_ApiSinElEndpoint_SinErrorYSinDesplegable()
        {
            A.CallTo(() => origenes.LeerOrigenes()).Returns(new List<OrigenClienteModel>());
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            Assert.IsFalse(vm.HayOrigenes);
            Assert.IsFalse(vm.MostrarOrigenEnDatosGenerales);
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task CrearCliente_ApiQueFalla_SinErrorYSinDesplegable()
        {
            A.CallTo(() => origenes.LeerOrigenes()).Throws(new Exception("500"));
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            Assert.IsFalse(vm.HayOrigenes);
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task CrearCliente_SinServicioDeOrigenes_NoRompe()
        {
            CrearClienteViewModel vm = Alta(null);
            await vm.CargaOrigenesEnCurso;

            Assert.IsFalse(vm.HayOrigenes);
            Assert.AreSame(vm.Origenes[0], vm.OrigenSeleccionado);
        }

        [TestMethod]
        public async Task CrearCliente_AltaNueva_MandaElOrigenEnElPost()
        {
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJ523", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle Mayor", Numero = "1", CodigoPostal = "28001" });
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ523" });
            vm.ClienteDireccion = "CALLE MAYOR, 1";
            vm.OrigenSeleccionado = vm.Origenes.Single(o => o.Codigo == META);

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c => c.Origen == META))).MustHaveHappened();
        }

        [TestMethod]
        public async Task CrearCliente_SinOrigen_MandaNull()
        {
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJ523", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle Mayor", Numero = "1", CodigoPostal = "28001" });
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;
            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ523" });
            vm.ClienteDireccion = "CALLE MAYOR, 1";

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c => c.Origen == null))).MustHaveHappened();
        }

        [TestMethod]
        public async Task CrearCliente_ElegirNingunoDespuesDeUnOrigen_LoQuita_YUnNullDeLaVistaNoLoQuita()
        {
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;
            vm.OrigenSeleccionado = vm.Origenes[1];

            vm.OrigenSeleccionado = null; // la vista lo manda al rehacerse la lista
            Assert.AreEqual(META, vm.ClienteOrigen);

            vm.OrigenSeleccionado = vm.Origenes[0];
            Assert.IsNull(vm.ClienteOrigen);
        }

        [TestMethod]
        public async Task ModificarCliente_PrecargaElOrigenDelDto_YLoMandaEnElPut()
        {
            A.CallTo(() => servicio.LeerClienteCrear("1", "15191", "0")).Returns(new ClienteCrear
            {
                Empresa = "1", Cliente = "15191", Contacto = "0", Direccion = "CALLE MAYOR, 1", Origen = META,
                PersonasContacto = new List<PersonaContactoDTO>()
            });
            A.CallTo(() => servicio.ModificarCliente(A<ClienteCrear>._)).Returns(new Nesto.Models.Nesto.Models.Clientes { Empresa = "1", Nº_Cliente = "15191", Contacto = "0" });
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            vm.AlLlegar(new ParametrosNavegacion { { "empresaParameter", "1" }, { "clienteParameter", "15191" }, { "contactoParameter", "0" } });
            await Task.Delay(300);

            Assert.AreEqual(META, vm.ClienteOrigen);
            Assert.AreEqual(META, vm.OrigenSeleccionado.Codigo);
            Assert.IsTrue(vm.MostrarOrigenEnDatosGenerales, "Al modificar se entra en Datos generales: allí se ve el origen");

            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.ModificarCliente(A<ClienteCrear>.That.Matches(c => c.Origen == META))).MustHaveHappened();
        }

        [TestMethod]
        public async Task ModificarCliente_SinOrigenEnElDto_LoLeeDeLaApi_AunqueYaNoEsteActivo()
        {
            A.CallTo(() => servicio.LeerClienteCrear("1", "15191", "0")).Returns(new ClienteCrear
            {
                Empresa = "1", Cliente = "15191", Contacto = "0", Direccion = "CALLE MAYOR, 1", PersonasContacto = new List<PersonaContactoDTO>()
            });
            A.CallTo(() => origenes.LeerOrigen("1", "15191")).Returns(new OrigenDeClienteModel { Origen = "FERIA-2025", Descripcion = "Feria 2025" });
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            vm.AlLlegar(new ParametrosNavegacion { { "empresaParameter", "1" }, { "clienteParameter", "15191" }, { "contactoParameter", "0" } });
            await Task.Delay(300);

            Assert.AreEqual("FERIA-2025", vm.ClienteOrigen);
            Assert.AreEqual("Feria 2025", vm.OrigenSeleccionado.Nombre, "El origen inactivo se añade a la lista para no perderlo");
        }

        // ---------- NestoAPI#848: origen propuesto según quién crea el cliente ----------

        private const string TIENDA_ALCOBENDAS = "TIENDA-ALCOBENDAS";

        private static List<OrigenClienteModel> CatalogoConPropuesto() => new()
        {
            new OrigenClienteModel { Codigo = META, Descripcion = "Lead de Meta", Campana = "Harmonia", Activo = true, HorasParaLlamar = 24, EsLead = true },
            new OrigenClienteModel { Codigo = TIENDA_ALCOBENDAS, Descripcion = "Tienda de Alcobendas", Activo = true, HorasParaLlamar = 24, EsLead = false, PropuestoParaElUsuario = true },
            new OrigenClienteModel { Codigo = "WEB", Descripcion = "Página web", Activo = true, HorasParaLlamar = 24, EsLead = false }
        };

        [TestMethod]
        public async Task CrearCliente_ConOrigenPropuesto_LoPreselecciona()
        {
            A.CallTo(() => origenes.LeerOrigenes()).Returns(CatalogoConPropuesto());
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            Assert.AreEqual(TIENDA_ALCOBENDAS, vm.ClienteOrigen);
            Assert.AreEqual("Tienda de Alcobendas", vm.OrigenSeleccionado.Nombre);
        }

        [TestMethod]
        public async Task CrearCliente_OrigenPropuesto_SePuedeCambiarYQuitar()
        {
            A.CallTo(() => origenes.LeerOrigenes()).Returns(CatalogoConPropuesto());
            A.CallTo(() => servicio.LeerDetalleDireccion("ChIJ848", A<string>.Ignored))
                .Returns(new DireccionDetalleModel { Calle = "Calle Mayor", Numero = "1", CodigoPostal = "28001" });
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            vm.OrigenSeleccionado = vm.Origenes.Single(o => o.Codigo == "WEB");
            Assert.AreEqual("WEB", vm.ClienteOrigen);

            vm.OrigenSeleccionado = vm.Origenes[0]; // «(ninguno)»
            Assert.IsNull(vm.ClienteOrigen);

            await vm.AplicarSugerenciaDireccionAsync(new SugerenciaDireccionModel { PlaceId = "ChIJ848" });
            vm.ClienteDireccion = "CALLE MAYOR, 1";
            vm.CrearClienteCommand.Execute(null);
            await Task.Delay(300);

            A.CallTo(() => servicio.CrearCliente(A<ClienteCrear>.That.Matches(c => c.Origen == null))).MustHaveHappened();
        }

        [TestMethod]
        public async Task CrearCliente_SinOrigenPropuesto_NadaElegido()
        {
            List<OrigenClienteModel> catalogo = CatalogoConPropuesto();
            catalogo.ForEach(o => o.PropuestoParaElUsuario = false);
            A.CallTo(() => origenes.LeerOrigenes()).Returns(catalogo);
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            Assert.IsNull(vm.ClienteOrigen);
            Assert.AreSame(vm.Origenes[0], vm.OrigenSeleccionado);
        }

        [TestMethod]
        public async Task ModificarCliente_SinOrigen_NoLePoneElPropuesto()
        {
            // El catálogo (con la propuesta) llega antes que el cliente: al modificar manda lo que tiene el cliente (nada).
            A.CallTo(() => origenes.LeerOrigenes()).Returns(CatalogoConPropuesto());
            A.CallTo(() => servicio.LeerClienteCrear("1", "15191", "0")).Returns(new ClienteCrear
            {
                Empresa = "1", Cliente = "15191", Contacto = "0", Direccion = "CALLE MAYOR, 1", PersonasContacto = new List<PersonaContactoDTO>()
            });
            CrearClienteViewModel vm = Alta(origenes);
            await vm.CargaOrigenesEnCurso;

            vm.AlLlegar(new ParametrosNavegacion { { "empresaParameter", "1" }, { "clienteParameter", "15191" }, { "contactoParameter", "0" } });
            await Task.Delay(300);

            Assert.IsNull(vm.ClienteOrigen);
        }

        // ---------- Ficha del cliente ----------

        [TestMethod]
        public async Task Ficha_ConOrigen_EnseniaDescripcionCampanaFechaYUsuario()
        {
            A.CallTo(() => origenes.LeerOrigen("1", "15191")).Returns(new OrigenDeClienteModel
            {
                Origen = META, Descripcion = "Lead de Meta", FechaOrigen = new DateTime(2026, 11, 5, 9, 30, 0), UsuarioOrigen = "NUEVAVISION\\Paloma"
            });
            OrigenClienteViewModel vm = Ficha();

            await vm.CargarAsync("1", "15191");

            Assert.IsTrue(vm.Disponible);
            Assert.AreEqual("Lead de Meta · campaña Harmonia (05/11/26, Paloma)", vm.TextoOrigen);
            Assert.AreEqual(META, vm.OpcionSeleccionada.Codigo);
            Assert.IsFalse(vm.Cambiado);
        }

        [TestMethod]
        public async Task Ficha_SinOrigen_DiceNinguno()
        {
            OrigenClienteViewModel vm = Ficha();

            await vm.CargarAsync("1", "15191");

            Assert.IsTrue(vm.Disponible, "Hay catálogo: se puede poner");
            Assert.AreEqual("(ninguno)", vm.TextoOrigen);
            Assert.IsNull(vm.OpcionSeleccionada.Codigo);
        }

        [TestMethod]
        public async Task Ficha_ApiSinLosEndpoints_SeEscondeSinError()
        {
            A.CallTo(() => origenes.LeerOrigenes()).Returns(new List<OrigenClienteModel>());
            OrigenClienteViewModel vm = Ficha();

            await vm.CargarAsync("1", "15191");

            Assert.IsFalse(vm.Disponible);
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Ficha_ApiQueFalla_SeEscondeSinError()
        {
            A.CallTo(() => origenes.LeerOrigenes()).Throws(new Exception("caída"));
            A.CallTo(() => origenes.LeerOrigen(A<string>._, A<string>._)).Throws(new Exception("caída"));
            OrigenClienteViewModel vm = Ficha();

            await vm.CargarAsync("1", "15191");

            Assert.IsFalse(vm.Disponible);
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Ficha_Vendedor_CambiaElOrigen_HaceElPut()
        {
            EnGrupo(OrigenClienteViewModel.GRUPO_VENDEDORES);
            A.CallTo(() => origenes.GuardarOrigen("1", "15191", META)).Returns(new OrigenDeClienteModel { Origen = META });
            OrigenClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191");
            Assert.IsTrue(vm.PuedeCambiar);

            vm.OpcionSeleccionada = vm.Opciones.Single(o => o.Codigo == META);
            Assert.IsTrue(vm.PuedeGuardar);
            await vm.GuardarAsync();

            A.CallTo(() => origenes.GuardarOrigen("1", "15191", META)).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.TieneOrigen);
            StringAssert.StartsWith(vm.TextoOrigen, "Lead de Meta · campaña Harmonia (");
            Assert.AreEqual("Origen guardado.", vm.Mensaje);
            Assert.IsFalse(vm.Cambiado);
        }

        [TestMethod]
        public async Task Ficha_QuitarElOrigen_MandaNull()
        {
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            A.CallTo(() => origenes.LeerOrigen("1", "15191")).Returns(new OrigenDeClienteModel { Origen = META });
            A.CallTo(() => origenes.GuardarOrigen("1", "15191", null)).Returns(new OrigenDeClienteModel());
            OrigenClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191");

            vm.OpcionSeleccionada = vm.Opciones[0];
            await vm.GuardarAsync();

            A.CallTo(() => origenes.GuardarOrigen("1", "15191", null)).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.TieneOrigen);
            Assert.AreEqual("Origen quitado.", vm.Mensaje);
        }

        [TestMethod]
        public async Task Ficha_LaApiRechaza_EnseniaSuMensajeYVuelveAlAnterior()
        {
            EnGrupo(Constantes.GruposSeguridad.TIENDAS);
            A.CallTo(() => origenes.GuardarOrigen(A<string>._, A<string>._, A<string>._))
                .Throws(new UnauthorizedAccessException("Solo Dirección, Administración, Vendedores y Tiendas pueden cambiar el origen."));
            OrigenClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191");

            vm.OpcionSeleccionada = vm.Opciones.Single(o => o.Codigo == META);
            await vm.GuardarAsync();

            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "Solo Dirección, Administración, Vendedores y Tiendas pueden cambiar el origen.");
            Assert.IsNull(vm.OpcionSeleccionada.Codigo, "Vuelve a «(ninguno)», lo que había");
            Assert.IsFalse(vm.TieneOrigen);
        }

        [TestMethod]
        public async Task Ficha_SinPermiso_SoloLectura_YNoGuarda()
        {
            OrigenClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191");

            Assert.IsTrue(vm.SoloLectura);
            vm.OpcionSeleccionada = vm.Opciones.Single(o => o.Codigo == META);
            Assert.IsFalse(vm.PuedeGuardar);
            await vm.GuardarAsync();

            A.CallTo(() => origenes.GuardarOrigen(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual(1, errores.Count);
        }

        [TestMethod]
        public void Texto_SinFechaNiUsuario_SoloDescripcionYCampana()
        {
            string texto = OrigenClienteViewModel.Texto(new OrigenDeClienteModel { Origen = META }, Catalogo());

            Assert.AreEqual("Lead de Meta · campaña Harmonia", texto);
        }
    }
}
