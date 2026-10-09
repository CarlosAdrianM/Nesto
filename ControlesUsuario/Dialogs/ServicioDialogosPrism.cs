using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2): primera implementación de <see cref="IServicioDialogos"/>, sobre el
    /// <see cref="IDialogService"/> de Prism: los diálogos que se abren, sus parámetros y su
    /// resultado son exactamente los de antes. Los diálogos genéricos (ShowError, ShowConfirmation...)
    /// los compone <see cref="ServicioDialogosBase"/>; aquí solo se abre el diálogo con Prism y se
    /// traducen <see cref="ParametrosDialogo"/>/<see cref="ResultadoDialogo"/> a y desde sus tipos.
    /// Convive con <see cref="ServicioDialogosNesto"/> (ventana propia) mientras dure el piloto.
    /// </summary>
    public class ServicioDialogosPrism : ServicioDialogosBase
    {
        private readonly IDialogService _prism;

        public ServicioDialogosPrism(IDialogService dialogService)
        {
            _prism = dialogService ?? throw new ArgumentNullException(nameof(dialogService));
        }

        public override void ShowDialog(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => _prism.ShowDialog(name, AParametrosPrism(parameters), Traducir(callback));

        // No modal y en el hilo de UI (con ServicioDialogosEnHiloUi, como el aviso de Amazon de Nesto#499):
        // quien lo usa suele estar en una tarea en segundo plano y WPF no puede crear la ventana fuera de la UI.
        public override void Show(string name, ParametrosDialogo parameters, Action<ResultadoDialogo> callback)
            => ServicioDialogosEnHiloUi.EnHiloUi(() => _prism.Show(name, AParametrosPrism(parameters), Traducir(callback)));

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
            return new ResultadoDialogo((ResultadoBoton)(int)resultado.Result, DesdeParametrosPrism(resultado.Parameters));
        }

        /// <summary>Los parámetros de Prism como <see cref="ParametrosDialogo"/>; nunca null (null → vacíos).</summary>
        internal static ParametrosDialogo DesdeParametrosPrism(IDialogParameters parametrosPrism)
        {
            var parametros = new ParametrosDialogo();
            if (parametrosPrism != null)
            {
                foreach (string clave in parametrosPrism.Keys)
                {
                    parametros.Add(clave, parametrosPrism.GetValue<object>(clave));
                }
            }
            return parametros;
        }
    }
}
