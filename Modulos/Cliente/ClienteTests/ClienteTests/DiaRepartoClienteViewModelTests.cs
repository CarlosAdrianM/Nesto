using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#606 (NestoAPI#799): «Día de reparto propio» de la dirección de entrega en la ficha del cliente (excepción al día de su
    /// código postal). Se guarda al elegir; solo Dirección, Almacén y Administración; API vieja (404) = se esconde sin error.
    /// </summary>
    [TestClass]
    public class DiaRepartoClienteViewModelTests
    {
        private readonly IDiaRepartoClienteService servicio = A.Fake<IDiaRepartoClienteService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly List<string> errores = new();

        public DiaRepartoClienteViewModelTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>._)).Returns(false);
            A.CallTo(() => configuracion.usuario).Returns("Carlos");
        }

        private void EnGrupo(string grupo) => A.CallTo(() => configuracion.UsuarioEnGrupo(grupo)).Returns(true);

        private DiaRepartoClienteViewModel Ficha() => new(servicio, dialogos, configuracion);

        private static DiaRepartoClienteModel Miercoles() => new()
        {
            Empresa = "1", Cliente = "2077", Contacto = "3", Ruta = "03", Dia = "miércoles", Motivo = "Solo abre los miércoles",
            Usuario = "Alfredo", FechaModificacion = new DateTime(2026, 11, 3, 10, 0, 0)
        };

        private static OpcionDiaReparto Opcion(string ruta) => DiaRepartoClienteViewModel.OPCIONES.Find(ruta);

        [TestMethod]
        public async Task Cargar_ConExcepcion_SeleccionaElDiaYElMotivo_SinGuardar()
        {
            A.CallTo(() => servicio.Leer("1", "2077", "3")).Returns(Miercoles());
            DiaRepartoClienteViewModel vm = Ficha();

            await vm.CargarAsync("1  ", "2077      ", "3  ");

            Assert.AreEqual("03", vm.OpcionSeleccionada.Ruta);
            Assert.AreEqual("Solo abre los miércoles", vm.Motivo);
            Assert.IsTrue(vm.TieneExcepcion);
            Assert.AreEqual("miércoles (Solo abre los miércoles)", vm.TextoDia);
            Assert.IsTrue(vm.Disponible);
            Assert.IsFalse(vm.PuedeGuardarMotivo, "El motivo no ha cambiado");
            A.CallTo(() => servicio.Guardar(A<string>._, A<string>._, A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Cargar_Sin_Excepcion_204_PorSuCodigoPostal()
        {
            A.CallTo(() => servicio.Leer("1", "2077", "0")).Returns(Task.FromResult<DiaRepartoClienteModel>(null));
            DiaRepartoClienteViewModel vm = Ficha();

            await vm.CargarAsync("1", "2077", null);

            Assert.IsNull(vm.OpcionSeleccionada.Ruta);
            Assert.AreEqual(DiaRepartoClienteViewModel.POR_CODIGO_POSTAL, vm.OpcionSeleccionada.Nombre);
            Assert.AreEqual("Por su código postal", vm.TextoDia);
            Assert.IsFalse(vm.TieneExcepcion);
            Assert.IsTrue(vm.Disponible);
            A.CallTo(() => servicio.Guardar(A<string>._, A<string>._, A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task ElegirUnDia_GuardaAlMomento_ConElMotivo_YAvisaDeLosPedidosRecalculados()
        {
            EnGrupo(Constantes.GruposSeguridad.ALMACEN);
            A.CallTo(() => servicio.Leer("1", "2077", "3")).Returns(Task.FromResult<DiaRepartoClienteModel>(null));
            A.CallTo(() => servicio.Guardar("1", "2077", "3", "03", "Solo abre los miércoles"))
                .Returns(new DiaRepartoGuardadoModel { Ruta = "03", Dia = "miércoles", PedidosRecalculados = 4 });
            DiaRepartoClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "2077", "3");
            Assert.IsTrue(vm.SePuedeElegir);

            vm.Motivo = "Solo abre los miércoles";
            vm.OpcionSeleccionada = Opcion("03");
            await vm.GuardadoEnCurso;

            A.CallTo(() => servicio.Guardar("1", "2077", "3", "03", "Solo abre los miércoles")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Día de reparto propio guardado. Se han recalculado 4 pedidos pendientes.", vm.Mensaje);
            Assert.IsTrue(vm.TieneExcepcion);
            Assert.AreEqual("miércoles (Solo abre los miércoles)", vm.TextoDia);
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Quitar_PorSuCodigoPostal_MandaRutaNull_YAvisaQuitado()
        {
            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            A.CallTo(() => servicio.Leer("1", "2077", "3")).Returns(Miercoles());
            A.CallTo(() => servicio.Guardar("1", "2077", "3", null, null))
                .Returns(new DiaRepartoGuardadoModel { Ruta = null, Dia = null, PedidosRecalculados = 2 });
            DiaRepartoClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "2077", "3");

            vm.OpcionSeleccionada = Opcion(null);
            await vm.GuardadoEnCurso;

            A.CallTo(() => servicio.Guardar("1", "2077", "3", null, null)).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Día de reparto propio quitado. Se han recalculado 2 pedidos pendientes.", vm.Mensaje);
            Assert.IsFalse(vm.TieneExcepcion);
            Assert.IsNull(vm.Motivo);
            Assert.AreEqual("Por su código postal", vm.TextoDia);
        }

        [TestMethod]
        public async Task CambiarSoloElMotivo_SeGuardaConSuBoton()
        {
            EnGrupo(Constantes.GruposSeguridad.ADMINISTRACION);
            A.CallTo(() => servicio.Leer("1", "2077", "3")).Returns(Miercoles());
            A.CallTo(() => servicio.Guardar("1", "2077", "3", "03", "Cierra lunes y martes"))
                .Returns(new DiaRepartoGuardadoModel { Ruta = "03", Dia = "miércoles", PedidosRecalculados = 1 });
            DiaRepartoClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "2077", "3");

            vm.Motivo = "Cierra lunes y martes";
            Assert.IsTrue(vm.PuedeGuardarMotivo);
            await vm.GuardarMotivoAsync();

            A.CallTo(() => servicio.Guardar("1", "2077", "3", "03", "Cierra lunes y martes")).MustHaveHappenedOnceExactly();
            Assert.AreEqual("Día de reparto propio guardado. Se ha recalculado 1 pedido pendiente.", vm.Mensaje);
            Assert.IsFalse(vm.PuedeGuardarMotivo);
        }

        [TestMethod]
        public async Task ApiVieja_404_EscondeElControl_SinError()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._, A<string>._)).ThrowsAsync(new DiaRepartoNoDisponibleException());
            DiaRepartoClienteViewModel vm = Ficha();

            await vm.CargarAsync("1", "2077", "3");

            Assert.IsFalse(vm.Disponible);
            Assert.IsFalse(vm.HayMensaje);
            Assert.AreEqual(0, errores.Count);
            A.CallTo(() => dialogos.ShowError(A<string>._)).MustNotHaveHappened();

            await vm.CargarAsync("1", "15191", "0");
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._, A<string>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task Guardar_403_EnseñaElTextoDeLaApi_YVuelveAlDiaGuardado()
        {
            EnGrupo(Constantes.GruposSeguridad.ALMACEN);
            A.CallTo(() => servicio.Leer("1", "2077", "3")).Returns(Miercoles());
            A.CallTo(() => servicio.Guardar(A<string>._, A<string>._, A<string>._, A<string>._, A<string>._))
                .ThrowsAsync(new UnauthorizedAccessException("Solo Dirección, Almacén y Administración pueden cambiar el día de reparto propio."));
            DiaRepartoClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "2077", "3");

            vm.OpcionSeleccionada = Opcion("05");
            await vm.GuardadoEnCurso;

            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "Solo Dirección, Almacén y Administración pueden cambiar el día de reparto propio.");
            Assert.AreEqual("03", vm.OpcionSeleccionada.Ruta, "Vuelve al día que había");
            Assert.IsFalse(vm.HayMensaje);
        }

        [TestMethod]
        public async Task Guardar_400_EnseñaElTextoDeLaApi()
        {
            EnGrupo(Constantes.GruposSeguridad.DIRECCION);
            A.CallTo(() => servicio.Leer("1", "2077", "9")).Returns(Task.FromResult<DiaRepartoClienteModel>(null));
            A.CallTo(() => servicio.Guardar(A<string>._, A<string>._, A<string>._, A<string>._, A<string>._))
                .ThrowsAsync(new Exception("El contacto 9 del cliente 2077 no existe."));
            DiaRepartoClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "2077", "9");

            vm.OpcionSeleccionada = Opcion("02");
            await vm.GuardadoEnCurso;

            Assert.AreEqual(1, errores.Count);
            StringAssert.Contains(errores[0], "El contacto 9 del cliente 2077 no existe.");
            Assert.IsNull(vm.OpcionSeleccionada.Ruta);
        }

        [TestMethod]
        public async Task SinPermiso_SoloLectura_ConElTextoDelDia()
        {
            A.CallTo(() => servicio.Leer("1", "2077", "3")).Returns(Miercoles());
            DiaRepartoClienteViewModel vm = Ficha();

            await vm.CargarAsync("1", "2077", "3");

            Assert.IsFalse(vm.PuedeCambiar);
            Assert.IsTrue(vm.SoloLectura);
            Assert.IsFalse(vm.SePuedeElegir);
            Assert.AreEqual("miércoles (Solo abre los miércoles)", vm.TextoDia);
        }

        [TestMethod]
        public void ExtraerMensaje_EntiendeMessage_YCadenaJson()
        {
            Assert.AreEqual("La ruta debe ser de 01 a 05.", DiaRepartoClienteService.ExtraerMensaje("{\"Message\":\"La ruta debe ser de 01 a 05.\"}"));
            Assert.AreEqual("El contacto no existe.", DiaRepartoClienteService.ExtraerMensaje("\"El contacto no existe.\""));
            Assert.AreEqual("texto plano", DiaRepartoClienteService.ExtraerMensaje("texto plano"));
        }

        [TestMethod]
        public void UrlLeer_LlevaEmpresaClienteYContactoSinEspacios()
        {
            Assert.AreEqual("Clientes/DiaReparto?empresa=1&cliente=2077&contacto=3", DiaRepartoClienteService.UrlLeer("1  ", "2077   ", "3  "));
            Assert.AreEqual("Clientes/DiaReparto?empresa=1&cliente=2077&contacto=0", DiaRepartoClienteService.UrlLeer("1", "2077", null));
        }
    }

    internal static class OpcionesDiaRepartoExtensiones
    {
        public static OpcionDiaReparto Find(this IReadOnlyList<OpcionDiaReparto> opciones, string ruta)
        {
            foreach (OpcionDiaReparto o in opciones)
            {
                if (o.Ruta == ruta)
                {
                    return o;
                }
            }
            throw new ArgumentException(ruta);
        }
    }
}
