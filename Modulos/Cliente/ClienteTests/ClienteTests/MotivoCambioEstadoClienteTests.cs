using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Nesto.Infrastructure.Shared;
using Nesto.Modulos.Cliente;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ClienteTests
{
    /// <summary>
    /// Nesto#558 (NestoAPI#716, 2.2): el catálogo de motivos del cambio de estado es copia exacta del de la API; el diálogo
    /// devuelve el código y el comentario (o nada) y nunca bloquea; y el ClienteCrear del PUT DejarDeVisitar lleva los campos.
    /// </summary>
    [TestClass]
    public class MotivoCambioEstadoClienteTests
    {
        // ---------- Catálogo (copia exacta de NestoAPI/Infraestructure/Clientes/MotivoCambioEstadoCliente.cs) ----------

        [TestMethod]
        public void Catalogo_CodigosYNombres_SonLosDeLaApi()
        {
            var esperado = new Dictionary<string, string>
            {
                { "Cierre", "El centro ha cerrado" },
                { "Traspaso", "Traspaso" },
                { "Competencia", "Compra a la competencia" },
                { "Impago", "Impago" },
                { "SinActividad", "Sin actividad" },
                { "Duplicado", "Ficha duplicada" },
                { "CambioTitular", "Cambio de titular" },
                { "PeticionCliente", "Lo pide el cliente" },
                { "Distribuidor", "Pasa a distribuidor" },
                { "PublicoFinal", "Pasa a público final" },
                { "Reactivacion", "Reactivación" },
                { "Otro", "Otro" }
            };

            CollectionAssert.AreEqual(esperado.Keys.ToList(), MotivoCambioEstadoCliente.CodigosManuales.ToList(), "El orden del desplegable es el de la API");
            foreach (var par in esperado)
            {
                Assert.AreEqual(par.Value, MotivoCambioEstadoCliente.Nombres[par.Key], par.Key);
                Assert.AreEqual(par.Value, MotivoCambioEstadoCliente.NombreDe(par.Key));
            }
            Assert.AreEqual(esperado.Count, MotivoCambioEstadoCliente.Opciones.Count);
            Assert.IsTrue(MotivoCambioEstadoCliente.Opciones.All(o => esperado[o.Codigo] == o.Nombre));
            Assert.IsFalse(MotivoCambioEstadoCliente.Nombres.ContainsKey("DejarDeVisitar"), "Los automáticos los pone la API, no se ofrecen");
        }

        [TestMethod]
        public void Catalogo_PideMotivo_SoloSiElEstadoCambiaAUnaBajaOSaleDeElla()
        {
            foreach (var baja in new[] { 9, -1, 93, 11, 22, 95, 99 })
            {
                Assert.IsTrue(MotivoCambioEstadoCliente.EsEstadoBaja(baja), $"Estado {baja}");
                Assert.IsTrue(MotivoCambioEstadoCliente.PideMotivo(0, baja), $"0 → {baja}");
                Assert.IsTrue(MotivoCambioEstadoCliente.PideMotivo(baja, 0), $"{baja} → 0 (reactivación)");
                Assert.IsFalse(MotivoCambioEstadoCliente.PideMotivo(baja, baja), $"{baja} sin cambio");
            }
            Assert.IsFalse(MotivoCambioEstadoCliente.PideMotivo(0, 0), "Sin cambio no se pregunta");
            Assert.IsFalse(MotivoCambioEstadoCliente.PideMotivo(0, 7), "0 → 7 (no visitable) no es una baja");
            Assert.IsFalse(MotivoCambioEstadoCliente.PideMotivo(0, 6), "0 → 6 (distribuidor) no es una baja");
            Assert.IsFalse(MotivoCambioEstadoCliente.PideMotivo(0, null));
            Assert.IsTrue(MotivoCambioEstadoCliente.PideMotivo(null, -1), "Sin estado de partida conocido, se pregunta igual");
        }

        [TestMethod]
        public void Respuesta_LimpiaLoQueNoVale()
        {
            var vacia = new MotivoCambioEstado(null, "   ");
            Assert.IsTrue(vacia.EstaVacio);
            Assert.IsNull(vacia.Motivo);
            Assert.IsNull(vacia.Comentario);

            var fueraDeCatalogo = new MotivoCambioEstado("Inventado", "x");
            Assert.IsNull(fueraDeCatalogo.Motivo, "Un código que no está en el catálogo no se manda (sería un 400)");
            Assert.AreEqual("x", fueraDeCatalogo.Comentario);

            var larga = new MotivoCambioEstado(" Cierre ", new string('a', 250));
            Assert.AreEqual("Cierre", larga.Motivo);
            Assert.AreEqual(200, larga.Comentario.Length, "La API no admite más de 200 (400)");
        }

        // ---------- Diálogo ----------

        [TestMethod]
        public void Dialogo_AlAbrir_TomaTituloYMensaje_YOfreceElCatalogoConSinMotivoDelante()
        {
            var vm = new MotivoCambioEstadoDialogViewModel();

            vm.OnDialogOpened(new ParametrosDialogo { { "title", "Dejar de visitar" }, { "message", "¿Por qué?" } });

            Assert.AreEqual("Dejar de visitar", vm.Title);
            Assert.AreEqual("¿Por qué?", vm.Message);
            Assert.IsNull(vm.Motivos[0].Codigo);
            CollectionAssert.AreEqual(MotivoCambioEstadoCliente.CodigosManuales.ToList(), vm.Motivos.Skip(1).Select(o => o.Codigo).ToList());
            Assert.AreEqual(200, vm.LongitudComentario);
        }

        [TestMethod]
        public void Dialogo_Aceptar_DevuelveCodigoYComentario()
        {
            var vm = new MotivoCambioEstadoDialogViewModel { Motivo = MotivoCambioEstadoCliente.TRASPASO, Comentario = " Lo lleva su hija " };
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.AceptarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("Traspaso", resultado.Parameters.GetValue<string>("motivo"));
            Assert.AreEqual("Lo lleva su hija", resultado.Parameters.GetValue<string>("comentario"));
        }

        [TestMethod]
        public void Dialogo_AceptarSinRellenar_DevuelveOkConTodoNull()
        {
            var vm = new MotivoCambioEstadoDialogViewModel();
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.AceptarCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.OK, resultado.Result, "No se bloquea a quien no lo sabe");
            Assert.IsNull(resultado.Parameters.GetValue<string>("motivo"));
            Assert.IsNull(resultado.Parameters.GetValue<string>("comentario"));
        }

        [TestMethod]
        public void Dialogo_SinMotivo_DevuelveCancel()
        {
            var vm = new MotivoCambioEstadoDialogViewModel { Motivo = MotivoCambioEstadoCliente.IMPAGO };
            ResultadoDialogo resultado = null;
            vm.RequestClose += r => resultado = r;

            vm.SinMotivoCommand.Execute(null);

            Assert.AreEqual(ResultadoBoton.Cancel, resultado.Result);
        }

        // ---------- La pregunta (lo que usan la ficha y el rapport) ----------

        private static IServicioDialogos DialogoQueContesta(ResultadoDialogo respuesta)
        {
            var dialogo = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogo.ShowDialog(MotivoCambioEstadoDialogViewModel.NOMBRE, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._))
                .Invokes((string nombre, ParametrosDialogo parametros, Action<ResultadoDialogo> callback) => callback(respuesta));
            return dialogo;
        }

        [TestMethod]
        public void Pedir_ConRespuesta_DevuelveMotivoYComentario()
        {
            var dialogo = DialogoQueContesta(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "motivo", "Cierre" }, { "comentario", "Cerró" } }));

            var respuesta = PeticionMotivoCambioEstado.Pedir(dialogo, "Título", "Mensaje");

            Assert.AreEqual("Cierre", respuesta.Motivo);
            Assert.AreEqual("Cerró", respuesta.Comentario);
            A.CallTo(() => dialogo.ShowDialog(MotivoCambioEstadoDialogViewModel.NOMBRE,
                A<ParametrosDialogo>.That.Matches(p => p.GetValue<string>("title") == "Título" && p.GetValue<string>("message") == "Mensaje"),
                A<Action<ResultadoDialogo>>._)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public void Pedir_SinMotivoOEnBlanco_DevuelveNull()
        {
            Assert.IsNull(PeticionMotivoCambioEstado.Pedir(DialogoQueContesta(new ResultadoDialogo(ResultadoBoton.Cancel)), "T", "M"));
            Assert.IsNull(PeticionMotivoCambioEstado.Pedir(DialogoQueContesta(new ResultadoDialogo(ResultadoBoton.OK)), "T", "M"), "OK sin rellenar nada");
            Assert.IsNull(PeticionMotivoCambioEstado.Pedir(null, "T", "M"), "Sin servicio de diálogos no se rompe nada");
        }

        [TestMethod]
        public void Pedir_SiElDialogoFalla_DevuelveNullYNoLanza()
        {
            var dialogo = A.Fake<IServicioDialogos>();
            A.CallTo(() => dialogo.ShowDialog(A<string>._, A<ParametrosDialogo>._, A<Action<ResultadoDialogo>>._)).Throws(new InvalidOperationException("sin registrar"));

            Assert.IsNull(PeticionMotivoCambioEstado.Pedir(dialogo, "T", "M"));
        }

        // ---------- ClienteCrear (PUT Clientes/DejarDeVisitar) ----------

        [TestMethod]
        public void ClienteCrear_LlevaMotivoYComentario_YNullSiNoSeEligen()
        {
            var sinNada = JObject.FromObject(new ClienteCrear { Empresa = "1", Cliente = "15191" });
            Assert.AreEqual(JTokenType.Null, sinNada["MotivoCambioEstado"].Type, "La API anota «DejarDeVisitar» ella sola");
            Assert.AreEqual(JTokenType.Null, sinNada["ComentarioCambioEstado"].Type);

            var conMotivo = JObject.FromObject(new ClienteCrear { Empresa = "1", Cliente = "15191", MotivoCambioEstado = MotivoCambioEstadoCliente.COMPETENCIA, ComentarioCambioEstado = "Compra en otro sitio" });
            Assert.AreEqual("Competencia", conMotivo["MotivoCambioEstado"].Value<string>());
            Assert.AreEqual("Compra en otro sitio", conMotivo["ComentarioCambioEstado"].Value<string>());
        }
    }
}
