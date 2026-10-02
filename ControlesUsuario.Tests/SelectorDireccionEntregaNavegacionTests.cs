using ControlesUsuario;
using ControlesUsuario.Models;
using ControlesUsuario.Services;
using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Models.Nesto.Models;
using Prism.Regions;
using System;
using System.Reflection;
using System.Threading;
using System.Windows;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#490 (4C.4): «Crear contacto» del selector de direcciones abre la ficha de cliente nueva con el
    /// NIF y el nombre de la dirección. Escrita contra el IRegionManager de Prism ANTES de migrar la
    /// navegación. El botón «Editar» de cada dirección busca su fila en la lista y no se puede probar sin pantalla.
    /// </summary>
    [TestClass]
    public class SelectorDireccionEntregaNavegacionTests
    {
        [TestMethod]
        public void CrearContacto_AbreLaFichaDeClienteConNifYNombre()
        {
            var navegacion = A.Fake<IRegionManager>();
            NavigationParameters recibidos = null;
            A.CallTo(() => navegacion.RequestNavigate("MainRegion", "CrearClienteView", A<NavigationParameters>._))
                .Invokes((string _, string _, NavigationParameters p) => recibidos = p);
            Exception error = null;

            var hilo = new Thread(() =>
            {
                try
                {
                    var sut = new SelectorDireccionEntrega(navegacion, new WeakReferenceMessenger(), A.Fake<IConfiguracion>(), A.Fake<IServicioDireccionesEntrega>());
                    sut.DireccionCompleta = new DireccionesEntregaCliente { nif = "B12345678", nombre = "PELUQUERÍA PRUEBA" };
                    typeof(SelectorDireccionEntrega)
                        .GetMethod("btnCrearContacto_Click", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(sut, new object[] { null, new RoutedEventArgs() });
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            hilo.SetApartmentState(ApartmentState.STA);
            hilo.Start();
            hilo.Join();

            Assert.IsNull(error, error?.ToString());
            Assert.IsNotNull(recibidos);
            Assert.AreEqual("B12345678", recibidos.GetValue<string>("nifParameter"));
            Assert.AreEqual("PELUQUERÍA PRUEBA", recibidos.GetValue<string>("nombreParameter"));
        }
    }
}
