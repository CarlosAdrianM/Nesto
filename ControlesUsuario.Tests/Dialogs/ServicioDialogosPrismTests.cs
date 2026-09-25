using ControlesUsuario.Dialogs;
using FakeItEasy;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Threading.Tasks;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2): la primera implementación de IServicioDialogos es delegación pura en el
    /// IDialogService de Prism. Estos tests fijan que se abre el mismo diálogo, con los mismos
    /// parámetros, y que el resultado vuelve traducido sin perder nada.
    /// </summary>
    [TestClass]
    public class ServicioDialogosPrismTests
    {
        private IDialogService _prism;
        private ServicioDialogosPrism _servicio;
        private string _nombreCapturado;
        private IDialogParameters _parametrosCapturados;

        [TestInitialize]
        public void Setup()
        {
            _prism = A.Fake<IDialogService>();
            _servicio = new ServicioDialogosPrism(_prism);
        }

        private void ResponderCon(ButtonResult boton, IDialogParameters parametrosVuelta = null)
        {
            A.CallTo(() => _prism.ShowDialog(A<string>._, A<IDialogParameters>._, A<Action<IDialogResult>>._))
                .Invokes((string nombre, IDialogParameters parametros, Action<IDialogResult> callback) =>
                {
                    _nombreCapturado = nombre;
                    _parametrosCapturados = parametros;
                    callback?.Invoke(new DialogResult(boton, parametrosVuelta ?? new DialogParameters()));
                });
        }

        [TestMethod]
        public void Constructor_SinDialogService_Lanza()
        {
            _ = Assert.ThrowsException<ArgumentNullException>(() => new ServicioDialogosPrism(null));
        }

        [TestMethod]
        public void ShowError_AbreNotificationDialogConTituloDeError()
        {
            ResponderCon(ButtonResult.OK);

            _servicio.ShowError("Algo ha fallado");

            Assert.AreEqual("NotificationDialog", _nombreCapturado);
            Assert.AreEqual("¡Error!", _parametrosCapturados.GetValue<string>("title"));
            Assert.AreEqual("Algo ha fallado", _parametrosCapturados.GetValue<string>("message"));
        }

        [TestMethod]
        public void ShowError_ConJsonDeNestoApi_MuestraSoloElMensaje()
        {
            ResponderCon(ButtonResult.OK);

            _servicio.ShowError("{\"error\":{\"code\":\"X\",\"message\":\"Mensaje legible\"}}");

            Assert.AreEqual("Mensaje legible", _parametrosCapturados.GetValue<string>("message"));
        }

        [TestMethod]
        public void ShowNotification_ConTitulo_PasaTituloYMensaje()
        {
            ResponderCon(ButtonResult.OK);

            _servicio.ShowNotification("Título", "Mensaje");

            Assert.AreEqual("NotificationDialog", _nombreCapturado);
            Assert.AreEqual("Título", _parametrosCapturados.GetValue<string>("title"));
            Assert.AreEqual("Mensaje", _parametrosCapturados.GetValue<string>("message"));
        }

        [TestMethod]
        public void ShowConfirmationAnswer_Aceptar_DevuelveTrue()
        {
            ResponderCon(ButtonResult.OK);

            bool respuesta = _servicio.ShowConfirmationAnswer("Guardar", "¿Seguro?");

            Assert.IsTrue(respuesta);
            Assert.AreEqual("ConfirmationDialog", _nombreCapturado);
        }

        [TestMethod]
        public void ShowConfirmationAnswer_Cancelar_DevuelveFalse()
        {
            ResponderCon(ButtonResult.Cancel);

            Assert.IsFalse(_servicio.ShowConfirmationAnswer("Guardar", "¿Seguro?"));
        }

        [TestMethod]
        public async Task ShowConfirmationAsync_Aceptar_DevuelveTrue()
        {
            ResponderCon(ButtonResult.OK);

            Assert.IsTrue(await _servicio.ShowConfirmationAsync("¿Seguro?"));
            Assert.AreEqual("Confirmación", _parametrosCapturados.GetValue<string>("title"));
        }

        [TestMethod]
        public void GetAmount_Aceptar_DevuelveElImporte()
        {
            ResponderCon(ButtonResult.OK, new DialogParameters { { "amount", 12.5M } });

            Assert.AreEqual(12.5M, _servicio.GetAmount("Importe", "¿Cuánto?"));
            Assert.AreEqual("InputAmountDialog", _nombreCapturado);
        }

        [TestMethod]
        public void GetText_Cancelar_DevuelveNull()
        {
            ResponderCon(ButtonResult.Cancel, new DialogParameters { { "text", "no debe llegar" } });

            Assert.IsNull(_servicio.GetText("Texto", "Escribe"));
        }

        [TestMethod]
        public void ShowInputText_TraduceResultadoYParametrosDeVuelta()
        {
            ResponderCon(ButtonResult.OK, new DialogParameters { { "text", "hola" } });
            ResultadoDialogo resultado = null;

            _servicio.ShowInputText("Texto", "Escribe", "por defecto", r => resultado = r);

            Assert.AreEqual("InputTextDialog", _nombreCapturado);
            Assert.AreEqual("por defecto", _parametrosCapturados.GetValue<string>("defaultText"));
            Assert.AreEqual(ResultadoBoton.OK, resultado.Result);
            Assert.AreEqual("hola", resultado.Parameters.GetValue<string>("text"));
        }

        [TestMethod]
        public void ShowDialog_PasaLosParametrosPropiosComoDialogParameters()
        {
            ResponderCon(ButtonResult.Yes);
            ResultadoDialogo resultado = null;
            var objeto = new object();

            _servicio.ShowDialog("MiDialogo", new ParametrosDialogo { { "numero", 7 }, { "objeto", objeto } }, r => resultado = r);

            Assert.AreEqual("MiDialogo", _nombreCapturado);
            Assert.AreEqual(7, _parametrosCapturados.GetValue<int>("numero"));
            Assert.AreSame(objeto, _parametrosCapturados.GetValue<object>("objeto"));
            Assert.AreEqual(ResultadoBoton.Yes, resultado.Result);
        }

        [TestMethod]
        public void ShowDialog_SinParametrosNiCallback_PasaNullAPrism()
        {
            _servicio.ShowDialog("MiDialogo", null, null);

            A.CallTo(() => _prism.ShowDialog("MiDialogo", null, null)).MustHaveHappenedOnceExactly();
        }

        [TestMethod]
        public async Task ShowDialogAsync_DevuelveElResultadoTraducido()
        {
            ResponderCon(ButtonResult.No, new DialogParameters { { "motivo", "x" } });

            ResultadoDialogo resultado = await _servicio.ShowDialogAsync("MiDialogo");

            Assert.AreEqual(ResultadoBoton.No, resultado.Result);
            Assert.AreEqual("x", resultado.Parameters.GetValue<string>("motivo"));
            Assert.IsNull(_parametrosCapturados);
        }

        [TestMethod]
        public void ResultadoBoton_TieneLosMismosValoresQueButtonResult()
        {
            foreach (ButtonResult boton in Enum.GetValues(typeof(ButtonResult)))
            {
                Assert.AreEqual(boton.ToString(), ((ResultadoBoton)(int)boton).ToString(), $"ButtonResult.{boton}");
            }
        }
    }
}
