using System.Windows.Controls;
using Nesto.Modulos.Cajas.ViewModels;

namespace Nesto.Modulos.Cajas.Views
{
    /// <summary>
    /// Nesto#261: consulta de auditoría de enlaces de pago (quién y cuándo los creó y adónde se enviaron).
    /// </summary>
    public partial class AuditoriaEnlacesPagoView : UserControl
    {
        public AuditoriaEnlacesPagoView(AuditoriaEnlacesPagoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
