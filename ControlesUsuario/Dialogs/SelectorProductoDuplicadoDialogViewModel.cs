using ControlesUsuario.Models;
using CommunityToolkit.Mvvm.Input;
using Nesto.Infrastructure.Contracts;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Selector que se muestra cuando un código de barras corresponde a varios productos
    /// (la API devuelve 409 con la lista de candidatos). El usuario elige uno y se devuelve
    /// su Número, que resuelve de forma única. Nesto#368.
    /// </summary>
    public class SelectorProductoDuplicadoDialogViewModel : DialogoNestoBase
    {
        private ProductoCodigoBarrasDuplicado _seleccionado;

        public ObservableCollection<ProductoCodigoBarrasDuplicado> Candidatos { get; } =
            new ObservableCollection<ProductoCodigoBarrasDuplicado>();

        public ProductoCodigoBarrasDuplicado Seleccionado
        {
            get => _seleccionado;
            set
            {
                if (SetProperty(ref _seleccionado, value))
                {
                    AceptarCommand.NotifyCanExecuteChanged();
                }
            }
        }

        public RelayCommand AceptarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        public SelectorProductoDuplicadoDialogViewModel()
        {
            Title = "Código de barras duplicado";
            AceptarCommand = new RelayCommand(Aceptar, () => Seleccionado != null);
            CancelarCommand = new RelayCommand(Cancelar);
        }

        private void Aceptar()
        {
            if (Seleccionado == null)
            {
                return;
            }

            ParametrosDialogo parameters = new ParametrosDialogo
            {
                { "producto", Seleccionado.Producto }
            };
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, parameters));
        }

        private void Cancelar()
        {
            RaiseRequestClose(ResultadoBoton.Cancel);
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
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
