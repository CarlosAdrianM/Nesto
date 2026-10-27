using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Services;
using Nesto.Modulos.Rapports;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#551 (sugerencia 760, DLopez): 1) con un cliente elegido en el selector, «Crear Rapport» sale con ese cliente y su
    /// contacto, como en NestoApp; 2) «Mis rapports de hoy» (o borrar el cliente del selector) vuelve a la lista del día sin
    /// cerrar la pestaña.
    /// </summary>
    [TestClass]
    public class ClientePrecargadoYRapportsDeHoyTests
    {
        private IRapportService _servicio;
        private IConfiguracion _configuracion;
        private IServicioDialogos _dialogos;
        private readonly List<(string vendedor, DateTime fecha)> _cargasPorFecha = new List<(string, DateTime)>();
        private readonly List<(string cliente, string contacto)> _cargasPorCliente = new List<(string, string)>();

        [TestInitialize]
        public void Inicializar()
        {
            _servicio = A.Fake<IRapportService>();
            A.CallTo(() => _servicio.cargarListaRapports(A<string>._, A<string>._, A<string>._))
                .Invokes((string _, string cliente, string contacto) => _cargasPorCliente.Add((cliente, contacto)))
                .Returns(Task.FromResult(new ObservableCollection<SeguimientoClienteDTO> { new SeguimientoClienteDTO { Id = 7, Cliente = "15191", Contacto = "2" } }));
            A.CallTo(() => _servicio.cargarListaRapports(A<string>._, A<DateTime>._))
                .Invokes((string vendedor, DateTime fecha) => _cargasPorFecha.Add((vendedor, fecha)))
                .Returns(Task.FromResult(new ObservableCollection<SeguimientoClienteDTO>()));
            A.CallTo(() => _servicio.CargarResumenVentasCliente(A<string>._, A<string>._, A<string>._))
                .Returns(Task.FromResult(new ResumenVentasClienteResponse { Datos = new List<VentaClienteResumenDTO>() }));
            _configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => _configuracion.usuario).Returns("carlos");
            A.CallTo(() => _configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult("NV"));
            _dialogos = A.Fake<IServicioDialogos>();
        }

        private ListaRapportsViewModel Nuevo() =>
            new ListaRapportsViewModel(A.Fake<IServicioNavegacion>(), _configuracion, _servicio, _dialogos, new WeakReferenceMessenger(),
                A.Fake<IServicioSugerenciasProductos>());

        #region 1) El cliente elegido ya puesto al crear el rapport

        [TestMethod]
        public async Task CrearRapport_ConClienteEnElSelector_SaleConEseClienteYSuContacto()
        {
            var vm = Nuevo();
            vm.vendedor = "NV";
            vm.clienteSeleccionado = "15191     "; // como viene de la BD
            vm.contactoSeleccionado = "2  ";
            await vm.CargarListaRapportsAsync(); // «Cargar Rapports» (el 2 de la captura)
            _cargasPorCliente.Clear();

            await vm.CrearRapportAsync(null, null, "V"); // «Crear Rapport» (el 3), sin parámetro

            SeguimientoClienteDTO nuevo = vm.listaRapports.Single(r => r.Id == 0);
            Assert.AreEqual("15191", nuevo.Cliente);
            Assert.AreEqual("2", nuevo.Contacto);
            Assert.AreEqual("V", nuevo.Tipo);
            Assert.AreEqual("NV", nuevo.Vendedor);
            Assert.AreEqual(0, _cargasPorCliente.Count, "Sus rapports ya estaban cargados: no se vuelven a pedir");
            Assert.AreEqual(2, vm.listaRapports.Count, "El rapport nuevo se añade a la lista del cliente");
        }

        [TestMethod]
        public async Task CrearRapport_SinClienteEnElSelector_SigueSaliendoEnBlanco()
        {
            var vm = Nuevo();

            await vm.CrearRapportAsync(null, null, "V");

            SeguimientoClienteDTO nuevo = vm.listaRapports.Single();
            Assert.IsNull(nuevo.Cliente);
            Assert.IsNull(nuevo.Contacto);
        }

        [TestMethod]
        public async Task CrearRapport_DesdeClientesParaContactar_SigueCargandoLosRapportsDeEseCliente()
        {
            // El camino de siempre (y el de los cheques regalo) no cambia: con cliente explícito se cargan sus rapports antes
            var vm = Nuevo();
            vm.clienteSeleccionado = "99999";

            await vm.CrearRapportAsync("15191", "0", "T");

            Assert.AreEqual(1, _cargasPorCliente.Count);
            Assert.AreEqual("15191", _cargasPorCliente[0].cliente);
            Assert.AreEqual("15191", vm.clienteSeleccionado);
            Assert.AreEqual("15191", vm.listaRapports.Single(r => r.Id == 0).Cliente);
            Assert.AreEqual("0", vm.listaRapports.Single(r => r.Id == 0).Contacto);
        }

        #endregion

        #region 2) Volver a los rapports del día sin cerrar la pestaña

        [TestMethod]
        public async Task VerRapportsDeHoy_VaciaElSelector_YCargaLaListaDelDiaDelUsuario()
        {
            var vm = Nuevo();
            vm.clienteSeleccionado = "15191";
            vm.contactoSeleccionado = "2";
            vm.ClienteCompleto = new object();
            vm.fechaSeleccionada = DateTime.Today.AddDays(-3);
            await vm.CargarListaRapportsAsync();
            _cargasPorFecha.Clear();
            _cargasPorCliente.Clear();

            await vm.VerRapportsDeHoyAsync();

            Assert.IsNull(vm.clienteSeleccionado);
            Assert.IsNull(vm.contactoSeleccionado);
            Assert.IsNull(vm.ClienteCompleto);
            Assert.IsFalse(vm.HayClienteSeleccionado);
            Assert.AreEqual(DateTime.Today, vm.fechaSeleccionada);
            Assert.AreEqual(1, _cargasPorFecha.Count, "Una sola carga, la del día (el setter del cliente no lanza otra)");
            Assert.AreEqual("carlos", _cargasPorFecha[0].vendedor, "Filtrar por usuario: los rapports del usuario");
            Assert.AreEqual(DateTime.Today, _cargasPorFecha[0].fecha);
            Assert.AreEqual(0, _cargasPorCliente.Count);
        }

        [TestMethod]
        public void VerRapportsDeHoyCommand_SePuedeEjecutarSiempre()
        {
            var vm = Nuevo();

            Assert.IsTrue(vm.VerRapportsDeHoyCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task BorrarElClienteDelSelector_VuelveLaListaDelDia()
        {
            // Lo que ya pasaba con "" y ahora también con Nothing (lo que manda el SelectorCliente al quedarse sin cliente)
            var vm = Nuevo();
            vm.clienteSeleccionado = "15191";
            await vm.CargarListaRapportsAsync();
            _cargasPorFecha.Clear();

            vm.clienteSeleccionado = null;
            await Task.Yield();

            Assert.IsNull(vm.ClienteCompleto);
            Assert.AreEqual(1, _cargasPorFecha.Count);
            Assert.AreEqual(DateTime.Today, _cargasPorFecha[0].fecha);
        }

        [TestMethod]
        public async Task CargarListaRapports_ConElSelectorEnBlanco_EsLaListaDelDia_NoLaDeUnClienteVacio()
        {
            // Antes, con "" (el selector vacío) se pedía api/Rapports del cliente "" porque solo se miraba IsNothing
            var vm = Nuevo();
            vm.clienteSeleccionado = "";
            await Task.Yield();
            _cargasPorFecha.Clear();
            _cargasPorCliente.Clear();

            await vm.CargarListaRapportsAsync();

            Assert.AreEqual(0, _cargasPorCliente.Count);
            Assert.AreEqual(1, _cargasPorFecha.Count);
        }

        #endregion
    }
}
