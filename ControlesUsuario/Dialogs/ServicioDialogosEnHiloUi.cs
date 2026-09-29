using Nesto.Infrastructure.Contracts;
using System;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2): lo mismo que <see cref="DialogServiceEnHiloUi"/>, pero sobre
    /// <see cref="IServicioDialogos"/>. Abre los diálogos SIEMPRE en el hilo de UI aunque quien
    /// llame esté en un hilo de pool.
    ///
    /// POR QUÉ EXISTE (caso real 21/08/26, cuadre de banco): las reglas de contabilización corren
    /// dentro de un <c>Task.Run</c> (NestoAPI#384/#386) y varias le preguntan cosas al usuario;
    /// WPF no puede crear una Window fuera del hilo de UI. Ver el comentario de
    /// <see cref="DialogServiceEnHiloUi"/> para la historia completa.
    ///
    /// Se envuelve cada método entero (no solo ShowDialog/Show como en el envoltorio de Prism,
    /// porque aquí GetAmount, ShowConfirmationAnswer, etc. son métodos del servicio y no
    /// extensiones que acaben en ShowDialog). Igual que antes, con <c>Dispatcher.Invoke</c>
    /// SÍNCRONO: los métodos que devuelven valor lo recogen del diálogo modal.
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

        public void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.ShowDialog(name, parameters, callback));

        public Task<ResultadoDialogo> ShowDialogAsync(string name, ParametrosDialogo parameters = null)
            => EnHiloUi(() => _interno.ShowDialogAsync(name, parameters));

        public void Show(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => EnHiloUi(() => _interno.Show(name, parameters, callback));

        private static void EnHiloUi(Action accion) => DialogServiceEnHiloUi.EnHiloUi(accion);

        private static T EnHiloUi<T>(Func<T> funcion)
        {
            T resultado = default;
            DialogServiceEnHiloUi.EnHiloUi(() => resultado = funcion());
            return resultado;
        }
    }
}
