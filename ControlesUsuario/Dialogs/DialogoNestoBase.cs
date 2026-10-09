using CommunityToolkit.Mvvm.ComponentModel;
using Nesto.Infrastructure.Contracts;
using Prism.Services.Dialogs;
using System;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#490 (4C.2, paso 3, punto 2): base de los ViewModels de diálogo ya migrados a
    /// <see cref="IDialogoNesto"/>. El ViewModel solo trabaja con tipos propios
    /// (<see cref="ParametrosDialogo"/>, <see cref="ResultadoDialogo"/>, <see cref="ResultadoBoton"/>) y
    /// pide cerrarse con <see cref="RaiseRequestClose"/>.
    ///
    /// Puente mientras convivan las dos ventanas (piloto <c>VentanaDialogosPropia</c>):
    /// - La ventana propia (<see cref="ServicioDialogosNesto"/>) lo ve como <see cref="IDialogoNesto"/> y
    ///   lo abre directamente.
    /// - La de Prism solo sabe abrir un <see cref="IDialogAware"/>: esta base lo implementa de forma
    ///   EXPLÍCITA y traduce los parámetros de entrada y el resultado, igual que hace
    ///   <see cref="ServicioDialogosPrism"/> con las llamadas. Así el ViewModel se comporta igual se abra
    ///   con una ventana o con la otra.
    /// Cuando la ventana propia sea la de todos, se quita el <see cref="IDialogAware"/> de aquí (y con él
    /// la referencia a Prism) sin tocar los ViewModels.
    /// </summary>
    public abstract class DialogoNestoBase : ObservableObject, IDialogoNesto, IDialogAware
    {
        private string _title;
        private Action<IDialogResult> _requestClosePrism;

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
        /// ventana que lo abrió, sea la propia o la de Prism.
        /// </summary>
        protected void RaiseRequestClose(ResultadoDialogo resultado)
        {
            resultado ??= new ResultadoDialogo(ResultadoBoton.None);
            RequestClose?.Invoke(resultado);
            _requestClosePrism?.Invoke(new DialogResult((ButtonResult)(int)resultado.Result, ServicioDialogosPrism.AParametrosPrism(resultado.Parameters)));
        }

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

        // --- Puente con la ventana de Prism (se quita con Prism) ---

        string IDialogAware.Title => Title;

        event Action<IDialogResult> IDialogAware.RequestClose
        {
            add => _requestClosePrism += value;
            remove => _requestClosePrism -= value;
        }

        bool IDialogAware.CanCloseDialog() => CanCloseDialog();

        void IDialogAware.OnDialogClosed() => OnDialogClosed();

        void IDialogAware.OnDialogOpened(IDialogParameters parameters)
            => OnDialogOpened(ServicioDialogosPrism.DesdeParametrosPrism(parameters));
    }
}
