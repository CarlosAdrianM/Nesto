using ControlesUsuario.Models;
using ControlesUsuario.Services;
using ControlesUsuario.ViewModels;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Models;
using Prism.Mvvm;
using Prism.Regions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Windows.Data;

namespace ControlesUsuario.Tests
{
    [TestClass]
    public class SelectorClienteTests : BindableBase
    {
        [TestMethod]
        public void SelectorCliente_SiCambiaElCliente_CogeElContactoPorDefecto()
        {
            // Arrange
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
            ClienteDTO cliente175450 = new()
            {
                empresa = "1",
                cliente = "17545",
                contacto = "0",
                nombre = "Cliente 17545",
                direccion = "Calle contacto principal",
            };
            A.CallTo(() => servicio.CargarCliente("1", "17545", null)).Returns(cliente175450);
            ClienteDTO cliente175454 = new()
            {
                empresa = "1",
                cliente = "17545",
                contacto = "4",
                nombre = "Cliente 17545",
                direccion = "Calle falsa 123",
            };
            A.CallTo(() => servicio.CargarCliente("1", "17545", "4")).Returns(cliente175454);
            ClienteDTO cliente347910 = new()
            {
                empresa = "1",
                cliente = "34791",
                contacto = "0",
                nombre = "Cliente 34791",
                direccion = "Calle falsa 34791",
            };
            A.CallTo(() => servicio.CargarCliente("1", "34791", null)).Returns(cliente347910);
            A.CallTo(() => servicio.CargarCliente("1", "34791", "4")).Returns((ClienteDTO)null);
            A.CallTo(() => servicio.BuscarClientes("1", null, "34791")).Returns(new ObservableCollection<ClienteDTO> { cliente347910 });
            SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);

            // Act
            vm.cargarCliente("1", "17545", null); // Cargamos un cliente
            vm.cargarCliente("1", "17545", "4"); // Cambiamos a otra dirección de entrega (se modifica contactoSeleccionado)
            vm.cargarCliente("1", "34791", "4"); // Cargamos otro cliente en el que no existe esa dirección de entrega


            // Assert
            Assert.AreEqual("0", (vm.listaClientes.ElementoSeleccionado as ClienteDTO).contacto);
            //Assert.AreEqual("0", vm.contactoSeleccionado);
        }

        [TestMethod]
        public void SelectorCliente_SiCambiaElClienteEnElBinding_ActualizaElClienteEnElSelector()
        {
            // Arrange
            SelectorCliente sut = null;
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            A.CallTo(() => servicio.CargarCliente(A<string>._, A<string>._, A<string>._)).Returns(new ClienteDTO
            {
                cliente = "10",
                contacto = "0"
            });
            SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);
            PedidoFiltrableSeleccionado = new PedidoFiltrable();
            // Crear los enlaces con las dependency properties del control de usuario
            var clienteBinding = new Binding("PedidoFiltrableSeleccionado.Cliente") { Source = this };
            var contactoBinding = new Binding("PedidoFiltrableSeleccionado.Contacto") { Source = this };

