using System;
using System.Threading.Tasks;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.2): servicio de diálogos propio de Nesto, para dejar de depender del
    /// <c>IDialogService</c> de Prism en los ViewModels.
    ///
    /// Plan en tres pasos (como se hizo con los eventos → IMessenger en el 4C.1):
    /// 1. Esta interfaz, con una primera implementación (<c>ControlesUsuario.Dialogs.ServicioDialogosPrism</c>)
    ///    que se limita a delegar en el IDialogService de Prism y en <c>DialogServiceExtensions</c>:
    ///    mismo comportamiento, mismos diálogos registrados.
    /// 2. Migrar las llamadas módulo a módulo: se cambia el tipo inyectado y poco más, porque los
    ///    métodos se llaman IGUAL que las extensiones de Prism (ShowError, ShowNotification,
    ///    ShowConfirmationAnswer...). Los nombres en inglés son a propósito, para que la migración
    ///    sea mecánica y no haya que tocar cientos de líneas.
    /// 3. Cambiar la implementación por una sin Prism. Por eso aquí NO aparece ningún tipo de
    ///    Prism: los parámetros y el resultado son <see cref="ParametrosDialogo"/> y
    ///    <see cref="ResultadoDialogo"/>. Los ViewModels de los propios diálogos (IDialogAware)
    ///    siguen con Prism hasta ese paso.
    ///
    /// Los diálogos genéricos (NotificationDialog, ConfirmationDialog, InputAmountDialog,
    /// InputTextDialog) se registran en Application.RegisterTypes; los de cada módulo, en su
    /// IModule.RegisterTypes.
    /// </summary>
    public interface IServicioDialogos
    {
        void ShowNotification(string message);
        void ShowNotification(string title, string message);

        /// <summary>Notificación con título "¡Error!". Si el mensaje trae el JSON de error de NestoAPI, se muestra solo error.message.</summary>
        void ShowError(string message);

        void ShowConfirmation(string message, Action<ResultadoDialogo> callBack);
        void ShowConfirmation(string title, string message, Action<ResultadoDialogo> callBack);

        /// <summary>Confirmación modal: true solo si el usuario pulsa Aceptar.</summary>
        bool ShowConfirmationAnswer(string title, string message);

        Task<bool> ShowConfirmationAsync(string message);
        Task<bool> ShowConfirmationAsync(string title, string message);

        /// <summary>El importe vuelve en el parámetro "amount" (decimal) del resultado.</summary>
        void ShowInputAmount(string message, Action<ResultadoDialogo> callback);
        void ShowInputAmount(string title, string message, Action<ResultadoDialogo> callback);
        void ShowInputAmount(string title, string message, string defaultAmount, Action<ResultadoDialogo> callback);

        /// <summary>Importe tecleado, o null si el usuario cancela.</summary>
        decimal? GetAmount(string title, string message);

        /// <summary>El texto vuelve en el parámetro "text" (string) del resultado.</summary>
        void ShowInputText(string message, Action<ResultadoDialogo> callback);
        void ShowInputText(string title, string message, Action<ResultadoDialogo> callback);
        void ShowInputText(string title, string message, string defaultText, Action<ResultadoDialogo> callback);

        /// <summary>Texto tecleado, o null si el usuario cancela.</summary>
        string GetText(string title, string message);
        string GetText(string title, string message, string defaultText);

        /// <summary>Abre modal el diálogo registrado con ese nombre.</summary>
        void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback);

        /// <summary>Como <see cref="ShowDialog"/>, pero devuelve el resultado como Task.</summary>
        Task<ResultadoDialogo> ShowDialogAsync(string name, ParametrosDialogo parameters = null);
    }
}
