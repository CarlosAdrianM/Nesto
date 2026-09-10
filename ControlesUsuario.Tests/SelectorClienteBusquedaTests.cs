using ControlesUsuario.Models;
using ControlesUsuario.Services;
using ControlesUsuario.ViewModels;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#473 (caracterización, 10/09/26): en Cajas, con un cliente ya seleccionado, buscar
    /// "luxury nador studio" no lo encontraba. Estos tests reproducen los pasos del usuario por las
    /// mismas entradas que usa el control —la barra de filtro (FijarFiltroCommand →
    /// HayQueCargarDatos → cargarCliente) y el clic en un resultado (brdCliente_MouseUp)— y el
    /// ViewModel busca bien en los tres casos, también con el código anterior a la issue. Es
    /// decir: el fallo NO está en SelectorClienteViewModel; queda en el control, en la vista o en
    /// su enlace con el ViewModel de Cajas. Se dejan para que, cuando se localice, el ViewModel
    /// siga cubierto.
    /// </summary>
    [TestClass]
    public class SelectorClienteBusquedaTests
    {
        private const string EMPRESA = "1";
        private const string TEXTO = "luxury nador studio";

        private static ClienteDTO Cliente(string numero, string nombre)
        {
            return new ClienteDTO { empresa = EMPRESA, cliente = numero, contacto = "0", nombre = nombre };
        }

        private static void EnSta(Action accion)
        {
            Exception error = null;
            Thread hilo = new Thread(() =>
            {
                try { accion(); }
                catch (Exception ex) { error = ex; }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();
            Assert.IsNull(error, error?.ToString());
        }

        private static (SelectorClienteViewModel vm, ISelectorClienteService servicio) Preparar()
        {
            var servicio = A.Fake<ISelectorClienteService>();
            A.CallTo(() => servicio.CargarCliente(EMPRESA, A<string>._, A<string>._)).Returns((ClienteDTO)null);
            A.CallTo(() => servicio.CargarCliente(EMPRESA, "12345", A<string>._)).Returns(Cliente("12345", "PELUQUERÍA DOCE"));
            A.CallTo(() => servicio.CargarCliente(EMPRESA, "41000", A<string>._)).Returns(Cliente("41000", "LUXURY NADOR STUDIO"));
            // "otra" devuelve DOS (con uno solo la colección lo selecciona ella misma); el texto, uno.
            A.CallTo(() => servicio.BuscarClientes(EMPRESA, A<string>._, "otra"))
                .Returns(new ObservableCollection<ClienteDTO> { Cliente("12345", "PELUQUERÍA DOCE"), Cliente("12346", "PELUQUERÍA OTRA") });
            A.CallTo(() => servicio.BuscarClientes(EMPRESA, A<string>._, TEXTO))
                .Returns(new ObservableCollection<ClienteDTO> { Cliente("41000", "LUXURY NADOR STUDIO") });

            var vm = new SelectorClienteViewModel(A.Fake<IConfiguracion>(), servicio);
            // Lo que hace SelectorCliente.xaml.cs: cuando la barra pide datos, se busca lo tecleado.
            vm.listaClientes.HayQueCargarDatos += () => vm.cargarCliente(EMPRESA, vm.listaClientes.Filtro, null);
            return (vm, servicio);
        }

        /// <summary>Lo que hace la BarraFiltro al pulsar Enter.</summary>
        private static void Enter(SelectorClienteViewModel vm, string texto)
        {
            if (vm.listaClientes.Filtro != texto)
            {
                vm.listaClientes.Filtro = texto;
            }
            vm.listaClientes.FijarFiltroCommand.Execute(texto);
        }

        /// <summary>Lo que hace brdCliente_MouseUp al pinchar un resultado.</summary>
        private static void Clic(SelectorClienteViewModel vm, IFiltrableItem elegido)
        {
            vm.listaClientes.ElementoSeleccionado = elegido;
            vm.listaClientes.Filtro = (elegido as ClienteDTO).cliente;
            vm.listaClientes.ListaOriginal?.Clear();
        }

        [TestMethod]
        public void BuscarOtraCosa_PincharUnResultado_YBuscarUnTexto_LoBusca()
        {
            EnSta(() =>
            {
                var (vm, servicio) = Preparar();
                Enter(vm, "otra");
                Assert.AreEqual(2, vm.listaClientes.Lista.Count, "la primera búsqueda enseña sus dos resultados");
                Clic(vm, vm.listaClientes.Lista.First());
                Assert.AreEqual("12345", (vm.listaClientes.ElementoSeleccionado as ClienteDTO).cliente);

                Enter(vm, TEXTO);

                A.CallTo(() => servicio.BuscarClientes(EMPRESA, A<string>._, TEXTO)).MustHaveHappenedOnceExactly();
                Assert.AreEqual("41000", (vm.listaClientes.ElementoSeleccionado as ClienteDTO)?.cliente,
                    "el único resultado del texto queda seleccionado");
            });
        }

        [TestMethod]
        public void CargarUnNumeroDirectamente_YBuscarUnTexto_LoBusca()
        {
            EnSta(() =>
            {
                var (vm, servicio) = Preparar();
                Enter(vm, "12345");
                Assert.AreEqual("12345", (vm.listaClientes.ElementoSeleccionado as ClienteDTO)?.cliente);

                Enter(vm, TEXTO);

                A.CallTo(() => servicio.BuscarClientes(EMPRESA, A<string>._, TEXTO)).MustHaveHappenedOnceExactly();
                Assert.AreEqual("41000", (vm.listaClientes.ElementoSeleccionado as ClienteDTO)?.cliente);
            });
        }

        [TestMethod]
        public void SinNadaSeleccionado_BuscarUnTexto_LoBusca()
        {
            EnSta(() =>
            {
                var (vm, servicio) = Preparar();

                Enter(vm, TEXTO);

                A.CallTo(() => servicio.BuscarClientes(EMPRESA, A<string>._, TEXTO)).MustHaveHappenedOnceExactly();
                Assert.AreEqual("41000", (vm.listaClientes.ElementoSeleccionado as ClienteDTO)?.cliente);
            });
        }
    }
}
