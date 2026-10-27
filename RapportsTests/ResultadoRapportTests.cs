using CommunityToolkit.Mvvm.Messaging;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Rapports;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RapportsTests
{
    /// <summary>
    /// Nesto#558 (NestoAPI#716, 2.1 y 2.2 desde el rapport): el resultado del rapport y el próximo contacto viajan en el
    /// mismo SeguimientoClienteDTO (null si no se marcan); el catálogo es copia exacta del de la API; el chip «Pedido» y el
    /// check «Pedido» van a la par; y con «Quitar de mi listado» se pregunta el motivo y se pasa al servicio.
    /// </summary>
    [TestClass]
    public class ResultadoRapportTests
    {
        // ---------- Catálogo (copia exacta de NestoAPI/Infraestructure/Rapports/ResultadoRapport.cs) ----------

        [TestMethod]
        public void Catalogo_CodigosYNombres_SonLosDeLaApi()
        {
            var esperado = new Dictionary<string, string>
            {
                { "Pedido", "Pedido" },
                { "PediraMasTarde", "Pedirá más tarde" },
                { "NoNecesita", "No necesita" },
                { "Precio", "Precio" },
                { "Competencia", "Competencia" },
                { "CerradoTraspaso", "Cerrado / traspaso" },
                { "SinStock", "Sin stock" },
                { "NoDecide", "No decide" },
                { "SoloInformacion", "Solo información" },
                { "Otro", "Otro" }
            };

            CollectionAssert.AreEqual(esperado.Keys.ToList(), ResultadoRapport.Codigos.ToList(), "El orden de los chips es el de la API");
            foreach (var par in esperado)
            {
                Assert.AreEqual(par.Value, ResultadoRapport.Nombres[par.Key], par.Key);
                Assert.AreEqual(par.Value, ResultadoRapport.NombreDe(par.Key));
            }
            Assert.AreEqual(esperado.Count, ResultadoRapport.Opciones.Count);
            Assert.IsTrue(ResultadoRapport.Opciones.All(o => esperado[o.Codigo] == o.Nombre));
        }

        [TestMethod]
        public void Catalogo_EsValidoYNombreDe_ConValoresRaros()
        {
            Assert.IsTrue(ResultadoRapport.EsValido("PediraMasTarde"));
            Assert.IsTrue(ResultadoRapport.EsValido(" Pedido "), "La API hace Trim");
            Assert.IsFalse(ResultadoRapport.EsValido(null));
            Assert.IsFalse(ResultadoRapport.EsValido(""));
            Assert.IsFalse(ResultadoRapport.EsValido("pedido"), "Los códigos distinguen mayúsculas (se mandan tal cual)");
            Assert.AreEqual(string.Empty, ResultadoRapport.NombreDe(null));
            Assert.AreEqual("Inventado", ResultadoRapport.NombreDe("Inventado"), "Un código desconocido (API más nueva) se enseña tal cual");
        }

        // ---------- DTO ----------

        [TestMethod]
        public void Dto_SinMarcarNada_ViajaNullEnResultadoYProximoContacto()
        {
            var rapport = new SeguimientoClienteDTO { Cliente = "15191", Comentarios = "Llamada" };

            var json = JObject.FromObject(rapport);

            Assert.IsNull(rapport.Resultado);
            Assert.IsNull(rapport.ProximoContacto);
            Assert.AreEqual(JTokenType.Null, json["Resultado"].Type, "La API con null no guarda nada");
            Assert.AreEqual(JTokenType.Null, json["ProximoContacto"].Type);
        }

        [TestMethod]
        public void Dto_ConResultadoYFecha_ViajanConElCodigoYSoloLaFecha()
        {
            var rapport = new SeguimientoClienteDTO
            {
                Resultado = ResultadoRapport.PEDIRA_MAS_TARDE,
                ProximoContacto = new DateTime(2026, 11, 9, 17, 35, 0)
            };

            var json = JObject.FromObject(rapport);

            Assert.AreEqual("PediraMasTarde", json["Resultado"].Value<string>());
            Assert.AreEqual(new DateTime(2026, 11, 9), rapport.ProximoContacto, "Solo la fecha: la hora se descarta");
            Assert.AreEqual(new DateTime(2026, 11, 9), json["ProximoContacto"].Value<DateTime>());
        }

        [TestMethod]
        public void Dto_ResultadoEnBlanco_SeGuardaComoNull()
        {
            var rapport = new SeguimientoClienteDTO { Resultado = "Pedido" };
            var cambios = new List<string>();
            rapport.PropertyChanged += (_, e) => cambios.Add(e.PropertyName);

            rapport.Resultado = "   ";

            Assert.IsNull(rapport.Resultado);
            CollectionAssert.Contains(cambios, "Resultado");
        }

        // ---------- ViewModel: chip «Pedido» y check «Pedido» ----------

        private static (RapportViewModel vm, IServicioDialogos dialogo, IRapportService servicio) CrearViewModel(Action<Action<ResultadoDialogo>> contestar = null)
        {
            var configuracion = A.Fake<IConfiguracion>();
            A.CallTo(() => configuracion.leerParametro(A<string>._, A<string>._)).Returns(Task.FromResult("NV"));
            A.CallTo(() => configuracion.usuario).Returns("vendedor");
            var dialogo = A.Fake<IServicioDialogos>();
            if (contestar != null)
            {
                A.CallTo(() => dialogo.ShowDialog(MotivoCambioEstadoDialogNombre, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                    .Invokes((string nombre, ParametrosDialogo parametros, Action<ResultadoDialogo> callback) => contestar(callback));
            }
            var servicio = A.Fake<IRapportService>();
            A.CallTo(() => servicio.crearRapport(A<SeguimientoClienteDTO>._)).Returns(Task.FromResult("Guardado"));
            A.CallTo(() => servicio.QuitarDeMiListado(A<SeguimientoClienteDTO>._, A<string>._, A<string>._, A<string>._, A<string>._)).Returns(Task.FromResult(true));
            var vm = new RapportViewModel(configuracion, servicio, dialogo, new WeakReferenceMessenger(),
                A.Fake<Nesto.Infrastructure.Services.IServicioSugerenciasProductos>(), A.Fake<IServicioNavegacion>());
            return (vm, dialogo, servicio);
        }

        private const string MotivoCambioEstadoDialogNombre = ControlesUsuario.Dialogs.MotivoCambioEstadoDialogViewModel.NOMBRE;

        [TestMethod]
        public void ViewModel_LosChipsSonElCatalogoEnOrden()
        {
            var (vm, _, _) = CrearViewModel();

            CollectionAssert.AreEqual(ResultadoRapport.Codigos.ToList(), vm.ResultadosRapport.Select(o => o.Codigo).ToList());
        }

        [TestMethod]
        public void ViewModel_MarcarChipPedido_MarcaElCheckPedido()
        {
            var (vm, _, _) = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor" };

            vm.rapport.Resultado = ResultadoRapport.PEDIDO;

            Assert.IsTrue(vm.rapport.Pedido);
        }

        [TestMethod]
        public void ViewModel_MarcarCheckPedidoSinResultado_PoneElChipPedido_YDesmarcarloLoQuita()
        {
            var (vm, _, _) = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor" };

            vm.rapport.Pedido = true;
            Assert.AreEqual(ResultadoRapport.PEDIDO, vm.rapport.Resultado);

            vm.rapport.Pedido = false;
            Assert.IsNull(vm.rapport.Resultado);
        }

        [TestMethod]
        public void ViewModel_CheckPedidoConOtroResultado_NoLoPisa()
        {
            var (vm, _, _) = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor", Resultado = ResultadoRapport.PRECIO };

            vm.rapport.Pedido = true;

            Assert.AreEqual(ResultadoRapport.PRECIO, vm.rapport.Resultado, "El vendedor eligió «Precio» a propósito");
            Assert.IsTrue(vm.rapport.Pedido);
        }

        [TestMethod]
        public void ViewModel_QuitarResultado_DejaNull()
        {
            var (vm, _, _) = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor", Resultado = ResultadoRapport.OTRO };

            vm.QuitarResultadoCommand.Execute(null);

            Assert.IsNull(vm.rapport.Resultado);
        }

        [TestMethod]
        public void ViewModel_Guardar_MandaElMismoDtoConResultadoYFecha()
        {
            var (vm, _, servicio) = CrearViewModel();
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor", Cliente = "15191", Contacto = "0", Resultado = ResultadoRapport.SIN_STOCK, ProximoContacto = new DateTime(2026, 11, 9) };
            vm.ClienteCompleto = new ControlesUsuario.Models.ClienteDTO { cliente = "15191", contacto = "0", estado = 0 };

            vm.cmdGuardarCambios.Execute(null);

            A.CallTo(() => servicio.crearRapport(A<SeguimientoClienteDTO>.That.Matches(r =>
                r.Resultado == "SinStock" && r.ProximoContacto == new DateTime(2026, 11, 9)))).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void ViewModel_LaApiDevuelve400_SeEnsenaSuMensajeTalCual()
        {
            var (vm, dialogo, servicio) = CrearViewModel();
            const string mensajeApi = "El resultado del rapport 'Foo' no está en el catálogo. Valores admitidos: Pedido, PediraMasTarde.";
            A.CallTo(() => servicio.crearRapport(A<SeguimientoClienteDTO>._)).Throws(new Exception("Se ha producido un error al crear el rapport\r" + mensajeApi));
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor", Cliente = "15191", Contacto = "0", Resultado = "Foo" };
            vm.ClienteCompleto = new ControlesUsuario.Models.ClienteDTO { cliente = "15191", contacto = "0", estado = 0 };

            vm.cmdGuardarCambios.Execute(null);

            A.CallTo(() => dialogo.ShowError(A<string>.That.Contains(mensajeApi))).MustHaveHappenedOnceExactly();
        }

        // ---------- «Quitar de mi listado»: motivo del cambio de estado ----------

        [TestMethod]
        public void QuitarDeMiListado_PreguntaElMotivo_YLoPasaAlServicio()
        {
            var (vm, _, servicio) = CrearViewModel(callback => callback(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo
            {
                { "motivo", MotivoCambioEstadoCliente.CIERRE },
                { "comentario", "Cerró en septiembre" }
            })));
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor", Cliente = "15191", Contacto = "0", Empresa = "1" };
            vm.ClienteCompleto = new ControlesUsuario.Models.ClienteDTO { cliente = "15191", contacto = "0", estado = 0 };
            vm.QuitarDeMiListado = true;

            vm.cmdGuardarCambios.Execute(null);

            A.CallTo(() => servicio.QuitarDeMiListado(vm.rapport, A<string>._, A<string>._, "Cierre", "Cerró en septiembre")).MustHaveHappenedOnceExactly();
            Assert.IsFalse(vm.QuitarDeMiListado);
        }

        [TestMethod]
        public void QuitarDeMiListado_SinMotivo_SeQuitaIgualYNoSeMandaNada()
        {
            var (vm, _, servicio) = CrearViewModel(callback => callback(new ResultadoDialogo(ResultadoBoton.Cancel)));
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor", Cliente = "15191", Contacto = "0", Empresa = "1" };
            vm.ClienteCompleto = new ControlesUsuario.Models.ClienteDTO { cliente = "15191", contacto = "0", estado = 0 };
            vm.QuitarDeMiListado = true;

            vm.cmdGuardarCambios.Execute(null);

            A.CallTo(() => servicio.QuitarDeMiListado(vm.rapport, A<string>._, A<string>._, null, null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void QuitarDeMiListado_AceptarConTodoEnBlanco_NoSeMandaNada()
        {
            var (vm, _, _) = CrearViewModel(callback => callback(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "motivo", null }, { "comentario", "  " } })));
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor", Cliente = "15191" };
            vm.QuitarDeMiListado = true;

            Assert.IsNull(vm.PedirMotivoDejarDeVisitar());
        }

        [TestMethod]
        public void SinQuitarDeMiListado_NoSePreguntaElMotivo()
        {
            var (vm, dialogo, servicio) = CrearViewModel(callback => callback(new ResultadoDialogo(ResultadoBoton.OK)));
            vm.rapport = new SeguimientoClienteDTO { Usuario = "vendedor", Cliente = "15191", Contacto = "0" };
            vm.ClienteCompleto = new ControlesUsuario.Models.ClienteDTO { cliente = "15191", contacto = "0", estado = 0 };

            vm.cmdGuardarCambios.Execute(null);

            A.CallTo(() => dialogo.ShowDialog(MotivoCambioEstadoDialogNombre, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).MustNotHaveHappened();
            A.CallTo(() => servicio.QuitarDeMiListado(A<SeguimientoClienteDTO>._, A<string>._, A<string>._, A<string>._, A<string>._)).MustNotHaveHappened();
        }
    }
}
