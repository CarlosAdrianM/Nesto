using Nesto.Infrastructure.Contracts;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2): envoltorio de <see cref="IServicioDialogos"/> que abre los diálogos SIEMPRE
    /// en el hilo de UI, aunque quien llame esté en un hilo de pool.
    ///
    /// POR QUÉ EXISTE (caso real 21/08/26, cuadre de banco): al contabilizar un apunte con la
    /// regla de "línea de riesgo" saltaba
    /// <c>"An unexpected error occured while resolving 'Prism.Services.Dialogs.IDialogWindow'"</c>.
    /// El arreglo de NestoAPI#384/#386 (17/08) pasó a ejecutar las reglas dentro de un
    /// <c>Task.Run</c> para que las llamadas HTTP síncronas no interbloquearan la ventana; pero
    /// varias reglas LE PREGUNTAN COSAS AL USUARIO desde dentro, y WPF no puede crear una Window
    /// fuera del hilo de UI. O sea que el arreglo que liberó la UI rompió justo las reglas que
    /// hablan con el usuario.
    ///
    /// Se envuelve cada método entero (GetAmount, ShowConfirmationAnswer, etc. son métodos del
    /// servicio). Se usa <c>Dispatcher.Invoke</c> SÍNCRONO a propósito: los métodos que devuelven
    /// valor lo recogen del diálogo modal, así que hay que esperar a que el usuario conteste. No hay
    /// riesgo de interbloqueo mientras el hilo de UI esté esperando con <c>await</c> —que es como
    /// quedó tras NestoAPI#384— y no bloqueado con <c>.Wait()</c>.
    ///
    /// Hasta el 02/10/26 existía también DialogServiceEnHiloUi, lo mismo sobre el IDialogService
    /// de Prism; se borró cuando ya no lo usaba nadie.
    /// </summary>
    public class ServicioDialogosEnHiloUi : IServicioDialogos
    {
        private readonly IServicioDialogos _interno;

        public ServicioDialogosEnHiloUi(IServicioDialogos interno)
        {
            _interno = interno ?? throw new ArgumentNullException(nameof(interno));
        }

        public void ShowNotification(string message) => EnHiloUi(() => _interno.ShowNotification(message));

        public void ShowNotification(string title, string message) => EnHiloUi(() => _interno.ShowNotification(title, message));

        public void ShowError(string message) => EnHiloUi(() => _interno.ShowError(message));

        public void ShowConfirmation(string message, Action<ResultadoDialogo> callBack)
            => EnHiloUi(() => _interno.ShowConfirmation(message, callBack));

        public void ShowConfirmation(string title, string message, Action<ResultadoDialogo> callBack)
            => EnHiloUi(() => _interno.ShowConfirmation(title, message, callBack));

        public bool ShowConfirmationAnswer(string title, string message)
            => EnHiloUi(() => _interno.ShowConfirmationAnswer(title, message));

        public Task<bool> ShowConfirmationAsync(string message)
            => EnHiloUi(() => _interno.ShowConfirmationAsync(message));

        public Task<bool> ShowConfirmationAsync(string title, string message)
            => EnHiloUi(() => _interno.ShowConfirmationAsync(title, message));

        public void ShowInputAmount(string message, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.ShowInputAmount(message, callback));

        public void ShowInputAmount(string title, string message, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.ShowInputAmount(title, message, callback));

        public void ShowInputAmount(string title, string message, string defaultAmount, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.ShowInputAmount(title, message, defaultAmount, callback));

        public decimal? GetAmount(string title, string message)
            => EnHiloUi(() => _interno.GetAmount(title, message));

        public void ShowInputText(string message, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.ShowInputText(message, callback));

        public void ShowInputText(string title, string message, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.ShowInputText(title, message, callback));

        public void ShowInputText(string title, string message, string defaultText, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.ShowInputText(title, message, defaultText, callback));

        public string GetText(string title, string message)
            => EnHiloUi(() => _interno.GetText(title, message));

        public string GetText(string title, string message, string defaultText)
            => EnHiloUi(() => _interno.GetText(title, message, defaultText));

        public DateTime? GetDate(string title, string message, DateTime? defaultDate)
            => EnHiloUi(() => _interno.GetDate(title, message, defaultDate));

        public void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.ShowDialog(name, parameters, callback));

        public Task<ResultadoDialogo> ShowDialogAsync(string name, ParametrosDialogo parameters = null)
            => EnHiloUi(() => _interno.ShowDialogAsync(name, parameters));

        public void Show(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.Show(name, parameters, callback));

        internal static void EnHiloUi(Action accion)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            // Sin Application (tests, procesos sin UI) o ya en el hilo bueno: llamada directa.
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                accion();
                return;
            }
            dispatcher.Invoke(accion);
        }

        private static T EnHiloUi<T>(Func<T> funcion)
        {
            T resultado = default;
            EnHiloUi(() => resultado = funcion());
            return resultado;
        }
    }
}
