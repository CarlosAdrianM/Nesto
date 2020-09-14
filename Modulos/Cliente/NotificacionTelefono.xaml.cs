using Prism.Interactivity.InteractionRequest;
using Prism.Regions;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Nesto.Modulos.Cliente
{
    /// <summary>
    /// Lógica de interacción para NotificacionTelefono.xaml
    /// </summary>
    public partial class NotificacionTelefono : UserControl, IInteractionRequestAware
    {
        public NotificacionTelefono()
        {
            InitializeComponent();
        }
        private IRegionManager RegionManager { get; }

        public INotification Notification { get; set; }
        public Action FinishInteraction { get; set; }

        private void OKButton_Click(object sender, RoutedEventArgs e)
        {
            if (FinishInteraction != null)
            {
                FinishInteraction.Invoke();
            }
        }
    }
}