            Thread thread = new Thread(() =>
            {
                // Establecer los enlaces en el control de usuario
                sut = new SelectorCliente(vm, regionManager);
                BindingOperations.SetBinding(sut, SelectorCliente.ClienteProperty, clienteBinding);
                BindingOperations.SetBinding(sut, SelectorCliente.ContactoProperty, contactoBinding);

                // Act: Cambiar el valor de pedidoSeleccionado.Cliente
                PedidoFiltrableSeleccionado.Cliente = "10";

                // Assert: Verificar que la propiedad Cliente del control de usuario se ha actualizado correctamente
                Assert.AreEqual("10", sut.Cliente);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Esperar un breve momento para que se ejecute la operación asincrónica
            Thread.Sleep(500);
        }

        [TestMethod]
        public void SelectorCliente_SiCambiaElContactoEnElBinding_ActualizaElContactoEnElSelector()
        {
            // Arrange
            SelectorCliente sut = null;
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            A.CallTo(() => servicio.CargarCliente(A<string>._, A<string>._, A<string>._)).Returns(new ClienteDTO
            {
                cliente = "10",
                contacto = "2"
            });

            SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);
            List<PedidoVentaDTO> listaPedidos = new List<PedidoVentaDTO>();
            PedidoFiltrableSeleccionado = new(); ;

            Thread thread = new Thread(() =>
            {
                // Crear los enlaces con las dependency properties del control de usuario
                var clienteBinding = new Binding("PedidoFiltrableSeleccionado.Cliente") { Source = this };
                var contactoBinding = new Binding("PedidoFiltrableSeleccionado.Contacto") { Source = this };
                // Establecer los enlaces en el control de usuario
                sut = new SelectorCliente(vm, regionManager);
                BindingOperations.SetBinding(sut, SelectorCliente.ClienteProperty, clienteBinding);
                BindingOperations.SetBinding(sut, SelectorCliente.ContactoProperty, contactoBinding);

                // Act: Cambiar el valor de pedidoSeleccionado.Cliente
                PedidoFiltrableSeleccionado.Contacto = "2";

                // Assert: Verificar que la propiedad Cliente del control de usuario se ha actualizado correctamente
                Assert.AreEqual("2", sut.Contacto);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Esperar un breve momento para que se ejecute la operación asincrónica
            Thread.Sleep(500);
        }

        [TestMethod]
        public void SelectorCliente_SiCambiaElClienteEnElSelector_ActualizaElClienteEnElBinding()
        {
            // Arrange
            SelectorCliente sut = null;
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            A.CallTo(() => servicio.CargarCliente("1", "10000", A<string>._)).Returns(new ClienteDTO
            {
                empresa = "1",
                cliente = "10000",
                contacto = "1"
            });
            A.CallTo(() => servicio.CargarCliente("1", "12345", A<string>._)).Returns(new ClienteDTO
            {
                empresa = "1",
                cliente = "12345",
                contacto = "0"
            });
            SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);
            PedidoFiltrableSeleccionado = new() {
                Cliente = "12345",
                Contacto = "0"
            };

            Thread thread = new Thread(() =>
            {
                // Crear los enlaces con las dependency properties del control de usuario
                var clienteBinding = new Binding("PedidoFiltrableSeleccionado.Cliente") { Source = this };
                var contactoBinding = new Binding("PedidoFiltrableSeleccionado.Contacto") { Source = this };
                // Establecer los enlaces en el control de usuario
                sut = new SelectorCliente(vm, regionManager);
                BindingOperations.SetBinding(sut, SelectorCliente.ClienteProperty, clienteBinding);
                BindingOperations.SetBinding(sut, SelectorCliente.ContactoProperty, contactoBinding);

                // Act: Cambiar el cliente en el selector
                sut.Contacto = null; // en ejecución esto lo hace txtFiltro.KeyUp
                sut.Cliente = "10000";

                // Esperar y bombear mensajes del Dispatcher para que se complete la operación asincrónica
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Threading.Timer((state) =>
                {
                    ((System.Windows.Threading.DispatcherFrame)state).Continue = false;
                }, frame, 500, System.Threading.Timeout.Infinite);
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                timer.Dispose();

                // Assert: Verificar que la propiedad Cliente del pedido seleccionado se ha actualizado correctamente
                Assert.AreEqual("10000", PedidoFiltrableSeleccionado.Cliente);
                Assert.AreEqual("1", PedidoFiltrableSeleccionado.Contacto);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void SelectorCliente_SiCambiaElContactoEnElSelector_ActualizaElContactoEnElBinding()
        {
            // Arrange
            SelectorCliente sut = null;
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);
            List<PedidoVentaDTO> listaPedidos = new List<PedidoVentaDTO>();
            PedidoFiltrableSeleccionado = new();

            Thread thread = new Thread(() =>
            {
                // Crear los enlaces con las dependency properties del control de usuario
                var clienteBinding = new Binding("PedidoFiltrableSeleccionado.Cliente") { Source = this };
                var contactoBinding = new Binding("PedidoFiltrableSeleccionado.Contacto") { Source = this };
                // Establecer los enlaces en el control de usuario
                sut = new SelectorCliente(vm, regionManager);
                BindingOperations.SetBinding(sut, SelectorCliente.ClienteProperty, clienteBinding);
                BindingOperations.SetBinding(sut, SelectorCliente.ContactoProperty, contactoBinding);

                // Act: Cambiar el valor de pedidoSeleccionado.Cliente
                sut.Contacto = "2";

                // Assert: Verificar que la propiedad Cliente del control de usuario se ha actualizado correctamente
                Assert.AreEqual("2", PedidoFiltrableSeleccionado.Contacto);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Esperar un breve momento para que se ejecute la operación asincrónica
            Thread.Sleep(500);
        }

        [TestMethod]
        public void SelectorCliente_SiNoHayNingunClienteSeleccionadoYSeSeleccionaUnClienteEnElSelector_ActualizaElClienteEnElBinding()
        {
            // Arrange
            SelectorCliente sut = null;
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
            IRegionManager regionManager = A.Fake<IRegionManager>();
            A.CallTo(() => servicio.CargarCliente(A<string>._, A<string>._, A<string>._)).Returns(new ClienteDTO
            {
                cliente = "10000",
                contacto = "1"
            });
            SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);
            PedidoFiltrableSeleccionado = new();

            Thread thread = new Thread(() =>
            {
                // Crear los enlaces con las dependency properties del control de usuario
                var clienteBinding = new Binding("PedidoFiltrableSeleccionado.Cliente") { Source = this };
                var contactoBinding = new Binding("PedidoFiltrableSeleccionado.Contacto") { Source = this };
                // Establecer los enlaces en el control de usuario
                sut = new SelectorCliente(vm, regionManager);
                BindingOperations.SetBinding(sut, SelectorCliente.ClienteProperty, clienteBinding);
                BindingOperations.SetBinding(sut, SelectorCliente.ContactoProperty, contactoBinding);

                // Act: Cambiar el cliente en el selector
                sut.Contacto = ""; // para que ponerlo a null salte OnContactoChanged
                sut.Contacto = null; // en ejecución esto lo hace txtFiltro.KeyUp
                sut.Cliente = "10000";

                // Esperar y bombear mensajes del Dispatcher para que se complete la operación asincrónica
                var frame = new System.Windows.Threading.DispatcherFrame();
                var timer = new System.Threading.Timer((state) =>
                {
                    ((System.Windows.Threading.DispatcherFrame)state).Continue = false;
                }, frame, 500, System.Threading.Timeout.Infinite);
                System.Windows.Threading.Dispatcher.PushFrame(frame);
                timer.Dispose();

                // Assert: Verificar que la propiedad Cliente del pedido seleccionado se ha actualizado correctamente
                Assert.AreEqual("10000", PedidoFiltrableSeleccionado.Cliente);
                Assert.AreEqual("1", PedidoFiltrableSeleccionado.Contacto);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
        }

        [TestMethod]
        public void SelectorCliente_SiCambiaElElementoEnlazado_ActualizaElClienteYContactoEnElSelector()
        {
            // Arrange
            SelectorCliente sut = null;
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.Vendedor)).Returns("NV");
            ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
            A.CallTo(() => servicio.CargarCliente(A<string>._, "111", null)).Returns(new ClienteDTO
            {
                cliente = "111",
                contacto = "0"
            });
            A.CallTo(() => servicio.CargarCliente(A<string>._, "222", null)).Returns(new ClienteDTO
            {
                cliente = "222",
                contacto = "1"
            });
            A.CallTo(() => servicio.BuscarClientes(A<string>._, A<string>._, "111")).Returns(new ObservableCollection<ClienteDTO>
            {
                new ClienteDTO
                {
                    cliente = "111",
                    contacto = "0"
                }
            });
            A.CallTo(() => servicio.BuscarClientes(A<string>._, A<string>._, "222")).Returns(new ObservableCollection<ClienteDTO>
            {
                new ClienteDTO
                {
                    cliente = "222",
                    contacto = "1"
                }
            });
            IRegionManager regionManager = A.Fake<IRegionManager>();
            SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);            
            PedidoFiltrable pedidoSeleccionadoInicial = new PedidoFiltrable
            {
                Cliente = "111",
                Contacto = "0"
            };
            PedidoFiltrable pedidoSeleccionadoNuevo = new PedidoFiltrable
            {
                Cliente = "222",
                Contacto = "1"
            };
            var listaPedidos = new List<PedidoFiltrable>()
            {
                pedidoSeleccionadoInicial,
                pedidoSeleccionadoNuevo
            };
            
            PedidoFiltrableSeleccionado = pedidoSeleccionadoInicial;

            Thread thread = new Thread(() =>
            {
                sut = new SelectorCliente(vm, regionManager);
                // Crear los enlaces con las dependency properties del control de usuario
                var clienteBinding = new Binding("PedidoFiltrableSeleccionado.Cliente") { Source = this };
                var contactoBinding = new Binding("PedidoFiltrableSeleccionado.Contacto") { Source = this };
                // Establecer los enlaces en el control de usuario                
                BindingOperations.SetBinding(sut, SelectorCliente.ClienteProperty, clienteBinding);
                BindingOperations.SetBinding(sut, SelectorCliente.ContactoProperty, contactoBinding);
                // Acceder al SelectorDireccionEntrega a través de la propiedad pública
                SelectorDireccionEntrega selectorDireccionEntrega = sut.ControlSelectorDireccionEntrega;
                // Realizar los bindings necesarios en SelectorDireccionEntrega
                BindingOperations.SetBinding(selectorDireccionEntrega, SelectorDireccionEntrega.ConfiguracionProperty,
                    new Binding(nameof(SelectorCliente.Configuracion)) { Source = sut, Mode = BindingMode.OneWay });
                BindingOperations.SetBinding(selectorDireccionEntrega, SelectorDireccionEntrega.EmpresaProperty,
                    new Binding(nameof(SelectorCliente.Empresa)) { Source = sut, Mode = BindingMode.TwoWay });
                BindingOperations.SetBinding(selectorDireccionEntrega, SelectorDireccionEntrega.ClienteProperty,
                    new Binding(nameof(SelectorCliente.Cliente)) { Source = sut, Mode = BindingMode.TwoWay });
                BindingOperations.SetBinding(selectorDireccionEntrega, SelectorDireccionEntrega.SeleccionadaProperty,
                    new Binding(nameof(SelectorCliente.Contacto)) { Source = sut, Mode = BindingMode.TwoWay });

                // Act: 
                PedidoFiltrableSeleccionado = listaPedidos
                    .Single(l => l.Cliente == pedidoSeleccionadoNuevo.Cliente && l.Contacto == pedidoSeleccionadoNuevo.Contacto);

                // Assert
                Assert.AreEqual("222", sut.Cliente);
                Assert.AreEqual("1", sut.Contacto);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Esperar un breve momento para que se ejecute la operación asincrónica
            Thread.Sleep(1000);
        }

        [TestMethod]
        public void SelectorCliente_SiCambiaElContactoDelElementoEnlazadoYDespuesSeleccionamosOtroElementoEnlazado_ActualizaElClienteYContactoEnElElementoEnlazadoAnteriorPeroNoEnElNuevo()
        {
            // Arrange
            SelectorCliente sut = null;
            IConfiguracion configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.Vendedor)).Returns("NV");
            ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
            // pongo este mock porque entra con contactoSeleccionado == null, pero no debería ser así, debería entrar con 0 o 2
            A.CallTo(() => servicio.CargarCliente(A<string>._, "111", null)).Returns(new ClienteDTO 
            {
                cliente = "111",
                contacto = "0"
            });
            A.CallTo(() => servicio.CargarCliente(A<string>._, "111", "0")).Returns(new ClienteDTO
            {
                cliente = "111",
                contacto = "0"
            });
            A.CallTo(() => servicio.CargarCliente(A<string>._, "111", "2")).Returns(new ClienteDTO
            {
                cliente = "111",
                contacto = "2"
            });
            A.CallTo(() => servicio.CargarCliente(A<string>._, "222", null)).Returns(new ClienteDTO
            {
                cliente = "222",
                contacto = "1"
            });
            A.CallTo(() => servicio.BuscarClientes(A<string>._, A<string>._, "111")).Returns(new ObservableCollection<ClienteDTO>
            {
                new ClienteDTO
                {
                    cliente = "111",
                    contacto = "0"
                },
                new ClienteDTO
                {
                    cliente = "111",
                    contacto = "2"
                }
            });
            A.CallTo(() => servicio.BuscarClientes(A<string>._, A<string>._, "222")).Returns(new ObservableCollection<ClienteDTO>
            {
                new ClienteDTO
                {
                    cliente = "222",
                    contacto = "1"
                }
            });
            IRegionManager regionManager = A.Fake<IRegionManager>();
            SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);
            PedidoFiltrable pedidoSeleccionadoInicial = new PedidoFiltrable
            {
                Cliente = "111",
                Contacto = "0"
            };
            PedidoFiltrable pedidoSeleccionadoNuevo = new PedidoFiltrable
            {
                Cliente = "222",
                Contacto = "1"
            };
            var listaPedidos = new List<PedidoFiltrable>()
            {
                pedidoSeleccionadoInicial,
                pedidoSeleccionadoNuevo
            };

            MiColeccionFiltrable = new ColeccionFiltrable(listaPedidos)
            {
                TieneDatosIniciales = true,
                SeleccionarPrimerElemento = true
            };

            Thread thread = new Thread(() =>
            {
                sut = new SelectorCliente(vm, regionManager);
                // Crear los enlaces con las dependency properties del control de usuario
                var clienteBinding = new Binding("MiColeccionFiltrable.ElementoSeleccionado.Cliente") { Source = this };
                var contactoBinding = new Binding("MiColeccionFiltrable.ElementoSeleccionado.Contacto") { Source = this };
                // Establecer los enlaces en el control de usuario
                BindingOperations.SetBinding(sut, SelectorCliente.ClienteProperty, clienteBinding);
                BindingOperations.SetBinding(sut, SelectorCliente.ContactoProperty, contactoBinding);
                
                // Act: cambiamos el contacto en el elemento seleccionado y luego cambiamos a otro elemento seleccionado diferente
                sut.Contacto = "2";
                MiColeccionFiltrable.ElementoSeleccionado = MiColeccionFiltrable.Lista.Single(c => ((PedidoFiltrable)c).Cliente == "222");

                // Assert
                Assert.AreEqual("222", sut.Cliente);
                Assert.AreEqual("1", sut.Contacto);
                Assert.AreEqual("111", pedidoSeleccionadoInicial.Cliente);
                Assert.AreEqual("2", pedidoSeleccionadoInicial.Contacto); // Este es el punto clave, que en el pedido 111 sí hemos cambiado el contacto aposta y debe mantenerse el cambio
                Assert.AreEqual("222", pedidoSeleccionadoNuevo.Cliente);
                Assert.AreEqual("1", pedidoSeleccionadoNuevo.Contacto);
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            // Esperar un breve momento para que se ejecute la operación asincrónica
            Thread.Sleep(1000);
        }

        //[TestMethod]
        //public void SelectorCliente_SiCambiaElElementoEnlazado_SeActualizaLaListaDeDirecciones()
        //{
        //    // COMENTO EL TEST PORQUE EN EL SELECTOR DE DIRECCIONES DE ENTREGA NO HAY UN SERVICIO Y ATACA DIRECTAMENTE AL SERVIDOR REAL

        //    // Arrange
        //    SelectorCliente sut = null;
        //    IConfiguracion configuracion = A.Fake<IConfiguracion>();
        //    A.CallTo(() => configuracion.leerParametro(A<string>._, Parametros.Claves.Vendedor)).Returns("NV");
        //    ISelectorClienteService servicio = A.Fake<ISelectorClienteService>();
        //    A.CallTo(() => servicio.CargarCliente(A<string>._, "111", null)).Returns(new ClienteDTO
        //    {
        //        cliente = "111",
        //        contacto = "0"
        //    });
        //    A.CallTo(() => servicio.CargarCliente(A<string>._, "222", null)).Returns(new ClienteDTO
        //    {
        //        cliente = "222",
        //        contacto = "1"
        //    });
        //    A.CallTo(() => servicio.CargarCliente(A<string>._, "222", "0")).Returns(new ClienteDTO
        //    {
        //        cliente = "222",
        //        contacto = "0"
        //    });
        //    A.CallTo(() => servicio.BuscarClientes(A<string>._, A<string>._, "111")).Returns(new ObservableCollection<ClienteDTO>
        //    {
        //        new ClienteDTO
        //        {
        //            cliente = "111",
        //            contacto = "0"
        //        },

        //    });
        //    A.CallTo(() => servicio.BuscarClientes(A<string>._, A<string>._, "222")).Returns(new ObservableCollection<ClienteDTO>
        //    {
        //        new ClienteDTO
        //        {
        //            cliente = "222",
        //            contacto = "0"
        //        },
        //        new ClienteDTO
        //        {
        //            cliente = "222",
        //            contacto = "1"
        //        },
        //        new ClienteDTO
        //        {
        //            cliente = "222",
        //            contacto = "2"
        //        }
        //    });
        //    IRegionManager regionManager = A.Fake<IRegionManager>();
        //    SelectorClienteViewModel vm = new SelectorClienteViewModel(configuracion, servicio);
        //    PedidoFiltrable pedidoSeleccionadoInicial = new PedidoFiltrable
        //    {
        //        Cliente = "111",
        //        Contacto = "0"
        //    };
        //    PedidoFiltrable pedidoSeleccionadoNuevo = new PedidoFiltrable
        //    {
        //        Cliente = "222",
        //        Contacto = "1"
        //    };
        //    var listaPedidos = new List<PedidoFiltrable>()
        //    {
        //        pedidoSeleccionadoInicial,
        //        pedidoSeleccionadoNuevo
        //    };

        //    PedidoFiltrableSeleccionado = pedidoSeleccionadoInicial;

        //    Thread thread = new Thread(() =>
        //    {
        //        sut = new SelectorCliente(vm, regionManager);
        //        // Crear los enlaces con las dependency properties del control de usuario
        //        var clienteBinding = new Binding("PedidoFiltrableSeleccionado.Cliente") { Source = this };
        //        var contactoBinding = new Binding("PedidoFiltrableSeleccionado.Contacto") { Source = this };
        //        // Establecer los enlaces en el control de usuario                
        //        BindingOperations.SetBinding(sut, SelectorCliente.ClienteProperty, clienteBinding);
        //        BindingOperations.SetBinding(sut, SelectorCliente.ContactoProperty, contactoBinding);
        //        // Acceder al SelectorDireccionEntrega a través de la propiedad pública
        //        SelectorDireccionEntrega selectorDireccionEntrega = sut.ControlSelectorDireccionEntrega;
        //        // Realizar los bindings necesarios en SelectorDireccionEntrega
        //        BindingOperations.SetBinding(selectorDireccionEntrega, SelectorDireccionEntrega.ConfiguracionProperty,
        //            new Binding(nameof(SelectorCliente.Configuracion)) { Source = sut, Mode = BindingMode.OneWay });
        //        BindingOperations.SetBinding(selectorDireccionEntrega, SelectorDireccionEntrega.EmpresaProperty,
        //            new Binding(nameof(SelectorCliente.Empresa)) { Source = sut, Mode = BindingMode.TwoWay });
        //        BindingOperations.SetBinding(selectorDireccionEntrega, SelectorDireccionEntrega.ClienteProperty,
        //            new Binding(nameof(SelectorCliente.Cliente)) { Source = sut, Mode = BindingMode.TwoWay });
        //        BindingOperations.SetBinding(selectorDireccionEntrega, SelectorDireccionEntrega.SeleccionadaProperty,
        //            new Binding(nameof(SelectorCliente.Contacto)) { Source = sut, Mode = BindingMode.TwoWay });

        //        // Act: 
        //        PedidoFiltrableSeleccionado = listaPedidos
        //            .Single(l => l.Cliente == pedidoSeleccionadoNuevo.Cliente && l.Contacto == pedidoSeleccionadoNuevo.Contacto);

        //        // Assert
        //        Assert.AreEqual(3, sut.NumeroDeDirecciones());
        //    });

        //    thread.SetApartmentState(ApartmentState.STA);
        //    thread.Start();
        //    thread.Join();

        //    // Esperar un breve momento para que se ejecute la operación asincrónica
        //    Thread.Sleep(1000);
        //}



        private PedidoFiltrable _pedidoFiltrableSeleccionado;
        public PedidoFiltrable PedidoFiltrableSeleccionado
        {
            get => _pedidoFiltrableSeleccionado; 
            set => SetProperty(ref _pedidoFiltrableSeleccionado, value);
        }

        //private PedidoVentaDTO _pedidoVentaSeleccionado;
        //public PedidoVentaDTO PedidoVentaSeleccionado
        //{
        //    get => _pedidoVentaSeleccionado;
        //    set => SetProperty(ref _pedidoVentaSeleccionado, value);
        //}

        private ColeccionFiltrable _miColeccionFiltrable;
        public ColeccionFiltrable MiColeccionFiltrable
        {
            get => _miColeccionFiltrable;
            set => SetProperty(ref _miColeccionFiltrable, value);
        }
    }



    public class PedidoFiltrable : BindableBase, IFiltrableItem
    {
        private string _cliente;
        public string Cliente 
        { 
            get => _cliente;
            set => SetProperty(ref _cliente, value);
        }
        private string _contacto;
        public string Contacto 
        { 
            get => _contacto;
            set => SetProperty(ref _contacto, value);
        }
        public bool Contains(string filtro) => Cliente == filtro;
    }
}