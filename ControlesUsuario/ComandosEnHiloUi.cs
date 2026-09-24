using CommunityToolkit.Mvvm.Input;
using System;
using System.Windows;
using System.Windows.Threading;

namespace ControlesUsuario
{
    /// <summary>
    /// Nesto#488: el <c>DelegateCommand</c> de Prism llevaba <c>CanExecuteChanged</c> al hilo de la UI;
    /// el <see cref="RelayCommand"/> de CommunityToolkit lo lanza en el hilo que llama. Un
    /// <c>NotifyCanExecuteChanged()</c> desde un hilo del pool hace que WPF toque el botón fuera de su
    /// hilo (excepción de acceso entre hilos, o el botón que no se entera).
    ///
    /// Lo correcto es no salir del hilo de la UI en el ViewModel (async/await sin ConfigureAwait(false)
    /// y NADA de <c>Task.Run(() => comando.Execute(...))</c>: lo vigila ComandosFueraDeHiloUiTests).
    /// Esto es la red para los sitios donde no se puede garantizar de quién viene la llamada.
    /// </summary>
    public static class ComandosEnHiloUi
    {
        /// <summary>
        /// <c>NotifyCanExecuteChanged()</c> en el hilo de la UI. Si ya se está en él (o no hay
        /// aplicación WPF, como en los tests) se llama directamente.
        /// </summary>
        public static void NotifyCanExecuteChangedEnUi(this IRelayCommand comando)
        {
            if (comando == null)
            {
                return;
            }
            Dispatcher dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                comando.NotifyCanExecuteChanged();
                return;
            }
            // Asíncrono a propósito: avisar de que cambió CanExecute no necesita esperar, y así no hay
            // riesgo de interbloqueo si el hilo de la UI estuviera esperando a quien llama.
            _ = dispatcher.BeginInvoke(new Action(comando.NotifyCanExecuteChanged));
        }
    }
}
