using Nesto.Infrastructure.Contracts;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#372: muestra el changelog de novedades en lenguaje de usuario. El llamante carga
    /// las novedades (popup tras actualizar o menú Ayuda → Novedades) y las pasa por parámetro.
    /// </summary>
    public class NovedadesDialogViewModel : BindableBase, IDialogAware
    {
        private DelegateCommand _closeDialogCommand;
        public DelegateCommand CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK))));

        private string _title = "Novedades de Nesto";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private List<NovedadUsuario> _novedades = new List<NovedadUsuario>();
        public List<NovedadUsuario> Novedades
        {
            get { return _novedades; }
            set { SetProperty(ref _novedades, value); }
        }

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("novedades"))
            {
                Novedades = parameters.GetValue<List<NovedadUsuario>>("novedades");
            }
            if (parameters.ContainsKey("title"))
            {
                Title = parameters.GetValue<string>("title");
            }
        }
    }
}
