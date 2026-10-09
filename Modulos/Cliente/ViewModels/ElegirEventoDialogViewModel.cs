using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Dialogs;
using Nesto.Infrastructure.Contracts;
using Nesto.Modulos.Cliente.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// NestoAPI#591: diálogo para elegir de qué evento es la señal un apunte del extracto. Recibe «eventos»
    /// (List&lt;EventoModel&gt;, ya ordenados: los próximos primero) y «apunte» (texto) y devuelve «eventoId».
    /// </summary>
    public class ElegirEventoDialogViewModel : DialogoNestoBase
    {
        public const string NOMBRE = "ElegirEventoDialog";

        private EventoModel _seleccionado;

        public ElegirEventoDialogViewModel()
        {
            AceptarCommand = new RelayCommand(Aceptar, () => Seleccionado != null);
            CancelarCommand = new RelayCommand(() => RaiseRequestClose(ResultadoBoton.Cancel));
            Title = "Es la señal del evento…";
        }

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

        public RelayCommand AceptarCommand { get; }
        public RelayCommand CancelarCommand { get; }

        private void Aceptar()
        {
            if (Seleccionado == null)
            {
                return;
            }
            RaiseRequestClose(new ResultadoDialogo(ResultadoBoton.OK, new ParametrosDialogo { { "eventoId", Seleccionado.Id } }));
        }

        public override void OnDialogOpened(ParametrosDialogo parameters)
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
