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

namespace Nesto.Modulos.Producto
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class ProductoView : UserControl
    {
        public ProductoView(ProductoViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        //private void UserControl_Loaded(object sender, RoutedEventArgs e)
        //{
        //    ((ProductoViewModel)DataContext).CargarProducto();
        //}
    }
}
