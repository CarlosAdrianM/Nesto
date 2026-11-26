using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#649 (gemela de NestoAPI#887, novedad 1421 de Paloma): «Copiar datos de transferencia» en los inscritos de un evento,
    /// solo en las inscripciones reservadas de pago. Copia el texto que da la API, listo para pegar en WhatsApp o en el correo.
    /// </summary>
    [TestClass]
    public class EventosDatosTransferenciaTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly IServicioPortapapeles portapapeles = A.Fake<IServicioPortapapeles>();
        private readonly List<string> errores = new();
        private readonly List<string> copiados = new();

        public EventosDatosTransferenciaTests()
        {
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(A<string>.Ignored)).Returns(true);
            A.CallTo(() => configuracion.leerParametro(A<string>.Ignored, Parametros.Claves.AlmacenPedidoVta)).Returns("REI");
            A.CallTo(() => portapapeles.Copiar(A<string>.Ignored)).ReturnsLazily((string t) => { copiados.Add(t); return true; });
        }

        private static InscripcionEventoModel Inscripcion(int id, string estado, string modalidad = null) => new()
        {
            Id = id, EventoId = 7, Evento = "Masterclass Rosácea", Empresa = "1", Cliente = "33895", Contacto = "0",
            Nombre = "Peluquería Ejemplo", Estado = estado, EstadoTexto = estado, Modalidad = modalidad
        };

        private async Task<MantenimientoEventosViewModel> Pantalla(params InscripcionEventoModel[] inscritos)
        {
            var evento = new EventoModel { Id = 7, Empresa = "1", Titulo = "Masterclass Rosácea", Fecha = DateTime.Today.AddDays(5), ImporteSenal = 50, Activo = true, Almacen = "REI" };
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { evento });
            A.CallTo(() => eventos.LeerInscripciones(7)).Returns(inscritos.ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos, portapapeles);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        [TestMethod]
        public async Task SoloEnLasReservadasDePago()
        {
            MantenimientoEventosViewModel vm = await Pantalla(
                Inscripcion(1, InscripcionEventoModel.RESERVADA),
                Inscripcion(2, InscripcionEventoModel.PAGADA),
                Inscripcion(3, InscripcionEventoModel.INSCRITA, InscribirEventoModel.MODALIDAD_GRATUITA));

            Assert.IsTrue(vm.CopiarDatosTransferenciaCommand.CanExecute(vm.Inscripciones.Single(i => i.Id == 1)));
            Assert.IsFalse(vm.CopiarDatosTransferenciaCommand.CanExecute(vm.Inscripciones.Single(i => i.Id == 2)));
            Assert.IsFalse(vm.CopiarDatosTransferenciaCommand.CanExecute(vm.Inscripciones.Single(i => i.Id == 3)));
        }

        [TestMethod]
        public async Task CopiaElTextoDeLaApi()
        {
            const string texto = "Beneficiario: NUEVA VISION, S.A.\nIBAN: ES00 0000 0000 0000 0000 0000\nConcepto: 33895 INS41\nImporte: 50,00 €";
            A.CallTo(() => eventos.LeerDatosTransferencia(41)).Returns(new DatosTransferenciaInscripcionModel { InscripcionId = 41, Texto = texto });
            MantenimientoEventosViewModel vm = await Pantalla(Inscripcion(41, InscripcionEventoModel.RESERVADA));

            string copiado = await vm.CopiarDatosTransferenciaAsync(vm.Inscripciones.Single());

            Assert.AreEqual(texto, copiado);
            Assert.AreEqual(texto, copiados.Single());
            StringAssert.Contains(vm.AvisoEnlace, "Datos de transferencia de Peluquería Ejemplo copiados");
            Assert.AreEqual(0, errores.Count);
        }

        [TestMethod]
        public async Task SinTexto_CopiaLosDatosUnoPorLinea()
        {
            A.CallTo(() => eventos.LeerDatosTransferencia(41)).Returns(new DatosTransferenciaInscripcionModel
            {
                Iban = "ES00 1111", Titular = "NUEVA VISION, S.A.", Concepto = "33895 INS41", Importe = 50
            });
            MantenimientoEventosViewModel vm = await Pantalla(Inscripcion(41, InscripcionEventoModel.RESERVADA));

            await vm.CopiarDatosTransferenciaAsync(vm.Inscripciones.Single());

            Assert.AreEqual("Beneficiario: NUEVA VISION, S.A.\nIBAN: ES00 1111\nConcepto: 33895 INS41\nImporte: 50,00 €", copiados.Single());
        }

        [TestMethod]
        public async Task ApiSinLaRuta_LoDiceYNoCopiaNada()
        {
            A.CallTo(() => eventos.LeerDatosTransferencia(41)).Returns(Task.FromResult<DatosTransferenciaInscripcionModel>(null));
            MantenimientoEventosViewModel vm = await Pantalla(Inscripcion(41, InscripcionEventoModel.RESERVADA));

            await vm.CopiarDatosTransferenciaAsync(vm.Inscripciones.Single());

            Assert.AreEqual(0, copiados.Count);
            Assert.AreEqual(DatosTransferenciaInscripcionModel.API_SIN_DATOS, errores.Single());
        }

        [TestMethod]
        public async Task ErrorDeLaApi_ConSuMensaje()
        {
            A.CallTo(() => eventos.LeerDatosTransferencia(41)).ThrowsAsync(new Exception("La inscripción 41 ya no está reservada."));
            MantenimientoEventosViewModel vm = await Pantalla(Inscripcion(41, InscripcionEventoModel.RESERVADA));

            await vm.CopiarDatosTransferenciaAsync(vm.Inscripciones.Single());

            Assert.AreEqual("La inscripción 41 ya no está reservada.", errores.Single());
            Assert.AreEqual(0, copiados.Count);
        }

        // ---------- El contrato real de NestoAPI#887 ----------

        private const string JSON_API = "{\"InscripcionId\":41,\"EventoId\":7,\"Evento\":\"Masterclass Rosácea\",\"Empresa\":\"1\",\"Cliente\":\"33895\"," +
            "\"Contacto\":\"0\",\"Iban\":\"ES00 1111\",\"Titular\":\"NUEVA VISION, S.A.\",\"Concepto\":\"Cliente 33895 - INS41\",\"Importe\":50.0," +
            "\"Texto\":\"IBAN: ES00 1111\\r\\nBeneficiario: NUEVA VISION, S.A.\\r\\nConcepto: Cliente 33895 - INS41\\r\\nImporte: 50,00 €\"," +
            "\"Recordatorio\":\"Cuando llegue la transferencia, Administración apunta el cobro en Cajas › Cobros (documento INS41).\"}";

        [TestMethod]
        public void LeeLosNombresDelContratoReal()
        {
            var datos = Newtonsoft.Json.JsonConvert.DeserializeObject<DatosTransferenciaInscripcionModel>(JSON_API);

            Assert.AreEqual(41, datos.InscripcionId);
            Assert.AreEqual(7, datos.EventoId);
            Assert.AreEqual("Masterclass Rosácea", datos.Evento);
            Assert.AreEqual("0", datos.Contacto);
            Assert.AreEqual(50m, datos.Importe);
            StringAssert.StartsWith(datos.Recordatorio, "Cuando llegue la transferencia");
        }

        [TestMethod]
        public async Task ElRecordatorio_SeEnseniaPeroNoSeCopia()
        {
            var datos = Newtonsoft.Json.JsonConvert.DeserializeObject<DatosTransferenciaInscripcionModel>(JSON_API);
            A.CallTo(() => eventos.LeerDatosTransferencia(41)).Returns(datos);
            MantenimientoEventosViewModel vm = await Pantalla(Inscripcion(41, InscripcionEventoModel.RESERVADA));

            await vm.CopiarDatosTransferenciaAsync(vm.Inscripciones.Single());

            Assert.AreEqual(datos.Texto, copiados.Single(), "se copia el Texto de la API tal cual");
            Assert.IsFalse(copiados.Single().Contains("Administración"), "el recordatorio no va al cliente");
            StringAssert.Contains(vm.AvisoEnlace, "copiados");
            StringAssert.Contains(vm.AvisoEnlace, "Administración apunta el cobro en Cajas › Cobros (documento INS41).");
        }

        [DataTestMethod]
        [DataRow(409, "{\"error\":{\"code\":\"BUSINESS_RULE_VIOLATION\",\"message\":\"La inscripción 41 está pagada: solo una reserva sin pagar tiene señal que pagar por transferencia.\"}}",
            "La inscripción 41 está pagada: solo una reserva sin pagar tiene señal que pagar por transferencia.")]
        [DataRow(404, "{\"error\":{\"code\":\"NOT_FOUND\",\"message\":\"No existe la inscripción 41.\"}}", "No existe la inscripción 41.")]
        [DataRow(403, "{\"error\":{\"message\":\"Los datos de la transferencia de una inscripción los da la casa.\"}}",
            "Los datos de la transferencia de una inscripción los da la casa.")]
        [DataRow(400, "{\"error\":{\"message\":\"La empresa 1 no tiene ninguna cuenta bancaria para recibir transferencias.\"}}",
            "La empresa 1 no tiene ninguna cuenta bancaria para recibir transferencias.")]
        public void LosErroresDeLaApi_ConSuMensajeTalCual(int estado, string cuerpo, string esperado)
        {
            Assert.IsFalse(EventosService.EsApiSinLaRuta(estado, cuerpo), "un 404 de negocio no es una API sin la ruta");
            Assert.AreEqual(esperado, EventosService.MensajeError(estado, cuerpo, "No se pudieron leer los datos de transferencia", talCual: true));
        }
    }
}
