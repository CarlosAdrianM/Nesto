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
    /// Nesto#517 (NestoAPI#615): el bloque «Propuestas» de «Señales de eventos». La API propone; Administración, Dirección
    /// e Informática confirman o descartan. Con una API sin propuestas (404) el bloque se esconde.
    /// </summary>
    [TestClass]
    public class PropuestasSenalesViewModelTests
    {
        private readonly IEventosService eventos = A.Fake<IEventosService>();
        private readonly IConfiguracion configuracion = A.Fake<IConfiguracion>();
        private readonly IServicioDialogos dialogos = A.Fake<IServicioDialogos>();
        private readonly IServicioNavegacion navegacion = A.Fake<IServicioNavegacion>();

        public PropuestasSenalesViewModelTests()
        {
            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(true);
        }

        private static PropuestaSenalModel Cobro(int apunte, params int[] propuestas) => new()
        {
            Empresa = "1", NumOrdenExtracto = apunte, Cliente = "15191", Contacto = "0", Nombre = "CENTRO DE ESTÉTICA LUNA",
            Importe = 50, Concepto = "S/Pago a cuenta", ConceptoBanco = "TRANSFERENCIA CENTRO LUNA · SEÑAL CLOASMA",
            Candidatos = propuestas.Select(id => new CandidatoSenalModel
            {
                Id = id, EventoId = id, Evento = $"Evento {id}", FechaEvento = new DateTime(2026, 10, 13), ImporteSenal = 50,
                Motivo = "Tiene la inscripción 7 reservada y el concepto dice «CLOASMA»."
            }).ToList()
        };

        private SenalesEventosViewModel Vm() => new(eventos, dialogos, navegacion, configuracion);

        [TestMethod]
        public async Task Cargar_AplanaUnaFilaPorCandidato()
        {
            A.CallTo(() => eventos.LeerPropuestas()).Returns(new List<PropuestaSenalModel> { Cobro(8001, 12, 13), Cobro(8002, 14) });
            SenalesEventosViewModel vm = Vm();

            await vm.CargarAsync();

            Assert.IsTrue(vm.HayApiDePropuestas);
            Assert.AreEqual(3, vm.Propuestas.Count);
            CollectionAssert.AreEqual(new[] { true, false, true }, vm.Propuestas.Select(p => p.PrimeraDelCobro).ToArray());
            Assert.AreEqual("Propuestas (2 cobros)", vm.TituloPropuestas);
            Assert.AreEqual("TRANSFERENCIA CENTRO LUNA · SEÑAL CLOASMA", vm.Propuestas[0].ConceptoBanco);
        }

        [TestMethod]
        public async Task ApiSinPropuestas_EscondeElBloque()
        {
            A.CallTo(() => eventos.LeerPropuestas()).Returns(Task.FromResult<List<PropuestaSenalModel>>(null));
            SenalesEventosViewModel vm = Vm();

            await vm.CargarAsync();

            Assert.IsFalse(vm.HayApiDePropuestas);
            Assert.AreEqual(0, vm.Propuestas.Count);
            A.CallTo(() => dialogos.ShowError(A<string>.Ignored)).MustNotHaveHappened();
        }

        [TestMethod]
        public void SoloAdministracionDireccionEInformatica_ConfirmanODescartan()
        {
            Assert.IsFalse(Vm().PuedeConfirmarPropuestas);
            Assert.IsFalse(new SenalesEventosViewModel(eventos, dialogos, navegacion).PuedeConfirmarPropuestas);
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.TIENDA_ON_LINE)).Returns(true);
            Assert.IsFalse(Vm().PuedeConfirmarPropuestas, "Tienda online ve la lista, pero no confirma");
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            Assert.IsTrue(Vm().PuedeConfirmarPropuestas);
        }

        [TestMethod]
        public async Task Confirmar_ElCandidatoElegido_YRecargaSenalesYPropuestas()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            A.CallTo(() => eventos.LeerPropuestas()).Returns(new List<PropuestaSenalModel> { Cobro(8001, 12, 13) });
            A.CallTo(() => eventos.ConfirmarPropuesta(13)).Returns(new SenalEventoModel { Id = 5, Evento = "Evento 13" });
            SenalesEventosViewModel vm = Vm();
            await vm.CargarAsync();
            Assert.IsFalse(vm.ConfirmarPropuestaCommand.CanExecute(null), "Sin elegir fila no se confirma");

            vm.PropuestaSeleccionada = vm.Propuestas[1];
            await vm.ConfirmarPropuestaAsync();

            A.CallTo(() => eventos.ConfirmarPropuesta(13)).MustHaveHappenedOnceExactly();
            A.CallTo(() => eventos.LeerSenales(A<string>.Ignored, A<int?>.Ignored)).MustHaveHappenedTwiceExactly();
            A.CallTo(() => eventos.LeerPropuestas()).MustHaveHappenedTwiceExactly();
        }

        [TestMethod]
        public async Task Descartar_SinConfirmarNoDescarta_YConfirmadoSi()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.DIRECCION)).Returns(true);
            A.CallTo(() => eventos.LeerPropuestas()).Returns(new List<PropuestaSenalModel> { Cobro(8001, 12) });
            SenalesEventosViewModel vm = Vm();
            await vm.CargarAsync();
            vm.PropuestaSeleccionada = vm.Propuestas.Single();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(false);
            await vm.DescartarPropuestaAsync();
            A.CallTo(() => eventos.DescartarPropuesta(A<int>.Ignored)).MustNotHaveHappened();

            A.CallTo(() => dialogos.ShowConfirmationAnswer(A<string>.Ignored, A<string>.Ignored)).Returns(true);
            await vm.DescartarPropuestaAsync();
            A.CallTo(() => eventos.DescartarPropuesta(12)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task BuscarAhora_PonLoQueDevuelveLaBusqueda()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.INFORMATICA)).Returns(true);
            A.CallTo(() => eventos.BuscarPropuestas(15)).Returns(new List<PropuestaSenalModel> { Cobro(9001, 20) });
            SenalesEventosViewModel vm = Vm();

            await vm.BuscarPropuestasAsync();

            Assert.AreEqual(20, vm.Propuestas.Single().IdPropuesta);
            Assert.AreEqual("Propuestas (1 cobro)", vm.TituloPropuestas);
        }

        [TestMethod]
        public async Task ErrorAlConfirmar_SeEnsena()
        {
            A.CallTo(() => configuracion.UsuarioEnGrupo(Constantes.GruposSeguridad.ADMINISTRACION)).Returns(true);
            A.CallTo(() => eventos.LeerPropuestas()).Returns(new List<PropuestaSenalModel> { Cobro(8001, 12) });
            A.CallTo(() => eventos.ConfirmarPropuesta(12)).Throws(new Exception("No se pudo confirmar la señal: Esta propuesta ya está descartada por Rosa."));
            SenalesEventosViewModel vm = Vm();
            await vm.CargarAsync();
            vm.PropuestaSeleccionada = vm.Propuestas.Single();

            await vm.ConfirmarPropuestaAsync();

            A.CallTo(() => dialogos.ShowError(A<string>.That.Contains("ya está descartada"))).MustHaveHappenedOnceExactly();
        }
    }
}
