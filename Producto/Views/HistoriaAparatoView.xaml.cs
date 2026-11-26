using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// NestoAPI#913 (corte 1): pestaña «Historia del aparato» de la ventana de Alquileres. El DataContext (HistoriaAparatoViewModel) lo
    /// pone la ventana que la incrusta.
    /// </summary>
    public partial class HistoriaAparatoView : UserControl
    {
        public HistoriaAparatoView()
        {
            InitializeComponent();
        }
    }
}
