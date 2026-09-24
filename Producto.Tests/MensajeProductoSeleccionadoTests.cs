using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Events;
using Nesto.Modules.Producto;
using Nesto.Modules.Producto.Models;
using Nesto.Modules.Producto.ViewModels;
using Prism.Regions;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;

namespace Producto.Tests
{
    /// <summary>
    /// Nesto#490 (4C.1): al elegir un producto en la pantalla de Productos se avisa (al detalle de
    /// pedido de venta) por el IMessenger, antes ProductoSeleccionadoEvent de Prism.
    /// </summary>
    [TestClass]
    public class MensajeProductoSeleccionadoTests
    {
        [TestMethod]
        public void SeleccionarProducto_EnviaProductoSeleccionadoConElCodigo()
        {
            IMessenger messenger = new WeakReferenceMessenger();
            var vm = new ProductoViewModel(A.Fake<IRegionManager>(), A.Fake<IConfiguracion>(), A.Fake<IProductoService>(),
                messenger, A.Fake<IDialogService>(), A.Fake<IServicioAutenticacion>());
            vm.ProductoResultadoSeleccionado = new ProductoModel { Producto = "17404" };
            var recibidos = new List<string>();
            messenger.Register<ProductoSeleccionadoMensaje>(this, (r, m) => recibidos.Add(m.Value));

            try
            {
                vm.SeleccionarProductoCommand.Execute(null);
            }
            catch (NullReferenceException)
            {
                // Tras enviar el mensaje se cierra la pestaña de Productos buscando la vista activa
                // de la región, que en el test no existe. No es lo que se prueba aquí.
            }

            CollectionAssert.AreEqual(new[] { "17404" }, recibidos);
        }
    }
}
