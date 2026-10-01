using System;

namespace Nesto.Infrastructure.Contracts
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3): ViewModel de un diálogo que abre <see cref="IServicioDialogos"/>,
    /// sin Prism. Es el equivalente del <c>IDialogAware</c> de Prism con los MISMOS nombres (como en
    /// <see cref="IServicioDialogos"/>), para que pasar cada diálogo sea cambiar los tipos y poco más:
    /// <c>IDialogParameters</c> → <see cref="ParametrosDialogo"/>, <c>IDialogResult</c> →
    /// <see cref="ResultadoDialogo"/>.
    ///
    /// La ventana propia (<c>ControlesUsuario.Dialogs.ServicioDialogosNesto</c>) acepta este tipo y,
    /// mientras dure la migración, también los <c>IDialogAware</c> de Prism.
    /// </summary>
    public interface IDialogoNesto
    {
        /// <summary>Título de la ventana.</summary>
        string Title { get; }

        /// <summary>El diálogo pide cerrarse con este resultado (lo recibe quien lo abrió).</summary>
        event Action<ResultadoDialogo> RequestClose;

        /// <summary>false impide cerrar la ventana (también con la X).</summary>
        bool CanCloseDialog();

        void OnDialogClosed();

        /// <summary>Se llama al abrir, con los parámetros de quien lo abre (nunca null).</summary>
        void OnDialogOpened(ParametrosDialogo parameters);
    }
}
