using Nesto.Infrastructure.Contracts;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ControlesUsuario.Dialogs
{
    /// <summary>
    /// Nesto#372: muestra el changelog de novedades en lenguaje de usuario. El llamante carga las
    /// novedades (popup tras actualizar o menú Ayuda → Novedades) y las pasa por parámetro.
    /// Las novedades pueden abarcar VARIAS versiones (al actualizar saltándose versiones); para no
    /// mezclarlas, se agrupan por versión y se muestra SOLO una a la vez, empezando por la más nueva,
    /// con navegación anterior/siguiente entre versiones.
    /// </summary>
    public class NovedadesDialogViewModel : ObservableObject, IDialogAware
    {
        // NestoAPI#520: feedback de los usuarios (votos y comentarios). Sin servicio (tests antiguos) o si la
        // API no trae los contadores, la ventana se ve exactamente como antes.
        private readonly INovedadesService _servicio;
        private readonly IPortapapelesImagenes _portapapeles;
        private readonly Func<string, bool> _preguntar;

        public NovedadesDialogViewModel() : this(null, null, null) { }

        /// <summary>El que usa el contenedor (Prism/Unity elige el constructor con más parámetros resolubles).</summary>
        public NovedadesDialogViewModel(INovedadesService servicio)
            : this(servicio, new PortapapelesImagenesWpf(), PreguntarConMessageBox) { }

        internal NovedadesDialogViewModel(INovedadesService servicio, IPortapapelesImagenes portapapeles, Func<string, bool> preguntar)
        {
            _servicio = servicio;
            _portapapeles = portapapeles;
            _preguntar = preguntar;
        }

        private static bool PreguntarConMessageBox(string pregunta)
            => MessageBox.Show(pregunta, "Novedades", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        private RelayCommand _closeDialogCommand;
        public RelayCommand CloseDialogCommand =>
            _closeDialogCommand ?? (_closeDialogCommand = new RelayCommand(() => RequestClose?.Invoke(new DialogResult(ButtonResult.OK))));

        // Novedades agrupadas por versión, de la más NUEVA (índice 0) a la más antigua.
        private List<IGrouping<string, NovedadItem>> _porVersion = new List<IGrouping<string, NovedadItem>>();
        private int _indice;

        private string _title = "Novedades de Nesto";
        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        private List<NovedadItem> _novedades = new List<NovedadItem>();
        public List<NovedadItem> Novedades
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
        private RelayCommand _versionAnteriorCommand;
        public RelayCommand VersionAnteriorCommand =>
            _versionAnteriorCommand ?? (_versionAnteriorCommand = new RelayCommand(
                () => MostrarVersion(_indice + 1), () => _indice < _porVersion.Count - 1));

        // "Versión siguiente" = una más nueva (índice menor).
        private RelayCommand _versionSiguienteCommand;
        public RelayCommand VersionSiguienteCommand =>
            _versionSiguienteCommand ?? (_versionSiguienteCommand = new RelayCommand(
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
            // NestoAPI#520: cada novedad se envuelve UNA vez (al navegar entre versiones se conserva su estado).
            _porVersion = todas
                .Where(n => n != null)
                .Select(n => new NovedadItem(n, _servicio, _portapapeles, _preguntar))
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
                Novedades = new List<NovedadItem>();
                VersionActual = null;
            }
            else
            {
                _indice = Math.Max(0, Math.Min(indice, _porVersion.Count - 1));
                IGrouping<string, NovedadItem> grupo = _porVersion[_indice];
                Novedades = grupo.ToList();
                VersionActual = string.IsNullOrWhiteSpace(grupo.Key) ? "Novedades" : $"Versión {grupo.Key}";
            }
            VersionAnteriorCommand.NotifyCanExecuteChanged();
            VersionSiguienteCommand.NotifyCanExecuteChanged();
        }

        private static Version ParsearVersion(string version)
            => Version.TryParse(version, out Version v) ? v : new Version(0, 0);
    }
}
