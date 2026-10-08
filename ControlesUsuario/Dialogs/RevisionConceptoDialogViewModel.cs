using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ControlesUsuario.Models;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// NestoAPI#609: «¿Quisiste decir…?» antes de crear un enlace de pago. Enseña el concepto escrito y el
    /// propuesto con los cambios resaltados; «Usar la corrección» devuelve OK con el propuesto en
    /// <see cref="PARAMETRO_CONCEPTO"/>, «Dejar el mío» (o cerrar) devuelve Cancel. IDialogAware de Prism como
    /// los demás diálogos hasta que se pasen todos a IDialogoNesto (Nesto#490).
    /// </summary>
    public class RevisionConceptoDialogViewModel : ObservableObject, IDialogAware
    {
        public const string NOMBRE = "RevisionConceptoDialog";
        public const string PARAMETRO_ORIGINAL = "original";
        public const string PARAMETRO_REVISION = "revision";
        public const string PARAMETRO_CONCEPTO = "concepto";

        public RevisionConceptoDialogViewModel()
        {
            UsarCorreccionCommand = new RelayCommand(UsarCorreccion);
            DejarElMioCommand = new RelayCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel)));
        }

        public string Title => "¿Quisiste decir…?";

        private string _original;
        /// <summary>El concepto tal como lo escribió el usuario.</summary>
        public string Original { get => _original; private set => SetProperty(ref _original, value); }

        private string _propuesto;
        public string Propuesto { get => _propuesto; private set => SetProperty(ref _propuesto, value); }

        private List<TramoConcepto> _tramos = new List<TramoConcepto>();
        /// <summary>El propuesto, troceado para resaltar lo que cambia.</summary>
        public List<TramoConcepto> Tramos { get => _tramos; private set => SetProperty(ref _tramos, value); }

        private string _textoCambios;
        /// <summary>Una línea por cambio: «micronileng» → «Microneedling».</summary>
        public string TextoCambios { get => _textoCambios; private set => SetProperty(ref _textoCambios, value); }

        public RelayCommand UsarCorreccionCommand { get; }
        public RelayCommand DejarElMioCommand { get; }

        public event Action<IDialogResult> RequestClose;

        private void UsarCorreccion()
            => RequestClose?.Invoke(new DialogResult(ButtonResult.OK, new DialogParameters { { PARAMETRO_CONCEPTO, Propuesto } }));

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            RevisionConcepto revision = parameters.ContainsKey(PARAMETRO_REVISION)
                ? parameters.GetValue<RevisionConcepto>(PARAMETRO_REVISION)
                : null;
            Original = parameters.ContainsKey(PARAMETRO_ORIGINAL)
                ? parameters.GetValue<string>(PARAMETRO_ORIGINAL)
                : revision?.Original;
            Propuesto = revision?.Propuesto ?? Original;
            Tramos = RevisionConcepto.Trocear(Propuesto, revision?.Cambios);
            TextoCambios = string.Join(Environment.NewLine, (revision?.Cambios ?? new List<CambioConcepto>())
                .Where(c => c != null && !string.IsNullOrEmpty(c.A))
                .Select(c => $"«{c.De}» → «{c.A}»"));
        }
    }
}
