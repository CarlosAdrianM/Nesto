using Prism.Services.Dialogs;
using System;
using System.Windows;
using System.Windows.Threading;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Envoltorio de <see cref="IDialogService"/> que abre los diálogos SIEMPRE en el hilo de UI,
    /// aunque quien llame esté en un hilo de pool.
    ///
    /// POR QUÉ EXISTE (caso real 21/08/26, cuadre de banco): al contabilizar un apunte con la
    /// regla de "línea de riesgo" saltaba
    /// <c>"An unexpected error occured while resolving 'Prism.Services.Dialogs.IDialogWindow'"</c>.
    /// El arreglo de NestoAPI#384/#386 (17/08) paso a ejecutar las reglas dentro de un
    /// <c>Task.Run</c> para que las llamadas HTTP sincronas no interbloquearan la ventana; pero
    /// varias reglas LE PREGUNTAN COSAS AL USUARIO desde dentro, y WPF no puede crear una Window
    /// fuera del hilo de UI: Prism falla al resolver IDialogWindow y Unity lo envuelve en ese
    /// mensaje tan poco explicativo. O sea que el arreglo que libero la UI rompio justo las
    /// reglas que hablan con el usuario.
    ///
    /// Como todas las extensiones de <see cref="DialogServiceExtensions"/> (ShowError,
    /// ShowNotification, ShowConfirmationAnswer, GetAmount...) acaban llamando a ShowDialog o a
    /// Show, envolver esos dos metodos las arregla todas de una vez, sin tocar ninguna regla.
    ///
    /// Se usa <c>Dispatcher.Invoke</c> (sincrono) a proposito: ShowDialog es modal y las
    /// extensiones que devuelven un valor lo recogen en el callback, asi que hay que esperar a que
    /// el usuario conteste. No hay riesgo de interbloqueo mientras el hilo de UI esté esperando
    /// con <c>await</c> —que es como quedo tras NestoAPI#384— y no bloqueado con <c>.Wait()</c>.
    /// </summary>
    public class DialogServiceEnHiloUi : IDialogService
    {
        private readonly IDialogService _interno;

        public DialogServiceEnHiloUi(IDialogService interno)
        {
            _interno = interno ?? throw new ArgumentNullException(nameof(interno));
        }

        public void Show(string name, IDialogParameters parameters, Action<IDialogResult> callback)
            => EnHiloUi(() => _interno.Show(name, parameters, callback));

        public void Show(string name, IDialogParameters parameters, Action<IDialogResult> callback, string windowName)
            => EnHiloUi(() => _interno.Show(name, parameters, callback, windowName));

        public void ShowDialog(string name, IDialogParameters parameters, Action<IDialogResult> callback)
            => EnHiloUi(() => _interno.ShowDialog(name, parameters, callback));

        public void ShowDialog(string name, IDialogParameters parameters, Action<IDialogResult> callback, string windowName)
            => EnHiloUi(() => _interno.ShowDialog(name, parameters, callback, windowName));

        private static void EnHiloUi(Action accion)
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
    }
}
