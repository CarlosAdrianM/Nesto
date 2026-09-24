using System.Windows;
using System.Windows.Controls;

namespace ControlesUsuario.Notificaciones
{
    /// <summary>
    /// Nesto#477: campana con el número de notificaciones sin leer y su panel. El DataContext es un
    /// <see cref="CampanaNotificacionesViewModel"/> (lo pone la ventana principal); al cargarse arranca
    /// su refresco periódico.
    /// </summary>
    public partial class CampanaNotificaciones : UserControl
    {
        public CampanaNotificaciones()
        {
            InitializeComponent();
            Loaded += (s, e) => (DataContext as CampanaNotificacionesViewModel)?.Iniciar();
            DataContextChanged += OnDataContextChanged;
        }

        private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            (e.OldValue as CampanaNotificacionesViewModel)?.Detener();
            if (IsLoaded)
            {
                (e.NewValue as CampanaNotificacionesViewModel)?.Iniciar();
            }
        }
    }
}
