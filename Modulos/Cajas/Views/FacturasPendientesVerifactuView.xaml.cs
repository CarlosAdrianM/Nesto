using System.Windows.Controls;
using Nesto.Modulos.Cajas.ViewModels;

namespace Nesto.Modulos.Cajas.Views
{
    /// <summary>
    /// NestoAPI#522: facturas pendientes de Verifactu (sin registrar o incorrectas en la AEAT).
    /// </summary>
    public partial class FacturasPendientesVerifactuView : UserControl
    {
        public FacturasPendientesVerifactuView(FacturasPendientesVerifactuViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
