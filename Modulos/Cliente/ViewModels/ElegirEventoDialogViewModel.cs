using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Nesto.Modulos.Cliente.Models;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#591: diálogo para elegir de qué evento es la señal un apunte del extracto. Recibe «eventos»
    /// (List&lt;EventoModel&gt;, ya ordenados: los próximos primero) y «apunte» (texto) y devuelve «eventoId».
    /// </summary>
    public class ElegirEventoDialogViewModel : ObservableObject, IDialogAware
    {
        public const string NOMBRE = "ElegirEventoDialog";

        private EventoModel _seleccionado;

        public ElegirEventoDialogViewModel()
        {
            AceptarCommand = new RelayCommand(Aceptar, () => Seleccionado != null);
            CancelarCommand = new RelayCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

        public string Title => "Es la señal del evento…";

        private string _apunte;
        public string Apunte { get => _apunte; private set => SetProperty(ref _apunte, value); }

        public ObservableCollection<EventoModel> Eventos { get; } = new();

        public EventoModel Seleccionado
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

        public event Action<IDialogResult> RequestClose;

        public RelayCommand AceptarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        private void Aceptar()
        {
            if (Seleccionado == null)
            {
                return;
            }
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK, new DialogParameters { { "eventoId", Seleccionado.Id } }));
        }

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            Apunte = parameters.ContainsKey("apunte") ? parameters.GetValue<string>("apunte") : null;
            Eventos.Clear();
            if (parameters.ContainsKey("eventos"))
            {
                foreach (EventoModel evento in parameters.GetValue<IEnumerable<EventoModel>>("eventos") ?? Array.Empty<EventoModel>())
                {
                    Eventos.Add(evento);
                }
            }
            Seleccionado = Eventos.Count > 0 ? Eventos[0] : null;
        }
    }
}
