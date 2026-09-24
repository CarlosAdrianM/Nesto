using ControlesUsuario.Models;
using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Infrastructure.Shared;
using Nesto.Models.Nesto.Models;
using CommunityToolkit.Mvvm.Messaging;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace ControlesUsuario.Tests
{
    /// <summary>
    /// Nesto#472: al crear un cliente, SelectorDireccionEntrega preseleccionaba su dirección con un
    /// Single() dentro de un handler async void. Si el contacto no estaba en la lista —o estaba con
    /// el relleno del char ("0  " frente a "0")— la excepción subía al manejador global y tumbaba la
    /// ventana justo después de crear el cliente (Sancho, 09/09/26, cinco veces seguidas).
    /// </summary>
    [TestClass]
    public class SelectorDireccionEntregaClienteCreadoTests
    {
        private static DireccionesEntregaCliente Direccion(string contacto, bool porDefecto = false)
        {
            return new DireccionesEntregaCliente { contacto = contacto, esDireccionPorDefecto = porDefecto };
        }

        #region BuscarDireccionDelContacto (puro, sin WPF)

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void BuscarDireccionDelContacto_ElContactoConRellenoDelChar_SeEncuentra()
        {
            // Lo que rompía: la API devuelve "0  " y el cliente creado trae "0".
            var lista = new ObservableCollection<IFiltrableItem> { Direccion("0  "), Direccion("1  ") };

            var resultado = SelectorDireccionEntrega.BuscarDireccionDelContacto(lista, "1");

            Assert.IsNotNull(resultado);
            Assert.AreEqual("1  ", resultado.contacto);
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void BuscarDireccionDelContacto_NoDistingueMayusculas()
        {
            var lista = new ObservableCollection<IFiltrableItem> { Direccion("a") };

            Assert.IsNotNull(SelectorDireccionEntrega.BuscarDireccionDelContacto(lista, "A "));
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void BuscarDireccionDelContacto_SiNoEsta_DevuelveNullSinLanzar()
        {
            var lista = new ObservableCollection<IFiltrableItem> { Direccion("0  ") };

            Assert.IsNull(SelectorDireccionEntrega.BuscarDireccionDelContacto(lista, "9"));
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void BuscarDireccionDelContacto_ListaOContactoNulos_DevuelveNull()
        {
            var lista = new ObservableCollection<IFiltrableItem> { Direccion("0  ") };

            Assert.IsNull(SelectorDireccionEntrega.BuscarDireccionDelContacto(null, "0"));
            Assert.IsNull(SelectorDireccionEntrega.BuscarDireccionDelContacto(lista, null));
            Assert.IsNull(SelectorDireccionEntrega.BuscarDireccionDelContacto(lista, "   "));
        }

        #endregion

        #region ProcesarClienteCreado (control real en hilo STA)

        // Lo que se puede mirar del control fuera de su hilo STA: una foto tomada dentro del hilo.
        private sealed class Resultado
        {
            public Exception Error;
            public string Empresa;
            public string Cliente;
            public string ContactoSeleccionado;
            public string Seleccionada;
        }

        private static Resultado EjecutarEnSta(IEnumerable<DireccionesEntregaCliente> direcciones, Clientes clienteCreado)
        {
            var resultado = new Resultado();

            Thread thread = new Thread(() =>
            {
                try
                {
                    var servicioDirecciones = A.Fake<IServicioDireccionesEntrega>();
                    A.CallTo(() => servicioDirecciones.ObtenerDireccionesEntrega(A<string>.Ignored, A<string>.Ignored, A<decimal?>.Ignored))
                        .Returns(direcciones);
                    var sut = new SelectorDireccionEntrega(A.Fake<IRegionManager>(), new WeakReferenceMessenger(), A.Fake<IConfiguracion>(), servicioDirecciones);

                    // El servicio falso devuelve una tarea ya completada, así que el await no
                    // cambia de hilo y el control sigue en su hilo STA.
                    sut.ProcesarClienteCreado(clienteCreado).GetAwaiter().GetResult();

                    resultado.Empresa = sut.Empresa;
                    resultado.Cliente = sut.Cliente;
                    resultado.ContactoSeleccionado = sut.DireccionCompleta?.contacto;
                    resultado.Seleccionada = sut.Seleccionada;
                }
                catch (Exception ex)
                {
                    resultado.Error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            return resultado;
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void ProcesarClienteCreado_ContactoConRelleno_PreseleccionaSuDireccion()
        {
            var cliente = new Clientes { Empresa = "1  ", Nº_Cliente = "12345", Contacto = "1" };

            var r = EjecutarEnSta(new[] { Direccion("0  ", porDefecto: true), Direccion("1  ") }, cliente);

            Assert.IsNull(r.Error, r.Error?.ToString());
            Assert.AreEqual("1", r.Empresa);
            Assert.AreEqual("12345", r.Cliente);
            Assert.AreEqual("1  ", r.ContactoSeleccionado);
            Assert.AreEqual("1", r.Seleccionada, "Seleccionada se recorta sola al cambiar DireccionCompleta");
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void ProcesarClienteCreado_ContactoQueNoEstaEnLaLista_NoLanzaYDejaLaSeleccionQueHubiera()
        {
            // Antes: Single() -> "Sequence contains no matching element" -> DispatcherUnhandledException.
            var cliente = new Clientes { Empresa = "1  ", Nº_Cliente = "12345", Contacto = "9" };

            var r = EjecutarEnSta(new[] { Direccion("0  ", porDefecto: true) }, cliente);

            Assert.IsNull(r.Error, r.Error?.ToString());
            // cargarDatos preselecciona la dirección por defecto cuando no hay ninguna; eso se conserva.
            Assert.AreEqual("0  ", r.ContactoSeleccionado);
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void ProcesarClienteCreado_ClienteNulo_NoHaceNada()
        {
            var r = EjecutarEnSta(new[] { Direccion("0  ") }, null);

            Assert.IsNull(r.Error, r.Error?.ToString());
            Assert.IsNull(r.ContactoSeleccionado);
        }

        #endregion

        #region Mensaje ClienteCreado (Nesto#490 4C.1: Messenger en vez de IEventAggregator)

        private static Resultado EnviarClienteCreadoEnSta(Action<SelectorDireccionEntrega, IMessenger> preparar, Clientes clienteCreado)
        {
            var resultado = new Resultado();
            Thread thread = new Thread(() =>
            {
                try
                {
                    var servicioDirecciones = A.Fake<IServicioDireccionesEntrega>();
                    A.CallTo(() => servicioDirecciones.ObtenerDireccionesEntrega(A<string>.Ignored, A<string>.Ignored, A<decimal?>.Ignored))
                        .Returns(new[] { Direccion("0  ", porDefecto: true), Direccion("1  ") });
                    var messenger = new WeakReferenceMessenger();
                    var sut = new SelectorDireccionEntrega(A.Fake<IRegionManager>(), messenger, A.Fake<IConfiguracion>(), servicioDirecciones);
                    preparar(sut, messenger);

                    // Se entrega en el hilo de quien envía (como ThreadOption.PublisherThread de Prism) y,
                    // con el servicio falso ya completado, el handler async termina aquí mismo.
                    messenger.Send(new ClienteCreadoMensaje(clienteCreado));

                    resultado.Empresa = sut.Empresa;
                    resultado.Cliente = sut.Cliente;
                    resultado.ContactoSeleccionado = sut.DireccionCompleta?.contacto;
                }
                catch (Exception ex)
                {
                    resultado.Error = ex;
                }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            return resultado;
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void MensajeClienteCreado_ConElControlCargado_PreseleccionaLaDireccionDelContacto()
        {
            var cliente = new Clientes { Empresa = "1  ", Nº_Cliente = "12345", Contacto = "1" };

            var r = EnviarClienteCreadoEnSta((sut, m) => sut.SuscribirMensajes(), cliente);

            Assert.IsNull(r.Error, r.Error?.ToString());
            Assert.AreEqual("12345", r.Cliente);
            Assert.AreEqual("1  ", r.ContactoSeleccionado);
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void MensajeClienteCreado_LoadedDosVeces_NoLanzaYSigueSuscrito()
        {
            // WPF puede lanzar Loaded dos veces; Prism admitía suscribirse dos veces, el Messenger lanza.
            var cliente = new Clientes { Empresa = "1  ", Nº_Cliente = "12345", Contacto = "1" };

            var r = EnviarClienteCreadoEnSta((sut, m) =>
            {
                sut.SuscribirMensajes();
                sut.SuscribirMensajes();
                Assert.IsTrue(m.IsRegistered<ClienteCreadoMensaje>(sut));
            }, cliente);

            Assert.IsNull(r.Error, r.Error?.ToString());
            Assert.AreEqual("1  ", r.ContactoSeleccionado);
        }

        [TestMethod]
        [TestCategory("SelectorDireccionEntrega")]
        public void MensajeClienteCreado_TrasUnloaded_NoLlega()
        {
            var cliente = new Clientes { Empresa = "1  ", Nº_Cliente = "12345", Contacto = "1" };

            var r = EnviarClienteCreadoEnSta((sut, m) =>
            {
                sut.SuscribirMensajes();
                sut.DesuscribirMensajes();
            }, cliente);

            Assert.IsNull(r.Error, r.Error?.ToString());
            Assert.IsNull(r.Cliente, "Sin suscripción el control no se entera del cliente creado");
            Assert.IsNull(r.ContactoSeleccionado);
        }

        #endregion
    }
}
