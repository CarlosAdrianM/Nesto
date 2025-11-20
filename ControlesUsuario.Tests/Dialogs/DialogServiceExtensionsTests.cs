using Microsoft.VisualStudio.TestTools.UnitTesting;
using FakeItEasy;
using ControlesUsuario.Dialogs;
using Prism.Services.Dialogs;
using System.Linq;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Tests para DialogServiceExtensions, especialmente para validar limpieza de mensajes con JSON.
    /// Carlos 20/11/24: Verifica que los errores con JSON se muestren solo con el texto limpio.
    /// </summary>
    [TestClass]
    public class DialogServiceExtensionsTests
    {
        /// <summary>
        /// Verifica que un mensaje de error simple se muestre tal cual.
        /// </summary>
        [TestMethod]
        [TestCategory("DialogService")]
        [TestCategory("ErrorHandling")]
        public void ShowError_MensajeSimple_SeMantieneIntacto()
        {
            // Arrange
            var dialogService = A.Fake<IDialogService>();
            string mensajeSimple = "Error al conectar con el servidor";

            // Capturar los parámetros pasados al diálogo
            DialogParameters parametrosCapturados = null;
            A.CallTo(() => dialogService.ShowDialog(
                A<string>._,
                A<IDialogParameters>._,
                A<System.Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters parameters, System.Action<IDialogResult> callback) =>
                {
                    parametrosCapturados = parameters as DialogParameters;
                });

            // Act
            dialogService.ShowError(mensajeSimple);

            // Assert
            Assert.IsNotNull(parametrosCapturados, "ShowDialog debería haber sido llamado");
            Assert.AreEqual(mensajeSimple, parametrosCapturados.GetValue<string>("message"),
                "El mensaje simple debería mostrarse tal cual");
        }

        /// <summary>
        /// Verifica que un mensaje con JSON de NestoAPI (formato error.message) extraiga el mensaje correcto.
        /// Carlos 20/11/24: Formato según GlobalExceptionFilter de NestoAPI.
        /// </summary>
        [TestMethod]
        [TestCategory("DialogService")]
        [TestCategory("ErrorHandling")]
        public void ShowError_MensajeConJSONNestoAPI_ExtraeErrorMessage()
        {
            // Arrange
            var dialogService = A.Fake<IDialogService>();
            string mensajeConJson = "{\"error\":{\"code\":\"FACTURACION_IVA_FALTANTE\",\"message\":\"El pedido 12345 no se puede facturar porque falta el IVA\",\"timestamp\":\"2025-01-19T10:30:00Z\"}}";
            string mensajeEsperado = "El pedido 12345 no se puede facturar porque falta el IVA";

            // Capturar los parámetros pasados al diálogo
            DialogParameters parametrosCapturados = null;
            A.CallTo(() => dialogService.ShowDialog(
                A<string>._,
                A<IDialogParameters>._,
                A<System.Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters parameters, System.Action<IDialogResult> callback) =>
                {
                    parametrosCapturados = parameters as DialogParameters;
                });

            // Act
            dialogService.ShowError(mensajeConJson);

            // Assert
            Assert.IsNotNull(parametrosCapturados, "ShowDialog debería haber sido llamado");
            Assert.AreEqual(mensajeEsperado, parametrosCapturados.GetValue<string>("message"),
                "El mensaje debería extraer error.message del JSON de NestoAPI");
        }

        /// <summary>
        /// Verifica que un mensaje con JSON de NestoAPI con details también funcione.
        /// </summary>
        [TestMethod]
        [TestCategory("DialogService")]
        [TestCategory("ErrorHandling")]
        public void ShowError_MensajeConJSONYDetails_ExtraeErrorMessage()
        {
            // Arrange
            var dialogService = A.Fake<IDialogService>();
            string mensajeConJson = "{\"error\":{\"code\":\"PEDIDO_INVALIDO\",\"message\":\"No se pudo crear el pedido\",\"details\":{\"empresa\":\"1\",\"cliente\":\"12345\"},\"timestamp\":\"2025-01-19T10:30:00Z\"}}";
            string mensajeEsperado = "No se pudo crear el pedido";

            // Capturar los parámetros pasados al diálogo
            DialogParameters parametrosCapturados = null;
            A.CallTo(() => dialogService.ShowDialog(
                A<string>._,
                A<IDialogParameters>._,
                A<System.Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters parameters, System.Action<IDialogResult> callback) =>
                {
                    parametrosCapturados = parameters as DialogParameters;
                });

            // Act
            dialogService.ShowError(mensajeConJson);

            // Assert
            Assert.IsNotNull(parametrosCapturados, "ShowDialog debería haber sido llamado");
            Assert.AreEqual(mensajeEsperado, parametrosCapturados.GetValue<string>("message"),
                "El mensaje debería extraer error.message incluso con details");
        }

        /// <summary>
        /// Verifica que un mensaje con texto antes del JSON extraiga el mensaje del JSON si es posible.
        /// </summary>
        [TestMethod]
        [TestCategory("DialogService")]
        [TestCategory("ErrorHandling")]
        public void ShowError_TextoAntesDeJSON_ExtraeErrorMessage()
        {
            // Arrange
            var dialogService = A.Fake<IDialogService>();
            string mensajeConJson = "Error en la API: {\"error\":{\"code\":\"INTERNAL_ERROR\",\"message\":\"Error al conectar con la base de datos\",\"timestamp\":\"2025-01-19T10:30:00Z\"}}";
            string mensajeEsperado = "Error al conectar con la base de datos";

            // Capturar los parámetros pasados al diálogo
            DialogParameters parametrosCapturados = null;
            A.CallTo(() => dialogService.ShowDialog(
                A<string>._,
                A<IDialogParameters>._,
                A<System.Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters parameters, System.Action<IDialogResult> callback) =>
                {
                    parametrosCapturados = parameters as DialogParameters;
                });

            // Act
            dialogService.ShowError(mensajeConJson);

            // Assert
            Assert.IsNotNull(parametrosCapturados, "ShowDialog debería haber sido llamado");
            Assert.AreEqual(mensajeEsperado, parametrosCapturados.GetValue<string>("message"),
                "El mensaje debería extraer error.message del JSON incluso con texto antes");
        }

        /// <summary>
        /// Verifica que un mensaje con JSON de NestoAPI en modo DEBUG (con stackTrace) extraiga solo el mensaje.
        /// </summary>
        [TestMethod]
        [TestCategory("DialogService")]
        [TestCategory("ErrorHandling")]
        public void ShowError_MensajeConJSONYStackTrace_ExtraeSoloMessage()
        {
            // Arrange
            var dialogService = A.Fake<IDialogService>();
            string mensajeConJson = "{\"error\":{\"code\":\"FACTURACION_ERROR\",\"message\":\"No se pudo facturar el pedido\",\"details\":{\"pedido\":12345},\"timestamp\":\"2025-01-19T10:30:00Z\",\"stackTrace\":\"at NestoAPI.Controllers.FacturasController.Facturar()\\r\\n at System.Web.Http...\"}}";
            string mensajeEsperado = "No se pudo facturar el pedido";

            // Capturar los parámetros pasados al diálogo
            DialogParameters parametrosCapturados = null;
            A.CallTo(() => dialogService.ShowDialog(
                A<string>._,
                A<IDialogParameters>._,
                A<System.Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters parameters, System.Action<IDialogResult> callback) =>
                {
                    parametrosCapturados = parameters as DialogParameters;
                });

            // Act
            dialogService.ShowError(mensajeConJson);

            // Assert
            Assert.IsNotNull(parametrosCapturados, "ShowDialog debería haber sido llamado");
            Assert.AreEqual(mensajeEsperado, parametrosCapturados.GetValue<string>("message"),
                "El mensaje debería extraer solo error.message ignorando stackTrace y otros campos");
        }

        /// <summary>
        /// Verifica que un JSON sin la estructura error.message se mantenga intacto (fallback).
        /// </summary>
        [TestMethod]
        [TestCategory("DialogService")]
        [TestCategory("ErrorHandling")]
        public void ShowError_JSONSinEstructuraError_SeMantieneIntacto()
        {
            // Arrange
            var dialogService = A.Fake<IDialogService>();
            string jsonSinEstructuraError = "{\"errorCode\":\"Invalid token\",\"status\":401}";

            // Capturar los parámetros pasados al diálogo
            DialogParameters parametrosCapturados = null;
            A.CallTo(() => dialogService.ShowDialog(
                A<string>._,
                A<IDialogParameters>._,
                A<System.Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters parameters, System.Action<IDialogResult> callback) =>
                {
                    parametrosCapturados = parameters as DialogParameters;
                });

            // Act
            dialogService.ShowError(jsonSinEstructuraError);

            // Assert
            Assert.IsNotNull(parametrosCapturados, "ShowDialog debería haber sido llamado");
            Assert.AreEqual(jsonSinEstructuraError, parametrosCapturados.GetValue<string>("message"),
                "Un JSON sin la estructura error.message debería mantenerse intacto (fallback)");
        }

        /// <summary>
        /// Verifica que un mensaje con texto antes de JSON malformado use el texto como fallback.
        /// </summary>
        [TestMethod]
        [TestCategory("DialogService")]
        [TestCategory("ErrorHandling")]
        public void ShowError_MensajeConJSONMalformado_UsaTextoAnteriorComoFallback()
        {
            // Arrange
            var dialogService = A.Fake<IDialogService>();
            string mensajeConJson = "No se pudo completar la operación. {\"details\":\"timeout\""; // JSON malformado (falta })
            string mensajeEsperado = "No se pudo completar la operación";

            // Capturar los parámetros pasados al diálogo
            DialogParameters parametrosCapturados = null;
            A.CallTo(() => dialogService.ShowDialog(
                A<string>._,
                A<IDialogParameters>._,
                A<System.Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters parameters, System.Action<IDialogResult> callback) =>
                {
                    parametrosCapturados = parameters as DialogParameters;
                });

            // Act
            dialogService.ShowError(mensajeConJson);

            // Assert
            Assert.IsNotNull(parametrosCapturados, "ShowDialog debería haber sido llamado");
            Assert.AreEqual(mensajeEsperado, parametrosCapturados.GetValue<string>("message"),
                "El mensaje debería usar el texto anterior cuando el JSON está malformado (fallback)");
        }

        /// <summary>
        /// Verifica que null o empty se maneje correctamente.
        /// </summary>
        [TestMethod]
        [TestCategory("DialogService")]
        [TestCategory("ErrorHandling")]
        public void ShowError_MensajeNullOVacio_SeMantieneIntacto()
        {
            // Arrange
            var dialogService = A.Fake<IDialogService>();

            // Capturar los parámetros pasados al diálogo
            DialogParameters parametrosCapturados = null;
            A.CallTo(() => dialogService.ShowDialog(
                A<string>._,
                A<IDialogParameters>._,
                A<System.Action<IDialogResult>>._))
                .Invokes((string name, IDialogParameters parameters, System.Action<IDialogResult> callback) =>
                {
                    parametrosCapturados = parameters as DialogParameters;
                });

            // Act & Assert - null
            dialogService.ShowError(null);
            Assert.IsNull(parametrosCapturados?.GetValue<string>("message"),
                "Un mensaje null debería mantenerse como null");

            // Act & Assert - empty
            dialogService.ShowError("");
            Assert.AreEqual("", parametrosCapturados?.GetValue<string>("message"),
                "Un mensaje vacío debería mantenerse vacío");
        }
    }
}
