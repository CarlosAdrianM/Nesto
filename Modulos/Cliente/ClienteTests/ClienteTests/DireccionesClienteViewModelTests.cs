using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// NestoAPI#879 (corte 3): pestaña «Direcciones» de la ficha. Facturación = ficha principal; entrega por defecto = ContactoDefecto de la
    /// principal; «Usar como entrega por defecto» la cambia (con confirmación). La de facturación no se cambia aquí.
    /// </summary>
    [TestClass]
    public class DireccionesClienteViewModelTests
    {
        private readonly IDireccionesClienteService servicio = A.Fake<IDireccionesClienteService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public DireccionesClienteViewModelTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(true);
        }

        private static List<DireccionClienteModel> Direcciones42143(string entrega = "0") => new()
        {
            new DireccionClienteModel { contacto = "0  ", clientePrincipal = true, esDireccionPorDefecto = entrega == "0", nombre = "LOHÉ ESTUDIO",
                direccion = "CALLE MAYOR, 3", direccionCompleta = "CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ ESTUDIO", codigoPostal = "28001", poblacion = "MADRID", provincia = "MADRID" },
            new DireccionClienteModel { contacto = "1  ", esDireccionPorDefecto = entrega == "1", nombre = "LOHÉ ALMACÉN", direccion = "CALLE MENOR, 5" }
        };

        private DireccionesClienteViewModel Pestana() => new(servicio, dialogos);

        [TestMethod]
        public async Task Cargar_MarcaFacturacionYEntrega_ConLaDireccionCompleta()
        {
            A.CallTo(() => servicio.Leer("1", "42143")).Returns(Direcciones42143());
            DireccionesClienteViewModel vm = Pestana();

            await vm.CargarAsync("1  ", "42143     ");

            Assert.AreEqual(2, vm.Direcciones.Count);
            DireccionClienteFila principal = vm.Direcciones[0];
            Assert.AreEqual("0", principal.Contacto);
            Assert.AreEqual("Facturación · Entrega por defecto", principal.Marcas);
            Assert.AreEqual("CALLE MAYOR, 3, LOCAL · RÓTULO: LOHÉ ESTUDIO", principal.Direccion);
            Assert.AreEqual("28001 MADRID (MADRID)", principal.Poblacion);
            Assert.IsFalse(principal.PuedeSerEntregaPorDefecto, "Ya lo es: sin botón");
            Assert.AreEqual(string.Empty, vm.Direcciones[1].Marcas);
            Assert.AreEqual("CALLE MENOR, 5", vm.Direcciones[1].Direccion, "Sin direccionCompleta (API vieja): la de siempre");
            Assert.IsTrue(vm.Direcciones[1].PuedeSerEntregaPorDefecto);
            Assert.IsFalse(vm.HayMensaje);
        }

        [TestMethod]
        public async Task Cargar_SinEntregaPorDefecto_LoDice()
        {
            A.CallTo(() => servicio.Leer("1", "42143")).Returns(Direcciones42143(entrega: null));
            DireccionesClienteViewModel vm = Pestana();
            await vm.CargarAsync("1", "42143");
            StringAssert.Contains(vm.Mensaje, "no tiene ninguna dirección de entrega por defecto");
        }

        [TestMethod]
        public async Task Cargar_ErrorDeLaApi_MensajeSinLanzar()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._)).ThrowsAsync(new Exception("sin red"));
            DireccionesClienteViewModel vm = Pestana();
            await vm.CargarAsync("1", "42143");
            Assert.AreEqual("sin red", vm.Mensaje);
            Assert.AreEqual(0, vm.Direcciones.Count);
        }

        [TestMethod]
        public async Task HacerEntregaPorDefecto_Confirmado_LlamaALaApiYRecarga()
        {
            A.CallTo(() => servicio.Leer("1", "42143")).ReturnsNextFromSequence(Direcciones42143("0"), Direcciones42143("1"));
            DireccionesClienteViewModel vm = Pestana();
            await vm.CargarAsync("1", "42143");

            await vm.HacerEntregaPorDefectoAsync(vm.Direcciones[1]);

            A.CallTo(() => servicio.CambiarEntregaPorDefecto("1", "42143", "1")).MustHaveHappenedOnceExactly();
            Assert.IsTrue(vm.Direcciones[1].EsEntregaPorDefecto, "Se ha vuelto a leer");
            Assert.AreEqual("Facturación", vm.Direcciones[0].Marcas, "La de facturación sigue siendo la principal");
            StringAssert.Contains(vm.Mensaje, "la del contacto 1");
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task HacerEntregaPorDefecto_SinConfirmar_NoHaceNada()
        {
            A.CallTo(() => servicio.Leer("1", "42143")).Returns(Direcciones42143());
            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).Returns(false);
            DireccionesClienteViewModel vm = Pestana();
            await vm.CargarAsync("1", "42143");

            await vm.HacerEntregaPorDefectoAsync(vm.Direcciones[1]);

            A.CallTo(() => servicio.CambiarEntregaPorDefecto(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task HacerEntregaPorDefecto_LaQueYaLoEs_NoPregunta()
        {
            A.CallTo(() => servicio.Leer("1", "42143")).Returns(Direcciones42143());
            DireccionesClienteViewModel vm = Pestana();
            await vm.CargarAsync("1", "42143");

            Assert.IsFalse(vm.HacerEntregaPorDefectoCommand.CanExecute(vm.Direcciones[0]));
            await vm.HacerEntregaPorDefectoAsync(vm.Direcciones[0]);

            A.CallTo(() => dialogos.ShowConfirmationAsync(A<string>._, A<string>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.CambiarEntregaPorDefecto(A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task HacerEntregaPorDefecto_LaApiLoRechaza_ErrorDeUsted()
        {
            A.CallTo(() => servicio.Leer("1", "42143")).Returns(Direcciones42143());
            A.CallTo(() => servicio.CambiarEntregaPorDefecto(A<string>._, A<string>._, A<string>._))
                .ThrowsAsync(new Exception("El contacto 1 del cliente 42143 está anulado: no puede ser la dirección de entrega por defecto."));
            DireccionesClienteViewModel vm = Pestana();
            await vm.CargarAsync("1", "42143");

            await vm.HacerEntregaPorDefectoAsync(vm.Direcciones[1]);

            Assert.AreEqual(1, errores.Count);
            StringAssert.StartsWith(errores[0], "No se ha podido cambiar la dirección de entrega por defecto: El contacto 1");
            Assert.IsFalse(vm.EstaOcupado);
        }

        [TestMethod]
        public void TextoConfirmacion_DeUstedConContactoYDireccion()
        {
            var fila = new DireccionClienteFila(Direcciones42143()[1]);
            string texto = DireccionesClienteViewModel.TextoConfirmacion("42143", fila);
            StringAssert.StartsWith(texto, "¿Quiere que la dirección de entrega por defecto del cliente 42143 sea la del contacto 1 (CALLE MENOR, 5)?");
            StringAssert.Contains(texto, "La dirección de facturación no cambia.");
        }

        [TestMethod]
        public void Servicio_UrlYMensajes()
        {
            Assert.AreEqual("PlantillaVentas/DireccionesEntrega?empresa=1&clienteDirecciones=42143", DireccionesClienteService.UrlLeer("1 ", " 42143"));
            Assert.IsTrue(DireccionesClienteService.EsMensajeDeLaApi("\"No existe el cliente 99999.\""));
            Assert.IsFalse(DireccionesClienteService.EsMensajeDeLaApi("{\"Message\":\"No HTTP resource was found that matches the request URI\"}"));
        }
    }
}
