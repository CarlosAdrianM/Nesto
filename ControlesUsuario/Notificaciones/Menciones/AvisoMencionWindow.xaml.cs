using System;
using System.Windows;
using System.Windows.Input;

namespace ControlesUsuario.Notificaciones.Menciones
{
    /// <summary>Sugerencia 1183: la ventanita de «le han mencionado». Su DataContext es un <see cref="AvisoMencion"/>.</summary>
    public partial class AvisoMencionWindow : Window
    {
        public AvisoMencionWindow()
        {
            InitializeComponent();
        }

        /// <summary>Se ha pulsado la ventanita (abre la novedad).</summary>
        public event EventHandler Pulsada;
        /// <summary>Se ha pulsado la X (se cierra sin abrir nada; la notificación sigue en la campana).</summary>
        public event EventHandler CerradaAMano;

        private void OnPulsada(object sender, MouseButtonEventArgs e)
        {
            if (e != null && e.Handled)
            {
                return;
            }
            Pulsada?.Invoke(this, EventArgs.Empty);
        }

        private void OnCerrar(object sender, RoutedEventArgs e)
        {
            if (e != null)
            {
                e.Handled = true;
            }
            CerradaAMano?.Invoke(this, EventArgs.Empty);
        }
    }
}
