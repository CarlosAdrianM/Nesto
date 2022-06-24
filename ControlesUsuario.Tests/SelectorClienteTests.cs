using ControlesUsuario.Models;
using ControlesUsuario.Services;
using ControlesUsuario.ViewModels;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System.Collections.ObjectModel;

namespace ControlesUsuario.Tests
{
    [TestClass]
    public class SelectorClienteTests
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
            Assert.AreEqual("0", vm.contactoSeleccionado);
        }
    }

}
