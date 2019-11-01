using Microsoft.Practices.Prism.Interactivity.InteractionRequest;
using Microsoft.Practices.Prism.Regions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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
