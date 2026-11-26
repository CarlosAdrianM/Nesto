using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// NestoAPI#913 (corte 1): pestaña «Descuadres» de la ventana de Alquileres. El DataContext (DescuadresAparatosViewModel) lo pone la
    /// ventana que la incrusta.
    /// </summary>
    public partial class DescuadresAparatosView : UserControl
    {
        public DescuadresAparatosView()
        {
            InitializeComponent();
        }
    }
}
