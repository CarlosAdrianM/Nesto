using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Nesto#528: la ventana «Recorrido del almacén». La lógica está en <see cref="RecorridoAlmacenViewModel"/> (lee al llegar,
    /// IReceptorNavegacion; pregunta al cerrar con cambios sin guardar, ITabCloseConfirmation).
    /// </summary>
    public partial class RecorridoAlmacenView : UserControl
    {
        public RecorridoAlmacenView(RecorridoAlmacenViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}
