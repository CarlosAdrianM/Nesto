using ControlesUsuario.Services;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Nesto.Modulos.Cliente.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ClienteTests
{
    /// <summary>
    /// Carlos 29/10/26: el enlace de pago de la inscripción se copia solo al portapapeles al inscribir y no se puede perder
    /// (no se cierra el diálogo sin haberlo copiado, salvo al segundo intento, avisado); se puede volver a copiar desde los
    /// inscritos del evento y desde la ficha del cliente. El correo se elige entre los de las personas de contacto. Origen
    /// «Cobro».
    /// </summary>
    [TestClass]
    public class EventosEnlacePagoTests
    {
        private const string URL = "https://api.nuevavision.es/pago/abc";

        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioPortapapeles portapapeles = A.Fake<IServicioPortapapeles>();
        private readonly List<string> copiados = new();
        private readonly List<string> errores = new();

        public EventosEnlacePagoTests()
        {
            A.CallTo(() => portapapeles.Copiar(A<string>._)).ReturnsLazily((string t) => { copiados.Add(t); return true; });
            A.CallTo(() => dialogos.ShowError(A<string>._)).Invokes((string m) => errores.Add(m));
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDAS)).Returns(true);
        }

        private static EventoModel Evento() =>
            new() { Id = 7, Empresa = "1", Titulo = "Masterclass Cloasma", Fecha = DateTime.Today.AddDays(5), ImporteSenal = 50, Activo = true, Almacen = "REI" };

        private static InscripcionEventoModel Inscripcion(int id, string url = URL) => new()
        {
            Id = id, EventoId = 7, Evento = "Masterclass Cloasma", Empresa = "1", Cliente = "15191", Contacto = "0",
            Nombre = "Peluquería Pepa ", Estado = InscripcionEventoModel.RESERVADA, EstadoTexto = "Reservada", UrlPago = url
        };

        private async Task<(InscribirEventoDialogViewModel vm, Func<ResultadoDialogo> resultado)> DialogoInscrito(string url = URL)
        {
            A.CallTo(() => eventos.LeerEventos(true)).Returns(new List<EventoModel> { Evento() });
            A.CallTo(() => eventos.Inscribir(A<int>._, A<InscribirEventoModel>._)).Returns(Inscripcion(11, url));
            var vm = new InscribirEventoDialogViewModel(eventos, portapapeles) { EsperaCargaPersonasMs = 0 };
            ResultadoDialogo resultado = null;
            ((IDialogoNesto)vm).RequestClose += r => resultado = r;
            ((IDialogoNesto)vm).OnDialogOpened(new ParametrosDialogo { { "cliente", "15191" }, { "contacto", "0" }, { "nombre", "Peluquería Pepa" } });
            await vm.CargarEventosAsync(null);
            await vm.InscribirAsync();
            return (vm, () => resultado);
        }

        // ---------------- Diálogo: copiar el enlace solo ----------------

        [TestMethod]
        public async Task Dialogo_AlInscribirConEnlace_LoCopiaSoloYLoDice()
        {
            (InscribirEventoDialogViewModel vm, _) = await DialogoInscrito();

            CollectionAssert.AreEqual(new[] { URL }, copiados);
            Assert.IsTrue(vm.EnlaceCopiado);
            Assert.AreEqual("Enlace copiado: péguelo en WhatsApp o donde quiera.", vm.TextoEnlace);
            Assert.IsTrue(vm.CopiarEnlaceCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Dialogo_SinEnlace_NoCopiaNada()
        {
            (InscribirEventoDialogViewModel vm, Func<ResultadoDialogo> resultado) = await DialogoInscrito(url: null);

            Assert.AreEqual(0, copiados.Count);
            Assert.IsFalse(vm.CopiarEnlaceCommand.CanExecute(null));
            vm.CerrarCommand.Execute(null);
            Assert.AreEqual(ResultadoBoton.OK, resultado().Result);
        }

        [TestMethod]
        public async Task Dialogo_SiNoSePudoCopiar_ElPrimerCerrarAvisaYNoCierra_YCopiandoYaCierra()
        {
            A.CallTo(() => portapapeles.Copiar(A<string>._)).Returns(false);
            (InscribirEventoDialogViewModel vm, Func<ResultadoDialogo> resultado) = await DialogoInscrito();
            Assert.IsFalse(vm.EnlaceCopiado);
            StringAssert.Contains(vm.TextoEnlace, "Copiar enlace");

            vm.CerrarCommand.Execute(null);

            Assert.IsNull(resultado(), "No se cierra con el enlace sin copiar");
            Assert.AreEqual(InscribirEventoDialogViewModel.AVISO_ENLACE_SIN_COPIAR, vm.Mensaje);

            A.CallTo(() => portapapeles.Copiar(A<string>._)).Returns(true);
            vm.CopiarEnlaceCommand.Execute(null);
            Assert.IsTrue(vm.EnlaceCopiado);
            Assert.IsNull(vm.Mensaje);

            vm.CerrarCommand.Execute(null);
            Assert.AreEqual(ResultadoBoton.OK, resultado().Result);
        }

        [TestMethod]
        public async Task Dialogo_SiNoSePudoCopiar_AlSegundoIntentoDejaCerrar_YDevuelveLaInscripcion()
        {
            A.CallTo(() => portapapeles.Copiar(A<string>._)).Returns(false);
            (InscribirEventoDialogViewModel vm, Func<ResultadoDialogo> resultado) = await DialogoInscrito();

            vm.CerrarCommand.Execute(null);
            vm.CerrarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado().Result);
            Assert.AreEqual(11, resultado().Parameters.GetValue<InscripcionEventoModel>("inscripcion").Id);
        }

        [TestMethod]
        public void Dialogo_LaXSinInscribir_Cierra()
        {
            var vm = new InscribirEventoDialogViewModel(eventos, portapapeles);

            Assert.IsTrue(((IDialogoNesto)vm).CanCloseDialog());
        }

        [TestMethod]
        public async Task Dialogo_LaXConElEnlaceSinCopiar_NoCierraYAvisa()
        {
            A.CallTo(() => portapapeles.Copiar(A<string>._)).Returns(false);
            (InscribirEventoDialogViewModel vm, Func<ResultadoDialogo> resultado) = await DialogoInscrito();

            Assert.IsFalse(((IDialogoNesto)vm).CanCloseDialog());

            Assert.AreEqual(InscribirEventoDialogViewModel.AVISO_ENLACE_SIN_COPIAR, vm.Mensaje);
            await Task.Delay(50);
            Assert.IsNull(resultado(), "Tampoco pide cerrar después");
        }

        [TestMethod]
        public async Task Dialogo_LaXDespuesDeInscribir_CierraConLaInscripcion_ParaQueRecarguen()
        {
            (InscribirEventoDialogViewModel vm, Func<ResultadoDialogo> resultado) = await DialogoInscrito();

            Assert.IsFalse(((IDialogoNesto)vm).CanCloseDialog(), "Este cierre se cancela…");
            for (int i = 0; i < 50 && resultado() == null; i++)
            {
                await Task.Delay(10);
            }

            Assert.IsNotNull(resultado(), "…y se pide otro con el resultado");
            Assert.AreEqual(ResultadoBoton.OK, resultado().Result);
            Assert.IsTrue(((IDialogoNesto)vm).CanCloseDialog(), "El cierre con resultado sí se deja");
        }

        // ---------------- Diálogo: el correo ----------------

        private static PersonaContactoEventoModel Persona(string numero, string nombre, string correo) =>
            new() { Contacto = "0", Numero = numero, Nombre = nombre, Correo = correo };

        [TestMethod]
        public async Task Dialogo_LosCorreosSonLosDeLasPersonasDeContacto_ElDeQuienAsistePrimero()
        {
            A.CallTo(() => eventos.LeerPersonasContacto("1", "15191", "0")).Returns(new List<PersonaContactoEventoModel>
            {
                Persona("1", "ANA LUNA", "ana@luna.es"), Persona("2", "EVA LUNA", " eva@luna.es "), Persona("3", "SIN CORREO", null)
            });
            var vm = new InscribirEventoDialogViewModel(eventos, portapapeles) { EsperaCargaPersonasMs = 0 };
            vm.Cliente = "15191";
            vm.Contacto = "0";
            await vm.CargarPersonasAsync();

            CollectionAssert.AreEqual(new[] { "ana@luna.es", "eva@luna.es" }, vm.Correos.ToList());

            vm.PersonaAsiste = vm.Personas.Single(p => p.Numero == "2");

            CollectionAssert.AreEqual(new[] { "eva@luna.es", "ana@luna.es" }, vm.Correos.ToList());
        }

        [TestMethod]
        public void Dialogo_ConTarjetaYUnCorreoSinFormato_NoDejaInscribir_ConEfectivoDaIgual()
        {
            var vm = new InscribirEventoDialogViewModel(eventos, portapapeles) { Evento = Evento(), Cliente = "15191" };
            Assert.IsTrue(vm.InscribirCommand.CanExecute(null));

            vm.Correo = "pepa@";

            Assert.IsFalse(vm.CorreoValido);
            Assert.IsFalse(vm.InscribirCommand.CanExecute(null));

            vm.FormaPago = vm.FormasPago.Single(f => f.Valor == MarcarPagoInscripcionModel.EFECTIVO);

            Assert.IsTrue(vm.InscribirCommand.CanExecute(null), "Sin enlace el correo no se manda");
        }

        [TestMethod]
        public void Dialogo_DesdeLaFicha_EnseñaElClienteFijo_YDesdeElMantenimientoElSelector()
        {
            var desdeFicha = new InscribirEventoDialogViewModel(eventos, portapapeles);
            ((IDialogoNesto)desdeFicha).OnDialogOpened(new ParametrosDialogo { { "cliente", "15191 " }, { "contacto", "0" }, { "nombre", "Peluquería Pepa" } });
            var desdeMantenimiento = new InscribirEventoDialogViewModel(eventos, portapapeles);
            ((IDialogoNesto)desdeMantenimiento).OnDialogOpened(new ParametrosDialogo { { "eventoId", 7 } });

            Assert.IsFalse(desdeFicha.ClienteElegible);
            Assert.AreEqual("15191/0 · Peluquería Pepa", desdeFicha.TextoClienteFijo);
            Assert.IsTrue(desdeMantenimiento.ClienteElegible);
        }

        // ---------------- Inscritos del evento y ficha del cliente: «Copiar enlace» ----------------

        private async Task<MantenimientoEventosViewModel> Mantenimiento(params InscripcionEventoModel[] inscritos)
        {
            A.CallTo(() => eventos.LeerEventos(false)).Returns(new List<EventoModel> { Evento() });
            A.CallTo(() => eventos.LeerInscripciones(7)).Returns(inscritos.ToList());
            var vm = new MantenimientoEventosViewModel(eventos, configuracion, dialogos, portapapeles);
            await vm.CargarAsync();
            vm.Seleccionado = vm.Eventos.Single();
            await vm.CargarInscripcionesAsync();
            return vm;
        }

        [TestMethod]
        public async Task Mantenimiento_CopiarEnlace_DeLaFilaODeLaSeleccionada()
        {
            MantenimientoEventosViewModel vm = await Mantenimiento(Inscripcion(1), Inscripcion(2, url: null));
            InscripcionEventoModel conEnlace = vm.Inscripciones.Single(i => i.Id == 1);
            InscripcionEventoModel sinEnlace = vm.Inscripciones.Single(i => i.Id == 2);

            Assert.IsTrue(vm.CopiarEnlaceCommand.CanExecute(conEnlace));
            Assert.IsFalse(vm.CopiarEnlaceCommand.CanExecute(sinEnlace));

            vm.CopiarEnlaceCommand.Execute(conEnlace);

            CollectionAssert.AreEqual(new[] { URL }, copiados);
            Assert.AreEqual("Enlace de Peluquería Pepa copiado: péguelo en WhatsApp o donde quiera.", vm.AvisoEnlace);

            vm.InscripcionSeleccionada = sinEnlace;
            Assert.IsFalse(vm.CopiarEnlaceCommand.CanExecute(null), "El menú contextual mira la seleccionada");
            vm.InscripcionSeleccionada = conEnlace;
            Assert.IsTrue(vm.CopiarEnlaceCommand.CanExecute(null));
        }

        [TestMethod]
        public async Task Mantenimiento_SiWindowsNoDejaCopiar_LoDice()
        {
            A.CallTo(() => portapapeles.Copiar(A<string>._)).Returns(false);
            MantenimientoEventosViewModel vm = await Mantenimiento(Inscripcion(1));

            vm.CopiarEnlace(vm.Inscripciones.Single());

            Assert.AreEqual(InscripcionEventoModel.ERROR_COPIAR_ENLACE, errores.Single());
            Assert.IsNull(vm.AvisoEnlace);
        }

        [TestMethod]
        public async Task FichaCliente_CopiarEnlace_DeLaSeleccionada()
        {
            A.CallTo(() => eventos.LeerInscripcionesCliente("1", "15191")).Returns(new List<InscripcionEventoModel> { Inscripcion(1) });
            var vm = new InscripcionesClienteViewModel(eventos, dialogos, portapapeles);
            vm.CambiarCliente("1", "15191", "0", "Peluquería Pepa");
            await vm.CargarAsync();
            Assert.IsFalse(vm.CopiarEnlaceCommand.CanExecute(null), "Sin seleccionar nada");

            vm.Seleccionada = vm.Inscripciones.Single();
            vm.CopiarEnlaceCommand.Execute(null);

            CollectionAssert.AreEqual(new[] { URL }, copiados);
            StringAssert.Contains(vm.AvisoEnlace, "copiado");
        }

        // ---------------- Contrato de la API ----------------

        [TestMethod]
        public void Modelo_UrlPagoAlLeerLaLista_YOrigenCobro()
        {
            const string json = "[{\"Id\":11,\"Origen\":\"Cobro\",\"UrlPago\":\"https://api.nuevavision.es/pago/x\"},{\"Id\":12,\"Origen\":\"Telefono\"}]";

            List<InscripcionEventoModel> lista = JsonConvert.DeserializeObject<List<InscripcionEventoModel>>(json);

            Assert.AreEqual("Cobro", lista[0].TextoOrigen);
            Assert.IsTrue(lista[0].TieneEnlacePago);
            Assert.AreEqual("Teléfono", lista[1].TextoOrigen);
            Assert.IsFalse(lista[1].TieneEnlacePago, "Con la API de antes no viene y no pasa nada");
        }
    }
}
