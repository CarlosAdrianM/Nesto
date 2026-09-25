using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;
using System.Threading.Tasks;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2): primera implementación de <see cref="IServicioDialogos"/>. Delegación pura
    /// en el <see cref="IDialogService"/> de Prism y en <see cref="DialogServiceExtensions"/>: los
    /// diálogos que se abren, sus parámetros y su resultado son exactamente los de antes. Solo
    /// traduce <see cref="ParametrosDialogo"/>/<see cref="ResultadoDialogo"/> a y desde los tipos
    /// de Prism. Cuando todos los módulos usen la interfaz, se sustituirá por una sin Prism.
    /// </summary>
    public class ServicioDialogosPrism : IServicioDialogos
    {
        private readonly IDialogService _prism;

        public ServicioDialogosPrism(IDialogService dialogService)
        {
            _prism = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public void ShowNotification(string message) => _prism.ShowNotification(message);

        public void ShowNotification(string title, string message) => _prism.ShowNotification(title, message);

        public void ShowError(string message) => _prism.ShowError(message);

        public void ShowConfirmation(string message, Action<ResultadoDialogo> callBack)
            => _prism.ShowConfirmation(message, Traducir(callBack));

        public void ShowConfirmation(string title, string message, Action<ResultadoDialogo> callBack)
            => _prism.ShowConfirmation(title, message, Traducir(callBack));

        public bool ShowConfirmationAnswer(string title, string message) => _prism.ShowConfirmationAnswer(title, message);

        public Task<bool> ShowConfirmationAsync(string message) => _prism.ShowConfirmationAsync(message);

        public Task<bool> ShowConfirmationAsync(string title, string message) => _prism.ShowConfirmationAsync(title, message);

        public void ShowInputAmount(string message, Action<ResultadoDialogo> callback)
            => _prism.ShowInputAmount(message, Traducir(callback));

        public void ShowInputAmount(string title, string message, Action<ResultadoDialogo> callback)
            => _prism.ShowInputAmount(title, message, Traducir(callback));

        public void ShowInputAmount(string title, string message, string defaultAmount, Action<ResultadoDialogo> callback)
            => _prism.ShowInputAmount(title, message, defaultAmount, Traducir(callback));

        public decimal? GetAmount(string title, string message) => _prism.GetAmount(title, message);

        public void ShowInputText(string message, Action<ResultadoDialogo> callback)
            => _prism.ShowInputText(message, Traducir(callback));

        public void ShowInputText(string title, string message, Action<ResultadoDialogo> callback)
            => _prism.ShowInputText(title, message, Traducir(callback));

        public void ShowInputText(string title, string message, string defaultText, Action<ResultadoDialogo> callback)
            => _prism.ShowInputText(title, message, defaultText, Traducir(callback));

        public string GetText(string title, string message) => _prism.GetText(title, message);

        public string GetText(string title, string message, string defaultText) => _prism.GetText(title, message, defaultText);

        public void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => _prism.ShowDialog(name, AParametrosPrism(parameters), Traducir(callback));

        public async Task<ResultadoDialogo> ShowDialogAsync(string name, ParametrosDialogo parameters = null)
            => DesdeResultadoPrism(await _prism.ShowDialogAsync(name, AParametrosPrism(parameters)));

        // Un callback null se pasa como null, igual que antes: Prism ya no lo invoca.
        private static Action<IDialogResult> Traducir(Action<ResultadoDialogo> callback)
            => callback == null ? null : r => callback(DesdeResultadoPrism(r));

        internal static IDialogParameters AParametrosPrism(ParametrosDialogo parametros)
        {
            if (parametros == null)
            {
                return null;
            }
            var prism = new DialogParameters();
            foreach (var entrada in parametros)
            {
                prism.Add(entrada.Key, entrada.Value);
            }
            return prism;
        }

        internal static ResultadoDialogo DesdeResultadoPrism(IDialogResult resultado)
        {
            if (resultado == null)
            {
                return null;
            }
            var parametros = new ParametrosDialogo();
            if (resultado.Parameters != null)
            {
                foreach (string clave in resultado.Parameters.Keys)
                {
                    parametros.Add(clave, resultado.Parameters.GetValue<object>(clave));
                }
            }
            return new ResultadoDialogo((ResultadoBoton)(int)resultado.Result, parametros);
        }
    }
}
