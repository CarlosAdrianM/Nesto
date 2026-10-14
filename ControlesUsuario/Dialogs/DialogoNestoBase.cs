using CommunityToolkit.Mvvm.ComponentModel;
using Nesto.Infrastructure.Contracts;
using System;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): base de los ViewModels de diálogo (<see cref="IDialogoNesto"/>).
    /// El ViewModel solo trabaja con tipos propios (<see cref="ParametrosDialogo"/>,
    /// <see cref="ResultadoDialogo"/>, <see cref="ResultadoBoton"/>) y pide cerrarse con
    /// <see cref="RaiseRequestClose(ResultadoDialogo)"/>. Los abre la ventana propia
    /// (<see cref="ServicioDialogosNesto"/>).
    ///
    /// Hasta el 14/10/26 implementaba también el IDialogAware de Prism, para abrirse igual con la ventana
    /// de Prism mientras duró el piloto de la propia (parámetro VentanaDialogosPropia). Al pasar la propia a
    /// todos se quitó, y con él la referencia a Prism.
    /// </summary>
    public abstract class DialogoNestoBase : ObservableObject, IDialogoNesto
    {
        private string _title;

        /// <summary>Título de la ventana (la ventana lo enlaza por nombre).</summary>
        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        /// <inheritdoc/>
        public event Action<ResultadoDialogo> RequestClose;

        /// <summary>
        /// Pide cerrar el diálogo con ese resultado (null = <see cref="ResultadoBoton.None"/>). Lo recibe la
        /// ventana que lo abrió.
        /// </summary>
        protected void RaiseRequestClose(ResultadoDialogo resultado)
            => RequestClose?.Invoke(resultado ?? new ResultadoDialogo(ResultadoBoton.None));

        /// <summary>Atajo de <see cref="RaiseRequestClose(ResultadoDialogo)"/> sin parámetros de vuelta.</summary>
        protected void RaiseRequestClose(ResultadoBoton boton) => RaiseRequestClose(new ResultadoDialogo(boton));

        public virtual bool CanCloseDialog() => true;

        public virtual void OnDialogClosed()
        {
        }

        /// <summary>Se llama al abrir, con los parámetros de quien lo abre (nunca null).</summary>
        public virtual void OnDialogOpened(ParametrosDialogo parameters)
        {
        }
    }
}
