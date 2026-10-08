using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Rapports;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity;
using idDescripcion = Nesto.Modulos.Rapports.RapportsModel.SeguimientoClienteDTO.idDescripcion;

namespace RapportsTests
{
    /// <summary>
    /// Al abrir Rapports se pedía dos veces seguidas GET api/Clientes/SugerenciasContacto (07/10/26: dos registros a
    /// 50 ms): una al poner el tipo en AlLlegar (antes OnNavigatedTo) y otra cuando el selector de subgrupos, al cargar, pone
    /// «(Todos los subgrupos)» (cadena vacía) donde había Nothing. Sin grupo y «todos» es lo mismo: una sola llamada.
    /// Cambiar de tipo o de grupo sigue recargando.
    /// </summary>
    [TestClass]
    public class CargaSugerenciasContactoTests
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
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.Vendedor)).Returns(Task.FromResult("MPP"));
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.UltTipoSeguimientoCliente)).Returns(Task.FromResult("T"));
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
        public void Abrir_PideLasSugerenciasUnaSolaVez()
        {
            Abrir();

            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarSugerenciasContacto("MPP", "Teléfono", A<string>.That.Matches(g => string.IsNullOrEmpty(g))))
                .MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Abrir_SiElSelectorTerminaAntesQueElTipo_NoPideNadaSinTipoYPideUnaVez()
        {
            var leerTipo = new TaskCompletionSource<string>();
            A.CallTo(() => _configuracion.leerParametro(A<string>._, Parametros.Claves.UltTipoSeguimientoCliente)).Returns(leerTipo.Task);
            var vm = new ListaRapportsViewModel(A.Fake<IServicioNavegacion>(), _configuracion, _servicio,
                A.Fake<IUnityContainer>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());

            vm.AlLlegar(new ParametrosNavegacion());
            vm.GrupoSubgrupoSeleccionado = string.Empty;
            leerTipo.SetResult("T");

            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ElegirGrupoAntesDeTenerTipo_NoPideConElTipoVacio()
        {
            var vm = new ListaRapportsViewModel(A.Fake<IServicioNavegacion>(), _configuracion, _servicio,
                A.Fake<IUnityContainer>(), A.Fake<IServicioDialogos>(), new WeakReferenceMessenger());

            vm.GrupoSubgrupoSeleccionado = "PEL/PEL";

            A.CallTo(() => _servicio.CargarSugerenciasContacto(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public void CambiarDeTipo_VuelveAPedirlas()
        {
            var vm = Abrir();
            Fake.ClearRecordedCalls(_servicio);

            vm.TipoRapportCambiaCommand.Execute("V");

            A.CallTo(() => _servicio.CargarSugerenciasContacto("MPP", "Visita", A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void CambiarDeGrupo_VuelveAPedirlasConEseGrupo()
        {
            var vm = Abrir();
            Fake.ClearRecordedCalls(_servicio);

            vm.GrupoSubgrupoSeleccionado = "PEL/PEL";
            vm.GrupoSubgrupoSeleccionado = string.Empty;

            A.CallTo(() => _servicio.CargarSugerenciasContacto("MPP", "Teléfono", "PEL/PEL")).MustHaveHappenedOnceExactly();
            A.CallTo(() => _servicio.CargarSugerenciasContacto("MPP", "Teléfono", string.Empty)).MustHaveHappenedOnceExactly();
        }
    }
}
