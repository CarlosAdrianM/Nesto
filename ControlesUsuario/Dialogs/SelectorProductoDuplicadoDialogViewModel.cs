using ControlesUsuario.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Selector que se muestra cuando un código de barras corresponde a varios productos
    /// (la API devuelve 409 con la lista de candidatos). El usuario elige uno y se devuelve
    /// su Número, que resuelve de forma única. Nesto#368.
    /// </summary>
    public class SelectorProductoDuplicadoDialogViewModel : BindableBase, IDialogAware
    {
        private string _title = "Código de barras duplicado";
        private ProductoCodigoBarrasDuplicado _seleccionado;

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public ObservableCollection<ProductoCodigoBarrasDuplicado> Candidatos { get; } =
            new ObservableCollection<ProductoCodigoBarrasDuplicado>();

        public ProductoCodigoBarrasDuplicado Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (SetProperty(ref _seleccionado, value))
                {
                    AceptarCommand.RaiseCanExecuteChanged();
                }
            }
        }

        public event Action<IDialogResult> RequestClose;

        public DelegateCommand AceptarCommand { get; }
        public DelegateCommand CancelarCommand { get; }

        public SelectorProductoDuplicadoDialogViewModel()
        {
            AceptarCommand = new DelegateCommand(Aceptar, () => Seleccionado != null);
            CancelarCommand = new DelegateCommand(Cancelar);
        }

        private void Aceptar()
        {
            if (Seleccionado == null)
            {
                return;
            }

            DialogParameters parameters = new DialogParameters
            {
                { "producto", Seleccionado.Producto }
            };
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, parameters));
        }

        private void Cancelar()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("title"))
            {
                Title = parameters.GetValue<string>("title");
            }

            Candidatos.Clear();
            if (parameters.ContainsKey("candidatos"))
            {
                IEnumerable<ProductoCodigoBarrasDuplicado> lista =
                    parameters.GetValue<IEnumerable<ProductoCodigoBarrasDuplicado>>("candidatos");
                if (lista != null)
                {
                    foreach (ProductoCodigoBarrasDuplicado candidato in lista)
                    {
                        Candidatos.Add(candidato);
                    }
                }
            }

            Seleccionado = Candidatos.Count > 0 ? Candidatos[0] : null;
        }
    }
}
