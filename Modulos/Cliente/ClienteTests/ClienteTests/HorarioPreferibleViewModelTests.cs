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
    /// Nesto#584 (NestoAPI#763): el «horario preferible» en la ficha del cliente (editor de días con mañana y tarde, aviso de que
    /// no hay compromiso de respetarlo, propuesta por revisar) y la lista «Horarios por revisar».
    /// </summary>
    [TestClass]
    public class HorarioPreferibleViewModelTests
    {
        private readonly IHorarioPreferibleService servicio = A.Fake<IHorarioPreferibleService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly List<string> errores = new();

        public HorarioPreferibleViewModelTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).Returns(true);
        }

        private static FranjaHorarioPreferibleModel F(int dia, string desde, string hasta) => new() { DiaSemana = dia, Desde = desde, Hasta = hasta };

        private static HorarioPreferibleModel Horario(string texto, params FranjaHorarioPreferibleModel[] franjas) => new()
        {
            Empresa = "1", Cliente = "15191", Contacto = "0", Texto = texto, Franjas = franjas.ToList(),
            Aviso = "Horario preferible del cliente: lo intentaremos, pero no hay compromiso de respetarlo.",
            Usuario = "Carlos", FechaModificacion = new DateTime(2026, 10, 30, 12, 0, 0)
        };

        private HorarioPreferibleClienteViewModel Ficha() => new(servicio, dialogos);

        [TestMethod]
        public async Task Cargar_PoneLasFranjasEnSuDia_MananaYTarde_YElAvisoDeLaRuta()
        {
            A.CallTo(() => servicio.Leer("1", "15191", "0")).Returns(Horario("L 10:00-14:00, 17:00-20:00; S 10:00-13:00",
                F(1, "17:00", "20:00"), F(1, "10:00", "14:00"), F(6, "10:00", "13:00")));
            HorarioPreferibleClienteViewModel vm = Ficha();

            await vm.CargarAsync("1  ", "15191     ", "0  ");

            Assert.AreEqual("10:00", vm.Dias[0].MananaDesde);
            Assert.AreEqual("14:00", vm.Dias[0].MananaHasta);
            Assert.AreEqual("17:00", vm.Dias[0].TardeDesde, "La segunda del día, a la tarde");
            Assert.AreEqual("10:00", vm.Dias[5].MananaDesde);
            Assert.IsNull(vm.Dias[2].MananaDesde);
            StringAssert.Contains(vm.Aviso, "no hay compromiso de respetarlo");
            StringAssert.StartsWith(vm.Resumen, "Guardado: L 10:00-14:00, 17:00-20:00; S 10:00-13:00");
            Assert.IsTrue(vm.TieneHorario);
            Assert.IsFalse(vm.HayPropuesta);
        }

        [TestMethod]
        public async Task SinHorario_ElAvisoSiempre_YLaPropuestaPendienteSeUsaSinGuardar()
        {
            HorarioPreferibleModel vacio = Horario(string.Empty);
            vacio.Aviso = null;
            vacio.PropuestaPendiente = new PropuestaHorarioPreferibleModel
            {
                Id = 7, HorarioPropuesto = "L-V 10:00-14:00", Franjas = Enumerable.Range(1, 5).Select(d => F(d, "10:00", "14:00")).ToList()
            };
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._, A<string>._)).Returns(vacio);
            HorarioPreferibleClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191", null);

            Assert.AreEqual(HorarioPreferibleClienteViewModel.AVISO_POR_DEFECTO, vm.Aviso, "Aunque la API no lo mande");
            Assert.AreEqual("Este contacto no tiene horario preferible.", vm.Resumen);
            Assert.IsTrue(vm.HayPropuesta);
            StringAssert.Contains(vm.TextoPropuesta, "L-V 10:00-14:00");
            Assert.AreEqual("0", vm.Contacto, "Sin contacto: el 0");

            vm.UsarPropuestaCommand.Execute(null);

            Assert.AreEqual("10:00", vm.Dias[4].MananaDesde);
            Assert.IsNull(vm.Dias[5].MananaDesde);
            A.CallTo(() => servicio.Guardar(A<string>._, A<string>._, A<string>._, A<List<FranjaHorarioPreferibleModel>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_MandaLasFranjasNormalizadas_YPintaLoGuardado()
        {
            List<FranjaHorarioPreferibleModel> mandadas = null;
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._, A<string>._)).Returns(Horario(string.Empty));
            A.CallTo(() => servicio.Guardar("1", "15191", "0", A<List<FranjaHorarioPreferibleModel>>._))
                .Invokes((string e, string c, string k, List<FranjaHorarioPreferibleModel> f) => mandadas = f)
                .Returns(Horario("L-V 09:30-14:00", Enumerable.Range(1, 5).Select(d => F(d, "09:30", "14:00")).ToArray()));
            HorarioPreferibleClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191", "0");
            vm.Dias[0].MananaDesde = "9.30";
            vm.Dias[0].MananaHasta = "14h";

            vm.CopiarLunes();
            await vm.GuardarAsync();

            Assert.AreEqual(5, mandadas.Count);
            Assert.IsTrue(mandadas.All(f => f.Desde == "09:30" && f.Hasta == "14:00"));
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5 }, mandadas.Select(f => f.DiaSemana).ToList());
            StringAssert.Contains(vm.Resumen, "L-V 09:30-14:00");
            StringAssert.Contains(vm.Mensaje, "no hay compromiso de respetarlo");
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task Guardar_FranjaACojas_OHoraMala_ErrorDeUsted_YNoLlamaALaApi()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._, A<string>._)).Returns(Horario(string.Empty));
            HorarioPreferibleClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191", "0");
            vm.Dias[2].TardeDesde = "17:00";

            await vm.GuardarAsync();
            vm.Dias[2].TardeHasta = "las 8";
            await vm.GuardarAsync();

            Assert.AreEqual("Al miércoles por la tarde le falta la hora de fin.", errores[0]);
            StringAssert.Contains(errores[1], "escríbala como 10:00");
            A.CallTo(() => servicio.Guardar(A<string>._, A<string>._, A<string>._, A<List<FranjaHorarioPreferibleModel>>._)).MustNotHaveHappened();
        }

        [TestMethod]
        public async Task Guardar_LaApiLoRechaza_EnseñaSuMotivo()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._, A<string>._)).Returns(Horario(string.Empty));
            A.CallTo(() => servicio.Guardar(A<string>._, A<string>._, A<string>._, A<List<FranjaHorarioPreferibleModel>>._))
                .Throws(new Exception("Las franjas 10:00-14:00 y 13:00-18:00 del lunes se solapan: júntelas en una o sepárelas."));
            HorarioPreferibleClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191", "0");
            vm.Dias[0].MananaDesde = "10:00"; vm.Dias[0].MananaHasta = "14:00";
            vm.Dias[0].TardeDesde = "13:00"; vm.Dias[0].TardeHasta = "18:00";

            await vm.GuardarAsync();

            StringAssert.Contains(errores.Single(), "se solapan");
        }

        [TestMethod]
        public async Task Quitar_PideConfirmacion_YMandaSinFranjas()
        {
            A.CallTo(() => servicio.Leer(A<string>._, A<string>._, A<string>._)).Returns(Horario("L 10:00-14:00", F(1, "10:00", "14:00")));
            A.CallTo(() => servicio.Guardar(A<string>._, A<string>._, A<string>._, A<List<FranjaHorarioPreferibleModel>>._)).Returns(Horario(string.Empty));
            HorarioPreferibleClienteViewModel vm = Ficha();
            await vm.CargarAsync("1", "15191", "0");

            await vm.QuitarAsync();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>._, A<string>._)).MustHaveHappened();
            A.CallTo(() => servicio.Guardar("1", "15191", "0", A<List<FranjaHorarioPreferibleModel>>.That.Matches(l => l.Count == 0))).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.TieneHorario);
            Assert.IsNull(vm.Dias[0].MananaDesde);
        }

        [DataTestMethod]
        [DataRow("9", "09:00")]
        [DataRow("9:30", "09:30")]
        [DataRow("9.30", "09:30")]
        [DataRow("13,15", "13:15")]
        [DataRow("14h", "14:00")]
        [DataRow(" 20:00 ", "20:00")]
        [DataRow("24:00", null)]
        [DataRow("9:5", null)]
        [DataRow("mediodía", null)]
        public void NormalizarHora(string texto, string esperado)
        {
            Assert.AreEqual(esperado, HorarioPreferibleClienteViewModel.NormalizarHora(texto));
        }

        // ================================================================ Horarios por revisar

        private static PropuestaHorarioPreferibleModel Propuesta(int id, string propuesto) => new()
        {
            Id = id, Empresa = "1", Cliente = "1000" + id, Contacto = "0", Nombre = "ESTÉTICA " + id,
            TextoOrigen = "HORARIO\r\nDE 10 A 14", HorarioPropuesto = propuesto
        };

        [TestMethod]
        public async Task PorRevisar_Carga_ConElHorarioPropuestoEditable_YCuentaLasQueNoTienen()
        {
            A.CallTo(() => servicio.Propuestas()).Returns(new List<PropuestaHorarioPreferibleModel> { Propuesta(1, "L-V 10:00-14:00"), Propuesta(2, null) });
            var vm = new HorariosPorRevisarViewModel(servicio, dialogos);

            await vm.CargarAsync();

            Assert.AreEqual(2, vm.Propuestas.Count);
            Assert.AreEqual("L-V 10:00-14:00", vm.Propuestas[0].HorarioAGuardar);
            Assert.AreEqual("HORARIO DE 10 A 14", vm.Propuestas[0].TextoOrigen);
            Assert.AreSame(vm.Propuestas[0], vm.Seleccionada);
            StringAssert.Contains(vm.Mensaje, "2 por revisar (1 sin propuesta");
            StringAssert.Contains(vm.Aviso, "no hay compromiso de respetarlo");
        }

        [TestMethod]
        public async Task PorRevisar_AceptarConLoCorregido_YDescartar_LasQuitaDeLaLista()
        {
            A.CallTo(() => servicio.Propuestas()).Returns(new List<PropuestaHorarioPreferibleModel> { Propuesta(1, "L-V 10:00-14:00"), Propuesta(2, null) });
            A.CallTo(() => servicio.Aceptar(1, "M-V 10:00-14:00")).Returns(Horario("M-V 10:00-14:00"));
            var vm = new HorariosPorRevisarViewModel(servicio, dialogos);
            await vm.CargarAsync();
            vm.Seleccionada.HorarioAGuardar = "M-V 10:00-14:00";

            await vm.AceptarAsync();

            A.CallTo(() => servicio.Aceptar(1, "M-V 10:00-14:00")).MustHaveHappenedOnceExactly();
            Assert.AreEqual(1, vm.Propuestas.Count);
            Assert.AreEqual(2, vm.Seleccionada.Id);
            StringAssert.Contains(vm.Mensaje, "M-V 10:00-14:00");

            await vm.AceptarAsync();
            StringAssert.Contains(errores.Single(), "Escriba el horario a guardar");
            A.CallTo(() => servicio.Aceptar(2, A<string>._)).MustNotHaveHappened();

            await vm.DescartarAsync();
            A.CallTo(() => servicio.Descartar(2)).MustHaveHappenedOnceExactly();
            Assert.AreEqual(0, vm.Propuestas.Count);
            Assert.AreEqual("No hay horarios por revisar.", vm.Mensaje);
        }

        [TestMethod]
        public void Url_DelHorario_ConContactoPorDefecto()
        {
            Assert.AreEqual("Clientes/1/15191/0/HorarioPreferible", HorarioPreferibleService.Url("1  ", "15191     ", null));
            Assert.AreEqual("Mal", HorarioPreferibleService.ExtraerMensaje("{\"Message\":\"Mal\"}"));
        }
    }
}
