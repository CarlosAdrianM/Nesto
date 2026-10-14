using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2): los diálogos genéricos (aviso, error, confirmación, importe, texto) se componen en
    /// ServicioDialogosBase sobre ShowDialog: qué diálogo se abre, con qué parámetros y cómo se lee el
    /// resultado. Antes se probaban con ServicioDialogosPrism (borrado al pasar la ventana propia a todos).
    /// </summary>
    [TestClass]
    public class ServicioDialogosBaseTests
    {
        private ServicioDialogosDePrueba _servicio;

        [TestInitialize]
        public void Setup()
        {
            _servicio = new ServicioDialogosDePrueba();
        }

        private void ResponderCon(ResultadoBoton boton, ParametrosDialogo parametrosVuelta = null)
            => _servicio.Respuesta = new ResultadoDialogo(boton, parametrosVuelta ?? new ParametrosDialogo());

        [TestMethod]
        public void ShowError_AbreNotificationDialogConTituloDeError()
        {
            _servicio.ShowError("Algo ha fallado");

            Assert.AreEqual("NotificationDialog", _servicio.Nombre);
            Assert.AreEqual("¡Error!", _servicio.Parametros.GetValue<string>("title"));
            Assert.AreEqual("Algo ha fallado", _servicio.Parametros.GetValue<string>("message"));
        }

        [TestMethod]
        public void ShowError_ConJsonDeNestoApi_MuestraSoloElMensaje()
        {
            _servicio.ShowError("{\"error\":{\"code\":\"X\",\"message\":\"Mensaje legible\"}}");

            Assert.AreEqual("Mensaje legible", _servicio.Parametros.GetValue<string>("message"));
        }

        [TestMethod]
        public void ShowNotification_ConTitulo_PasaTituloYMensaje()
        {
            _servicio.ShowNotification("Título", "Mensaje");

            Assert.AreEqual("NotificationDialog", _servicio.Nombre);
            Assert.AreEqual("Título", _servicio.Parametros.GetValue<string>("title"));
            Assert.AreEqual("Mensaje", _servicio.Parametros.GetValue<string>("message"));
        }

        [TestMethod]
        public void ShowConfirmationAnswer_Aceptar_DevuelveTrue()
        {
            ResponderCon(ResultadoBoton.OK);

            bool respuesta = _servicio.ShowConfirmationAnswer("Guardar", "¿Seguro?");

            Assert.IsTrue(respuesta);
            Assert.AreEqual("ConfirmationDialog", _servicio.Nombre);
        }

        [TestMethod]
        public void ShowConfirmationAnswer_Cancelar_DevuelveFalse()
        {
            ResponderCon(ResultadoBoton.Cancel);

            Assert.IsFalse(_servicio.ShowConfirmationAnswer("Guardar", "¿Seguro?"));
        }

        [TestMethod]
        public async Task ShowConfirmationAsync_Aceptar_DevuelveTrue()
        {
            ResponderCon(ResultadoBoton.OK);

            Assert.IsTrue(await _servicio.ShowConfirmationAsync("¿Seguro?"));
            Assert.AreEqual("Confirmación", _servicio.Parametros.GetValue<string>("title"));
        }

        [TestMethod]
        public void GetAmount_Aceptar_DevuelveElImporte()
        {
            ResponderCon(ResultadoBoton.OK, new ParametrosDialogo { { "amount", 12.5M } });

            Assert.AreEqual(12.5M, _servicio.GetAmount("Importe", "¿Cuánto?"));
            Assert.AreEqual("InputAmountDialog", _servicio.Nombre);
        }

        [TestMethod]
        public void GetText_Cancelar_DevuelveNull()
        {
            ResponderCon(ResultadoBoton.Cancel, new ParametrosDialogo { { "text", "no debe llegar" } });

            Assert.IsNull(_servicio.GetText("Texto", "Escribe"));
        }

        [TestMethod]
        public void ShowInputText_PasaElTextoPorDefectoYDevuelveElEscrito()
        {
            ResponderCon(ResultadoBoton.OK, new ParametrosDialogo { { "text", "hola" } });
            ResultadoDialogo resultado = null;

            _servicio.ShowInputText("Texto", "Escribe", "por defecto", r => resultado = r);

            Assert.AreEqual("InputTextDialog", _servicio.Nombre);
            Assert.AreEqual("por defecto", _servicio.Parametros.GetValue<string>("defaultText"));
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("hola", resultado.Parameters.GetValue<string>("text"));
        }

        [TestMethod]
        public async Task ShowDialogAsync_DevuelveElResultado()
        {
            ResponderCon(ResultadoBoton.No, new ParametrosDialogo { { "motivo", "x" } });

            ResultadoDialogo resultado = await _servicio.ShowDialogAsync("MiDialogo");

            Assert.AreEqual("MiDialogo", _servicio.Nombre);
            Assert.AreEqual(ResultadoBoton.No, resultado.Result);
            Assert.AreEqual("x", resultado.Parameters.GetValue<string>("motivo"));
        }
    }
}
