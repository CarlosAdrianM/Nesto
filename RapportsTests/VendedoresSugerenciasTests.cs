using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Rapports;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Unity;
using idDescripcion = Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO.idDescripcion;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#521: un jefe de ventas (su equipo) o Dirección (todos) elige de qué vendedor ver los «clientes para contactar».
    /// Con un solo vendedor no hay desplegable. Cambiarlo recarga con ese vendedor; los rapports que se crean siguen
    /// siendo del vendedor del usuario.
    /// </summary>
    [TestClass]
    public class VendedoresSugerenciasTests
    {
        private IRapportService _servicio;
        private IConfiguracion _configuracion;

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IRapportService>();
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _servicio.CargarListaTipos()).Returns(new List<idDescripcion>
            {
                new idDescripcion("V", "Visita"),
                new idDescripcion("T", "Teléfono")
            });
            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._))
                .ReturnsLazily((string vendedor, string tipo, string grupo) => Task.FromResult(RespuestaDe(vendedor)));
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.Vendedor)).Returns(Task.FromResult("ASH"));
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.UltTipoSeguimientoCliente)).Returns(Task.FromResult("T"));
        }

        private static SugerenciasContactoRespuesta RespuestaDe(string vendedor) => new SugerenciasContactoRespuesta
        {
            Vendedor = vendedor,
            Ritmo = new RitmoContactoDTO { Frase = "Frase de " + vendedor },
            Sugerencias = new List<ClienteProbabilidadVenta> { new ClienteProbabilidadVenta { cliente = "cliente de " + vendedor, contacto = "0" } }
        };

        private void ConVendedores(params string[] vendedores)
        {
            A.CallTo(() => _servicio.CargarVendedoresSugerencias()).Returns(Task.FromResult(
                vendedores.Select(v => new VendedorSugerencias { Vendedor = v, Nombre = "Nombre " + v }).ToList()));
        }

        private ListaRapportsViewModel Abrir()
        {
            var vm = new ListaRapportsViewModel(A.Fake<IServicioNavegacion>(), _configuracion, _servicio,
                A.Fake<IUnityContainer>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());
            vm.AlLlegar(new ParametrosNavegacion());
            // Lo que hace SelectorSubgrupoProducto al terminar de cargar: elige «(Todos los subgrupos)»
            vm.GrupoSubgrupoSeleccionado = string.Empty;
            return vm;
        }

        [TestMethod]
        public void Abrir_ConUnSoloVendedor_SinDesplegableYConElSuyo()
        {
            ConVendedores("ASH");

            var vm = Abrir();

            Assert.IsFalse(vm.HayVariosVendedoresSugerencias);
            Assert.AreEqual("ASH", vm.VendedorSugerencias);
            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarSugerenciasContacto("ASH", "Teléfono", A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Abrir_SinVendedoresODesdeUnaApiSinElEndpoint_SinDesplegable()
        {
            A.CallTo(() => _servicio.CargarVendedoresSugerencias()).Returns(Task.FromResult(new List<VendedorSugerencias>()));

            var vm = Abrir();

            Assert.IsFalse(vm.HayVariosVendedoresSugerencias);
            A.CallTo(() => _servicio.CargarSugerenciasContacto("ASH", "Teléfono", A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Abrir_JefeDeVentas_ConDesplegablePorDefectoElSuyoYUnaSolaCarga()
        {
            ConVendedores("ASH", "DV", "JE");

            var vm = Abrir();

            Assert.IsTrue(vm.HayVariosVendedoresSugerencias);
            Assert.AreEqual(3, vm.ListaVendedoresSugerencias.Count);
            Assert.AreEqual("ASH", vm.VendedorSugerencias);
            Assert.AreEqual("Nombre DV (DV)", vm.ListaVendedoresSugerencias[1].Descripcion);
            A.CallTo(() => _servicio.CargarVendedoresSugerencias()).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void VolverALaPestana_NoVuelveAPedirLosVendedoresNiPierdeLaEleccion()
        {
            ConVendedores("ASH", "DV", "JE");
            var vm = Abrir();
            vm.VendedorSugerencias = "DV";
            Fake.ClearRecordedCalls(_servicio);

            vm.AlLlegar(new ParametrosNavegacion());

            A.CallTo(() => _servicio.CargarVendedoresSugerencias()).MustNotHaveHappened();
            A.CallTo(() => _servicio.CargarSugerenciasContacto("DV", A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void CambiarElVendedor_RecargaListaYRitmoDeEseVendedor()
        {
            ConVendedores("ASH", "DV", "JE");
            var vm = Abrir();
            Fake.ClearRecordedCalls(_servicio);

            vm.VendedorSugerencias = "DV";

            A.CallTo(() => _servicio.CargarSugerenciasContacto("DV", "Teléfono", string.Empty)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("cliente de DV", vm.ListaClientesProbabilidad.Single().cliente);
            Assert.AreEqual("Frase de DV", vm.RitmoContacto.Frase);
        }

        [TestMethod]
        public void CambiarElVendedor_ElMismoONada_NoRecarga()
        {
            ConVendedores("ASH", "DV", "JE");
            var vm = Abrir();
            Fake.ClearRecordedCalls(_servicio);

            vm.VendedorSugerencias = "ASH";
            vm.VendedorSugerencias = null;
            vm.VendedorSugerencias = " ";

            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
            Assert.AreEqual("ASH", vm.VendedorSugerencias);
        }

        [TestMethod]
        public void CambiarElVendedor_ElTipoYElGrupoSiguenPidiendoConElElegido()
        {
            ConVendedores("ASH", "DV", "JE");
            var vm = Abrir();
            vm.VendedorSugerencias = "JE";
            Fake.ClearRecordedCalls(_servicio);

            vm.TipoRapportCambiaCommand.Execute("V");
            vm.GrupoSubgrupoSeleccionado = "PEL/PEL";

            A.CallTo(() => _servicio.CargarSugerenciasContacto("JE", "Visita", A<string>._)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => _servicio.CargarSugerenciasContacto("ASH", A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void CambiarElVendedor_SiLaListaAnteriorLlegaTarde_NoPisaALaDelElegido()
        {
            ConVendedores("ASH", "DV", "JE");
            var delJefe = new TaskCompletionSource<SugerenciasContactoRespuesta>();
            A.CallTo(() => _servicio.CargarSugerenciasContacto("ASH", A<string>._, A<string>._)).Returns(delJefe.Task);
            var vm = Abrir();

            vm.VendedorSugerencias = "DV";
            delJefe.SetResult(RespuestaDe("ASH"));

            Assert.AreEqual("cliente de DV", vm.ListaClientesProbabilidad.Single().cliente);
            Assert.AreEqual("Frase de DV", vm.RitmoContacto.Frase);
            Assert.IsFalse(vm.IsLoadingClientesProbabilidad);
        }

        [TestMethod]
        public void CrearRapport_ConOtroVendedorElegido_SigueSiendoDelVendedorDelUsuario()
        {
            ConVendedores("ASH", "DV", "JE");
            A.CallTo(() => _servicio.cargarListaRapports(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ObservableCollection<SeguimientoClienteDTO>()));
            A.CallTo(() => _servicio.CargarResumenVentasCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ResumenVentasClienteResponse { Datos = new List<VentaClienteResumenDTO>() }));
            var vm = Abrir();
            vm.VendedorSugerencias = "DV";

            vm.cmdCrearRapport.Execute(vm.ListaClientesProbabilidad.Single());

            SeguimientoClienteDTO nuevo = vm.listaRapports.Single(r => r.Id == 0);
            Assert.AreEqual("ASH", nuevo.Vendedor);
            Assert.AreEqual("cliente de DV", nuevo.Cliente);
        }
    }
}
