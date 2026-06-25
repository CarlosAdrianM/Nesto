using Nesto.Infrastructure.Contracts;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#372: muestra el changelog de novedades en lenguaje de usuario. El llamante carga las
    /// novedades (popup tras actualizar o menú Ayuda → Novedades) y las pasa por parámetro.
    /// Las novedades pueden abarcar VARIAS versiones (al actualizar saltándose versiones); para no
    /// mezclarlas, se agrupan por versión y se muestra SOLO una a la vez, empezando por la más nueva,
    /// con navegación anterior/siguiente entre versiones.
    /// </summary>
    public class NovedadesDialogViewModel : BindableBase, IDialogAware
    {
        private DelegateCommand _closeDialogCommand;
        public DelegateCommand CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new DelegateCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK))));

        // Novedades agrupadas por versión, de la más NUEVA (índice 0) a la más antigua.
        private List<IGrouping<string, NovedadUsuario>> _porVersion = new List<IGrouping<string, NovedadUsuario>>();
        private int _indice;

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

        // Texto de la versión que se está mostrando (p. ej. "Versión 1.10.8.0").
        private string _versionActual;
        public string VersionActual
        {
            get { return _versionActual; }
            set { SetProperty(ref _versionActual, value); }
        }

        // "Versión anterior" = una más antigua (índice mayor, porque están de nueva a antigua).
        private DelegateCommand _versionAnteriorCommand;
        public DelegateCommand VersionAnteriorCommand =>
            _versionAnteriorCommand ?? (_versionAnteriorCommand = new DelegateCommand(
                () => MostrarVersion(_indice + 1), () => _indice < _porVersion.Count - 1));

        // "Versión siguiente" = una más nueva (índice menor).
        private DelegateCommand _versionSiguienteCommand;
        public DelegateCommand VersionSiguienteCommand =>
            _versionSiguienteCommand ?? (_versionSiguienteCommand = new DelegateCommand(
                () => MostrarVersion(_indice - 1), () => _indice > 0));

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog() => true;

        public void OnDialogClosed() { }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            if (parameters.ContainsKey("title"))
            {
                Title = parameters.GetValue<string>("title");
            }

            List<NovedadUsuario> todas = parameters.ContainsKey("novedades")
                ? (parameters.GetValue<List<NovedadUsuario>>("novedades") ?? new List<NovedadUsuario>())
                : new List<NovedadUsuario>();

            // Agrupar por versión y ordenar de la más nueva a la más antigua (por System.Version si
            // parsea; si no, por texto, para no romper con versiones con formato raro).
            _porVersion = todas
                .GroupBy(n => (n.Version ?? string.Empty).Trim())
                .OrderByDescending(g => ParsearVersion(g.Key))
                .ThenByDescending(g => g.Key, StringComparer.Ordinal)
                .ToList();

            MostrarVersion(0);
        }

        private void MostrarVersion(int indice)
        {
            if (_porVersion.Count == 0)
            {
                _indice = 0;
                Novedades = new List<NovedadUsuario>();
                VersionActual = null;
            }
            else
            {
                _indice = Math.Max(0, Math.Min(indice, _porVersion.Count - 1));
                IGrouping<string, NovedadUsuario> grupo = _porVersion[_indice];
                Novedades = grupo.ToList();
                VersionActual = string.IsNullOrWhiteSpace(grupo.Key) ? "Novedades" : $"Versión {grupo.Key}";
            }
            VersionAnteriorCommand.RaiseCanExecuteChanged();
            VersionSiguienteCommand.RaiseCanExecuteChanged();
        }

        private static Version ParsearVersion(string version)
            => Version.TryParse(version, out Version v) ? v : new Version(0, 0);
    }
}
