using Nesto.Modules.Producto.ViewModels;
using System.Windows.Controls;
using System.Windows.Input;

namespace Nesto.Modules.Producto.Views
{
    /// <summary>
    /// Diferencias de reposición (Nesto#526, NestoAPI#634). La lógica está en <see cref="DiferenciasReposicionViewModel"/>,
    /// que se lee a sí mismo al llegar (IReceptorNavegacion).
    /// </summary>
    public partial class DiferenciasReposicionView : UserControl
    {
        public DiferenciasReposicionView(DiferenciasReposicionViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        /// <summary>
        /// Sugerencia 790 (Paloma): doble clic en el producto o en su descripción abre la ficha del producto, como en
        /// «Recibir reposición» (sugerencia 599). Solo en esas celdas; el resto de la fila sigue seleccionando sin más.
        /// </summary>
        private void Producto_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (sender is not DataGridCell celda || DataContext is not DiferenciasReposicionViewModel vm)
            {
                return;
            }
            string producto = celda.DataContext switch
            {
                FilaDiferenciaReposicion fila => fila.Producto,
                _ => null
            };
            if (string.IsNullOrWhiteSpace(producto) || vm.AbrirFichaProductoCommand?.CanExecute(producto) != true)
            {
                return;
            }
            e.Handled = true;
            vm.AbrirFichaProductoCommand.Execute(producto);
        }
    }
}
