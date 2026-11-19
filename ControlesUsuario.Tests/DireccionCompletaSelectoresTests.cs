using ControlesUsuario.Models;
using ControlesUsuario.Services;
using ControlesUsuario.ViewModels;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Newtonsoft.Json;
using System;
using System.Threading;
using System.Windows;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// NestoAPI#879 (novedad 1320): el selector de clientes y el de direcciones de entrega pintan la dirección COMPLETA (Dirección +
    /// Dirección 2, la compone la API); con la API vieja, la dirección de siempre. Solo lectura: nada de esto se guarda.
    /// </summary>
    [TestClass]
    public class DireccionCompletaSelectoresTests
    {
        [TestMethod]
        public void DireccionParaMostrar_LaCompletaYSiNoLlegaLaDeSiempre()
        {
            Assert.AreEqual("CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ", DireccionParaMostrar.Elegir("CALLE MAYOR, 3", "CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ"));
            Assert.AreEqual("CALLE MAYOR, 3", DireccionParaMostrar.Elegir("CALLE MAYOR, 3", null));
            Assert.AreEqual("CALLE MAYOR, 3", DireccionParaMostrar.Elegir("CALLE MAYOR, 3", "  "));
        }

        [TestMethod]
        public void ClienteDTO_LeeDireccionCompletaDeLaApi_YNoMandaDireccionMostrar()
        {
            var cliente = JsonConvert.DeserializeObject<ClienteDTO>("{\"cliente\":\"42143\",\"direccion\":\"CALLE MAYOR, 3\",\"direccionCompleta\":\"CALLE MAYOR, 3, 1º D\"}");
            Assert.AreEqual("CALLE MAYOR, 3, 1º D", cliente.DireccionMostrar);
            Assert.AreEqual("CALLE MAYOR, 3", cliente.direccion, "La dirección de siempre no cambia");
            StringAssert.DoesNotMatch(JsonConvert.SerializeObject(cliente), new System.Text.RegularExpressions.Regex("DireccionMostrar"));

            var apiVieja = JsonConvert.DeserializeObject<ClienteDTO>("{\"cliente\":\"42143\",\"direccion\":\"CALLE MAYOR, 3\"}");
            Assert.AreEqual("CALLE MAYOR, 3", apiVieja.DireccionMostrar);
        }

        [TestMethod]
        public void ClienteDTO_ElFiltroEncuentraPorLaDireccion2()
        {
            var cliente = new ClienteDTO { direccion = "CALLE MAYOR, 3", direccionCompleta = "CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ ESTUDIO" };
            Assert.IsTrue(cliente.Contains("lohé"));
        }

        [TestMethod]
        public void DireccionesEntregaCliente_DireccionMostrar()
        {
            var direccion = JsonConvert.DeserializeObject<DireccionesEntregaCliente>("{\"contacto\":\"1\",\"direccion\":\"CALLE MENOR, 5\",\"direccionCompleta\":\"CALLE MENOR, 5, NAVE 4\"}");
            Assert.AreEqual("CALLE MENOR, 5, NAVE 4", direccion.DireccionMostrar);
            Assert.AreEqual("CALLE MENOR, 5", new DireccionesEntregaCliente { direccion = "CALLE MENOR, 5" }.DireccionMostrar);
            Assert.IsTrue(direccion.Contains("nave"));
        }

        [TestMethod]
        public void SelectorCliente_PintaLaDireccionCompletaDelClienteElegido()
        {
            EnSta(() =>
            {
                var vm = new SelectorClienteViewModel(A.Fake<IConfiguracion>(), A.Fake<ISelectorClienteService>());
                var sut = new SelectorCliente(vm, A.Fake<IServicioNavegacion>());
                sut.Measure(new Size(400, 400));
                sut.Arrange(new Rect(0, 0, 400, 400));

                vm.listaClientes.ElementoSeleccionado = new ClienteDTO
                {
                    empresa = "1", cliente = "42143", contacto = "0", nombre = "LOHÉ ESTUDIO",
                    direccion = "CALLE MAYOR, 3", direccionCompleta = "CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ ESTUDIO"
                };
                // Que la vinculación se aplique (va por el Dispatcher)
                System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

                Assert.AreEqual("LOHÉ ESTUDIO", sut.txtNombreCliente.Text, "El nombre, como siempre");
                Assert.AreEqual("CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ ESTUDIO", sut.txtDireccionCliente.Text);
            });
        }

        private static void EnSta(Action prueba)
        {
            Exception error = null;
            var hilo = new Thread(() =>
            {
                try
                {
                    prueba();
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            if (!hilo.Join(TimeSpan.FromSeconds(30)))
            {
                Assert.Fail("La prueba no ha terminado");
            }
            if (error != null)
            {
                throw new AssertFailedException(error.ToString(), error);
            }
        }
    }
}
