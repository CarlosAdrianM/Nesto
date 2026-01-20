using Microsoft.VisualStudio.TestTools.UnitTesting;
using ControlesUsuario.Dialogs;
using Prism.Services.Dialogs;

namespace ControlesUsuario.Tests.Dialogs
{
    /// <summary>
    /// Tests para NotificationDialogViewModel.
    /// Issue #271: Verificar que el título por defecto se mantiene cuando no se pasa parámetro.
    /// </summary>
    [TestClass]
    public class NotificationDialogViewModelTests
    {
        /// <summary>
        /// Verifica que el título por defecto es "Información".
        /// </summary>
        [TestMethod]
        [TestCategory("NotificationDialog")]
        public void NotificationDialogViewModel_TituloPorDefecto_EsInformacion()
        {
            // Arrange & Act
            var viewModel = new NotificationDialogViewModel();

            // Assert
            Assert.AreEqual("Información", viewModel.Title);
        }

        /// <summary>
        /// Issue #271: Verifica que el título por defecto se mantiene cuando no se pasa el parámetro "title".
        /// </summary>
        [TestMethod]
        [TestCategory("NotificationDialog")]
        public void OnDialogOpened_SinParametroTitulo_MantieneTituloPorDefecto()
        {
            // Arrange
            var viewModel = new NotificationDialogViewModel();
            var parameters = new DialogParameters
            {
                { "message", "Este es un mensaje de prueba" }
            };

            // Act
            viewModel.OnDialogOpened(parameters);

            // Assert
            Assert.AreEqual("Información", viewModel.Title,
                "El título por defecto 'Información' debe mantenerse cuando no se pasa el parámetro 'title'");
            Assert.AreEqual("Este es un mensaje de prueba", viewModel.Message);
        }

        /// <summary>
        /// Verifica que el título se sobrescribe cuando se pasa el parámetro "title".
        /// </summary>
        [TestMethod]
        [TestCategory("NotificationDialog")]
        public void OnDialogOpened_ConParametroTitulo_SobrescribeTitulo()
        {
            // Arrange
            var viewModel = new NotificationDialogViewModel();
            var parameters = new DialogParameters
            {
                { "message", "Mensaje de prueba" },
                { "title", "Título personalizado" }
            };

            // Act
            viewModel.OnDialogOpened(parameters);

            // Assert
            Assert.AreEqual("Título personalizado", viewModel.Title);
            Assert.AreEqual("Mensaje de prueba", viewModel.Message);
        }

        /// <summary>
        /// Verifica que el mensaje se mantiene null si no se pasa el parámetro "message".
        /// </summary>
        [TestMethod]
        [TestCategory("NotificationDialog")]
        public void OnDialogOpened_SinParametroMessage_MessageEsNull()
        {
            // Arrange
            var viewModel = new NotificationDialogViewModel();
            var parameters = new DialogParameters
            {
                { "title", "Solo título" }
            };

            // Act
            viewModel.OnDialogOpened(parameters);

            // Assert
            Assert.IsNull(viewModel.Message);
            Assert.AreEqual("Solo título", viewModel.Title);
        }

        /// <summary>
        /// Verifica que ambos valores por defecto se mantienen con parámetros vacíos.
        /// </summary>
        [TestMethod]
        [TestCategory("NotificationDialog")]
        public void OnDialogOpened_SinParametros_MantieneValoresPorDefecto()
        {
            // Arrange
            var viewModel = new NotificationDialogViewModel();
            var parameters = new DialogParameters();

            // Act
            viewModel.OnDialogOpened(parameters);

            // Assert
            Assert.AreEqual("Información", viewModel.Title,
                "El título por defecto debe mantenerse con parámetros vacíos");
            Assert.IsNull(viewModel.Message,
                "El mensaje debe ser null si no se proporciona");
        }
    }
}
