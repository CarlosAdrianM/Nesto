using Nesto.Models;
using Nesto.Modulos.Cajas.ViewModels;
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

namespace Nesto.Modulos.Cajas.Views
{
    /// <summary>
    /// Lógica de interacción para BancosView.xaml
    /// </summary>
    public partial class BancosView : UserControl
    {
        public BancosView()
        {
            InitializeComponent();
        }

        private void DataGrid_AutoGeneratingColumn(object sender, DataGridAutoGeneratingColumnEventArgs e)
        {

        }

        private void DataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            // Obtén la fila seleccionada
            var dataGrid = sender as DataGrid;
            if (dataGrid == null) return;

            var clickedElement = e.OriginalSource as FrameworkElement;
            if (clickedElement == null) return;

            // Encuentra la celda de la columna "Pedido" en la que se hizo doble clic
            var cell = clickedElement.Parent as DataGridCell;
            if (cell != null)
            {
                var column = cell.Column as DataGridTextColumn;
                if (column != null && column.Header.ToString() == "Pedido")
                {
                    // Obtén el DataContext de la fila seleccionada
                    var selectedItem = dataGrid.SelectedItem;
                    if (selectedItem != null)
                    {
                        // Accede a la propiedad Pedido (suponiendo que el objeto tiene esta propiedad)
                        var pedido = (selectedItem as PrepagoDTO);

                        // Ejecuta el comando en el ViewModel pasando el número de pedido
                        var viewModel = this.DataContext as BancosViewModel;
                        if (viewModel != null && viewModel.AbrirPedidoCommand.CanExecute(pedido))
                        {
                            viewModel.AbrirPedidoCommand.Execute(pedido);
                        }
                    }
                }
            }
        }
    }
}
